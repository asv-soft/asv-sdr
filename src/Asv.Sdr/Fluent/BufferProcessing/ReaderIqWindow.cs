using System;
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
            for (var i = 0; i < _filter.Length; i++)
            {
                output[i] = input[i] * _filter[i];
            }
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
            for (var i = 0; i < _filter.Length; i++)
            {
                output[i] = input[i] * _filter[i];
            }
        }
    }
}
