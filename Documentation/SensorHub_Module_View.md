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
| Use an intermediary / abstract common services | Interfaces `ISensorSource`, `ITransformation`, `IOutputSink` isolate the hub from concrete implementations |
| Defer binding | Transformation chain, conversions and sinks are selected from the config file, not from the code |
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
| Configuration | Core | Settings refer to shared types such as `SensorType` |
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
| Use an intermediary | `Application` works through interfaces, `Configuration` creates the concrete objects | Adding a new implementation does not require changing `Hub` |
| Increase semantic coherence | `Core` holds only shared data, with no behavior | The most used module is also the most stable |
| Defer binding | `Configuration` decides at runtime which classes are created | Behavior changes with the config file, not with a rebuild |

## 4. Class view

The class view shows the classes and interfaces inside the modules, with their attributes, operations and relationships. It is the view that maps directly to the C# code. It is split into six diagrams, one per module (the `Application` diagram also shows the interfaces the hub depends on), to keep each one readable.

| Diagram | Shows |
|---|---|
| Class View - Application | `Hub` and the three interfaces it holds (`ISensorSource`, `ITransformation`, `IOutputSink`) |
| Class View - Ingestion | `ISensorSource`, `CsvSensorSource`, `RandomSensorSource` |
| Class View - Processing | `ITransformation`, `UnitConverter`, `MovingAverageSmoother`, `ThresholdAlerter`, `TransformationChain` |
| Class View - Output | `IOutputSink`, `ConsoleDashboardSink`, `LogFileSink` |
| Class View - Configuration | `Program`, `ConfigLoader`, `HubConfig`, `ComponentConfig` and the three factories |
| Class View - Core | `SensorReading`, `Alert`, `SensorType`, `AlertSeverity` |

Classes that belong to another module but are used by the module in the diagram (for example `SensorReading` in `Ingestion`) appear as references with a `«use»` dependency. Their details are in their own module's diagram.

### 4.1 Notation

| Relationship | Meaning | Example |
|---|---|---|
| Realization (dashed, hollow triangle) | The class implements the interface | `CsvSensorSource` implements `ISensorSource` |
| Aggregation (hollow diamond on the whole) | An object holds other objects as parts | `Hub` holds `ISensorSource`, `ITransformation` and `IOutputSink` |
| Association (solid arrow) | A property whose type is another class | `SensorReading.Type` is a `SensorType` |
| Usage `«use»` (dashed arrow) | A class uses another one (parameter, return type, creation) without owning it | `SourceFactory` creates `CsvSensorSource` |

Note: composition (filled diamond) would be more precise for `SensorReading` and `Alert`, and for `HubConfig` and `ComponentConfig`. The model uses aggregation for both.

### 4.2 Core

| Type | Members (C#) |
|---|---|
| `SensorReading` | `string SensorId`, `SensorType Type`, `double Value`, `string Unit`, `DateTime Timestamp`, `List<Alert> Alerts` |
| `Alert` | `AlertSeverity Severity`, `string Message`, `DateTime Timestamp` |
| `SensorType` (enum) | `Temperature`, `CO2`, `Humidity` |
| `AlertSeverity` (enum) | `Warning`, `Critical` |

A reading carries its own alerts. This lets every transformation use the same signature and lets the sinks show the alerts together with the reading.

### 4.3 Ingestion

| Type | Members (C#) |
|---|---|
| `ISensorSource` | `IEnumerable<SensorReading> Read()` |
| `CsvSensorSource` | `string _filePath`, `IEnumerable<SensorReading> Read()` |
| `RandomSensorSource` | `Random _random`, `SensorType _sensorType`, `int _count`, `IEnumerable<SensorReading> Read()` |

### 4.4 Processing

| Type | Members (C#) |
|---|---|
| `ITransformation` | `SensorReading Apply(SensorReading reading)` |
| `UnitConverter` | `SensorType _sensorType`, `string _fromUnit`, `string _toUnit`, `double _factor`, `double _offset`, `Apply(...)` |
| `MovingAverageSmoother` | `int _windowSize`, `Dictionary<string, Queue<double>> _windows`, `Apply(...)` |
| `ThresholdAlerter` | `SensorType _sensorType`, `double _warningThreshold`, `double _criticalThreshold`, `Apply(...)` |
| `TransformationChain` | `List<ITransformation> _steps`, `void Add(ITransformation step)`, `Apply(...)` |

`TransformationChain` implements `ITransformation` and holds other `ITransformation` objects (Composite pattern). The chain can be used anywhere a single transformation is expected.

### 4.5 Output

| Type | Members (C#) |
|---|---|
| `IOutputSink` | `void Write(SensorReading reading)`, `void Close()` |
| `ConsoleDashboardSink` | `Dictionary<string, SensorReading> _latest`, `Write(...)`, `Close()`, private `Render()` |
| `LogFileSink` | `StreamWriter _writer`, `Write(...)`, `Close()` |

`Close()` lets the log file be flushed and closed properly when the hub stops.

### 4.6 Configuration and Application

| Type | Members (C#) |
|---|---|
| `Program` | `static void Main(string[] args)` |
| `Hub` | `List<ISensorSource> _sources`, `ITransformation _chain`, `List<IOutputSink> _sinks`, `void Run()` |
| `ConfigLoader` | `HubConfig Load(string path)` |
| `HubConfig` | `List<ComponentConfig> Sources`, `List<ComponentConfig> Transformations`, `List<ComponentConfig> Sinks` |
| `ComponentConfig` | `string Type`, `Dictionary<string, string> Settings` |
| `SourceFactory` | `ISensorSource Create(ComponentConfig config)` |
| `TransformationFactory` | `ITransformation Create(ComponentConfig config)`, `TransformationChain CreateChain(List<ComponentConfig> configs)` |
| `SinkFactory` | `IOutputSink Create(ComponentConfig config)` |

`ComponentConfig` is a generic entry (type name and settings), so adding a new source, transformation or sink does not require a new config class. The factories map the type name to the concrete class. They are the only place where the concrete classes of `Ingestion`, `Processing` and `Output` are named, and this is the dependency shown in the uses view.

### 4.7 Mapping to C# code

| UML | C# |
|---|---|
| Package | Namespace `SensorHub.<Module>` (one folder per module) |
| `«interface»` | `interface` with the `I` prefix |
| Class | `class`, one file per class |
| `«enumeration»` | `enum` |
| Private attributes | Private fields with a leading underscore |
| Public attributes of `Core` types and `HubConfig` | Auto-properties (`{ get; set; }`) |

### 4.8 Tactics and rationale

| Tactic | Decision | Because |
|---|---|---|
| Use an intermediary / abstract common services | `Hub` holds `ISensorSource`, `ITransformation` and `IOutputSink` | A new source, transformation or sink does not change `Hub` |
| Defer binding | `ComponentConfig` and the factories create objects from the config file | The chain and the conversions can be changed without a rebuild |
| Increase semantic coherence | One class per responsibility (convert, smooth, alert) | A change to one rule touches one class |
| Encapsulate | The chain is hidden behind `ITransformation` | The hub does not know how many steps there are or in what order |

## 5. Next views

- **Component-and-connector view:** runtime components and the connectors between them.
- **Allocation view (if relevant):** how the modules map to files, projects or the execution environment.
