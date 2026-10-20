# DSP and Filters

## Choose a Stage by Data Representation

| Operation | Input and output |
| --- | --- |
| `AddIqDcBlocker(sampleRate, cutoffHz)` | Time-domain IQ; independent I/Q DC suppression, double output |
| `FrequencyShift(sampleRate, freqHz)` | Time-domain IQ multiplied by a complex oscillator; double output |
| `Magnitude()` | Each pair becomes `(sqrt(I*I + Q*Q), 0)`; block length is preserved |
| `WindowFilter(type)` | Multiplies scalar values by the generated window coefficients |
| `Fft1d()` | Time-domain IQ to interleaved complex FFT coefficients |
| `AddIqFilter(iFilter, qFilter)` | Applies separate stateful filters to I and Q |
| `AddIFilter(iFilter)` | Filters I while retaining the paired Q values |
| `Select(...)` | Custom span-based block transformation or per-pair mapping |
| `Preview(...)` | Inspects a block and forwards the same memory |

Float overloads commonly produce double output. Check the method return type
when composing stages. `Magnitude()` is not a packed array of amplitudes: when
copying or exporting its values, use the even indices.

## Frequency Shift and DC Removal

`FrequencyShift` uses the complex rotation `exp(+j * 2*pi*freqHz*n/sampleRate)` and
keeps oscillator state across blocks. It shifts the complex baseband frequency;
it does not retune the hardware local oscillator.

The DC blocker is a stateful first-order high-pass filter with separate state for
each channel. Use a positive sample rate and a cutoff strictly between zero and
half the sample rate. Allow its transient to settle when interpreting measurements.
Removing DC from a carrier-centered signal can remove the carrier used as the
normalization reference by AM extraction; choose stage order for the intended measurement.

## Stateful Filters

`IDspFilter.Process(double)` consumes one scalar value and returns the next filtered
value. Implementations include moving average, median, Kalman, biquad, CIC/sinc,
and Kaiser FIR filters. Construct a separate filter instance per independent signal
or I/Q channel so that histories are not mixed.

```c#
using System;
using Asv.Sdr;

var filter = new MovingAverageDspFilter(windowSize: 4);
foreach (var value in new[] { 1.0, 2.0, 3.0, 4.0 })
{
    Console.WriteLine(filter.Process(value));
}
```

For scalar observables, `AverageFilter(windowSize)`, `MedianFilter(windowSize)`,
and `KalmanFilter(...)` wrap the corresponding filters. A window of 8 means 8
published measurements, not 8 original IQ pairs. Use `AverageRadianFilter` or
`KalmanRadianFilter` for circular angle data instead of an ordinary arithmetic filter.

## Custom Processing

Span callbacks must finish synchronously and fully initialize their output. For
`Select<TIn, TOut>(callback, outputSize)`, `outputSize` is the number of output scalar
values. Keep IQ output even and document any representation change. Avoid retaining
input spans or buffers; see [IQ Pipelines](iq-pipelines.md).

The window implementation currently generates coefficients for the scalar block
length and multiplies element by element. Do not assume an automatic per-complex-pair
window or coherent-gain correction when using it for calibrated amplitude work.

Source: [fluent helpers](https://github.com/asv-soft/asv-sdr/blob/main/src/Asv.Sdr/Fluent/IReaderIq.cs).
