# ADS-B and Mode-S

The core library provides pulse-processing helpers, an observable message parser,
and ADS-B/Mode-S message models. RF acquisition and message interpretation are
separate layers: receiving a block of IQ data does not itself produce aircraft tracks.

## Parser Input

`AdsbMessageParser.ProcessSample(byte)` consumes 0/1 pulse decisions at half-bit
resolution, including the preamble. It does not accept packed Mode-S frame bytes,
ASCII hexadecimal messages, or raw IQ values. Preserve pulse order and use one
parser instance per independent input stream.

The parser checks frame CRC, obtains a message identifier, creates a registered model,
and publishes deserialized messages. `OnError` reports parser failures. Registering
default messages adds ADS-B identification, position, velocity, status, and related
extended-squitter models. It does not register every Mode-S class in the repository.
Unknown message types are reported as errors; add a suitable factory with `Register`
only when its model matches the identifier being decoded.

```c#
using System;
using System.Reactive.Linq;
using Asv.Sdr;

using var parser = new AdsbMessageParser().RegisterDefaultMessages();
using var messages = parser.OnMessage.Subscribe(Console.WriteLine);
using var errors = parser.OnError.Subscribe(error =>
    Console.Error.WriteLine(error.Message));

// Feed each 0/1 half-bit decision, including the preamble, to parser.ProcessSample.
```

For typed consumers, use `parser.Filter<AdsbAircraftIdentification>()` or apply
System.Reactive operators to `OnMessage`. Keep callbacks short and handle parser
errors independently from device acquisition errors.

## From IQ to Pulse Decisions

The helper chain is:

```text
IQ source -> Magnitude -> AdsbPulseDetector -> AdsbNormalize
          -> AdsbPulseTruncate -> ProcessSample -> OnMessage / OnError
```

`AdsbPulseDetector(sampleRate)`, `AdsbNormalize(sampleRate)`, and
`AdsbPulseTruncate(sampleRate)` derive pulse width with integer division by
2,000,000. Supply an appropriate integer multiple of that rate. For the current
majority decision in `AdsbPulseTruncate`, use at least two samples per half-bit
(for example 4,000,000 IQ pairs/s); a one-sample half-pulse has a zero integer
majority threshold. These helpers are not general-purpose resamplers.

`Magnitude()` retains the interleaved layout `(magnitude, 0)`. The detector reads
only the even-index amplitude values and emits a packed scalar pulse buffer.
Normalization and truncation then operate on that packed output. Do not pack the
magnitudes before `AdsbPulseDetector`: it would skip every second amplitude.
The following method expects a sampled IQ stream at 4 MHz and attaches the pulse
stages to an existing parser:

```c#
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Asv.Sdr;

static IDisposable AttachAdsb(
    IReaderIqSubject<double> iq, AdsbMessageParser parser)
{
    const int sampleRate = 4_000_000;
    var magnitude = iq.Magnitude();
    var detected = magnitude.AdsbPulseDetector(sampleRate);
    var normalized = detected.AdsbNormalize(sampleRate);
    var decisions = normalized.AdsbPulseTruncate(sampleRate);
    var subscription = decisions.Subscribe(block =>
    {
        foreach (var decision in block.Span)
            parser.ProcessSample(decision);
    });
    return new CompositeDisposable(
        subscription, decisions, normalized, detected, magnitude);
}
```

Construct this chain and parser subscriptions before starting acquisition. The returned
disposable owns the added stages; the caller owns the sampler, reader, and parser.
This is a wiring example, not a validated RF receiver performance claim. Check timing,
block boundaries, normalization, CRC statistics, and registered models against known
recordings before interpreting live messages.

The streaming parser uses direct equality between calculated and received CRC.
Do not assume it accepts every Mode-S parity/address-overlay format simply because
a corresponding message model exists. Position reconstruction and aircraft tracking
also require application-level state and interpretation.

Source: [pulse helpers](https://github.com/asv-soft/asv-sdr/blob/main/src/Asv.Sdr/Protocol/ADS-B/AdsbHelper.cs)
and [parser](https://github.com/asv-soft/asv-sdr/blob/main/src/Asv.Sdr/Protocol/ADS-B/Parser/AdsbMessageParser.cs).

## IFR6000 Pulse Amplitude Variation

The current IFR6000 register protocol reports the worst within-reply pulse
amplitude variation over a batch of 200 interrogations. Decode the unsigned
Q12.4 register values directly:

`variationDb = raw / 16.0`

No additional logarithmic-base correction is applied. Do not combine the A and
C registers as a min/max pair. The dB APIs and the A/C snapshot use the same
conversion.

| Register | Measurement | API |
| --- | --- | --- |
| `0x0333` | Mode A, `RatioAmpDb_A` | `ReadModeAPulseVariationDb()` |
| `0x0334` | Mode C, `RatioAmpDb_C` | `ReadModeCPulseVariationDb()` |
| `0x0335` | Mode S, `RatioAmpDb_S` | `ReadModeSPulseVariationDb()` |

For example, raw `24` is `1.5 dB`, raw `32` is `2.0 dB`, and raw `33` is
`2.0625 dB`. The dB APIs preserve Q12.4 precision.
`Ifr6000ModeAcSnapshot.ModeAPulseVariation` and
`ModeCPulseVariation` remain nullable bytes in tenths of a dB, rounded away from
zero at midpoint and saturated at 255. A mode with no replies has a null
variation. Establish a fresh reply batch before interpreting the measurements.

Mode S short and long replies share register `0x0335`. The caller configures
the required reply length, waits for a fresh matching reply batch, then calls
`ReadModeSPulseVariationDb()` and attributes that result to the selected length.
Two consecutive reads without changing the acquisition context do not provide
independent short and long measurements.

The SDK always reads and decodes this register, including zero. Firmware that
currently reports zero produces `0.0 dB`; a later implementation requires no
SDK change if the register protocol remains the same. The two former
short/long-specific methods are replaced by this single reader.
