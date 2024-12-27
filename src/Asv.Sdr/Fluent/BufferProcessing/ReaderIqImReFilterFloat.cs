using System;

namespace Asv.Sdr.V2
{
    public class ReaderIqFilterIqDouble : ReaderIqSubject<double, double>
    {
        private readonly IDspFilter _iFilter;
        private readonly IDspFilter _qFilter;

        public ReaderIqFilterIqDouble(
            IReaderIqSubject<double> input,
            IDspFilter iFilter,
            IDspFilter qFilter,
            bool useArrayPool = true
        )
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            _iFilter = iFilter;
            _qFilter = qFilter;
        }

        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            input.CopyTo(output);
            for (var i = 0; i < OutputBufferSize / 2; i++)
            {
                output[i * 2] = _iFilter.Process(output[i * 2]);
                output[(i * 2) + 1] = _qFilter.Process(output[(i * 2) + 1]);
            }
        }
    }

    public class ReaderIqFilterIqFloat : ReaderIqSubject<float, double>
    {
        private readonly IDspFilter _iFilter;
        private readonly IDspFilter _qFilter;

        public ReaderIqFilterIqFloat(
            IReaderIqSubject<float> input,
            IDspFilter iFilter,
            IDspFilter qFilter,
            bool useArrayPool = true
        )
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            _iFilter = iFilter;
            _qFilter = qFilter;
        }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            input.CopyTo(output);
            for (var i = 0; i < OutputBufferSize / 2; i++)
            {
                output[i * 2] = _iFilter.Process(output[i * 2]);
                output[(i * 2) + 1] = _qFilter.Process(output[(i * 2) + 1]);
            }
        }
    }
}
