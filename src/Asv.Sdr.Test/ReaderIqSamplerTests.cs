using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Asv.Sdr.Test
{
    public class ReaderIqSamplerTests
    {
        [Fact]
        public void SampleFloatDoubleReadsFloatAndPublishesDouble()
        {
            using var source = new OneShotFloatReader(new[] { 1.25f, -2.5f, 3.75f, -4.0f });
            using var sampler = source.Sample<float, double>(
                4,
                out var start,
                useArrayPool: false,
                priority: ThreadPriority.Normal
            );
            var received = Array.Empty<double>();
            using var receivedEvent = new ManualResetEventSlim(false);
            using var subscription = sampler.Subscribe(buffer =>
            {
                received = buffer.ToArray();
                receivedEvent.Set();
            });

            start();

            Assert.True(receivedEvent.Wait(TimeSpan.FromSeconds(2)));
            Assert.Equal(new[] { 1.25, -2.5, 3.75, -4.0 }, received);
        }

        private sealed class OneShotFloatReader : IReaderIq<float>, IDisposable
        {
            private readonly float[] _samples;
            private int _readCount;

            public OneShotFloatReader(float[] samples)
            {
                _samples = samples;
            }

            public async Task<int> Read(Memory<float> iqBuffer, CancellationToken cancel = default)
            {
                if (Interlocked.Exchange(ref _readCount, 1) == 0)
                {
                    _samples.CopyTo(iqBuffer);
                    return _samples.Length;
                }

                await Task.Delay(Timeout.InfiniteTimeSpan, cancel);
                return 0;
            }

            public void Dispose()
            {
            }
        }
    }
}
