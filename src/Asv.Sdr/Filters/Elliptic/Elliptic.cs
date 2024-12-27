using System;

namespace Asv.Sdr
{
    public abstract class EllipticFilterBase : IDspFilter
    {
        private readonly double[] _states;
        private readonly double[] _zNum;
        private readonly double[] _zDen;

        protected EllipticFilterBase(double[] zNum, double[] zDen)
        {
            ArgumentNullException.ThrowIfNull(zNum);

            ArgumentNullException.ThrowIfNull(zDen);

            if (zNum.Length != ((zDen.Length / 2) + 1))
            {
                throw new ArgumentException("Invalid argument length for Elliptic Filter");
            }

            _states = new double[zDen.Length];
            _zNum = zNum;
            _zDen = zDen;
        }

        public double Process(double sample)
        {
            var sumDen = 0.0;
            var sumNum = 0.0;
            var lastIndex = _states.Length - 1;
            for (var i = 0; i < _states.Length; i++)
            {
                sumDen += _states[i] * _zDen[i];
                sumNum += _states[i] * _zNum[i < _zNum.Length ? i : _states.Length - i];
                if (i < lastIndex)
                {
                    _states[i] = _states[i + 1];
                }
            }

            _states[lastIndex] = sample - sumDen;
            sumNum += _states[lastIndex] * _zNum[0];
            return sumNum;
        }
    }

    public class CustomLowPassElliptic8kHzFilter : EllipticFilterBase
    {
        public CustomLowPassElliptic8kHzFilter()
            : base(
                new[] { 3.55913675e-02, 1.543561252e-02, 1.554841621e-02, 7.140834236e-02 },
                new[]
                {
                    9.78033577e-02,
                    -0.6706407089,
                    2.124672252,
                    -3.813517492,
                    4.307268182,
                    -2.841026455,
                }
            ) { }
    }
}
