using System;

namespace Asv.Sdr.V2
{
    public class ReaderIqFindMaxAbs : ReaderIqSimpleSubject<double, (double,double)>
    {
        public ReaderIqFindMaxAbs(IReaderIqSubject<double> input) : base(input)
        {

        }

        protected override (double, double) Process(ReadOnlySpan<double> input, out bool selfPublish)
        {
            var maxAbs = double.MinValue;
            var maxPhase = double.MinValue;
            for (int i = 0; i < input.Length / 2; i++)
            {
                var abs = DspMathEx.Abs(input[i * 2], input[i * 2 + 1]);
                if (abs > maxAbs)
                {
                    maxAbs = abs;
                    maxPhase = DspMathEx.Phase(input[i * 2], input[i * 2 + 1]);
                }
            }
            selfPublish = false;
            return (maxAbs, maxPhase);
        }
    }
}
