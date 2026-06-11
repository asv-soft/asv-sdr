using System;

namespace Asv.Sdr
{
    public readonly struct ReaderIqGoertzelResult
    {
        public ReaderIqGoertzelResult(double real, double imaginary)
        {
            Real = real;
            Imaginary = imaginary;
        }

        public double Real { get; }
        public double Imaginary { get; }
        public double Magnitude => DspMathEx.Abs(Real, Imaginary);
        public double Phase => Math.Atan2(Imaginary, Real);
    }

    public class ReaderIqGoertzelSubject : ReaderIqSimpleSubject<double, ReaderIqGoertzelResult>
    {
        private readonly double _sampleRate;
        private readonly double _frequencyHz;

        public ReaderIqGoertzelSubject(IReaderIqSubject<double> input, double sampleRate, double frequencyHz) : base(input)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (frequencyHz < 0 || frequencyHz > sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(frequencyHz));

            _sampleRate = sampleRate;
            _frequencyHz = frequencyHz;
        }

        protected override ReaderIqGoertzelResult Process(ReadOnlySpan<double> input, out bool selfPublish)
        {
            selfPublish = false;
            return Calculate(input, _sampleRate, _frequencyHz);
        }

        internal static ReaderIqGoertzelResult Calculate(ReadOnlySpan<double> input, double sampleRate, double frequencyHz)
        {
            return Calculate(input, sampleRate, frequencyHz, 0, 0);
        }

        private static ReaderIqGoertzelResult Calculate(
            ReadOnlySpan<double> input,
            double sampleRate,
            double frequencyHz,
            double offsetReal,
            double offsetImaginary)
        {
            var angleStep = -2.0 * Math.PI * frequencyHz / sampleRate;
            var stepReal = Math.Cos(angleStep);
            var stepImaginary = Math.Sin(angleStep);
            var twiddleReal = 1.0;
            var twiddleImaginary = 0.0;
            var sumReal = 0.0;
            var sumImaginary = 0.0;

            for (var i = 0; i < input.Length / 2; i++)
            {
                var real = input[i * 2] - offsetReal;
                var imaginary = input[i * 2 + 1] - offsetImaginary;

                sumReal += real * twiddleReal - imaginary * twiddleImaginary;
                sumImaginary += real * twiddleImaginary + imaginary * twiddleReal;

                var nextTwiddleReal = twiddleReal * stepReal - twiddleImaginary * stepImaginary;
                twiddleImaginary = twiddleReal * stepImaginary + twiddleImaginary * stepReal;
                twiddleReal = nextTwiddleReal;
            }

            return new ReaderIqGoertzelResult(sumReal, sumImaginary);
        }

        internal static ReaderIqGoertzelResult CalculateCentered(
            ReadOnlySpan<double> input,
            double sampleRate,
            double frequencyHz,
            ReaderIqGoertzelResult dc)
        {
            var iqPairCount = input.Length / 2;
            if (iqPairCount == 0)
            {
                return new ReaderIqGoertzelResult(0, 0);
            }

            return Calculate(input, sampleRate, frequencyHz, dc.Real / iqPairCount, dc.Imaginary / iqPairCount);
        }
    }

    public class ReaderIqGoertzelAmSubject : ReaderIqSimpleSubject<double, double>
    {
        private readonly double _sampleRate;
        private readonly double _frequencyHz;

        public ReaderIqGoertzelAmSubject(IReaderIqSubject<double> input, double sampleRate, double frequencyHz) : base(input)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (frequencyHz < 0 || frequencyHz > sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(frequencyHz));

            _sampleRate = sampleRate;
            _frequencyHz = frequencyHz;
        }

        protected override double Process(ReadOnlySpan<double> input, out bool selfPublish)
        {
            selfPublish = false;
            var dc = ReaderIqGoertzelSubject.Calculate(input, _sampleRate, 0);
            if (dc.Magnitude <= double.Epsilon)
            {
                return 0;
            }

            var target = ReaderIqGoertzelSubject.CalculateCentered(input, _sampleRate, _frequencyHz, dc);
            return 2.0 * target.Magnitude / dc.Magnitude;
        }
    }
}
