using System;
using System.Collections.Generic;
using System.Linq;

namespace Asv.Sdr
{
    public static class DspMathEx
    {
        private const double Pi2 = 2 * Math.PI;
        private const double PiDev180Deg = Math.PI / 180.0;
        private const double Deg180DevPi = 180.0 / Math.PI;

        public static double Phase(double x, double y)
        {
            return Math.Atan2(y, x);
        }

        public static (double, double) FromPolarCoordinates(double magnitude, double phase) => (magnitude * Math.Cos(phase), magnitude * Math.Sin(phase));

        public static double Abs(double x, double y)
        {
            if (double.IsInfinity(x) || double.IsInfinity(y))
                return double.PositiveInfinity;
            var num1 = Math.Abs(x);
            var num2 = Math.Abs(y);
            if (num1 > num2)
            {
                var num3 = num2 / num1;
                return num1 * Math.Sqrt(1.0 + num3 * num3);
            }
            if (num2 == 0.0)
                return num1;
            var num4 = num1 / num2;
            return num2 * Math.Sqrt(1.0 + num4 * num4);
        }

        public static void CopyTo(this ReadOnlySpan<float> src, Span<double> destination)
        {
            for (var i = 0; i < src.Length; i++)
            {
                destination[i] = src[i];
            }
        }

        public static double GetDistanceAngleDeg(double a, double b)
        {
            // https://en.wikipedia.org/wiki/Mean_of_circular_quantities
            var distance = (a - b) % 360;
            if (distance < -180)
                distance += 360;
            else if (distance > 179)
                distance -= 360;
            return distance;
        }

        public static double GetDistanceAngleRad(double a, double b)
        {
            var distance = a - b;

            while (distance >= Pi2)
            {
                distance -= Pi2;
            }

            while (distance <= -Pi2)
            {
                distance += Pi2;
            }

            if (distance < -Math.PI)
                distance += Pi2;
            else if (distance > Math.PI)
                distance -= Pi2;

            return distance;

        }

        public static double GetAvgAngleRad(IEnumerable<double> angles)
        {
            // https://rosettacode.org/wiki/Averages/Mean_angle#C.23
            var sumI = 0.0;
            var sumQ = 0.0;
            var count = 0;
            foreach (var angle in angles)
            {
                sumI += Math.Cos(angle);
                sumQ += Math.Sin(angle);
                ++count;
            }

            return Math.Atan2(sumQ / count, sumI / count);
        }
        public static double GetAvgAngleDeg(IEnumerable<double> angles)
        {
            // https://rosettacode.org/wiki/Averages/Mean_angle#C.23
            var sumI = 0.0;
            var sumQ = 0.0;
            var count = 0;
            

            foreach (var angle in angles)
            {
                sumI += Math.Cos(angle* PiDev180Deg);
                sumQ += Math.Sin(angle* PiDev180Deg);
                ++count;
            }

            return Math.Atan2(sumQ / count, sumI / count) * Deg180DevPi;
        }

        // public static double GetAvgAngleDeg(double[] angles)
        // {
        //     // https://rosettacode.org/wiki/Averages/Mean_angle#C.23
        //     var x = angles.Sum(a => Math.Cos(a * Math.PI / 180)) / angles.Length;
        //     var y = angles.Sum(a => Math.Sin(a * Math.PI / 180)) / angles.Length;
        //     return Math.Atan2(y, x) * 180 / Math.PI;
        // }

        /// <summary>
        /// Calculate мВт в мкВ
        /// </summary>
        /// <param name="mW"></param>
        /// <param name="ohm"></param>
        /// <returns></returns>
        public static double mW2uV(double mW, double ohm)
        {
            return Math.Sqrt((mW / 1000) * ohm) * 1e6;
        }
        /// <summary>
        /// Calculate дБм в мВт
        /// </summary>
        /// <param name="mW"></param>
        /// <returns></returns>
        public static double mW2dBm(double mW)
        {
            return 10 * Math.Log10(mW);
        }

        /// <summary>
        /// Calculate мВт в дБм
        /// </summary>
        /// <param name="dBm"></param>
        /// <returns></returns>
        public static double dBm2mW(double dBm)
        {
            return Math.Pow(10, dBm / 10);
        }

        /// <summary>
        /// Calculate dBm в мкВ
        /// </summary>
        /// <param name="dBm"></param>
        /// <param name="ohm"></param>
        /// <returns></returns>
        public static double dBm2uV(double dBm, double ohm)
        {
            return mW2uV(dBm2mW(dBm), ohm);
        }

        /// <summary>
        ///   Interpolates data using a piece-wise linear function.
        /// </summary>
        /// <param name="value">The value to be calculated.</param>
        /// <param name="x">The input data points <c>x</c>. Those values need to be sorted.</param>
        /// <param name="y">The output data points <c>y</c>.</param>
        /// <param name="lower">
        /// The value to be returned for values before the first point in <paramref name="x" />.</param>
        /// <param name="upper">
        /// The value to be returned for values after the last point in <paramref name="x" />.</param>
        /// <returns>Computes the output for f(value) by using a piecewise linear
        /// interpolation of the data points <paramref name="x" /> and <paramref name="y" />.</returns>
        public static double Interpolate1D(
            double value,
            double[] x,
            double[] y,
            double lower,
            double upper)
        {
            for (int index1 = 0; index1 < x.Length; ++index1)
            {
                if (value < x[index1])
                {
                    if (index1 == 0)
                        return lower;
                    int index2 = index1 - 1;
                    int index3 = index1;
                    double num = (value - x[index2]) / (x[index3] - x[index2]);
                    return y[index2] + (y[index3] - y[index2]) * num;
                }
            }
            return upper;
        }
    }
}