<#
.SYNOPSIS
  Checks that the module, C&C and allocation views of Documentation/SensorHub.qea agree.

.DESCRIPTION
  Run it in Windows PowerShell while Enterprise Architect has SensorHub.qea open:
      powershell -File Documentation/tools/Check-ViewConsistency.ps1
  It reads the open model through EA's automation interface (read-only queries) and prints
  the class-to-view mapping, then five checks. Exit code 0 when every check passes, 1 otherwise.

  Checks:
    1. every module package is manifested by exactly one assembly;
    2. every C&C component (except the SensorHub boundary and external ones) traces to a class;
    3. every connector between C&C components is allowed by the uses view
       (same module, or a use dependency from the source module to the target module; a component
       that spans several modules passes if one of its module pairs is allowed);
    4. the reference dependencies between assemblies are exactly the uses view;
    5. every assembly is deployed in an execution environment.

  Safety: EA opens a modal error box for any invalid SQL, and that box blocks every later call.
  So every id put into a query goes through Id/IdList, which stop the script in PowerShell
  before a malformed query can reach EA.
#>
$ErrorActionPreference = 'Stop'
$r = ([Runtime.InteropServices.Marshal]::GetActiveObject('EA.App')).Repository

# Runs a read-only query and writes its rows as plain objects. Callers always store the result
# with @(Q ...) before looping, so one row, several rows and no row all give an array.
function Q([string]$sql) {
    $x = [xml]$r.SQLQuery($sql)
    foreach ($row in $x.SelectNodes('//Row')) {
        $h = [ordered]@{}
        foreach ($node in $row.ChildNodes) { $h[$node.Name] = $node.InnerText }
        [pscustomobject]$h
    }
}
# One integer id, or stop. An array or an empty value never reaches the SQL text.
function Id($value) {
    if ($null -eq $value -or $value -is [array] -or "$value" -notmatch '^\d+$') { throw "Invalid id '$value': query not sent to EA" }
    return [int]$value
}
# A non-empty comma-separated list of integer ids, or stop (an empty "in ()" is invalid SQL).
function IdList($values) {
    $ids = @($values | ForEach-Object { Id $_ })
    if ($ids.Count -eq 0) { throw 'Empty id list: query not sent to EA' }
    return ($ids -join ',')
}
$script:failed = 0
function Result($ok, $text) { if ($ok) { "  OK    $text" } else { "  FAIL  $text"; $script:failed++ } }

# ---- read the model ----
$rootRows = @(Q "select Package_ID from t_package where Name='SensorHub'")
if ($rootRows.Count -ne 1) { "Expected one package named SensorHub, found $($rootRows.Count): is SensorHub.qea open in EA?"; exit 1 }
$root = Id $rootRows[0].Package_ID

$mods = @{}      # module package id -> module name
$children = @(Q "select Package_ID, Name from t_package where Parent_ID=$root")
foreach ($p in $children) {
    $pid_ = Id $p.Package_ID
    $count = @(Q "select count(*) as N from t_object where Package_ID=$pid_ and Object_Type in ('Class','Interface','Enumeration')")
    if ($count.Count -eq 1 -and [int]$count[0].N -gt 0) { $mods[$pid_] = $p.Name }
}
if ($mods.Count -eq 0) { 'No module package found under SensorHub: is SensorHub.qea open in EA?'; exit 1 }

$pkgEl = @{}     # module name -> object id of its package element
$pkgRows = @(Q "select Object_ID, Name, PDATA1 from t_object where Object_Type='Package'")
foreach ($o in $pkgRows) { if ("$($o.PDATA1)" -match '^\d+$' -and $mods.ContainsKey([int]$o.PDATA1)) { $pkgEl[$o.Name] = Id $o.Object_ID } }
$modOfPkgEl = @{}; foreach ($k in $pkgEl.Keys) { $modOfPkgEl[$pkgEl[$k]] = $k }

$modIds = IdList $mods.Keys
$pkgIds = IdList $pkgEl.Values
$classes  = @(Q "select Object_ID, Name, Object_Type, Package_ID from t_object where Package_ID in ($modIds) and Object_Type in ('Class','Interface','Enumeration') order by Package_ID, Name")
$trace    = @(Q "select c.Start_Object_ID as Comp, o.Name as CompName, c.End_Object_ID as Cls from t_connector c join t_object o on o.Object_ID=c.Start_Object_ID where c.Stereotype='trace'")
$manifest = @(Q "select a.Name as AName, t.Name as TName, t.Object_Type as TType from t_connector c join t_object a on a.Object_ID=c.Start_Object_ID join t_object t on t.Object_ID=c.End_Object_ID where c.Stereotype='manifest'")
$uses     = @(Q "select Start_Object_ID as S, End_Object_ID as E from t_connector where Connector_Type='Usage' and Start_Object_ID in ($pkgIds) and End_Object_ID in ($pkgIds)")
$clsMod = @{}; foreach ($c in $classes) { $clsMod[[int]$c.Object_ID] = $mods[[int]$c.Package_ID] }

