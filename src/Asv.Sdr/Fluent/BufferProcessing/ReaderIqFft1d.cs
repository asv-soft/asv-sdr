using System;

namespace Asv.Sdr
{
    public class ReaderIqFft1dDouble : ReaderIqSubject<double, double>
    {
        private readonly alglib.xparams _params;
        private readonly alglib.ftbase.fasttransformplan _plan;

        public ReaderIqFft1dDouble(IReaderIqSubject<double> input) : base(input, input.OutputBufferSize, false)
        {
            try
            {
                _params = alglib.parallel;
                _plan = new alglib.ftbase.fasttransformplan();
                alglib.ftbase.ftcomplexfftplan(input.OutputBufferSize / 2, 1, _plan, _params);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
           
        }

        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            input.CopyTo(output);
            alglib.ftbase.ftapplyplan(_plan, Buffer, 0, 1, _params);
        }
    }

    public class ReaderIqFft1dFloat : ReaderIqSubject<float, double>
    {
        private readonly alglib.xparams _params;
        private readonly alglib.ftbase.fasttransformplan _plan;

        public ReaderIqFft1dFloat(IReaderIqSubject<float> input) : base(input, input.OutputBufferSize, false)
        {
            _params = alglib.parallel;
            _plan = new alglib.ftbase.fasttransformplan();
            alglib.ftbase.ftcomplexfftplan(input.OutputBufferSize / 2, 1, _plan, _params);
        }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            input.CopyTo(output);
            alglib.ftbase.ftapplyplan(_plan, Buffer, 0, 1, _params);
        }
    }
}
