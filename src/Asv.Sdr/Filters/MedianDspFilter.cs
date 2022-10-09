using MathNet.Filtering.Median;

namespace Asv.Sdr
{
    public class MedianDspFilter:OnlineMedianFilter,IDspFilter
    {
        public MedianDspFilter(int windowSize) : base(windowSize)
        {

        }

        public double Process(double input)
        {
            return base.ProcessSample(input);
        }
    }
}
