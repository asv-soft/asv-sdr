# Simulation

`Asv.Sdr.Simulate` supplies `VirtualReaderIqIls1F`, an `IReaderIq<double>` that generates
an envelope containing 90 Hz and 150 Hz components. It is useful for exercising
buffer handling and modulation extraction without native libraries or hardware.
See [Getting Started](getting-started.md) for a complete direct-read example.

## Parameters and Component Order

The constructor accepts the IQ-pair sample rate, dimensionless `ddm` and `sdm`
values, and `DdmSdmType`. In the current implementation:

| Type | 90 Hz modulation | 150 Hz modulation |
| --- | --- | --- |
| `AM90_150` | `(sdm - ddm) / 2` | `(sdm + ddm) / 2` |
| `AM150_90` | `(sdm + ddm) / 2` | `(sdm - ddm) / 2` |

For `ddm=0.10`, `sdm=0.60`, and `AM90_150`, the components are 0.25 and 0.35.
This table describes the source implementation; it does not establish a device,
runway, or display sign convention. Match downstream subtraction order explicitly.

Use a positive sample rate and physically meaningful component values. The current
constructor checks upper bounds for DDM and SDM but does not enforce every lower
bound or combination constraint; do not rely on it as a complete signal validator.

## Timing and State

The source fills interleaved I/Q values with equal channel values and advances its
phase accumulator for each IQ pair. Repeated reads continue the generated signal.
Create a new source when a fresh phase origin is required.

`Read` generates a block through `Task.Run`; cancellation is passed to that task.
It does not pace acquisition, simulate USB transport, add noise, or reproduce analog
front-end impairments. A one-second block can be generated in less than one second.
Use even buffer lengths and serialize reads of the same source because its phase
state is mutable.

## Validate a Processing Chain

Start with a direct read, verify block size, and then use the
[streaming AM example](fft-measurements.md). Choose a block containing an integer
number of modulation cycles to reduce FFT-bin leakage. Compare the two modulation
components before applying subtraction, scaling, smoothing, or application thresholds.

Implementation: [VirtualReaderIqIls1F](https://github.com/asv-soft/asv-sdr/blob/main/src/Asv.Sdr.Simulate/VirtualReaderIqILS1F.cs).
