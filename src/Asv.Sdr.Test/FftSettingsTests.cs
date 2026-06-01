using Xunit;

namespace Asv.Sdr.Test
{
    public class FftSettingsTests
    {
        [Theory]
        [InlineData(8)]
        [InlineData(6)]
        public void ManagedFftMatchesAlglib(int complexSampleCount)
        {
            var source = CreateSourceBuffer(complexSampleCount);
            var alglibOutput = (double[])source.Clone();
            var managedOutput = (double[])source.Clone();

            new AlglibReaderIqFftPlan(complexSampleCount).Transform(alglibOutput);
            new ManagedReaderIqFftPlan(complexSampleCount).Transform(managedOutput);

            for (var i = 0; i < alglibOutput.Length; i++)
            {
                Assert.Equal(alglibOutput[i], managedOutput[i], 10);
            }
        }

        [Fact]
        public void ArmOptimizedFftMatchesAlglib()
        {
            const int complexSampleCount = 8;
            var source = CreateSourceBuffer(complexSampleCount);
            var alglibOutput = (double[])source.Clone();
            var armOutput = (double[])source.Clone();

            new AlglibReaderIqFftPlan(complexSampleCount).Transform(alglibOutput);
            new ArmOptimizedReaderIqFftPlan(complexSampleCount).Transform(armOutput);

            for (var i = 0; i < alglibOutput.Length; i++)
            {
                Assert.Equal(alglibOutput[i], armOutput[i], 10);
            }
        }

        [Fact]
        public void DefaultFftImplementationIsAlglib()
        {
            Assert.Equal(ReaderIqFftImplementation.Alglib, ReaderIqFftSettings.Implementation);
        }

        private static double[] CreateSourceBuffer(int complexSampleCount)
        {
            var buffer = new double[complexSampleCount * 2];
            for (var i = 0; i < complexSampleCount; i++)
            {
                buffer[i * 2] = i + 1;
                buffer[i * 2 + 1] = i % 3 - 1;
            }

            return buffer;
        }
    }
}
