# Sensor Data Processing Hub - Module View

This document describes the module view of the Sensor Data Processing Hub: the decomposition, the uses view and the class view. For each module it lists the responsibility, the elements it contains, the dependencies and the modifiability tactics it supports.

The system is implemented in **C#**. Each module is a package in the UML model and maps to a C# namespace under the root namespace `SensorHub` (for example `SensorHub.Ingestion`).

## 1. Overview

| Module | C# namespace | Responsibility |
|---|---|---|
| Application | `SensorHub.Application` | Entry point and orchestration of the whole hub |
| Ingestion | `SensorHub.Ingestion` | Provides sensor readings from different sources |
| Processing | `SensorHub.Processing` | Transforms readings through a configurable chain |
| Output | `SensorHub.Output` | Delivers results to the console dashboard and the log file |
| Configuration | `SensorHub.Configuration` | Loads the config file and builds the pipeline from it |
| Core | `SensorHub.Core` | Shared data types used by all other modules |

## 2. Decomposition view

### 2.1 Application

**Purpose:** Starts the hub and runs the processing loop: read from the sources, pass readings through the transformation chain, send the results to the sinks.

**Elements**
- `Program`: application entry point (`Main`). Loads the configuration and starts the hub.
- `Hub`: orchestrates sources, transformation chain and sinks.

**Notes:** `Hub` only works with interfaces (`ISensorSource`, `ITransformation`, `IOutputSink`) and never with concrete implementations, so adding a new source, transformation or sink does not require changing it.

### 2.2 Ingestion

**Purpose:** Collects readings (temperature, CO2, humidity) from simulated sensors and hands them to the rest of the system as `SensorReading` objects.

**Elements**
- `ISensorSource` (interface): contract for any sensor data source.
- `CsvSensorSource`: reads readings from a CSV file.
- `RandomSensorSource`: generates random readings (a socket feed can be added later as a third implementation).

**Notes:** The system must ingest from two sources. Each source hides its format behind `ISensorSource`.

### 2.3 Processing

**Purpose:** Applies a chain of transformations to each reading: unit conversion, smoothing and threshold alerts. The chain and its order are defined in the configuration file.

**Elements**
- `ITransformation` (interface): contract for one transformation step.
- `UnitConverter`: converts units (for example Celsius to Fahrenheit), with the conversion defined in the config file.
- `MovingAverageSmoother`: smooths noisy values with a moving average.
- `ThresholdAlerter`: raises an `Alert` when a value crosses a configured threshold.
- `TransformationChain`: runs the configured transformations in order.

**Notes:** Each transformation is independent. A new transformation is added by implementing `ITransformation` and naming it in the config file.

### 2.4 Output

**Purpose:** Sends processed readings and alerts to the outputs. The system has two sinks: a console dashboard and a log file.

**Elements**
- `IOutputSink` (interface): contract for any output destination.
- `ConsoleDashboardSink`: shows current values and alerts on the console.
- `LogFileSink`: appends results to a log file.

### 2.5 Configuration

**Purpose:** Reads the config file and builds the sources, the transformation chain and the sinks from it. This is where unit conversions and the transformation chain are defined, as required.

**Elements**
- `ConfigLoader`: reads and parses the config file (JSON).
- `HubConfig`: holds the parsed settings (lists of sources, transformations and sinks).
- `ComponentConfig`: one generic entry of the config file (a type name and key/value settings), used for every source, transformation and sink.
- `SourceFactory`: creates `ISensorSource` instances from the config.
- `TransformationFactory`: creates the `TransformationChain` from the config.
- `SinkFactory`: creates `IOutputSink` instances from the config.
- `ConfigSettings` (internal, static): helper used by the three factories to read typed settings (`GetString`, `GetDouble`, `GetInt`, `GetSensorType`) from a `ComponentConfig` and to build clear error messages.

**Notes:** The factories are the only place where concrete classes are named. Changing the system behavior is done in the config file or by registering a new class in a factory.

### 2.6 Core

**Purpose:** Holds the data types shared by every module, with no behavior of its own.

**Elements**
- `SensorReading`: a single measurement (sensor id, sensor type, value, unit, timestamp and the alerts raised on it).
- `Alert`: an alert raised on a reading (severity, message, timestamp).
- `SensorType` (enumeration): Temperature, CO2, Humidity.
- `AlertSeverity` (enumeration): Warning, Critical.

**Notes:** `Core` does not depend on any other module, so a change in any other module cannot affect it.

