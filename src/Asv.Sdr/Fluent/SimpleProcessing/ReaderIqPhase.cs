using System;

namespace Asv.Sdr
{
    public class ReaderIqPhaseDoubleSubject : ReaderIqSimpleSubject<double, double>
    {
        private readonly int _freqIndex;

        public ReaderIqPhaseDoubleSubject(IReaderIqSubject<double> input, int sampleRate, int freqHz) : base(input)
        {
            var factor = sampleRate / (input.OutputBufferSize / 2.0);
            _freqIndex = (int)Math.Round(freqHz / factor);
        }

        protected override double Process(ReadOnlySpan<double> input, out bool selfPublish)
        {
            selfPublish = false;
            return Math.Atan2(input[_freqIndex*2], input[_freqIndex * 2 +1]);
        }
    }

    public class ReaderIqPhaseFloatSubject : ReaderIqSimpleSubject<float, double>
    {
        private readonly int _freqIndex;

        public ReaderIqPhaseFloatSubject(IReaderIqSubject<float> input, int sampleRate, int freqHz) : base(input)
        {
            var factor = sampleRate / (input.OutputBufferSize / 2.0);
            _freqIndex = (int)Math.Round(freqHz / factor);
        }

        protected override double Process(ReadOnlySpan<float> input, out bool selfPublish)
        {
            selfPublish = false;
            return Math.Atan2(input[_freqIndex * 2], input[_freqIndex * 2 + 1]);
        }
    }
}