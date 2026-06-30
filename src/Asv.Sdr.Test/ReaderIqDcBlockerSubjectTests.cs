using System;
using Xunit;

namespace Asv.Sdr.Test
{
    public class ReaderIqDcBlockerSubjectTests
    {
        [Fact]
        public void AddIqDcBlocker_RemovesConstantIqOffset()
        {
            var input = new[]
            {
                2.0,
                -3.0,
                2.0,
                -3.0,
                2.0,
                -3.0,
                2.0,
                -3.0,
            };

            var received = Publish(input);

            Assert.All(received, value => Assert.True(Math.Abs(value) < 1e-12));
        }

        [Fact]
        public void AddIqDcBlocker_RemovesConstantFloatIqOffset()
        {
            var input = new[]
            {
                2.0f,
                -3.0f,
                2.0f,
                -3.0f,
                2.0f,
                -3.0f,
                2.0f,
                -3.0f,
            };

            var received = Publish(input);

            Assert.All(received, value => Assert.True(Math.Abs(value) < 1e-12));
        }

        [Fact]
        public void AddIqDcBlocker_PreservesCarrierAwayFromDc()
        {
            const int sampleRate = 96_000;
            const double carrierHz = 6_000.0;
            const double cutoffHz = 200.0;
            const int iqPairCount = 24_000;
            const int settlePairCount = 2_000;

            var input = new double[iqPairCount * 2];
            var phaseStep = 2.0 * Math.PI * carrierHz / sampleRate;
            for (var index = 0; index < iqPairCount; index++)
            {
                input[index * 2] = 1.75 + Math.Cos(phaseStep * index);
                input[index * 2 + 1] = -0.85 + Math.Sin(phaseStep * index);
            }

            var received = Publish(input, sampleRate, cutoffHz);

            var meanI = 0.0;
            var meanQ = 0.0;
            var magnitudeSum = 0.0;
            var count = iqPairCount - settlePairCount;
            for (var index = settlePairCount; index < iqPairCount; index++)
            {
                var i = received[index * 2];
                var q = received[index * 2 + 1];
                meanI += i;
                meanQ += q;
                magnitudeSum += Math.Sqrt(i * i + q * q);
            }

            meanI /= count;
            meanQ /= count;
            var meanMagnitude = magnitudeSum / count;

            Assert.True(Math.Abs(meanI) < 0.01);
            Assert.True(Math.Abs(meanQ) < 0.01);
            Assert.True(meanMagnitude is > 0.98 and < 1.02);
        }

        private static double[] Publish(
            double[] input,
            double sampleRate = 96_000,
            double cutoffHz = 200.0
        )
        {
            using var source = new TestReaderIqSubject<double>(input.Length);
            using var blocker = source.AddIqDcBlocker(sampleRate, cutoffHz, useArrayPool: false);
            var received = Array.Empty<double>();
            using var subscription = blocker.Subscribe(buffer => received = buffer.ToArray());

            source.Publish(input);

            return received;
        }

        private static double[] Publish(
            float[] input,
            double sampleRate = 96_000,
            double cutoffHz = 200.0
        )
        {
            using var source = new TestReaderIqSubject<float>(input.Length);
            using var blocker = source.AddIqDcBlocker(sampleRate, cutoffHz, useArrayPool: false);
            var received = Array.Empty<double>();
            using var subscription = blocker.Subscribe(buffer => received = buffer.ToArray());

            source.Publish(input);

            return received;
        }
    }
}
