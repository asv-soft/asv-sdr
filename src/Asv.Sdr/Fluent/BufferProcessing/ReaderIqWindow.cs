using System;
using System.Numerics;
using System.Reactive;

namespace Asv.Sdr
{
    public class ReaderIqWindowFilterDouble : ReaderIqSubject<double, double>
    {
        private readonly double[] _filter;

        public ReaderIqWindowFilterDouble(
            IReaderIqSubject<double> input,
            WindowFilterEnum type,
            bool useArrayPool
        )
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            _filter = WindowFilters.Create(type, input.OutputBufferSize);
        }

        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            ReaderIqWindowFilterProcessor.Process(input, _filter, output);
        }
    }

    public class ReaderIqWindowFilterFloat : ReaderIqSubject<float, double>
    {
        private readonly double[] _filter;

        public ReaderIqWindowFilterFloat(
            IReaderIqSubject<float> input,
            WindowFilterEnum type,
            bool useArrayPool
        )
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            _filter = WindowFilters.Create(type, input.OutputBufferSize);
        }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            ReaderIqWindowFilterProcessor.Process(input, _filter, output);
        }
    }

    internal static class ReaderIqWindowFilterProcessor
    {
        public static void Process(ReadOnlySpan<double> input, ReadOnlySpan<double> filter, Span<double> output)
        {
            var index = 0;

            if (Vector.IsHardwareAccelerated)
            {
                var vectorSize = Vector<double>.Count;
                var vectorLimit = filter.Length - filter.Length % vectorSize;

                for (; index < vectorLimit; index += vectorSize)
                {
                    (
                        new Vector<double>(input.Slice(index))
                        * new Vector<double>(filter.Slice(index))
                    ).CopyTo(output.Slice(index));
                }
            }

            for (; index < filter.Length; index++)
            {
                output[index] = input[index] * filter[index];
            }
        }

        public static void Process(ReadOnlySpan<float> input, ReadOnlySpan<double> filter, Span<double> output)
        {
            var index = 0;

            if (Vector.IsHardwareAccelerated)
            {
                var inputVectorSize = Vector<float>.Count;
                var outputVectorSize = Vector<double>.Count;
                var vectorLimit = filter.Length - filter.Length % inputVectorSize;

                for (; index < vectorLimit; index += inputVectorSize)
                {
                    Vector.Widen(new Vector<float>(input.Slice(index)), out var lower, out var upper);
                    (lower * new Vector<double>(filter.Slice(index))).CopyTo(output.Slice(index));
                    (upper * new Vector<double>(filter.Slice(index + outputVectorSize))).CopyTo(
                        output.Slice(index + outputVectorSize)
                    );
                }
            }

            for (; index < filter.Length; index++)
            {
                output[index] = input[index] * filter[index];
            }
        }
    }
}
