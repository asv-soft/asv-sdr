using System;
using Asv.Common;

namespace Asv.Sdr
{
    public class ReaderIqFrequencyOffsetFloatSubject : ReaderIqSimpleSubject<float, double>
    {
        private readonly int _readSamples;

        public ReaderIqFrequencyOffsetFloatSubject(IReaderIqSubject<float> input)
            : base(input)
        {
            _readSamples = input.OutputBufferSize / 2;
            if (_readSamples == 1)
            {
                throw new ArgumentException("Input buffer size must be greater than 2");
            }
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

            sumX /= _readSamples - 1;
            sumY /= _readSamples - 1;
            var offset = -Math.Atan2(sumY, sumX) * _readSamples;
            return offset;
        }
    }

    public class ReaderIqFrequencyOffsetDoubleSubject : ReaderIqSimpleSubject<double, double>
    {
        private readonly int _readSamples;

        public ReaderIqFrequencyOffsetDoubleSubject(IReaderIqSubject<double> input)
            : base(input)
        {
            _readSamples = input.OutputBufferSize / 2;
            if (_readSamples == 1)
            {
                throw new ArgumentException("Input buffer size must be greater than 2");
            }
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

            sumX /= _readSamples - 1;
            sumY /= _readSamples - 1;
            var offset = -Math.Atan2(sumY, sumX) * _readSamples;
            return offset;
        }
    }
}
