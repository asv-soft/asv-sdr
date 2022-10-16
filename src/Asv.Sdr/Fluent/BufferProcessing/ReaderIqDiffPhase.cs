using System;
using System.Buffers;
using Asv.Common;

namespace Asv.Sdr
{
    public class ReaderIqDiffPhaseFloat:ReaderIqSubject<float,double>
    {
        private readonly int _size;

        public ReaderIqDiffPhaseFloat(IReaderIqSubject<float> input, bool useArrayPool) : base(input, input.OutputBufferSize, useArrayPool)
        {
            _size = input.OutputBufferSize / 2;
        }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            output[0] = input[0];
            for (var i = 1; i < _size; i++)
            {
                output[i * 2] = input[i * 2];
                output[i * 2 + 1] = DspMathEx.GetDistanceAngleRad(input[(i-1)*2+1], input[i * 2 + 1]);
            }
            
        }
    }

    public class ReaderIqDiffPhaseDouble : ReaderIqSubject<double, double>
    {
        private readonly int _size;

        public ReaderIqDiffPhaseDouble(IReaderIqSubject<double> input, bool useArrayPool) : base(input, input.OutputBufferSize, useArrayPool)
        {
            _size = input.OutputBufferSize / 2 - 1;
        }

        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            for (var i = 0; i < _size; i++)
            {
                output[i * 2] = input[i * 2];
                output[i * 2 + 1] = DspMathEx.GetDistanceAngleRad(input[i * 2 + 1], input[(i + 1) * 2 + 1]);
            }
            output[_size * 2] = input[_size * 2];
            output[_size * 2 + 1] = 0;

        }
    }
}