using System.Linq;
using Asv.Common;

namespace Asv.Sdr
{
    public class MovingAverageDspFilter : IDspFilter
    {
        private readonly CircularBuffer2<double> _window;

        public MovingAverageDspFilter(int windowSize)
        {
            _window = new CircularBuffer2<double>(windowSize);
        }

        public double Process(double input)
        {
            _window.PushFront(input);
            return _window.Average();
        }
    }

    public class MovingAverageRadianDspFilter : IDspFilter
    {
        private readonly CircularBuffer2<double> _window;

        public MovingAverageRadianDspFilter(int windowSize)
        {
            _window = new CircularBuffer2<double>(windowSize);
        }

        public double Process(double input)
        {
            _window.PushFront(input);
            return DspMathEx.GetAvgAngleRad(_window);
        }
    }
}