### 2.7 Modifiability tactics reflected in the decomposition

| Tactic | Where it appears |
|---|---|
| Encapsulate | Interfaces `ISensorSource`, `ITransformation`, `IOutputSink` hide each implementation behind an explicit interface |
| Use an intermediary | `Hub` sits between sources, chain and sinks, and the factories sit between `Program` and the concrete classes |
| Defer binding (configuration-time binding) | Transformation chain, conversions and sinks are selected from the config file at startup, not from the code |
| Increase semantic coherence | One responsibility per module: ingest, process, output, configure, share data |
| Restrict dependencies | `Ingestion`, `Processing` and `Output` do not know each other. Only `Application` coordinates them (see the uses view) |

## 3. Uses view

The uses view shows which module uses which. A module A uses a module B if A needs a correct B to work (for example, A calls B or refers to its types). In the UML model each relation is a `Usage` dependency drawn from the user to the used module.

### 3.1 Dependencies and reasons

| From | To | Reason |
|---|---|---|
| Application | Configuration | `Program` loads the config and asks it to build the pipeline |
| Application | Ingestion | `Hub` reads readings through `ISensorSource` |
| Application | Processing | `Hub` passes readings to the transformation chain |
| Application | Output | `Hub` sends results to the sinks through `IOutputSink` |
| Application | Core | `Hub` handles `SensorReading` and `Alert` |
| Configuration | Ingestion | `SourceFactory` creates the concrete sources |
| Configuration | Processing | `TransformationFactory` creates the transformations and the chain |
| Configuration | Output | `SinkFactory` creates the concrete sinks |
| Configuration | Core | `SourceFactory` and `TransformationFactory` turn the sensor type named in the config file into a `SensorType` |
| Ingestion | Core | Produces `SensorReading` objects |
| Processing | Core | Consumes and produces `SensorReading`, raises `Alert` |
| Output | Core | Displays and logs `SensorReading` and `Alert` |

### 3.2 Dependency rules

- `Ingestion`, `Processing` and `Output` **must not use each other**. They only meet through `Application`.
- `Core` **uses no other module**.
- Only `Application` and `Configuration` may name concrete classes of `Ingestion`, `Processing` and `Output`. `Application` itself only uses their interfaces.
- No dependency cycles. Dependencies go from the top (`Application`, `Configuration`) down to `Core`.

### 3.3 Impact of a change

If a module changes, the modules that use it may have to change too. This is a module-level view, so a change inside a concrete class usually stays in its module and the factory that creates it.

| Changed module | Modules that may be affected |
|---|---|
| Core | Application, Configuration, Ingestion, Processing, Output |
| Ingestion | Configuration, Application |
| Processing | Configuration, Application |
| Output | Configuration, Application |
| Configuration | Application |
| Application | none |

`Core` has the widest impact, so it contains only simple data types and should change rarely. This table is also the starting point for the modifiability scenarios (for example "add a new sensor source" touches `Ingestion` and `Configuration`).

### 3.4 Mapping to C#

- The allowed dependencies above are the only `using` directives allowed between the namespaces `SensorHub.*`. For example, no file in `SensorHub.Processing` should contain `using SensorHub.Output;`.
- If the code is split into one project per module, these are the only project references allowed.
- The rules can be checked automatically with an architecture test (optional).

### 3.5 Tactics and rationale

| Tactic | Decision | Because |
|---|---|---|
| Restrict dependencies | No dependencies between `Ingestion`, `Processing` and `Output` | A change to a source, a transformation or a sink cannot propagate to the other two |
| Encapsulate | `Application` works only through the interfaces of `Ingestion`, `Processing` and `Output` | Adding a new implementation does not require changing `Hub` |
| Use an intermediary | `Configuration` (the factories) creates the concrete objects, `Hub` connects them | Sources, transformations and sinks never reference each other |
| Increase semantic coherence | `Core` holds only shared data, with no behavior | The most used module is also the most stable |
| Defer binding | `Configuration` decides at runtime which classes are created | Behavior changes with the config file, not with a rebuild |

## 4. Class view

The class view shows the classes and interfaces inside the modules, with their attributes, operations and relationships. It is the view that maps directly to the C# code. It is split into six diagrams, one per module (the `Application` diagram also shows the interfaces the hub depends on), to keep each one readable.

