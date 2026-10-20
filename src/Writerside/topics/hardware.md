# Hardware Integrations

Choose the integration that matches the device and the required data path. Building
a managed project does not confirm that a device can open or that its native runtime
is installed. Verify architecture, vendor runtime, connection, and sample format on
the target machine.

## Integration Map

| Project | Entry point | Runtime boundary |
| --- | --- | --- |
| `Asv.Sdr.LimeSdr` | `ILimeSdrDevice`, `LimeReaderIq`, `LimeWriterIq` | LimeSuite native library and streams |
| `Asv.Sdr.SignalHound` | `ShBbDevice`, `BbApi`, `SaApi`, `SgApi` | Vendor BB/SA/SG libraries, including `bb_api.dll`, `sa_api.dll`, `sg_api.dll` |
| `Asv.Sdr.Msi` | `MsiDevice` | `mirsdrapi-rsp` native API |
| `Asv.Sdr.AdSdr` | `AdSdrDevice` | `libiio-sharp.dll` and its libiio dependencies |
| `Asv.Sdr.Evsg` | `EvsgDevice` | Device connection, commands, and measurement stream |
| `Asv.Sdr.Gspn1` | `Gspn1Api` | Serial commands and responses |

Native binary names describe the current bindings, not a promise of support for all
operating systems. Check the project's imports and vendor distribution for your OS
and process architecture. EVSG and GSPN1 are device-control integrations; they are
not interchangeable implementations of `IReaderIq<T>`.

## LimeSDR Acquisition

`LimeReaderIq` implements `IReaderIq<float>`. Its constructor takes an existing
`ILimeSdrDevice` and `LimeSourceIqConfig`, configures the device, creates an RX stream,
and starts that stream. Construction performs hardware work and can block or fail.

Configuration includes frequency, sample rate, bandwidth, normalized gain, RX path,
channel, low-pass/GFIR filters, calibration, and transport buffering options.
Frequencies and bandwidths are expressed in Hz. Use the configured sample rate in
every downstream DSP stage.

`LimeReaderIq.Read` returns the native complex-sample count and reports a short read
with a console message. The sampler does not crop partial buffers; plan an adapter
or recovery policy when incomplete device reads are possible. Check device and stream
ownership explicitly: keep the device handle available and verify stream stop/cleanup
for the concrete implementation used by the application.

For a float source that needs double-only stages, use `Sample<float, double>`.
See [IQ Pipelines](iq-pipelines.md) before transferring buffers between threads.

## Protocol-Specific LimeSDR Helpers

The LimeSDR project also contains ADS-B, Mode-S, Mode A/C, DME, and IFR6000-related
device classes. Their acquisition profiles, timing, and result models are specific
to those workflows. Select them by the corresponding interface and source rather
than assuming that a generic reader example configures the same measurement path.
This guide does not prescribe transmitter setup or certify measurement accuracy.

## Diagnose Integration Failures

First confirm that the native library can load with the current process architecture.
Then enumerate/open the device, verify configuration, and read a block directly before
adding processing stages. Record short-read counts and acquisition errors separately
from DSP and parser errors. Use the [simulator](simulation.md) to isolate the managed
processing path from hardware issues.

Source: [LimeReaderIq](https://github.com/asv-soft/asv-sdr/blob/main/src/Asv.Sdr.LimeSdr/LimeReaderIq.cs).
