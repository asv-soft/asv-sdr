# Asv.Sdr

Asv.Sdr is a set of .NET libraries for software-defined radio, IQ stream processing,
digital filters, ADS-B/Mode-S message handling, simulation, and hardware integration.
The core pipeline uses `IReaderIq<T>` sources and observable processing stages.

## Choose a Guide

| Task | Guide |
| --- | --- |
| Install packages and read your first IQ block | [Getting Started](getting-started.md) |
| Choose a library or locate its implementation | [Packages and Architecture](packages.md) |
| Connect a source, handle buffers, and release resources | [IQ Pipelines](iq-pipelines.md) |
| Transform samples and filter measurements | [DSP and Filters](dsp-filters.md) |
| Compute spectra and extract AM components | [FFT and Measurements](fft-measurements.md) |
| Generate repeatable IQ input without hardware | [Simulation](simulation.md) |
| Connect pulse detection to a message parser | [ADS-B and Mode-S](adsb.md) |
| Integrate a physical device | [Hardware Integrations](hardware.md) |
| Diagnose missing data and incorrect results | [Troubleshooting](troubleshooting.md) |
| Build, test, and publish documentation | [Development](development.md) |

## Data Flow

A reader fills an interleaved IQ buffer. A sampler publishes blocks. Buffer stages
transform those blocks; measurement stages publish scalar values or tuples.
Subscribers consume results through System.Reactive observables.

Keep the data representation explicit at every boundary: raw IQ, FFT coefficients,
envelope values, and pulse decisions have different meanings even when they use
the same numeric element type. Start with [IQ Pipelines](iq-pipelines.md).

Source code, issues, and the package release workflows are in the
[GitHub repository](https://github.com/asv-soft/asv-sdr).
