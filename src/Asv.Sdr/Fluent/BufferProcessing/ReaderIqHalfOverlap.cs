using System;

namespace Asv.Sdr
{
    public class ReaderIqHalfOverlapDouble : ReaderIqSubject<double, double>
    {
        private readonly int _overlapIndex;

        public ReaderIqHalfOverlapDouble(IReaderIqSubject<double> input, bool useArrayPool = true)
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            _overlapIndex = (int)Math.Round(input.OutputBufferSize / 2.0);
        }

        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            var old1 = output[.._overlapIndex];
            var old2 = output[_overlapIndex..];
            old2.CopyTo(old1);

            var new1 = input[.._overlapIndex];
            var new2 = input[_overlapIndex..];
            new1.CopyTo(old2);

            Publish();
            old2.CopyTo(old1);
            new2.CopyTo(old2);

            // next call Publish() will be call after exit func in base class
        }
    }

    public class ReaderIqHalfOverlapFloat : ReaderIqSubject<float, double>
    {
        private readonly int _overlapIndex;

        public ReaderIqHalfOverlapFloat(IReaderIqSubject<float> input, bool useArrayPool = true)
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            _overlapIndex = (int)Math.Round(input.OutputBufferSize / 2.0);
        }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            var old1 = output[.._overlapIndex];
            var old2 = output[_overlapIndex..];
            old2.CopyTo(old1);

            var new1 = input[.._overlapIndex];
            var new2 = input[_overlapIndex..];
            new1.CopyTo(old2);

            Publish();
            old2.CopyTo(old1);
            new2.CopyTo(old2);

            // next call Publish() will be call after exit func in base class
        }
    }
}
