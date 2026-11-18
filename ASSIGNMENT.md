# Sensor Data Processing Hub

## Domain

A system that collects readings from several environmental sensors (temperature, CO₂, humidity), processes them, and sends results to different outputs. The sensors can be simulated: CSV files, a random generator, or a simple socket feed.

## Core functionality

- Ingest data from **two sources**.
- Apply a **chain of transformations** (unit conversion, smoothing, threshold alerts).
- Output to **two sinks**: a console dashboard and a log file.
- The conversion (f.ex Celsius to Fahrenheit) should be written in a **config file**, the same should be true for the **transformation chain**.

## Delivery 1

An architecture PDF document with:

- A **module view** (decomposition, classes and uses) (UML)
- A **component-and-connector view** (UML)
- An **allocation view** if relevant (UML)
- A **mapping between views**
- **Rationale for key decisions**, e.g. short Architectural Design Rationales with relation to the tactics used. Hence, *this tactic is used because...*
- **Modifiability scenarios** with concrete response measures. Write at least **3 example scenarios** for a change that could (and probably likely to) come.

**Working code in C#** with a short **README** (zipped with the delivery).

## Part 2

Part 2 of the project will be announced **30.11.2026 at 00:15**.

If you have problems uploading to Canvas due to some Canvas problem, you must send the project to **arne.wiklund@uia.no** before 30.11.2026 at 00:15.

## Presentation day

You must be able to **run your code during the presentation**.
