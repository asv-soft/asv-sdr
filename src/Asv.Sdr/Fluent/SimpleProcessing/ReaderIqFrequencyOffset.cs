using System;
using Asv.Common;

namespace Asv.Sdr
{
    public class ReaderIqFrequencyOffsetFloatSubject : ReaderIqSimpleSubject<float, double>
    {
        private readonly short _fOffset;
        private readonly int _readSamples;

        public ReaderIqFrequencyOffsetFloatSubject(IReaderIqSubject<float> input, short fOffset) : base(input)
        {
            _fOffset = (short)(-1 * fOffset);
            _readSamples = input.OutputBufferSize / 2;
        }

        protected override double Process(ReadOnlySpan<float> input, out bool selfPublish)
        {
            selfPublish = false;
            var prev = Math.Atan2(input[0], input[1]);
            var sumX = 0.0;
            var sumY = 0.0;
            for (var i = 2; i < input.Length; i += 2)
            {
                var cur = Math.Atan2(input[i], input[i + 1]);
                var dist = MathEx.GetDistanceAngleDeg(cur * 180 / Math.PI, prev * 180 / Math.PI);
                prev = cur;
                sumX += Math.Cos(dist * Math.PI / 180);
                sumY += Math.Sin(dist * Math.PI / 180);
            }
            sumX /= (_readSamples - 1);
            sumY /= (_readSamples - 1);
            var offset = Math.Atan2(sumY, sumX) * _readSamples;
            return ((_fOffset - offset) - 844 ) / 1.0471875;
        }
    }
    
    public class ReaderIqFrequencyOffsetDoubleSubject : ReaderIqSimpleSubject<double, double>
    {
        private readonly short _fOffset;
        private readonly int _readSamples;

        public ReaderIqFrequencyOffsetDoubleSubject(IReaderIqSubject<double> input, short fOffset) : base(input)
        {
            _fOffset = (short)(-1 * fOffset);
            _readSamples = input.OutputBufferSize / 2;
        }

        protected override double Process(ReadOnlySpan<double> input, out bool selfPublish)
        {
            selfPublish = false;
            var prev = Math.Atan2(input[0], input[1]);
            var sumX = 0.0;
            var sumY = 0.0;
            for (var i = 2; i < input.Length; i += 2)
            {
                var cur = Math.Atan2(input[i], input[i + 1]);
                var dist = MathEx.GetDistanceAngleDeg(cur * 180 / Math.PI, prev * 180 / Math.PI);
                prev = cur;
                sumX += Math.Cos(dist * Math.PI / 180);
                sumY += Math.Sin(dist * Math.PI / 180);
            }
            sumX /= (_readSamples - 1);
            sumY /= (_readSamples - 1);
            var offset = Math.Atan2(sumY, sumX) * _readSamples;
            return ((_fOffset - offset) - 844 ) / 1.0471875;
        }
    }
}