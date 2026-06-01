using System;

namespace Asv.Sdr
{
    public class ReaderIqFft1dDouble : ReaderIqSubject<double, double>
    {
        private readonly IReaderIqFftPlan _plan;

        public ReaderIqFft1dDouble(IReaderIqSubject<double> input) : base(input, input.OutputBufferSize, false)
        {
            _plan = ReaderIqFftSettings.CreatePlan(input.OutputBufferSize / 2);
        }

        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            input.CopyTo(output);
            _plan.Transform(Buffer);
        }
    }

    public class ReaderIqFft1dFloat : ReaderIqSubject<float, double>
    {
        private readonly IReaderIqFftPlan _plan;

        public ReaderIqFft1dFloat(IReaderIqSubject<float> input) : base(input, input.OutputBufferSize, false)
        {
            _plan = ReaderIqFftSettings.CreatePlan(input.OutputBufferSize / 2);
        }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            input.CopyTo(output);
            _plan.Transform(Buffer);
        }
    }
}
