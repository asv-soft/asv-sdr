# asv-sdr

[![NuGet version](https://img.shields.io/nuget/v/Asv.Sdr)](https://www.nuget.org/packages/Asv.Sdr)
[![Build and tests](https://github.com/asv-soft/asv-sdr/actions/workflows/code-quality.yml/badge.svg?branch=main)](https://github.com/asv-soft/asv-sdr/actions/workflows/code-quality.yml)
[![Documentation](https://github.com/asv-soft/asv-sdr/actions/workflows/docs.yml/badge.svg?branch=main)](https://asv.me/asv-sdr/)
[![GitHub Packages](https://img.shields.io/badge/GitHub-Packages-blue?logo=github)](https://github.com/orgs/asv-soft/packages/nuget/package/Asv.Sdr)

`asv-sdr` is a set of .NET libraries for software-defined radio (SDR), IQ stream
processing, digital filters, ADS-B/Mode-S parsing, simulation, and selected SDR
hardware integrations.

The core package provides a fluent, observable-style processing pipeline for IQ
samples. Device-specific projects add bindings and helpers for supported SDR
hardware.

## Documentation

Read the [Asv.Sdr documentation](https://asv.me/asv-sdr/) for the project overview,
installation, development, and documentation publishing instructions.

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

For local development, reference the projects from `src/Asv.Sdr.slnx`.

## Build

From the repository root:

```bash
dotnet restore src/Asv.Sdr.slnx
dotnet build src/Asv.Sdr.slnx -c Release
```

Run tests:

```bash
dotnet test --solution src/Asv.Sdr.slnx -c Release
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

Read the [Getting Started guide](https://asv.me/asv-sdr/getting-started.html) for a
complete hardware-free example. The [FFT and Measurements guide](https://asv.me/asv-sdr/fft-measurements.html)
shows streaming AM extraction with explicit stage disposal.

`GetAm` expects FFT coefficients. Use the processing order
`source.Sample(...)` → `Fft1d()` → `GetAm(sampleRate, 90, 150)`.
Sampler block sizes count scalar values; each IQ pair occupies two values.

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
magnitude samples.

See [ADS-B and Mode-S](https://asv.me/asv-sdr/adsb.html) for parser setup, sample-rate
constraints, and a complete chain attachment example. `Magnitude()` preserves
interleaved `(magnitude, 0)` values; the pulse detector reads the even indices
and emits a packed scalar pulse buffer. `ProcessSample` consumes 0/1 half-bit
decisions including the preamble, rather than packed frame bytes.

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
|   `-- Asv.Sdr.slnx
`-- LICENSE
```

## Versioning

Package versions are defined in project files and shared build properties under
`src/Directory.Build.props`. The documentation build reads `ProductVersion` from
that file and displays it as the documentation version. Changes to the file
trigger the documentation workflow.

The Windows build script uses the latest Git tag via `git describe --tags` and
the `dotnet-setversion` tool before packing the main packages.

## License

This project is licensed under the MIT License. See `LICENSE` for details.

