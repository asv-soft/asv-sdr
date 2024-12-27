using System;

namespace Asv.Sdr
{
    public class ReaderIqMoveIToQDouble : ReaderIqSubject<double, double>
    {
        private readonly int _size;

        public ReaderIqMoveIToQDouble(IReaderIqSubject<double> input, bool useArrayPool)
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            _size = input.OutputBufferSize / 2;
        }

        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            for (var i = 0; i < _size; i++)
            {
                output[(i * 2) + 1] = input[i * 2];
                output[i * 2] = 0;
            }
        }
    }

    public class ReaderIqMoveIToQFloat : ReaderIqSubject<float, double>
    {
        private readonly int _size;

        public ReaderIqMoveIToQFloat(IReaderIqSubject<float> input, bool useArrayPool)
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            _size = input.OutputBufferSize / 2;
        }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            for (var i = 0; i < _size; i++)
            {
                output[(i * 2) + 1] = input[i * 2];
                output[i * 2] = 0;
            }
        }
    }

    public class ReaderIqMoveQToIDouble : ReaderIqSubject<double, double>
    {
        private readonly int _size;

        public ReaderIqMoveQToIDouble(IReaderIqSubject<double> input, bool useArrayPool)
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            _size = input.OutputBufferSize / 2;
        }

        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            for (var i = 0; i < _size; i++)
            {
                output[i * 2] = input[(i * 2) + 1];
                output[(i * 2) + 1] = 0;
            }
        }
    }

    public class ReaderIqMoveQToIFloat : ReaderIqSubject<float, double>
    {
        private readonly int _size;

        public ReaderIqMoveQToIFloat(IReaderIqSubject<float> input, bool useArrayPool)
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            _size = input.OutputBufferSize / 2;
        }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            for (var i = 0; i < _size; i++)
            {
                output[i * 2] = input[(i * 2) + 1];
                output[(i * 2) + 1] = 0;
            }
        }
    }
}
