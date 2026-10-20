# Getting Started

## Requirements

Use a .NET SDK that supports the target framework in `src/Directory.Build.props`
(currently .NET 10). The core and simulator can be used without an SDR device.
Physical devices require the corresponding vendor runtime and compatible hardware;
see [Hardware Integrations](hardware.md).

## Create an Application

```bash
dotnet new console -n SdrDemo
cd SdrDemo
dotnet add package Asv.Sdr
dotnet add package Asv.Sdr.Simulate
```

Keep the core and integration package versions aligned. Package releases are available
from [NuGet](https://www.nuget.org/packages/Asv.Sdr) and
[GitHub Packages](https://github.com/orgs/asv-soft/packages/nuget/package/Asv.Sdr).
For unpublished source changes, reference the local projects instead.

## Read a Block Without Hardware

Replace `Program.cs` with this complete example:

```c#
using System;
using Asv.Sdr.Simulate;

const int sampleRate = 48_000;
const int iqPairs = 4_800;
var source = new VirtualReaderIqIls1F(
    sampleRate, ddm: 0.10, sdm: 0.60, DdmSdmType.AM90_150);

var buffer = new double[iqPairs * 2];
await source.Read(buffer);

Console.WriteLine($"IQ pairs: {buffer.Length / 2}");
Console.WriteLine($"First pair: I={buffer[0]:F6}, Q={buffer[1]:F6}");
```

Run it with `dotnet run`. The buffer contains 4,800 complex samples, or 0.1 seconds
of simulated signal time. The simulator generates blocks on demand; it does not
wait for 0.1 seconds of wall-clock time.

## Process a Stream

Use `Sample(readSize, out start)` to turn a reader into an observable block stream.
Build the processing stages and subscribe before calling `start()` once. The block
size is measured in scalar values: 4,800 IQ pairs require 9,600 values.

Continue with [IQ Pipelines](iq-pipelines.md) for lifetime and threading rules and
[FFT and Measurements](fft-measurements.md) for a complete streaming example.
