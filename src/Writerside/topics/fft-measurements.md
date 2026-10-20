# FFT and Measurements

## FFT Input and Output

`Fft1d()` treats each adjacent I/Q pair as one complex sample and produces an
interleaved complex spectrum of the same scalar length. It does not convert the
spectrum to decibels, shift the zero-frequency bin to the center, or calibrate it
to dBm. For `N` complex samples at rate `Fs`, the bin spacing is `Fs / N` Hz.

Choose the global backend before constructing FFT stages:

```c#
using Asv.Sdr;

ReaderIqFftSettings.Implementation = ReaderIqFftImplementation.Managed;
```

| Backend | Behavior |
| --- | --- |
| `Alglib` | Default ALGLIB-based transform |
| `Managed` | Radix-2 FFT for power-of-two sizes; DFT fallback for other sizes |
| `ManagedArmOptimized` | ARM64 AdvSimd fast path with managed fallback |

Each stage creates its plan during construction; changing the global setting does
not rebuild existing plans. Use power-of-two blocks for efficient managed transforms,
or select a block length that aligns with the measurement frequencies you need.

## Streaming AM Example

`GetAm` reads FFT bins. Apply it after `Fft1d()`, not directly to time-domain IQ.
It returns a dimensionless modulation fraction: `0.20` means 20%.
The implementation divides twice the selected bin magnitude by the DC-bin magnitude.
A missing carrier reference can therefore produce non-finite or unstable results.

This complete console example uses a one-second block so that 90 Hz and 150 Hz
fall exactly on FFT bins. It runs until Enter is pressed:

```c#
using System;
using System.Reactive.Linq;
using System.Threading;
using Asv.Sdr;
using Asv.Sdr.Simulate;

const int sampleRate = 48_000;
var source = new VirtualReaderIqIls1F(
    sampleRate, ddm: 0.10, sdm: 0.60, DdmSdmType.AM90_150);

using var sampler = source.Sample(
    sampleRate * 2, out var start, priority: ThreadPriority.Normal);
using var spectrum = sampler.Fft1d();
var components = spectrum.GetAm(sampleRate, 90, 150);
using var componentStage = (IDisposable)components;
using var subscription = components.Subscribe(am =>
    Console.WriteLine($"90 Hz: {am.Item1:F3}; 150 Hz: {am.Item2:F3}"));

start();
Console.ReadLine();
sampler.Dispose();
```

The simulator runs as fast as blocks are requested; this is a processing example,
not a real-time signal generator. With these settings the expected components are
approximately 0.25 and 0.35. No window is applied in this example.

`GetDdmSdm()` subtracts tuple item 2 from item 1 and adds the two components.
`GetDdm(second)` likewise computes first minus second. This API order must be chosen
explicitly: the simulator's positive DDM with `AM90_150` puts the larger component
at 150 Hz, so subtracting 150 Hz from 90 Hz reverses that constructor parameter's sign.
These are library arithmetic conventions, not an automatic selection of an operational
radio-navigation sign convention or an acceptance criterion.

## Goertzel and Carrier Tracking

For selected frequencies, `Goertzel(sampleRate, frequencyHz)` works directly on
IQ blocks and returns `ReaderIqGoertzelResult` with `Real`, `Imaginary`, `Magnitude`,
and `Phase` (radians). Do not put an FFT before this operation.

`GetGoertzelMagnitude`, `GetGoertzelPhase`, and `GetGoertzelFrequencyOffset` expose
individual results. The frequency-offset overload with `searchRangeHz` supports a
search around the nominal frequency. Use the actual IQ sample rate.

`TrackCarrierPll(options)` returns frequency and offset in Hz, phase in radians,
tracking counts, magnitude, and `IsLocked`. Inspect lock and signal quality along
with the numeric estimate. Configure `ReaderIqCarrierPllOptions` for the acquisition
rate and expected carrier rather than treating its defaults as calibrated settings.
The older parameterless `FrequencyOffset()` does not accept a sample rate; its raw
result should not be labeled Hz without deriving the conversion from its implementation.

Source: [AM extraction](https://github.com/asv-soft/asv-sdr/blob/main/src/Asv.Sdr/Fluent/SimpleProcessing/ReaderIqAmSubject.cs)
and [FFT backends](https://github.com/asv-soft/asv-sdr/blob/main/src/Asv.Sdr/Fluent/BufferProcessing/ReaderIqFftSettings.cs).
