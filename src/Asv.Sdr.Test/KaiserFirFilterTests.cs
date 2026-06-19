using System;
using Xunit;

namespace Asv.Sdr.Test
{
    public class KaiserFirFilterTests
    {
        [Fact]
        public void LowPassPassesLowBandAndRejectsHighBand()
        {
            const int sampleRate = 96_000;
            var passRms = MeasureRms(
                () => new KaiserLowPassFilter(sampleRate, 15_500, 16_500, 60),
                sampleRate,
                10_000
            );
            var stopRms = MeasureRms(
                () => new KaiserLowPassFilter(sampleRate, 15_500, 16_500, 60),
                sampleRate,
                24_000
            );

            Assert.True(passRms > 0.65, $"Expected pass RMS above 0.65, got {passRms}.");
            Assert.True(stopRms < 0.01, $"Expected stop RMS below 0.01, got {stopRms}.");
        }

        [Fact]
        public void BandPassPassesMiddleBandAndRejectsSideBands()
        {
            const int sampleRate = 96_000;
            var passRms = MeasureRms(
                () => new KaiserBandPassFilter(sampleRate, 15_500, 16_500, 31_500, 32_500, 60),
                sampleRate,
                24_000
            );
            var lowStopRms = MeasureRms(
                () => new KaiserBandPassFilter(sampleRate, 15_500, 16_500, 31_500, 32_500, 60),
                sampleRate,
                10_000
            );
            var highStopRms = MeasureRms(
                () => new KaiserBandPassFilter(sampleRate, 15_500, 16_500, 31_500, 32_500, 60),
                sampleRate,
                40_000
            );

            Assert.True(passRms > 0.65, $"Expected pass RMS above 0.65, got {passRms}.");
            Assert.True(lowStopRms < 0.01, $"Expected low stop RMS below 0.01, got {lowStopRms}.");
            Assert.True(highStopRms < 0.01, $"Expected high stop RMS below 0.01, got {highStopRms}.");
        }

        private static double MeasureRms(
            Func<IDspFilter> createFilter,
            int sampleRate,
            double frequency
        )
        {
            const int sampleCount = 8192;
            const int warmupCount = 1024;
            var filter = createFilter();
            var sum = 0.0;
            var count = 0;

            for (var i = 0; i < sampleCount; i++)
            {
                var input = Math.Sin(2.0 * Math.PI * frequency * i / sampleRate);
                var output = filter.Process(input);
                if (i < warmupCount)
                {
                    continue;
                }

                sum += output * output;
                count++;
            }

            return Math.Sqrt(sum / count);
        }
    }
}
