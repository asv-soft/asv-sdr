using System;
using Asv.Common;

namespace Asv.Sdr
{
    public class ReaderIqFrequencyOffsetFloatSubject : ReaderIqSimpleSubject<float, double>
    {
        private readonly Func<double, double> _freqAdjustment;
        private readonly short _fOffset;
        private readonly int _readSamples;

        public ReaderIqFrequencyOffsetFloatSubject(IReaderIqSubject<float> input, short freqOffset, Func<double, double> freqAdjustment = default) : base(input)
        {
            _freqAdjustment = freqAdjustment ?? (_ => _) ;
            _fOffset = (short)(-1 * freqOffset);
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
                var dist = MathEx.GetDistanceAngleRad(cur, prev);
                prev = cur;
                sumX += Math.Cos(dist);
                sumY += Math.Sin(dist);
            }
            sumX /= (_readSamples - 1);
            sumY /= (_readSamples - 1);
            var offset = Math.Atan2(sumY, sumX) * _readSamples;

            return _freqAdjustment.Invoke(_fOffset - offset);
        }
    }
    
    public class ReaderIqFrequencyOffsetDoubleSubject : ReaderIqSimpleSubject<double, double>
    {
        private readonly Func<double, double> _freqAdjustment;
        private readonly short _fOffset;
        private readonly int _readSamples;

        public ReaderIqFrequencyOffsetDoubleSubject(IReaderIqSubject<double> input, short freqOffset, Func<double, double> freqAdjustment = default) : base(input)
        {
            _freqAdjustment = freqAdjustment ?? (_ => _) ;
            _fOffset = (short)(-1 * freqOffset);
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
                var dist = MathEx.GetDistanceAngleRad(cur, prev);
                prev = cur;
                sumX += Math.Cos(dist);
                sumY += Math.Sin(dist);
            }
            sumX /= (_readSamples - 1);
            sumY /= (_readSamples - 1);
            var offset = Math.Atan2(sumY, sumX) * _readSamples;
            return _freqAdjustment.Invoke(_fOffset - offset);
        }
    }
}