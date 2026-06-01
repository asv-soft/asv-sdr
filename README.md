# asv-sdr

`asv-sdr` is a set of .NET libraries for software-defined radio (SDR), IQ stream
processing, digital filters, ADS-B/Mode-S parsing, simulation, and selected SDR
hardware integrations.

The core package provides a fluent, observable-style processing pipeline for IQ
samples. Device-specific projects add bindings and helpers for supported SDR
hardware.

## Features

- Fluent IQ stream processing based on `IReaderIq<T>` and `IReaderIqSubject<T>`.
- DSP helpers for magnitude, phase, differential phase, FFT, frequency shift,
  overlap, windowing, I/Q manipulation, and custom transforms.
- Reusable filters: moving average, median, Kalman, biquad, sinc/CIC, elliptic,
  pulse correlation, and window filters.
- ADS-B and Mode-S frame detection, normalization, parsing, and message models.
- AM/FM helpers for extracting modulation, DDM/SDM, frequency offset, and code ID.
- Simulation source for ILS-like IQ signals.
- Hardware integration projects for LimeSDR, Signal Hound, MSI SDR, EVSG, GSPN1,
  and AD936x/libiio-based SDR devices.

## Projects

| Project | Purpose |
| --- | --- |
| `Asv.Sdr` | Core DSP, fluent IQ processing, filters, math helpers, ADS-B/Mode-S parser. |
| `Asv.Sdr.Simulate` | Virtual IQ sources for tests and offline processing experiments. |
| `Asv.Sdr.LimeSdr` | LimeSDR reader/writer and protocol-specific helpers. |
| `Asv.Sdr.SignalHound` | Signal Hound BB/SA/SG API bindings and helpers. |
| `Asv.Sdr.Msi` | MSI/SDRplay-compatible native API wrapper. |
| `Asv.Sdr.Evsg` | EVSG device integration. |
| `Asv.Sdr.Gspn1` | GSPN1 serial device integration. |
| `Asv.Sdr.AdSdr` | AD936x/libiio-based SDR integration. |
| `Asv.Sdr.Test` | xUnit test project. |

## Requirements

- .NET SDK with support for the target frameworks used by the solution.
- The current shared target framework is configured in
  `src/Directory.Build.props` as `net10.0`.
- Some device projects require native vendor libraries to be installed and
  discoverable through the runtime library path.

Native library expectations include:

- LimeSDR: `LimeSuite`
- Signal Hound: `bb_api.dll`, `sa_api.dll`, `sg_api.dll`
- MSI/SDRplay-compatible wrapper: `mirsdrapi-rsp`
- AD936x/libiio project: `libiio-sharp.dll`

## Installation

Use the package that matches the layer you need:

```bash
dotnet add package Asv.Sdr
dotnet add package Asv.Sdr.Simulate
dotnet add package Asv.Sdr.LimeSdr
dotnet add package Asv.Sdr.SignalHound
```

For local development, reference the projects from `src/Asv.Sdr.sln`.

## Build

From the repository root:

```bash
dotnet restore src/Asv.Sdr.sln
dotnet build src/Asv.Sdr.sln -c Release
```

Run tests:

```bash
dotnet test src/Asv.Sdr.Test/Asv.Sdr.Test.csproj -c Release
```

Create NuGet packages:

```bash
dotnet pack src/Asv.Sdr/Asv.Sdr.csproj -c Release
dotnet pack src/Asv.Sdr.Simulate/Asv.Sdr.Simulate.csproj -c Release
dotnet pack src/Asv.Sdr.LimeSdr/Asv.Sdr.LimeSdr.csproj -c Release
dotnet pack src/Asv.Sdr.SignalHound/Asv.Sdr.SignalHound.csproj -c Release
```

On Windows, the repository also contains `build.bat`, which updates project
versions from the latest Git tag, restores, builds, and packs the main packages.

## Quick Start

The example below uses the simulator package to create an IQ source and process
90 Hz / 150 Hz AM components through the fluent API.

```csharp
using System;
using Asv.Sdr;
using Asv.Sdr.Simulate;

const int sampleRate = 86_000;

var source = new VirtualReaderIqIls1F(
    sampleRate,
    ddm: 0.40,
    sdm: 0.90,
    type: DdmSdmType.AM90_150);

var sampler = source.Sample(sampleRate, out var start).Parallel();

var am90 = sampler.GetAm(sampleRate, 90);
var am150 = sampler.GetAm(sampleRate, 150);

using var subscription = am90
    .GetDdm(am150)
    .AverageFilter(windowSize: 8)
    .Subscribe(ddm => Console.WriteLine($"DDM: {ddm:F6}"));

start();

Console.ReadLine();
```

## FFT Backend

`Fft1d()` uses ALGLIB by default to preserve existing behavior. You can switch
the FFT implementation globally before creating FFT pipeline stages:

```csharp
using Asv.Sdr;

ReaderIqFftSettings.Implementation = ReaderIqFftImplementation.Alglib;
// or
ReaderIqFftSettings.Implementation = ReaderIqFftImplementation.Managed;
// or
ReaderIqFftSettings.Implementation = ReaderIqFftImplementation.ManagedArmOptimized;
```

Available implementations:

- `Alglib`: existing ALGLIB-backed FFT implementation.
- `Managed`: built-in managed FFT implementation. It uses a radix-2 FFT for
  power-of-two transform sizes and a DFT fallback for other sizes.
- `ManagedArmOptimized`: managed FFT with an ARM64 AdvSimd fast path for
  radix-2 transforms. It falls back to the regular managed implementation when
  ARM64 SIMD is not available.

## ADS-B Processing

`Asv.Sdr` includes helpers for building an ADS-B processing chain from IQ
magnitude samples:

```csharp
using Asv.Sdr;

var parser = new AdsbMessageParser()
    .RegisterDefaultMessages();

using var messages = parser.OnMessage.Subscribe(message =>
{
    Console.WriteLine(message);
});

// Typical processing stages:
// iq.Magnitude()
//   .AdsbPulseDetector(sampleRate)
//   .AdsbNormalize(sampleRate)
//   .AdsbPulseTruncate(sampleRate)
//   .Subscribe(bits => { foreach (var bit in bits.Span) parser.ProcessSample(bit); });
```

## Repository Layout

```text
.
|-- build.bat
|-- publish_nuget.bat
|-- src
|   |-- Asv.Sdr
|   |-- Asv.Sdr.Simulate
|   |-- Asv.Sdr.LimeSdr
|   |-- Asv.Sdr.SignalHound
|   |-- Asv.Sdr.Msi
|   |-- Asv.Sdr.Evsg
|   |-- Asv.Sdr.Gspn1
|   |-- Asv.Sdr.AdSdr
|   |-- Asv.Sdr.Test
|   `-- Asv.Sdr.sln
`-- LICENSE
```

## Versioning

Package versions are defined in project files and shared build properties under
`src/Directory.Build.props`. The current `ProductVersion` is `1.5.34`.

The Windows build script uses the latest Git tag via `git describe --tags` and
the `dotnet-setversion` tool before packing the main packages.

## License

This project is licensed under the MIT License. See `LICENSE` for details.

