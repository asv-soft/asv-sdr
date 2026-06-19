using System;
using System.Numerics;

namespace Asv.Sdr
{
    public class ReaderIqMagnitudeDouble : ReaderIqSubject<double, double>
    {
        public ReaderIqMagnitudeDouble(IReaderIqSubject<double> src, bool useArrayPool)
            : base(src, src.OutputBufferSize, useArrayPool)
        {
        }

        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            ReaderIqMagnitudeProcessor.Process(input, output, OutputBufferSize);
        }
    }

    public class ReaderIqMagnitudeFloat : ReaderIqSubject<float, double>
    {
        public ReaderIqMagnitudeFloat(IReaderIqSubject<float> src, bool useArrayPool)
            : base(src, src.OutputBufferSize, useArrayPool)
        {
        }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            ReaderIqMagnitudeProcessor.Process(input, output, OutputBufferSize);
        }
    }

    internal static class ReaderIqMagnitudeProcessor
    {
        private static readonly double MaxFastPathValue = Math.Sqrt(double.MaxValue / 2.0);
        private const double MinFastPathValue = 1.4916681462400413E-154;

        public static void Process(ReadOnlySpan<double> input, Span<double> output, int outputBufferSize)
        {
            var valueCount = outputBufferSize & ~1;
            var index = 0;

            if (Vector.IsHardwareAccelerated)
            {
                var vectorSize = Vector<double>.Count;
                var vectorLimit = valueCount - valueCount % vectorSize;
                var maxFastPath = new Vector<double>(MaxFastPathValue);
                var minFastPath = new Vector<double>(MinFastPathValue);
                Span<double> squaredValues = stackalloc double[Vector<double>.Count];
                Span<double> magnitudeValues = stackalloc double[Vector<double>.Count];

                for (; index < vectorLimit; index += vectorSize)
                {
                    var values = new Vector<double>(input.Slice(index));
                    var absoluteValues = Vector.Abs(values);
                    if (
                        Vector.GreaterThanAny(absoluteValues, maxFastPath)
                        || HasTinyNonZeroValue(absoluteValues, minFastPath, squaredValues)
                    )
                    {
                        ProcessScalar(input, output, index, vectorSize);
                        continue;
                    }

                    WriteMagnitudeVector(values, output.Slice(index), squaredValues, magnitudeValues);
                }
            }

            ProcessScalar(input, output, index, valueCount - index);
        }

        public static void Process(ReadOnlySpan<float> input, Span<double> output, int outputBufferSize)
        {
            var valueCount = outputBufferSize & ~1;
            var index = 0;

            if (Vector.IsHardwareAccelerated)
            {
                var inputVectorSize = Vector<float>.Count;
                var outputVectorSize = Vector<double>.Count;
                var vectorLimit = valueCount - valueCount % inputVectorSize;
                var maxFastPath = new Vector<float>(float.MaxValue);
                Span<double> squaredValues = stackalloc double[Vector<double>.Count];
                Span<double> magnitudeValues = stackalloc double[Vector<double>.Count];

                for (; index < vectorLimit; index += inputVectorSize)
                {
                    var values = new Vector<float>(input.Slice(index));
                    if (Vector.GreaterThanAny(Vector.Abs(values), maxFastPath))
                    {
                        ProcessScalar(input, output, index, inputVectorSize);
                        continue;
                    }

                    Vector.Widen(values, out var lower, out var upper);
                    WriteMagnitudeVector(
                        lower,
                        output.Slice(index),
                        squaredValues,
                        magnitudeValues
                    );
                    WriteMagnitudeVector(
                        upper,
                        output.Slice(index + outputVectorSize),
                        squaredValues,
                        magnitudeValues
                    );
                }
            }

            ProcessScalar(input, output, index, valueCount - index);
        }

        private static void WriteMagnitudeVector(
            Vector<double> values,
            Span<double> output,
            Span<double> squaredValues,
            Span<double> magnitudeValues
        )
        {
            (values * values).CopyTo(squaredValues);

            for (var offset = 0; offset < Vector<double>.Count; offset += 2)
            {
                magnitudeValues[offset] = squaredValues[offset] + squaredValues[offset + 1];
                magnitudeValues[offset + 1] = 0;
            }

            Vector.SquareRoot(new Vector<double>(magnitudeValues)).CopyTo(output);
        }

        private static bool HasTinyNonZeroValue(
            Vector<double> absoluteValues,
            Vector<double> minFastPath,
            Span<double> values
        )
        {
            if (Vector.LessThanAny(absoluteValues, minFastPath) == false)
            {
                return false;
            }

            absoluteValues.CopyTo(values);
            for (var index = 0; index < values.Length; index++)
            {
                if (values[index] != 0.0 && values[index] < MinFastPathValue)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ProcessScalar(
            ReadOnlySpan<double> input,
            Span<double> output,
            int startIndex,
            int count
        )
        {
            var endIndex = startIndex + count;
            for (var index = startIndex; index < endIndex; index += 2)
            {
                output[index] = DspMathEx.Abs(input[index], input[index + 1]);
                output[index + 1] = 0;
            }
        }

        private static void ProcessScalar(
            ReadOnlySpan<float> input,
            Span<double> output,
            int startIndex,
            int count
        )
        {
            var endIndex = startIndex + count;
            for (var index = startIndex; index < endIndex; index += 2)
            {
                output[index] = DspMathEx.Abs(input[index], input[index + 1]);
                output[index + 1] = 0;
            }
        }
    }

}