| Diagram | Shows |
|---|---|
| Class View - Application | `Hub` and the three interfaces it holds (`ISensorSource`, `ITransformation`, `IOutputSink`) |
| Class View - Ingestion | `ISensorSource`, `CsvSensorSource`, `RandomSensorSource` |
| Class View - Processing | `ITransformation`, `UnitConverter`, `MovingAverageSmoother`, `ThresholdAlerter`, `TransformationChain` |
| Class View - Output | `IOutputSink`, `ConsoleDashboardSink`, `LogFileSink` (with `SensorReading`, `Alert` and `SensorType` as references) |
| Class View - Configuration | `Program`, `ConfigLoader`, `HubConfig`, `ComponentConfig` and the three factories (with `SensorType` as reference) |
| Class View - Core | `SensorReading`, `Alert`, `SensorType`, `AlertSeverity` |

Classes that belong to another module but are used by the module in the diagram (for example `SensorReading` in `Ingestion`) appear as references with a `«use»` dependency. Their details are in their own module's diagram.

### 4.1 Notation

| Relationship | Meaning | Example |
|---|---|---|
| Realization (dashed, hollow triangle) | The class implements the interface | `CsvSensorSource` implements `ISensorSource` |
| Directed association (solid line, open arrow) | A field whose type is another class or interface; the arrow shows who knows whom | `Hub` → `ISensorSource` (role `-_sources 1..*`) |
| Aggregation (hollow diamond on the whole) | The whole holds parts that can exist on their own | `TransformationChain` holds its steps (role `-_steps`) |
| Composition (filled diamond on the whole) | The parts live and die with the whole | `SensorReading` owns its `Alerts`; `HubConfig` owns its `Sources`, `Transformations` and `Sinks` entries |
| Usage `«use»` (dashed arrow) | A class uses another one (parameter, return type, creation) without owning it | `SourceFactory` creates `CsvSensorSource` |

An association end reads `visibility role multiplicity`. For example `-_sources 1..*` means: a private field named `_sources` that refers to one or more `ISensorSource`. The `-` is UML (private; `+` public, `#` protected, `~` package), the leading underscore is only the C# naming convention for private fields.

A UML attribute and an association end are the same thing (a property of the class), so each property is drawn once:

- a property whose type is an enumeration (`SensorType`, `AlertSeverity`) or a library type (`string`, `double`, `Dictionary`) is an **attribute**;
- a property whose type is a class or interface of the model is an **association end** with a role name (`_sources`, `_chain`, `_sinks`, `_steps`, `Alerts`, `Sources`, `Transformations`, `Sinks`).

Static members are underlined (`Program.Main`).

### 4.2 Core

