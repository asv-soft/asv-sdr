using System;

namespace Asv.Sdr
{
    public static class DspMathEx
    {
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
    }
}