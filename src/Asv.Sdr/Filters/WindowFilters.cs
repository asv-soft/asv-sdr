using System;

namespace Asv.Sdr
{
    public enum WindowFilterEnum
    {
        None,
        Hamming,
        Hann,
        Cosine
    }

    public static class WindowFilters
    {
        public static double[] Create(WindowFilterEnum filter, int width)
        {
            return filter switch
            {
                WindowFilterEnum.None => None(width),
                WindowFilterEnum.Hamming => Hamming(width),
                WindowFilterEnum.Hann => Hann(width),
                WindowFilterEnum.Cosine => Cosine(width),
                _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
            };
        }

        public static double[] None(int width)
        {
            var numArray = new double[width];
            Array.Fill(numArray, 1.0);
            return numArray;
        }

        public static double[] Hamming(int width)
        {
            var num = 2.0 * Math.PI / (double)width;
            var numArray = new double[width];
            for (var index = 0; index < numArray.Length; ++index)
                numArray[index] = 0.53836 + -0.46164 * Math.Cos((double)index * num);
            return numArray;
        }
        public static double[] Hann(int width)
        {
            var num = 2.0 * Math.PI / (double)width;
            var numArray = new double[width];
            for (var index = 0; index < numArray.Length; ++index)
                numArray[index] = 0.5 - 0.5 * Math.Cos((double)index * num);
            return numArray;
        }
        public static double[] Cosine(int width)
        {
            var num = Math.PI / (double)width;
            var numArray = new double[width];
            for (var index = 0; index < numArray.Length; ++index)
                numArray[index] = Math.Sin((double)index * num);
            return numArray;
        }
    }
}
