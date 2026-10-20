# IQ Pipelines

## Buffer Layout

IQ streams normally use interleaved scalar values:

```text
I0, Q0, I1, Q1, I2, Q2, ...
```

A buffer with `2 * N` values represents `N` complex samples. Use even buffer sizes.
The sample rate used by DSP stages is the number of IQ pairs per second.

| API | Size parameter or output |
| --- | --- |
| `Sample(readSize, out start)` | `readSize` scalar values per block |
| `OutputBufferSize` | Scalar values published in a block |
| `SplitSample(iqPairs)` | `iqPairs` complex samples; output size is twice this value |
| `CopyToArray()` | Separate I and Q arrays, each half the source block length |

For 48,000 pairs/s and a 9,600-value block, each block represents 0.1 seconds.
Changing block size changes processing latency and FFT resolution; it does not
change the source sample rate.

## Reader Contract and Sampler Behavior

`IReaderIq<T>.Read(Memory<T>, CancellationToken)` supplies data to a caller-owned
buffer. Reader implementations differ in their returned count convention: the
simulator returns the scalar length, while `LimeReaderIq` returns the native IQ-pair
count. Check the concrete reader when reading directly.

The current sampler publishes its entire fixed-size buffer and does not use the
returned count to shorten it. A reader adapter used with `Sample` must therefore
fill the requested block or handle partial reads before returning. A zero-length
or short read is not an end-of-stream signal for this sampler.

The generic conversion overload `Sample<float, double>(...)` converts float input
to double output. Other type conversions currently throw `NotSupportedException`.

## Construction and Lifetime

1. Create the source and sampler.
2. Create each processing stage and keep its disposable handle.
3. Subscribe to the result stream.
4. Call the returned `start` action once.
5. Stop acquisition and release the sampler, stages, subscriptions, and device when finished.

Stages usually subscribe to their inputs during construction. Disposing the final
subscription alone does not dispose the full chain. Most stages detach their own
input subscription when disposed; they do not own every upstream stage. `Parallel()`
is an exception: its implementation also owns its source. Prefer explicit lifetime
management over assuming recursive disposal.

## Memory Ownership

Published `Memory<T>` normally refers to a stage-owned buffer that is reused on the
next block. Process it within the callback. To keep data, call `ToArray()` before
the callback returns. Copy before scheduling asynchronous work or using `ObserveOn`.
`CopyToArray()` creates independent I and Q arrays for each published block, at the
cost of allocations. Dispose its stage as well as the downstream subscription.

`useArrayPool` changes buffer allocation, not ownership. A pooled buffer is still
borrowed data and must not be retained after its producer is disposed.

## Threading and Errors

Ordinary stages process synchronously when the source publishes. Subscribers should
finish quickly. `Parallel()` dispatches subscribers to workers and waits for all
of them for each block; it is not an unbounded queue that permits buffer retention.

The sampler starts a background thread and awaits reader operations. Do not assume
that every callback stays on one thread or runs on the UI thread. Marshal copied
results to a UI scheduler when integrating with an application.

The current sampler exits its loop on read or callback exceptions without forwarding
those exceptions as an observable error. Instrument the concrete reader and monitor
acquisition progress if your application needs fault reporting.

Implementation: [sampler](https://github.com/asv-soft/asv-sdr/blob/main/src/Asv.Sdr/Fluent/ReaderIqSampler.cs)
and [buffer stages](https://github.com/asv-soft/asv-sdr/blob/main/src/Asv.Sdr/Fluent/BufferProcessing/ReaderIqSubject.cs).
