using System;

namespace Asv.Sdr
{
    public class ReaderIqPcmAvgNormalizer:ReaderIqSubject<double,double>
    {
        private readonly int _skip;
        private readonly int _count;
        private readonly double _lowValue;
        private readonly double _highValue;

        public ReaderIqPcmAvgNormalizer(IReaderIqSubject<double> input, int skip, int count, double lowValue, double highValue, bool useArrayPool) 
            : base(input, input.OutputBufferSize, useArrayPool)
        {
            if (skip < 0) ArgumentOutOfRangeException.ThrowIfLessThan(0,skip);
            if (count <= 0) ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(0,count);
            if (skip+count>input.OutputBufferSize) ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(input.OutputBufferSize,skip+count);
            _skip = skip;
            _count = count;
            _lowValue = lowValue;
            _highValue = highValue;
        }


        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            var sum = 0.0;
            for (var i = 0; i < _count; i++)
            {
                sum += input[_skip + i];
            }
            var avg = sum / _count;
            for (var i = 0; i < input.Length; i++)
            {
                output[i] = input[i] > avg ? _highValue : _lowValue;
            } 
        }
    }
}