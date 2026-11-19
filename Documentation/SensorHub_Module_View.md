# Sensor Data Processing Hub - Module View (Decomposition)

This document describes the decomposition view of the Sensor Data Processing Hub. It lists the modules, what each one is responsible for, the elements it contains and the modifiability tactics it supports.

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

## 2. Modules

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
- `HubConfig`: holds the parsed settings.
- `SourceFactory`: creates `ISensorSource` instances from the config.
- `TransformationFactory`: creates the `TransformationChain` from the config.
- `SinkFactory`: creates `IOutputSink` instances from the config.

**Notes:** The factories are the only place where concrete classes are named. Changing the system behavior is done in the config file or by registering a new class in a factory.

### 2.6 Core

**Purpose:** Holds the data types shared by every module, with no behavior of its own.

**Elements**
- `SensorReading`: a single measurement (sensor type, value, unit, timestamp).
- `Alert`: an alert raised on a reading.
- `SensorType` (enumeration): Temperature, CO2, Humidity.
- `AlertSeverity` (enumeration): severity levels of an alert.

**Notes:** `Core` does not depend on any other module, so a change in any other module cannot affect it.

## 3. Modifiability tactics reflected in the decomposition

| Tactic | Where it appears |
|---|---|
| Use an intermediary / abstract common services | Interfaces `ISensorSource`, `ITransformation`, `IOutputSink` isolate the hub from concrete implementations |
| Defer binding | Transformation chain, conversions and sinks are selected from the config file, not from the code |
| Increase semantic coherence | One responsibility per module: ingest, process, output, configure, share data |
| Restrict dependencies | `Ingestion`, `Processing` and `Output` do not know each other. Only `Application` coordinates them (to be shown in the uses view) |