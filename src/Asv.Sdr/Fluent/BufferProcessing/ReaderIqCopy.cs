using System;

namespace Asv.Sdr
{
    public class ReaderIqCopyIToQDouble : ReaderIqSubject<double, double>
    {
        private readonly int _size;

        public ReaderIqCopyIToQDouble(IReaderIqSubject<double> input, bool useArrayPool)
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            _size = input.OutputBufferSize / 2;
        }

        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            for (var i = 0; i < _size; i++)
            {
                output[i * 2] = input[i * 2];
                output[(i * 2) + 1] = input[i * 2];
            }
        }
    }

    public class ReaderIqCopyIToQFloat : ReaderIqSubject<float, double>
    {
        private readonly int _size;

        public ReaderIqCopyIToQFloat(IReaderIqSubject<float> input, bool useArrayPool)
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            _size = input.OutputBufferSize / 2;
        }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            for (var i = 0; i < _size; i++)
            {
                output[i * 2] = input[i * 2];
                output[(i * 2) + 1] = input[i * 2];
            }
        }
    }

    public class ReaderIqCopyQToIDouble : ReaderIqSubject<double, double>
    {
        private readonly int _size;

        public ReaderIqCopyQToIDouble(IReaderIqSubject<double> input, bool useArrayPool)
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            _size = input.OutputBufferSize / 2;
        }

        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            for (var i = 0; i < _size; i++)
            {
                output[i * 2] = input[(i * 2) + 1];
                output[(i * 2) + 1] = input[(i * 2) + 1];
            }
        }
    }

    public class ReaderIqCopyQToIFloat : ReaderIqSubject<float, double>
    {
        private readonly int _size;

        public ReaderIqCopyQToIFloat(IReaderIqSubject<float> input, bool useArrayPool)
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            _size = input.OutputBufferSize / 2;
        }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            for (var i = 0; i < _size; i++)
            {
                output[i * 2] = input[(i * 2) + 1];
                output[(i * 2) + 1] = input[(i * 2) + 1];
            }
        }
    }
}