"Class mapping (module | element | C&C component | assembly)"
foreach ($c in $classes) {
    $mod = $mods[[int]$c.Package_ID]
    $comps = @($trace | Where-Object { $_.Cls -eq $c.Object_ID } | ForEach-Object { $_.CompName }) -join ', '
    $asm = @($manifest | Where-Object { $_.TName -eq $mod -and $_.TType -eq 'Package' } | ForEach-Object { $_.AName }) -join ', '
    "  $mod | $($c.Name) ($($c.Object_Type)) | $(if ($comps) { $comps } else { '-' }) | $asm"
}

"1. Each module is manifested by exactly one assembly"
foreach ($m in $mods.Values) {
    $n = @($manifest | Where-Object { $_.TName -eq $m -and $_.TType -eq 'Package' }).Count
    Result ($n -eq 1) "$m ($n)"
}

"2. Each C&C component traces to a class"
$ccRows = @(Q "select Package_ID from t_package where Name='Runtime (C&C)'")
if ($ccRows.Count -ne 1) { "Expected one package named 'Runtime (C&C)', found $($ccRows.Count)"; exit 1 }
$ccPkg = Id $ccRows[0].Package_ID
$components = @(Q "select Object_ID, Name, Stereotype from t_object where Package_ID=$ccPkg and Object_Type='Component'")
foreach ($o in $components) {
    if ($o.Name -eq 'SensorHub' -or $o.Stereotype -eq 'external') { "  skip  $($o.Name) (boundary or external)"; continue }
    $n = @($trace | Where-Object { $_.Comp -eq $o.Object_ID }).Count
    Result ($n -ge 1) "$($o.Name) ($n trace)"
}

"3. Each C&C connector is allowed by the uses view"
function ModsOf($compId) { @($trace | Where-Object { $_.Comp -eq $compId } | ForEach-Object { $clsMod[[int]$_.Cls] } | Select-Object -Unique) }
$cc = @(Q "select c.Connector_Type as T, c.Stereotype as St, c.Name as N, c.Start_Object_ID as S, c.End_Object_ID as E, s.Name as SN, e.Name as EN from t_connector c join t_object s on s.Object_ID=c.Start_Object_ID join t_object e on e.Object_ID=c.End_Object_ID where s.Package_ID=$ccPkg and e.Package_ID=$ccPkg and s.Object_Type='Component' and e.Object_Type='Component' and s.Name<>'SensorHub' and e.Name<>'SensorHub' and (e.Stereotype is null or e.Stereotype<>'external')")
foreach ($c in $cc) {
    $pairs = @(foreach ($a in (ModsOf $c.S)) { foreach ($b in (ModsOf $c.E)) {
        if ($a -eq $b) { "$a (same module)" }
        elseif (@($uses | Where-Object { [int]$_.S -eq $pkgEl[$a] -and [int]$_.E -eq $pkgEl[$b] }).Count) { "$a -> $b" } } })
    $label = if ($c.N) { $c.N } else { $c.St }
    Result ($pairs.Count -gt 0) "$($c.SN) -> $($c.EN) [$label]: $($pairs -join ', ')"
}

"4. Project references are exactly the uses view"
$refs = @(Q "select s.Name as SN, e.Name as EN from t_connector c join t_object s on s.Object_ID=c.Start_Object_ID join t_object e on e.Object_ID=c.End_Object_ID where c.Stereotype='reference'")
$refSet  = @($refs | ForEach-Object { ($_.SN -replace '^SensorHub\.|\.dll$', '') + '->' + ($_.EN -replace '^SensorHub\.|\.dll$', '') } | Sort-Object)
$usesSet = @($uses | ForEach-Object { $modOfPkgEl[[int]$_.S] + '->' + $modOfPkgEl[[int]$_.E] } | Sort-Object)
$diff = @(Compare-Object $refSet $usesSet)
Result ($diff.Count -eq 0 -and $refSet.Count -gt 0) "$($refSet.Count) references, $($usesSet.Count) uses dependencies"
foreach ($d in $diff) { "        only in $(if ($d.SideIndicator -eq '<=') { 'references' } else { 'uses view' }): $($d.InputObject)" }

"5. Each assembly is deployed in an execution environment"
$asmRows = @(Q "select o.Name, p.Name as PN, p.Object_Type as PT from t_object o left join t_object p on p.Object_ID=o.ParentID where o.Stereotype='assembly' and o.Name not like '%Tests%'")
foreach ($a in $asmRows) { Result ($a.PT -eq 'ExecutionEnvironment') "$($a.Name) in $(if ($a.PN) { $a.PN } else { 'nothing' })" }

if ($script:failed) { "$($script:failed) check(s) failed"; exit 1 } else { 'All checks passed'; exit 0 }
