using System;

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
            for (var i = 0; i < OutputBufferSize / 2; i++)
            {
                output[i * 2] = DspMathEx.Abs(input[i * 2], input[i * 2 + 1]);
                output[i * 2 + 1] = 0;
            }
        }
    }

    public class ReaderIqMagnitudeFloat : ReaderIqSubject<float, double>
    {
        public ReaderIqMagnitudeFloat(IReaderIqSubject<float> src,
            bool useArrayPool) : base(src, src.OutputBufferSize, useArrayPool)
        {

        }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            for (var i = 0; i < OutputBufferSize / 2; i++)
            {
                //output[i * 2] = Math.Sqrt((double)input[i * 2] * (double)input[i * 2] + (double)input[i * 2 + 1] * (double)input[i * 2 + 1]);
                output[i * 2] = DspMathEx.Abs(input[i * 2], input[i * 2 + 1]);
                output[i * 2 + 1] = 0;
            }
        }
    }


}
