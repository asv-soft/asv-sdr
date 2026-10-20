# Packages and Architecture

## Projects

| Project | Responsibility | Entry points |
| --- | --- | --- |
| `Asv.Sdr` | IQ pipelines, DSP, filters, and ADS-B/Mode-S models | `IReaderIq<T>`, `FluentDspHelper`, `IDspFilter`, `AdsbMessageParser` |
| `Asv.Sdr.Simulate` | Generated IQ input | `VirtualReaderIqIls1F` |
| `Asv.Sdr.LimeSdr` | LimeSuite bindings, readers/writers, and protocol-specific device helpers | `ILimeSdrDevice`, `LimeReaderIq`, `LimeWriterIq` |
| `Asv.Sdr.SignalHound` | Signal Hound BB, SA, and SG bindings | `IShBbDevice`, `ShBbDevice`, `BbApi`, `SaApi`, `SgApi` |
| `Asv.Sdr.Msi` | MSI/SDRplay-compatible wrapper | `MsiDevice` |
| `Asv.Sdr.Evsg` | EVSG commands and measurement streams | `IEvsgDevice`, `EvsgDevice` |
| `Asv.Sdr.Gspn1` | GSPN1 serial commands | `Gspn1Api` |
| `Asv.Sdr.AdSdr` | AD936x/libiio device integration | `AdSdrDevice` |
| `Asv.Sdr.Test` | Existing automated core and simulator tests | xUnit test classes |

These are source projects; the table does not imply that every integration has a
current public NuGet release or implements the same reader interface. Choose an
integration after checking its project and runtime dependencies.

## Core Layers

`Fluent/BufferProcessing` contains block transformations. Most return
`IReaderIqSubject<T>` and preserve a reusable output buffer. `Fluent/SimpleProcessing`
contains measurement streams, such as FFT-bin AM extraction, Goertzel, PLL tracking,
and code identification. `Filters` contains stateful sample-by-sample filters.
`Protocol/ADS-B` contains pulse helpers, parsers, and message models.

The core references `Asv.Common`, `Asv.IO`, `System.Reactive`, and `MathNet.Filtering`.
Use the System.Reactive operators for this API. Other ASV repositories may use R3;
that does not change the observable types used here.

## Versions and Dependencies

Shared target framework and package version properties are in `src/Directory.Build.props`.
Dependency versions are declared in project files or referenced properties such as
`AsvCommonVersion`. Read those files when reproducing a build instead of assuming
that the most recently published NuGet package matches your checkout.

See [Development](development.md) for solution commands and documentation versioning.
