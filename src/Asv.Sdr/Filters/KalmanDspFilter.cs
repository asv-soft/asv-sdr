using System;

namespace Asv.Sdr
{

    public class KalmanRadianDspFilter:IDspFilter
    {
        private readonly KalmanDspFilter _cos;
        private readonly KalmanDspFilter _sin;

        public KalmanRadianDspFilter(double q, double r, double f = 1, double h = 1)
        {
            _cos = new KalmanDspFilter(q, r, f, h);
            _sin = new KalmanDspFilter(q, r, f, h);
        }
        public double Process(double input)
        {
            var cos = Math.Cos(input);
            var sin = Math.Sin(input);
            var x = _cos.Process(cos);
            var y = _sin.Process(sin);
            return Math.Atan2(y, x);

        }
    }
    public class KalmanDspFilter:IDspFilter
    {
        /// <summary>
        /// predicted state
        /// </summary>
        private double _x0;

        /// <summary>
        /// predicted covariance
        /// </summary>
        private double _p0;

        /// <summary>
        /// factor of real value to previous real value
        /// </summary>
        private readonly double _f;

        /// <summary>
        /// measurement noise
        /// </summary>
        private readonly double _q;

        /// <summary>
        /// factor of measured value to real value
        /// </summary>
        private readonly double _h;

        /// <summary>
        ///  environment noise
        /// </summary>
        private readonly double _r;

        private double _state;
        private double _covariance;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="q">measurement noise</param>
        /// <param name="r">environment noise</param>
        /// <param name="f">factor of real value to previous real value</param>
        /// <param name="h">factor of measured value to real value</param>
        public KalmanDspFilter(double q, double r, double f = 1, double h = 1)
        {
            _q = q;
            _r = r;
            _f = f;
            _h = h;
        }

        public void SetState(double state, double covariance)
        {
            _state = state;
            _covariance = covariance;
        }

        public double Process(double input)
        {
            //time update - prediction
            _x0 = _f * _state;
            _p0 = _f * _covariance * _f + _q;

            //measurement update - correction
            var k = _h * _p0 / (_h * _p0 * _h + _r);
            _state = _x0 + k * (input - _h * _x0);
            _covariance = (1 - k * _h) * _p0;
            return _state;
        }
    }
}