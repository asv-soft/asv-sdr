using System;

namespace Asv.Sdr
{
    public class ReaderIqAmSubject : ReaderIqSimpleSubject<double,double>
    {
        private readonly int _amIndex;

        public ReaderIqAmSubject(IReaderIqSubject<double> input, int sampleRate,  int amFreq) :base(input)
        {
            var factor = sampleRate / (input .OutputBufferSize/ 2.0);
            _amIndex = (int)Math.Round(amFreq / factor);
        }
        
        protected override double Process(ReadOnlySpan<double> input, out bool selfPublish)
        {
            selfPublish = false;
            var lvl0 = DspMathEx.Abs(input[0], input[1]);
            var amValue = DspMathEx.Abs(input[_amIndex * 2], input[_amIndex * 2 + 1]);
            return (amValue * 2) / lvl0;
        }
    }

    public class ReaderIqAm2Subject : ReaderIqSimpleSubject<double, (double,double)>
    {
        private readonly int _amIndex1;
        private readonly int _amIndex2;

        public ReaderIqAm2Subject(IReaderIqSubject<double> input, int sampleRate, int amFreq1, int amFreq2) : base(input)
        {
            var factor = sampleRate / (input.OutputBufferSize / 2.0);
            _amIndex1 = (int)Math.Round(amFreq1 / factor);
            _amIndex2 = (int)Math.Round(amFreq2 / factor);
        }

        protected override (double, double) Process(ReadOnlySpan<double> input, out bool selfPublish)
        {
            selfPublish = false;
            var lvl0 = DspMathEx.Abs(input[0], input[1]);
            var am1Value = DspMathEx.Abs(input[_amIndex1 * 2], input[_amIndex1 * 2 + 1]);
            var am2Value = DspMathEx.Abs(input[_amIndex2 * 2], input[_amIndex2 * 2 + 1]);
            return ((am1Value * 2) / lvl0, (am2Value * 2) / lvl0);
        }
    }
}
