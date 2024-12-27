using System;

namespace Asv.Sdr
{
    public class ReaderIqFilterIDouble : ReaderIqSubject<double, double>
    {
        private readonly IDspFilter _iFilter;

        public ReaderIqFilterIDouble(
            IReaderIqSubject<double> input,
            IDspFilter iFilter,
            bool useArrayPool = true
        )
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            _iFilter = iFilter;
        }

        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            input.CopyTo(output);
            for (var i = 0; i < OutputBufferSize / 2; i++)
            {
                output[i * 2] = _iFilter.Process(output[i * 2]);
                output[(i * 2) + 1] = output[(i * 2) + 1];
            }
        }
    }

    public class ReaderIqFilterIFloat : ReaderIqSubject<float, double>
    {
        private readonly IDspFilter _iFilter;

        public ReaderIqFilterIFloat(
            IReaderIqSubject<float> input,
            IDspFilter iFilter,
            bool useArrayPool = true
        )
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            _iFilter = iFilter;
        }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = input[i];
            }

            for (var i = 0; i < OutputBufferSize / 2; i++)
            {
                output[i * 2] = _iFilter.Process(output[i * 2]);
                output[(i * 2) + 1] = output[(i * 2) + 1];
            }
        }
    }
}
