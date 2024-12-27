using System;

namespace Asv.Sdr
{
    public class ReaderIqMagnitudeAndPhaseDouble : ReaderIqSubject<double, double>
    {
        public ReaderIqMagnitudeAndPhaseDouble(IReaderIqSubject<double> src, bool useArrayPool)
            : base(src, src.OutputBufferSize, useArrayPool) { }

        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            for (var i = 0; i < OutputBufferSize / 2; i++)
            {
                output[i * 2] = DspMathEx.Abs(input[i * 2], input[(i * 2) + 1]);
                output[(i * 2) + 1] = DspMathEx.Phase(input[i * 2], input[(i * 2) + 1]);
            }
        }
    }

    public class ReaderIqMagnitudeAndPhaseFloat : ReaderIqSubject<float, double>
    {
        public ReaderIqMagnitudeAndPhaseFloat(IReaderIqSubject<float> src, bool useArrayPool)
            : base(src, src.OutputBufferSize, useArrayPool) { }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            for (var i = 0; i < OutputBufferSize / 2; i++)
            {
                output[i * 2] = DspMathEx.Abs(input[i * 2], input[(i * 2) + 1]);
                output[(i * 2) + 1] = DspMathEx.Phase(input[i * 2], input[(i * 2) + 1]);
            }
        }
    }
}