| Type | Members (C#) |
|---|---|
| `SensorReading` | `string SensorId`, `SensorType Type`, `double Value`, `string Unit`, `DateTime Timestamp`, `List<Alert> Alerts` (composition role, `0..*`) |
| `Alert` | `AlertSeverity Severity`, `string Message`, `DateTime Timestamp` |
| `SensorType` (enum) | `Temperature`, `CO2`, `Humidity` |
| `AlertSeverity` (enum) | `Warning`, `Critical` |

A reading carries its own alerts. This lets every transformation use the same signature and lets the sinks show the alerts together with the reading.

### 4.3 Ingestion

| Type | Members (C#) |
|---|---|
| `ISensorSource` | `IEnumerable<SensorReading> Read()` |
| `CsvSensorSource` | `string _filePath`, `IEnumerable<SensorReading> Read()` (parses the `type` column into a `SensorType`) |
| `RandomSensorSource` | `Random _random`, `SensorType _sensorType`, `int _count`, `IEnumerable<SensorReading> Read()` |

### 4.4 Processing

| Type | Members (C#) |
|---|---|
| `ITransformation` | `SensorReading Apply(SensorReading reading)` |
| `UnitConverter` | `SensorType _sensorType`, `string _fromUnit`, `string _toUnit`, `double _factor`, `double _offset`, `Apply(...)` |
| `MovingAverageSmoother` | `int _windowSize`, `Dictionary<(string, SensorType), Queue<double>> _windows`, `Apply(...)` |
| `ThresholdAlerter` | `SensorType _sensorType`, `double _warningThreshold`, `double _criticalThreshold`, `Apply(...)` |
| `TransformationChain` | `List<ITransformation> _steps` (aggregation role, `0..*`), `void Add(ITransformation step)`, `Apply(...)` |

`TransformationChain` implements `ITransformation` and holds other `ITransformation` objects (Composite pattern). The chain can be used anywhere a single transformation is expected. The multiplicity on the chain side is `0..1`: a step belongs to at most one chain, and the root chain belongs to none (it is held by `Hub`).

The smoothing windows are keyed by `(SensorId, SensorType)`, not by `SensorId` alone, because one sensor id (for example `room-a`) reports temperature, CO2 and humidity. With the id alone, a CO2 value would be averaged with a temperature. The key also means that a new sensor type or a new room needs no change in the smoother.

### 4.5 Output

| Type | Members (C#) |
|---|---|
| `IOutputSink` | `void Write(SensorReading reading)`, `void Close()` |
| `ConsoleDashboardSink` | `Dictionary<(string, SensorType), SensorReading> _latest`, `Queue<Alert> _recentAlerts` (the last 5 alerts), `Write(...)`, `Close()`, private `Render()` |
| `LogFileSink` | `StreamWriter _writer`, `Write(...)`, `Close()` |

`Close()` lets the log file be flushed and closed properly when the hub stops. The dashboard keeps the latest reading per `(SensorId, SensorType)`, for the same reason as the smoother: one room reports several types. The Output class view also shows `Alert`, since the sinks display the alerts carried by each reading.

### 4.6 Configuration and Application

| Type | Members (C#) |
|---|---|
| `Program` | `static void Main(string[] args)` |
| `Hub` | `List<ISensorSource> _sources` (`1..*`), `ITransformation _chain` (`1`), `List<IOutputSink> _sinks` (`1..*`), all association roles; `TimeSpan _delay`; constructor `Hub(sources, chain, sinks, delay)`; `void Run()` |
| `ConfigLoader` | `HubConfig Load(string path)` |
| `HubConfig` | `List<ComponentConfig> Sources` (`1..*`), `List<ComponentConfig> Transformations` (`0..*`), `List<ComponentConfig> Sinks` (`1..*`), three composition roles; `int DelayMs` (pause after each reading, 0 = none) |
| `ComponentConfig` | `string Type`, `Dictionary<string, string> Settings` |
| `SourceFactory` | `ISensorSource Create(ComponentConfig config)` |
| `TransformationFactory` | `ITransformation Create(ComponentConfig config)`, `TransformationChain CreateChain(List<ComponentConfig> configs)` |
| `SinkFactory` | `IOutputSink Create(ComponentConfig config)` |
| `ConfigSettings` (internal) | static `GetString`, `GetDouble`, `GetInt`, `GetSensorType`, `UnknownType`, private static `Error`; used by the three factories |

`ComponentConfig` is a generic entry (type name and settings), so adding a new source, transformation or sink does not require a new config class. The factories map the type name to the concrete class. They are the only place where the concrete classes of `Ingestion`, `Processing` and `Output` are named, and this is the dependency shown in the uses view.

### 4.7 Mapping to C# code

| UML | C# |
|---|---|
| Package | Namespace `SensorHub.<Module>` (one folder per module) |
| `«interface»` | `interface` with the `I` prefix |
| Class | `class`, one file per class |
| `«enumeration»` | `enum` |
| Private attributes and private association roles (`-_sources`) | Private fields with a leading underscore |
| Public attributes and public association roles (`+Alerts`, `+Sources`) | Auto-properties (`{ get; set; }`) |
| Multiplicity `1..*` / `0..*` | `List<T>` |
| Static (underlined) | `static` |

### 4.8 Tactics and rationale

| Tactic | Decision | Because |
|---|---|---|
| Encapsulate | `Hub` holds `ISensorSource`, `ITransformation` and `IOutputSink`, never a concrete class | A new source, transformation or sink does not change `Hub` |
| Use an intermediary | The factories are the only classes that name the concrete classes | Adding a class touches its own module and one factory |
| Defer binding | `ComponentConfig` and the factories create objects from the config file | The chain and the conversions can be changed without a rebuild |
| Increase semantic coherence | One class per responsibility (convert, smooth, alert) | A change to one rule touches one class |
| Encapsulate | The chain is hidden behind `ITransformation` | The hub does not know how many steps there are or in what order |

## 5. Next views

- **Component-and-connector view:** runtime components and the connectors between them, see `SensorHub_CnC_View.md`.
- **Allocation view:** how the modules map to projects, assemblies and the machine, see `SensorHub_Allocation_View.md`.
- **Mapping between the three views:** see `SensorHub_View_Mapping.md`.
