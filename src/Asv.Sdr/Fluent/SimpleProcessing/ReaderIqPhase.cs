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
            return Math.Atan2(input[_freqIndex * 2 + 1], input[_freqIndex * 2]);
        }
    }
    
    public class ReaderIqPhase2DoubleSubject : ReaderIqSimpleSubject<double, (double, double)>
    {
        private readonly int _freqIndex1;
        private readonly int _freqIndex2;

        public ReaderIqPhase2DoubleSubject(IReaderIqSubject<double> input, int sampleRate, int freq1Hz, int freq2Hz) : base(input)
        {
            var factor = sampleRate / (input.OutputBufferSize / 2.0);
            _freqIndex1 = (int)Math.Round(freq1Hz / factor);
            _freqIndex2 = (int)Math.Round(freq2Hz / factor);
        }

        protected override (double, double) Process(ReadOnlySpan<double> input, out bool selfPublish)
        {
            selfPublish = false;
            return (Math.Atan2(input[_freqIndex1 * 2 + 1], input[_freqIndex1 * 2]), Math.Atan2(input[_freqIndex2 * 2 + 1], input[_freqIndex2 * 2]));
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
            return Math.Atan2(input[_freqIndex * 2 + 1], input[_freqIndex * 2]);
        }
    }
    
    public class ReaderIqPhase2FloatSubject : ReaderIqSimpleSubject<float, (double, double)>
    {
        private readonly int _freqIndex1;
        private readonly int _freqIndex2;

        public ReaderIqPhase2FloatSubject(IReaderIqSubject<float> input, int sampleRate, int freq1Hz, int freq2Hz) : base(input)
        {
            var factor = sampleRate / (input.OutputBufferSize / 2.0);
            _freqIndex1 = (int)Math.Round(freq1Hz / factor);
            _freqIndex2 = (int)Math.Round(freq2Hz / factor);
        }

        protected override (double, double) Process(ReadOnlySpan<float> input, out bool selfPublish)
        {
            selfPublish = false;
            return (Math.Atan2(input[_freqIndex1 * 2 + 1], input[_freqIndex1 * 2]), Math.Atan2(input[_freqIndex2 * 2 + 1], input[_freqIndex2 * 2]));
        }
    }
}
