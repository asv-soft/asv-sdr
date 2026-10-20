# Troubleshooting

| Symptom | Check |
| --- | --- |
| No observable output | Build stages and subscriptions before calling the sampler's `start` action once. Check direct reads and reader-side errors. |
| Stream silently stops | The sampler catches exceptions and exits. Instrument the reader and callbacks; observable `OnError` alone may not reveal the failure. |
| Samples change after callback | Copy reused memory inside the callback before storing or scheduling it. |
| Sample duration or FFT spacing is wrong | Distinguish scalar values from IQ pairs; use the actual device sample rate. |
| Tail of a block looks stale | The sampler publishes a full buffer even after a short read. Handle incomplete blocks in the reader adapter. |
| AM estimate is wrong or non-finite | Feed FFT coefficients to `GetAm`; verify carrier/DC reference, bin alignment, and component order. |
| Simulation runs faster than expected | The simulator generates samples on demand and does not pace wall-clock time. |
| Negative DDM where a positive value was expected | `GetDdm` computes first minus second. Match the simulator type and application subtraction order. |
| ADS-B parser produces no messages | Verify interleaved magnitude input to the detector, half-bit decisions, preamble, sample rate, CRC behavior, and registered factories. |
| Native library fails to load | Match OS and architecture; install the vendor runtime and its dependent libraries. |
| UI freezes or callbacks are slow | Callbacks normally execute in the acquisition path. Copy data before scheduling UI or long-running work. |

## Separate Acquisition from Processing

Reproduce a processing problem with a known input block or the
[simulator](simulation.md). If that works, verify direct hardware reads and configuration.
For retained or recorded data, preserve the sample rate, element type, IQ order,
block length, center frequency, and any preprocessing applied. The core reader
interface does not define a universal recording-file format or metadata container.

## Build and Documentation Problems

Use `src/Asv.Sdr.slnx`, not the older `.sln` filename. Read the target framework
and dependency versions from the checkout. See [Development](development.md).

For Writerside, register every topic in `asv-sdr.tree` and resolve errors in
`artifacts/report.json`. `ProductVersion` is injected into the build copy of
`writerside.cfg`; changing a page title does not change the displayed version.
A local documentation build does not publish GitHub Pages. The publication workflow
must exist in the remote `main` branch and its deploy job must complete successfully.
