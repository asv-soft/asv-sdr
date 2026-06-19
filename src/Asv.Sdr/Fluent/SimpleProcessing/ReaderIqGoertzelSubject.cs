using System;
using System.Collections.Generic;

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
        public double MagnitudeSquared => Real * Real + Imaginary * Imaginary;
        public double Magnitude => DspMathEx.Abs(Real, Imaginary);
        public double Phase => Math.Atan2(Imaginary, Real);
    }

    public readonly struct ReaderIqVorReferenceResult
    {
        public ReaderIqVorReferenceResult(
            ReaderIqGoertzelResult reference,
            double meanFrequencyOffsetHz,
            int sampleCount
        )
        {
            Reference = reference;
            MeanFrequencyOffsetHz = meanFrequencyOffsetHz;
            SampleCount = sampleCount;
        }

        public ReaderIqGoertzelResult Reference { get; }
        public double Magnitude => Reference.Magnitude;
        public double Phase => Reference.Phase;
        public double MeanFrequencyOffsetHz { get; }
        public int SampleCount { get; }
    }

    public class ReaderIqGoertzelSubject : ReaderIqSimpleSubject<double, ReaderIqGoertzelResult>
    {
        private readonly double _sampleRate;
        private readonly double _frequencyHz;

        public ReaderIqGoertzelSubject(
            IReaderIqSubject<double> input,
            double sampleRate,
            double frequencyHz
        )
            : base(input)
        {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (frequencyHz < 0 || frequencyHz > sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(frequencyHz));

            _sampleRate = sampleRate;
            _frequencyHz = frequencyHz;
        }

        protected override ReaderIqGoertzelResult Process(
            ReadOnlySpan<double> input,
            out bool selfPublish
        )
        {
            selfPublish = false;
            return Calculate(input, _sampleRate, _frequencyHz);
        }

        internal static ReaderIqGoertzelResult Calculate(
            ReadOnlySpan<double> input,
            double sampleRate,
            double frequencyHz
        )
        {
            return Calculate(input, sampleRate, frequencyHz, 0, 0);
        }

        internal static ReaderIqGoertzelResult Calculate(
            ReadOnlySpan<float> input,
            double sampleRate,
            double frequencyHz
        )
        {
            if (frequencyHz == 0)
            {
                return CalculateDc(input);
            }

            var angleStep = -2.0 * Math.PI * frequencyHz / sampleRate;
            var stepReal = Math.Cos(angleStep);
            var stepImaginary = Math.Sin(angleStep);
            var twiddleReal = 1.0;
            var twiddleImaginary = 0.0;
            var sumReal = 0.0;
            var sumImaginary = 0.0;

            for (var i = 0; i < input.Length / 2; i++)
            {
                var real = input[i * 2];
                var imaginary = input[i * 2 + 1];

                sumReal += real * twiddleReal - imaginary * twiddleImaginary;
                sumImaginary += real * twiddleImaginary + imaginary * twiddleReal;

                var nextTwiddleReal = twiddleReal * stepReal - twiddleImaginary * stepImaginary;
                twiddleImaginary = twiddleReal * stepImaginary + twiddleImaginary * stepReal;
                twiddleReal = nextTwiddleReal;
            }

            return new ReaderIqGoertzelResult(sumReal, sumImaginary);
        }

        private static ReaderIqGoertzelResult Calculate(
            ReadOnlySpan<double> input,
            double sampleRate,
            double frequencyHz,
            double offsetReal,
            double offsetImaginary
        )
        {
            if (frequencyHz == 0)
            {
                return CalculateDc(input, offsetReal, offsetImaginary);
            }

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

        internal static (ReaderIqGoertzelResult First, ReaderIqGoertzelResult Second) Calculate(
            ReadOnlySpan<double> input,
            double sampleRate,
            double frequency1Hz,
            double frequency2Hz
        )
        {
            return Calculate(input, sampleRate, frequency1Hz, frequency2Hz, 0, 0);
        }

        private static (ReaderIqGoertzelResult First, ReaderIqGoertzelResult Second) Calculate(
            ReadOnlySpan<double> input,
            double sampleRate,
            double frequency1Hz,
            double frequency2Hz,
            double offsetReal,
            double offsetImaginary
        )
        {
            var angleStep1 = -2.0 * Math.PI * frequency1Hz / sampleRate;
            var step1Real = Math.Cos(angleStep1);
            var step1Imaginary = Math.Sin(angleStep1);
            var twiddle1Real = 1.0;
            var twiddle1Imaginary = 0.0;
            var sum1Real = 0.0;
            var sum1Imaginary = 0.0;

            var angleStep2 = -2.0 * Math.PI * frequency2Hz / sampleRate;
            var step2Real = Math.Cos(angleStep2);
            var step2Imaginary = Math.Sin(angleStep2);
            var twiddle2Real = 1.0;
            var twiddle2Imaginary = 0.0;
            var sum2Real = 0.0;
            var sum2Imaginary = 0.0;

            for (var i = 0; i < input.Length / 2; i++)
            {
                var real = input[i * 2] - offsetReal;
                var imaginary = input[i * 2 + 1] - offsetImaginary;

                sum1Real += real * twiddle1Real - imaginary * twiddle1Imaginary;
                sum1Imaginary += real * twiddle1Imaginary + imaginary * twiddle1Real;
                sum2Real += real * twiddle2Real - imaginary * twiddle2Imaginary;
                sum2Imaginary += real * twiddle2Imaginary + imaginary * twiddle2Real;

                var nextTwiddle1Real =
                    twiddle1Real * step1Real - twiddle1Imaginary * step1Imaginary;
                twiddle1Imaginary = twiddle1Real * step1Imaginary + twiddle1Imaginary * step1Real;
                twiddle1Real = nextTwiddle1Real;

                var nextTwiddle2Real =
                    twiddle2Real * step2Real - twiddle2Imaginary * step2Imaginary;
                twiddle2Imaginary = twiddle2Real * step2Imaginary + twiddle2Imaginary * step2Real;
                twiddle2Real = nextTwiddle2Real;
            }

            return (
                new ReaderIqGoertzelResult(sum1Real, sum1Imaginary),
                new ReaderIqGoertzelResult(sum2Real, sum2Imaginary)
            );
        }

        private static (
            ReaderIqGoertzelResult First,
            ReaderIqGoertzelResult Second,
            ReaderIqGoertzelResult Third
        ) Calculate(
            ReadOnlySpan<double> input,
            double sampleRate,
            double frequency1Hz,
            double frequency2Hz,
            double frequency3Hz,
            double offsetReal,
            double offsetImaginary
        )
        {
            var angleStep1 = -2.0 * Math.PI * frequency1Hz / sampleRate;
            var step1Real = Math.Cos(angleStep1);
            var step1Imaginary = Math.Sin(angleStep1);
            var twiddle1Real = 1.0;
            var twiddle1Imaginary = 0.0;
            var sum1Real = 0.0;
            var sum1Imaginary = 0.0;

            var angleStep2 = -2.0 * Math.PI * frequency2Hz / sampleRate;
            var step2Real = Math.Cos(angleStep2);
            var step2Imaginary = Math.Sin(angleStep2);
            var twiddle2Real = 1.0;
            var twiddle2Imaginary = 0.0;
            var sum2Real = 0.0;
            var sum2Imaginary = 0.0;

            var angleStep3 = -2.0 * Math.PI * frequency3Hz / sampleRate;
            var step3Real = Math.Cos(angleStep3);
            var step3Imaginary = Math.Sin(angleStep3);
            var twiddle3Real = 1.0;
            var twiddle3Imaginary = 0.0;
            var sum3Real = 0.0;
            var sum3Imaginary = 0.0;

            for (var i = 0; i < input.Length / 2; i++)
            {
                var real = input[i * 2] - offsetReal;
                var imaginary = input[i * 2 + 1] - offsetImaginary;

                sum1Real += real * twiddle1Real - imaginary * twiddle1Imaginary;
                sum1Imaginary += real * twiddle1Imaginary + imaginary * twiddle1Real;
                sum2Real += real * twiddle2Real - imaginary * twiddle2Imaginary;
                sum2Imaginary += real * twiddle2Imaginary + imaginary * twiddle2Real;
                sum3Real += real * twiddle3Real - imaginary * twiddle3Imaginary;
                sum3Imaginary += real * twiddle3Imaginary + imaginary * twiddle3Real;

                var nextTwiddle1Real =
                    twiddle1Real * step1Real - twiddle1Imaginary * step1Imaginary;
                twiddle1Imaginary = twiddle1Real * step1Imaginary + twiddle1Imaginary * step1Real;
                twiddle1Real = nextTwiddle1Real;

                var nextTwiddle2Real =
                    twiddle2Real * step2Real - twiddle2Imaginary * step2Imaginary;
                twiddle2Imaginary = twiddle2Real * step2Imaginary + twiddle2Imaginary * step2Real;
                twiddle2Real = nextTwiddle2Real;

                var nextTwiddle3Real =
                    twiddle3Real * step3Real - twiddle3Imaginary * step3Imaginary;
                twiddle3Imaginary = twiddle3Real * step3Imaginary + twiddle3Imaginary * step3Real;
                twiddle3Real = nextTwiddle3Real;
            }

            return (
                new ReaderIqGoertzelResult(sum1Real, sum1Imaginary),
                new ReaderIqGoertzelResult(sum2Real, sum2Imaginary),
                new ReaderIqGoertzelResult(sum3Real, sum3Imaginary)
            );
        }

        private static ReaderIqGoertzelResult CalculateDc(ReadOnlySpan<float> input)
        {
            var sumReal = 0.0;
            var sumImaginary = 0.0;

            for (var i = 0; i < input.Length; i += 2)
            {
                sumReal += input[i];
                sumImaginary += input[i + 1];
            }

            return new ReaderIqGoertzelResult(sumReal, sumImaginary);
        }

        private static ReaderIqGoertzelResult CalculateDc(
            ReadOnlySpan<double> input,
            double offsetReal,
            double offsetImaginary
        )
        {
            var sumReal = 0.0;
            var sumImaginary = 0.0;

            for (var i = 0; i < input.Length; i += 2)
            {
                sumReal += input[i] - offsetReal;
                sumImaginary += input[i + 1] - offsetImaginary;
            }

            return new ReaderIqGoertzelResult(sumReal, sumImaginary);
        }

        internal static ReaderIqGoertzelResult CalculateCentered(
            ReadOnlySpan<double> input,
            double sampleRate,
            double frequencyHz,
            ReaderIqGoertzelResult dc
        )
        {
            var iqPairCount = input.Length / 2;
            if (iqPairCount == 0)
            {
                return new ReaderIqGoertzelResult(0, 0);
            }

            return Calculate(
                input,
                sampleRate,
                frequencyHz,
                dc.Real / iqPairCount,
                dc.Imaginary / iqPairCount
            );
        }

        internal static (
            ReaderIqGoertzelResult Dc,
            ReaderIqGoertzelResult Target
        ) CalculateDcAndCentered(ReadOnlySpan<double> input, double sampleRate, double frequencyHz)
        {
            var iqPairCount = input.Length / 2;
            if (iqPairCount == 0)
            {
                var empty = new ReaderIqGoertzelResult(0, 0);
                return (empty, empty);
            }

            var angleStep = -2.0 * Math.PI * frequencyHz / sampleRate;
            var stepReal = Math.Cos(angleStep);
            var stepImaginary = Math.Sin(angleStep);
            var twiddleReal = 1.0;
            var twiddleImaginary = 0.0;
            var twiddleSumReal = 0.0;
            var twiddleSumImaginary = 0.0;
            var dcReal = 0.0;
            var dcImaginary = 0.0;
            var targetReal = 0.0;
            var targetImaginary = 0.0;

            for (var i = 0; i < iqPairCount; i++)
            {
                var real = input[i * 2];
                var imaginary = input[i * 2 + 1];

                dcReal += real;
                dcImaginary += imaginary;
                targetReal += real * twiddleReal - imaginary * twiddleImaginary;
                targetImaginary += real * twiddleImaginary + imaginary * twiddleReal;
                twiddleSumReal += twiddleReal;
                twiddleSumImaginary += twiddleImaginary;

                var nextTwiddleReal = twiddleReal * stepReal - twiddleImaginary * stepImaginary;
                twiddleImaginary = twiddleReal * stepImaginary + twiddleImaginary * stepReal;
                twiddleReal = nextTwiddleReal;
            }

            var dc = new ReaderIqGoertzelResult(dcReal, dcImaginary);
            return (
                dc,
                SubtractDcFromTarget(
                    new ReaderIqGoertzelResult(targetReal, targetImaginary),
                    twiddleSumReal,
                    twiddleSumImaginary,
                    dc,
                    iqPairCount
                )
            );
        }

        internal static (
            ReaderIqGoertzelResult First,
            ReaderIqGoertzelResult Second
        ) CalculateCentered(
            ReadOnlySpan<double> input,
            double sampleRate,
            double frequency1Hz,
            double frequency2Hz,
            ReaderIqGoertzelResult dc
        )
        {
            var iqPairCount = input.Length / 2;
            if (iqPairCount == 0)
            {
                var empty = new ReaderIqGoertzelResult(0, 0);
                return (empty, empty);
            }

            return Calculate(
                input,
                sampleRate,
                frequency1Hz,
                frequency2Hz,
                dc.Real / iqPairCount,
                dc.Imaginary / iqPairCount
            );
        }

        internal static (
            ReaderIqGoertzelResult Dc,
            ReaderIqGoertzelResult First,
            ReaderIqGoertzelResult Second
        ) CalculateDcAndCentered(
            ReadOnlySpan<double> input,
            double sampleRate,
            double frequency1Hz,
            double frequency2Hz
        )
        {
            var iqPairCount = input.Length / 2;
            if (iqPairCount == 0)
            {
                var empty = new ReaderIqGoertzelResult(0, 0);
                return (empty, empty, empty);
            }

            var angleStep1 = -2.0 * Math.PI * frequency1Hz / sampleRate;
            var step1Real = Math.Cos(angleStep1);
            var step1Imaginary = Math.Sin(angleStep1);
            var twiddle1Real = 1.0;
            var twiddle1Imaginary = 0.0;
            var twiddleSum1Real = 0.0;
            var twiddleSum1Imaginary = 0.0;
            var target1Real = 0.0;
            var target1Imaginary = 0.0;

            var angleStep2 = -2.0 * Math.PI * frequency2Hz / sampleRate;
            var step2Real = Math.Cos(angleStep2);
            var step2Imaginary = Math.Sin(angleStep2);
            var twiddle2Real = 1.0;
            var twiddle2Imaginary = 0.0;
            var twiddleSum2Real = 0.0;
            var twiddleSum2Imaginary = 0.0;
            var target2Real = 0.0;
            var target2Imaginary = 0.0;

            var dcReal = 0.0;
            var dcImaginary = 0.0;

            for (var i = 0; i < iqPairCount; i++)
            {
                var real = input[i * 2];
                var imaginary = input[i * 2 + 1];

                dcReal += real;
                dcImaginary += imaginary;

                target1Real += real * twiddle1Real - imaginary * twiddle1Imaginary;
                target1Imaginary += real * twiddle1Imaginary + imaginary * twiddle1Real;
                twiddleSum1Real += twiddle1Real;
                twiddleSum1Imaginary += twiddle1Imaginary;

                target2Real += real * twiddle2Real - imaginary * twiddle2Imaginary;
                target2Imaginary += real * twiddle2Imaginary + imaginary * twiddle2Real;
                twiddleSum2Real += twiddle2Real;
                twiddleSum2Imaginary += twiddle2Imaginary;

                var nextTwiddle1Real =
                    twiddle1Real * step1Real - twiddle1Imaginary * step1Imaginary;
                twiddle1Imaginary = twiddle1Real * step1Imaginary + twiddle1Imaginary * step1Real;
                twiddle1Real = nextTwiddle1Real;

                var nextTwiddle2Real =
                    twiddle2Real * step2Real - twiddle2Imaginary * step2Imaginary;
                twiddle2Imaginary = twiddle2Real * step2Imaginary + twiddle2Imaginary * step2Real;
                twiddle2Real = nextTwiddle2Real;
            }

            var dc = new ReaderIqGoertzelResult(dcReal, dcImaginary);
            return (
                dc,
                SubtractDcFromTarget(
                    new ReaderIqGoertzelResult(target1Real, target1Imaginary),
                    twiddleSum1Real,
                    twiddleSum1Imaginary,
                    dc,
                    iqPairCount
                ),
                SubtractDcFromTarget(
                    new ReaderIqGoertzelResult(target2Real, target2Imaginary),
                    twiddleSum2Real,
                    twiddleSum2Imaginary,
                    dc,
                    iqPairCount
                )
            );
        }

        internal static (
            ReaderIqGoertzelResult First,
            ReaderIqGoertzelResult Second,
            ReaderIqGoertzelResult Third
        ) CalculateCentered(
            ReadOnlySpan<double> input,
            double sampleRate,
            double frequency1Hz,
            double frequency2Hz,
            double frequency3Hz,
            ReaderIqGoertzelResult dc
        )
        {
            var iqPairCount = input.Length / 2;
            if (iqPairCount == 0)
            {
                var empty = new ReaderIqGoertzelResult(0, 0);
                return (empty, empty, empty);
            }

            return Calculate(
                input,
                sampleRate,
                frequency1Hz,
                frequency2Hz,
                frequency3Hz,
                dc.Real / iqPairCount,
                dc.Imaginary / iqPairCount
            );
        }

        internal static (
            ReaderIqGoertzelResult Dc,
            ReaderIqGoertzelResult First,
            ReaderIqGoertzelResult Second,
            ReaderIqGoertzelResult Third
        ) CalculateDcAndCentered(
            ReadOnlySpan<double> input,
            double sampleRate,
            double frequency1Hz,
            double frequency2Hz,
            double frequency3Hz
        )
        {
            var iqPairCount = input.Length / 2;
            if (iqPairCount == 0)
            {
                var empty = new ReaderIqGoertzelResult(0, 0);
                return (empty, empty, empty, empty);
            }

            var angleStep1 = -2.0 * Math.PI * frequency1Hz / sampleRate;
            var step1Real = Math.Cos(angleStep1);
            var step1Imaginary = Math.Sin(angleStep1);
            var twiddle1Real = 1.0;
            var twiddle1Imaginary = 0.0;
            var twiddleSum1Real = 0.0;
            var twiddleSum1Imaginary = 0.0;
            var target1Real = 0.0;
            var target1Imaginary = 0.0;

            var angleStep2 = -2.0 * Math.PI * frequency2Hz / sampleRate;
            var step2Real = Math.Cos(angleStep2);
            var step2Imaginary = Math.Sin(angleStep2);
            var twiddle2Real = 1.0;
            var twiddle2Imaginary = 0.0;
            var twiddleSum2Real = 0.0;
            var twiddleSum2Imaginary = 0.0;
            var target2Real = 0.0;
            var target2Imaginary = 0.0;

            var angleStep3 = -2.0 * Math.PI * frequency3Hz / sampleRate;
            var step3Real = Math.Cos(angleStep3);
            var step3Imaginary = Math.Sin(angleStep3);
            var twiddle3Real = 1.0;
            var twiddle3Imaginary = 0.0;
            var twiddleSum3Real = 0.0;
            var twiddleSum3Imaginary = 0.0;
            var target3Real = 0.0;
            var target3Imaginary = 0.0;

            var dcReal = 0.0;
            var dcImaginary = 0.0;

            for (var i = 0; i < iqPairCount; i++)
            {
                var real = input[i * 2];
                var imaginary = input[i * 2 + 1];

                dcReal += real;
                dcImaginary += imaginary;

                target1Real += real * twiddle1Real - imaginary * twiddle1Imaginary;
                target1Imaginary += real * twiddle1Imaginary + imaginary * twiddle1Real;
                twiddleSum1Real += twiddle1Real;
                twiddleSum1Imaginary += twiddle1Imaginary;

                target2Real += real * twiddle2Real - imaginary * twiddle2Imaginary;
                target2Imaginary += real * twiddle2Imaginary + imaginary * twiddle2Real;
                twiddleSum2Real += twiddle2Real;
                twiddleSum2Imaginary += twiddle2Imaginary;

                target3Real += real * twiddle3Real - imaginary * twiddle3Imaginary;
                target3Imaginary += real * twiddle3Imaginary + imaginary * twiddle3Real;
                twiddleSum3Real += twiddle3Real;
                twiddleSum3Imaginary += twiddle3Imaginary;

                var nextTwiddle1Real =
                    twiddle1Real * step1Real - twiddle1Imaginary * step1Imaginary;
                twiddle1Imaginary = twiddle1Real * step1Imaginary + twiddle1Imaginary * step1Real;
                twiddle1Real = nextTwiddle1Real;

                var nextTwiddle2Real =
                    twiddle2Real * step2Real - twiddle2Imaginary * step2Imaginary;
                twiddle2Imaginary = twiddle2Real * step2Imaginary + twiddle2Imaginary * step2Real;
                twiddle2Real = nextTwiddle2Real;

                var nextTwiddle3Real =
                    twiddle3Real * step3Real - twiddle3Imaginary * step3Imaginary;
                twiddle3Imaginary = twiddle3Real * step3Imaginary + twiddle3Imaginary * step3Real;
                twiddle3Real = nextTwiddle3Real;
            }

            var dc = new ReaderIqGoertzelResult(dcReal, dcImaginary);
            return (
                dc,
                SubtractDcFromTarget(
                    new ReaderIqGoertzelResult(target1Real, target1Imaginary),
                    twiddleSum1Real,
                    twiddleSum1Imaginary,
                    dc,
                    iqPairCount
                ),
                SubtractDcFromTarget(
                    new ReaderIqGoertzelResult(target2Real, target2Imaginary),
                    twiddleSum2Real,
                    twiddleSum2Imaginary,
                    dc,
                    iqPairCount
                ),
                SubtractDcFromTarget(
                    new ReaderIqGoertzelResult(target3Real, target3Imaginary),
                    twiddleSum3Real,
                    twiddleSum3Imaginary,
                    dc,
                    iqPairCount
                )
            );
        }

        private static ReaderIqGoertzelResult SubtractDcFromTarget(
            ReaderIqGoertzelResult target,
            double twiddleSumReal,
            double twiddleSumImaginary,
            ReaderIqGoertzelResult dc,
            int iqPairCount
        )
        {
            var offsetReal = dc.Real / iqPairCount;
            var offsetImaginary = dc.Imaginary / iqPairCount;
            var dcContributionReal =
                offsetReal * twiddleSumReal - offsetImaginary * twiddleSumImaginary;
            var dcContributionImaginary =
                offsetReal * twiddleSumImaginary + offsetImaginary * twiddleSumReal;

            return new ReaderIqGoertzelResult(
                target.Real - dcContributionReal,
                target.Imaginary - dcContributionImaginary
            );
        }
    }

    public class ReaderIqGoertzelPhase2Subject : ReaderIqSimpleSubject<double, (double, double)>
    {
        private readonly double _sampleRate;
        private readonly double _frequency1Hz;
        private readonly double _frequency2Hz;

        public ReaderIqGoertzelPhase2Subject(
            IReaderIqSubject<double> input,
            double sampleRate,
            double frequency1Hz,
            double frequency2Hz
        )
            : base(input)
        {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (frequency1Hz < 0 || frequency1Hz > sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(frequency1Hz));
            if (frequency2Hz < 0 || frequency2Hz > sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(frequency2Hz));

            _sampleRate = sampleRate;
            _frequency1Hz = frequency1Hz;
            _frequency2Hz = frequency2Hz;
        }

        protected override (double, double) Process(
            ReadOnlySpan<double> input,
            out bool selfPublish
        )
        {
            selfPublish = false;
            var result = ReaderIqGoertzelSubject.Calculate(
                input,
                _sampleRate,
                _frequency1Hz,
                _frequency2Hz
            );
            return (result.First.Phase, result.Second.Phase);
        }
    }

    public class ReaderIqVorReferenceSubject
        : ReaderIqSimpleSubject<double, ReaderIqVorReferenceResult>
    {
        private readonly double _sampleRate;
        private readonly double _referenceHz;
        private readonly double[] _sidebandFrequenciesHz;
        private readonly double[] _basebandStepReal;
        private readonly double[] _basebandStepImaginary;
        private readonly double[] _componentReal;
        private readonly double[] _componentImaginary;
        private readonly double[] _basebandTwiddleReal;
        private readonly double[] _basebandTwiddleImaginary;

        public ReaderIqVorReferenceSubject(
            IReaderIqSubject<double> input,
            double sampleRate,
            double subcarrierHz,
            double referenceHz,
            double sidebandBandwidthHz
        )
            : base(input)
        {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (subcarrierHz <= 0 || subcarrierHz >= sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(subcarrierHz));
            if (referenceHz <= 0 || referenceHz >= sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(referenceHz));
            if (
                sidebandBandwidthHz < 0
                || double.IsNaN(sidebandBandwidthHz)
                || double.IsInfinity(sidebandBandwidthHz)
            )
                throw new ArgumentOutOfRangeException(nameof(sidebandBandwidthHz));

            _sampleRate = sampleRate;
            _referenceHz = referenceHz;

            var sidebandFrequenciesHz = new List<double>();
            var basebandOffsetsHz = new List<double>();
            var minOffset = (int)Math.Ceiling(-sidebandBandwidthHz / referenceHz);
            var maxOffset = (int)Math.Floor(sidebandBandwidthHz / referenceHz);

            for (var offset = minOffset; offset <= maxOffset; offset++)
            {
                var basebandOffsetHz = offset * referenceHz;
                var sidebandFrequencyHz = subcarrierHz + basebandOffsetHz;
                if (sidebandFrequencyHz <= 0 || sidebandFrequencyHz >= sampleRate / 2.0)
                {
                    continue;
                }

                sidebandFrequenciesHz.Add(sidebandFrequencyHz);
                basebandOffsetsHz.Add(basebandOffsetHz);
            }

            if (sidebandFrequenciesHz.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sidebandBandwidthHz));
            }

            _sidebandFrequenciesHz = sidebandFrequenciesHz.ToArray();
            _basebandStepReal = new double[_sidebandFrequenciesHz.Length];
            _basebandStepImaginary = new double[_sidebandFrequenciesHz.Length];
            _componentReal = new double[_sidebandFrequenciesHz.Length];
            _componentImaginary = new double[_sidebandFrequenciesHz.Length];
            _basebandTwiddleReal = new double[_sidebandFrequenciesHz.Length];
            _basebandTwiddleImaginary = new double[_sidebandFrequenciesHz.Length];

            for (var i = 0; i < _sidebandFrequenciesHz.Length; i++)
            {
                var angleStep = 2.0 * Math.PI * basebandOffsetsHz[i] / sampleRate;
                _basebandStepReal[i] = Math.Cos(angleStep);
                _basebandStepImaginary[i] = Math.Sin(angleStep);
            }
        }

        protected override ReaderIqVorReferenceResult Process(
            ReadOnlySpan<double> input,
            out bool selfPublish
        )
        {
            selfPublish = false;

            var sampleCount = input.Length / 2;
            if (sampleCount <= 1)
            {
                return new ReaderIqVorReferenceResult(new ReaderIqGoertzelResult(0, 0), 0, 0);
            }

            for (var i = 0; i < _sidebandFrequenciesHz.Length; i++)
            {
                var component = ReaderIqGoertzelSubject.Calculate(
                    input,
                    _sampleRate,
                    _sidebandFrequenciesHz[i]
                );
                _componentReal[i] = component.Real;
                _componentImaginary[i] = component.Imaginary;
                _basebandTwiddleReal[i] = 1.0;
                _basebandTwiddleImaginary[i] = 0.0;
            }

            var referenceAngleStep = -2.0 * Math.PI * _referenceHz / _sampleRate;
            var referenceStepReal = Math.Cos(referenceAngleStep);
            var referenceStepImaginary = Math.Sin(referenceAngleStep);
            var referenceTwiddleAngle = referenceAngleStep * 0.5;
            var referenceTwiddleReal = Math.Cos(referenceTwiddleAngle);
            var referenceTwiddleImaginary = Math.Sin(referenceTwiddleAngle);
            var referenceTwiddleSumReal = 0.0;
            var referenceTwiddleSumImaginary = 0.0;
            var targetReal = 0.0;
            var targetImaginary = 0.0;
            var sumPhaseDelta = 0.0;
            var deltaCount = 0;
            var previousPhase = 0.0;

            for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                var basebandReal = 0.0;
                var basebandImaginary = 0.0;

                for (var i = 0; i < _sidebandFrequenciesHz.Length; i++)
                {
                    var twiddleReal = _basebandTwiddleReal[i];
                    var twiddleImaginary = _basebandTwiddleImaginary[i];
                    basebandReal +=
                        _componentReal[i] * twiddleReal - _componentImaginary[i] * twiddleImaginary;
                    basebandImaginary +=
                        _componentReal[i] * twiddleImaginary + _componentImaginary[i] * twiddleReal;

                    var nextTwiddleReal =
                        twiddleReal * _basebandStepReal[i]
                        - twiddleImaginary * _basebandStepImaginary[i];
                    _basebandTwiddleImaginary[i] =
                        twiddleReal * _basebandStepImaginary[i]
                        + twiddleImaginary * _basebandStepReal[i];
                    _basebandTwiddleReal[i] = nextTwiddleReal;
                }

                var phase = Math.Atan2(basebandImaginary, basebandReal);
                if (sampleIndex == 0)
                {
                    previousPhase = phase;
                    continue;
                }

                var phaseDelta = DspMathEx.GetDistanceAngleRad(phase, previousPhase);
                previousPhase = phase;
                sumPhaseDelta += phaseDelta;
                targetReal += phaseDelta * referenceTwiddleReal;
                targetImaginary += phaseDelta * referenceTwiddleImaginary;
                referenceTwiddleSumReal += referenceTwiddleReal;
                referenceTwiddleSumImaginary += referenceTwiddleImaginary;
                deltaCount++;

                var nextReferenceTwiddleReal =
                    referenceTwiddleReal * referenceStepReal
                    - referenceTwiddleImaginary * referenceStepImaginary;
                referenceTwiddleImaginary =
                    referenceTwiddleReal * referenceStepImaginary
                    + referenceTwiddleImaginary * referenceStepReal;
                referenceTwiddleReal = nextReferenceTwiddleReal;
            }

            if (deltaCount == 0)
            {
                return new ReaderIqVorReferenceResult(new ReaderIqGoertzelResult(0, 0), 0, 0);
            }

            var meanPhaseDelta = sumPhaseDelta / deltaCount;
            var meanContributionReal = meanPhaseDelta * referenceTwiddleSumReal;
            var meanContributionImaginary = meanPhaseDelta * referenceTwiddleSumImaginary;
            var reference = new ReaderIqGoertzelResult(
                targetReal - meanContributionReal,
                targetImaginary - meanContributionImaginary
            );
            var meanFrequencyOffsetHz = meanPhaseDelta * _sampleRate / (2.0 * Math.PI);
            return new ReaderIqVorReferenceResult(reference, meanFrequencyOffsetHz, deltaCount);
        }
    }

    public class ReaderIqGoertzelFmSubcarrierSubject : ReaderIqSimpleSubject<double, double>
    {
        private readonly double _sampleRate;
        private readonly double _subcarrierHz;
        private readonly double _sidebandStepHz;
        private readonly double _searchBandwidthHz;

        public ReaderIqGoertzelFmSubcarrierSubject(
            IReaderIqSubject<double> input,
            double sampleRate,
            double subcarrierHz,
            double sidebandStepHz,
            double searchBandwidthHz
        )
            : base(input)
        {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (subcarrierHz <= 0 || subcarrierHz >= sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(subcarrierHz));
            if (sidebandStepHz <= 0 || sidebandStepHz >= sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(sidebandStepHz));
            if (
                searchBandwidthHz < 0
                || double.IsNaN(searchBandwidthHz)
                || double.IsInfinity(searchBandwidthHz)
            )
                throw new ArgumentOutOfRangeException(nameof(searchBandwidthHz));

            _sampleRate = sampleRate;
            _subcarrierHz = subcarrierHz;
            _sidebandStepHz = sidebandStepHz;
            _searchBandwidthHz = searchBandwidthHz;
        }

        protected override double Process(ReadOnlySpan<double> input, out bool selfPublish)
        {
            selfPublish = false;
            var iqPairCount = input.Length / 2;
            if (iqPairCount == 0)
            {
                return 0;
            }

            var dc = ReaderIqGoertzelSubject.Calculate(input, _sampleRate, 0);
            var dcMagnitude = dc.Magnitude;
            if (dcMagnitude <= double.Epsilon)
            {
                return 0;
            }

            var minOffset = (int)Math.Ceiling(-_searchBandwidthHz / _sidebandStepHz);
            var maxOffset = (int)Math.Floor(_searchBandwidthHz / _sidebandStepHz);
            var sum = 0.0;

            // ICAO Doc 8071 Vol. I, Ch. 2, Table I-2-2: 9960 Hz modulation depth
            // is the full FM subcarrier depth, so use all 30 Hz-spaced sidebands.
            for (var offset = minOffset; offset <= maxOffset; offset++)
            {
                var freqHz = _subcarrierHz + (offset * _sidebandStepHz);
                if (freqHz <= 0 || freqHz >= _sampleRate / 2.0)
                {
                    continue;
                }

                var component = ReaderIqGoertzelSubject.CalculateCentered(
                    input,
                    _sampleRate,
                    freqHz,
                    dc
                );
                var componentModulationDepth = 2.0 * component.Magnitude / dcMagnitude;
                sum += componentModulationDepth * componentModulationDepth;
            }

            return Math.Sqrt(sum);
        }
    }

    public class ReaderIqGoertzelAmSubject : ReaderIqSimpleSubject<double, double>
    {
        private readonly double _sampleRate;
        private readonly double _frequencyHz;

        public ReaderIqGoertzelAmSubject(
            IReaderIqSubject<double> input,
            double sampleRate,
            double frequencyHz
        )
            : base(input)
        {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (frequencyHz < 0 || frequencyHz > sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(frequencyHz));

            _sampleRate = sampleRate;
            _frequencyHz = frequencyHz;
        }

        protected override double Process(ReadOnlySpan<double> input, out bool selfPublish)
        {
            selfPublish = false;
            var result = ReaderIqGoertzelSubject.CalculateDcAndCentered(
                input,
                _sampleRate,
                _frequencyHz
            );
            var dcMagnitude = result.Dc.Magnitude;
            if (dcMagnitude <= double.Epsilon)
            {
                return 0;
            }

            return 2.0 * result.Target.Magnitude / dcMagnitude;
        }
    }

    public class ReaderIqGoertzelAm2Subject : ReaderIqSimpleSubject<double, (double, double)>
    {
        private readonly double _sampleRate;
        private readonly double _frequency1Hz;
        private readonly double _frequency2Hz;

        public ReaderIqGoertzelAm2Subject(
            IReaderIqSubject<double> input,
            double sampleRate,
            double frequency1Hz,
            double frequency2Hz
        )
            : base(input)
        {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (frequency1Hz < 0 || frequency1Hz > sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(frequency1Hz));
            if (frequency2Hz < 0 || frequency2Hz > sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(frequency2Hz));

            _sampleRate = sampleRate;
            _frequency1Hz = frequency1Hz;
            _frequency2Hz = frequency2Hz;
        }

        protected override (double, double) Process(
            ReadOnlySpan<double> input,
            out bool selfPublish
        )
        {
            selfPublish = false;
            var result = ReaderIqGoertzelSubject.CalculateDcAndCentered(
                input,
                _sampleRate,
                _frequency1Hz,
                _frequency2Hz
            );
            var dcMagnitude = result.Dc.Magnitude;
            if (dcMagnitude <= double.Epsilon)
            {
                return (0, 0);
            }

            return (
                2.0 * result.First.Magnitude / dcMagnitude,
                2.0 * result.Second.Magnitude / dcMagnitude
            );
        }
    }

    public class ReaderIqGoertzelAm3Subject
        : ReaderIqSimpleSubject<double, (double, double, double)>
    {
        private readonly double _sampleRate;
        private readonly double _frequency1Hz;
        private readonly double _frequency2Hz;
        private readonly double _frequency3Hz;

        public ReaderIqGoertzelAm3Subject(
            IReaderIqSubject<double> input,
            double sampleRate,
            double frequency1Hz,
            double frequency2Hz,
            double frequency3Hz
        )
            : base(input)
        {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (frequency1Hz < 0 || frequency1Hz > sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(frequency1Hz));
            if (frequency2Hz < 0 || frequency2Hz > sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(frequency2Hz));
            if (frequency3Hz < 0 || frequency3Hz > sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(frequency3Hz));

            _sampleRate = sampleRate;
            _frequency1Hz = frequency1Hz;
            _frequency2Hz = frequency2Hz;
            _frequency3Hz = frequency3Hz;
        }

        protected override (double, double, double) Process(
            ReadOnlySpan<double> input,
            out bool selfPublish
        )
        {
            selfPublish = false;
            var result = ReaderIqGoertzelSubject.CalculateDcAndCentered(
                input,
                _sampleRate,
                _frequency1Hz,
                _frequency2Hz,
                _frequency3Hz
            );
            var dcMagnitude = result.Dc.Magnitude;
            if (dcMagnitude <= double.Epsilon)
            {
                return (0, 0, 0);
            }

            return (
                2.0 * result.First.Magnitude / dcMagnitude,
                2.0 * result.Second.Magnitude / dcMagnitude,
                2.0 * result.Third.Magnitude / dcMagnitude
            );
        }
    }

    public class ReaderIqGoertzelAmStableCarrierSubject : ReaderIqSimpleSubject<double, double>
    {
        private readonly double _sampleRate;
        private readonly double _frequencyHz;
        private readonly int _carrierSampleCount;
        private readonly double[] _carrierBuffer;
        private int _carrierBufferIndex;
        private double _carrierMagnitude;
        private bool _hasCarrierEstimate;

        public ReaderIqGoertzelAmStableCarrierSubject(
            IReaderIqSubject<double> input,
            double sampleRate,
            double frequencyHz,
            int carrierSampleCount
        )
            : base(input)
        {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (frequencyHz < 0 || frequencyHz > sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(frequencyHz));
            if (carrierSampleCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(carrierSampleCount));

            _sampleRate = sampleRate;
            _frequencyHz = frequencyHz;
            _carrierSampleCount = carrierSampleCount;
            _carrierBuffer = new double[carrierSampleCount * 2];
        }

        protected override double Process(ReadOnlySpan<double> input, out bool selfPublish)
        {
            UpdateCarrierEstimate(input);

            var targetSampleCount = input.Length / 2;
            if (
                targetSampleCount == 0
                || _hasCarrierEstimate == false
                || _carrierMagnitude <= double.Epsilon
            )
            {
                selfPublish = true;
                return 0;
            }

            selfPublish = false;
            var target = ReaderIqGoertzelSubject.CalculateDcAndCentered(
                input,
                _sampleRate,
                _frequencyHz
            );
            return 2.0
                * target.Target.Magnitude
                * _carrierSampleCount
                / (_carrierMagnitude * targetSampleCount);
        }

        private void UpdateCarrierEstimate(ReadOnlySpan<double> input)
        {
            var inputIndex = 0;
            while (inputIndex < input.Length)
            {
                var copyLength = Math.Min(
                    _carrierBuffer.Length - _carrierBufferIndex,
                    input.Length - inputIndex
                );
                input
                    .Slice(inputIndex, copyLength)
                    .CopyTo(_carrierBuffer.AsSpan(_carrierBufferIndex, copyLength));

                _carrierBufferIndex += copyLength;
                inputIndex += copyLength;

                if (_carrierBufferIndex != _carrierBuffer.Length)
                {
                    continue;
                }

                _carrierMagnitude = ReaderIqGoertzelSubject
                    .Calculate(_carrierBuffer, _sampleRate, 0)
                    .Magnitude;
                _hasCarrierEstimate = true;
                _carrierBufferIndex = 0;
            }
        }
    }

    public class ReaderIqGoertzelFrequencyOffsetSubject : ReaderIqSimpleSubject<double, double>
    {
        private readonly double _sampleRate;
        private readonly double _frequencyHz;
        private readonly double _searchRangeHz;
        private bool _hasPreviousPhase;
        private double _previousPhase;
        private double _previousSearchFrequencyHz;

        public ReaderIqGoertzelFrequencyOffsetSubject(
            IReaderIqSubject<double> input,
            double sampleRate,
            double frequencyHz
        )
            : this(input, sampleRate, frequencyHz, 0) { }

        public ReaderIqGoertzelFrequencyOffsetSubject(
            IReaderIqSubject<double> input,
            double sampleRate,
            double frequencyHz,
            double searchRangeHz
        )
            : base(input)
        {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (frequencyHz < 0 || frequencyHz > sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(frequencyHz));
            if (
                searchRangeHz < 0
                || double.IsNaN(searchRangeHz)
                || double.IsInfinity(searchRangeHz)
            )
                throw new ArgumentOutOfRangeException(nameof(searchRangeHz));

            _sampleRate = sampleRate;
            _frequencyHz = frequencyHz;
            _searchRangeHz = searchRangeHz;
        }

        protected override double Process(ReadOnlySpan<double> input, out bool selfPublish)
        {
            var sampleCount = input.Length / 2;
            if (sampleCount <= 0)
            {
                selfPublish = true;
                return 0;
            }

            var searchFrequencyHz = FindPeakFrequency(input, sampleCount);
            var phase = ReaderIqGoertzelSubject
                .Calculate(input, _sampleRate, searchFrequencyHz)
                .Phase;
            if (_hasPreviousPhase == false)
            {
                _previousPhase = phase;
                _previousSearchFrequencyHz = searchFrequencyHz;
                _hasPreviousPhase = true;
                selfPublish = true;
                return 0;
            }

            if (Math.Abs(searchFrequencyHz - _previousSearchFrequencyHz) > double.Epsilon)
            {
                _previousPhase = phase;
                _previousSearchFrequencyHz = searchFrequencyHz;
                selfPublish = false;
                return searchFrequencyHz - _frequencyHz;
            }

            var phaseDelta = DspMathEx.GetDistanceAngleRad(phase, _previousPhase);
            _previousPhase = phase;
            _previousSearchFrequencyHz = searchFrequencyHz;
            selfPublish = false;
            var fineOffsetHz = phaseDelta * _sampleRate / (2.0 * Math.PI * sampleCount);
            return searchFrequencyHz - _frequencyHz + fineOffsetHz;
        }

        private double FindPeakFrequency(ReadOnlySpan<double> input, int sampleCount)
        {
            if (_searchRangeHz <= 0)
            {
                return _frequencyHz;
            }

            var stepHz = _sampleRate / sampleCount;
            var minFrequencyHz = Math.Max(-_sampleRate / 2.0, _frequencyHz - _searchRangeHz);
            var maxFrequencyHz = Math.Min(_sampleRate / 2.0, _frequencyHz + _searchRangeHz);
            var bestFrequencyHz = _frequencyHz;
            var bestMagnitudeSquared = double.MinValue;

            for (
                var frequencyHz = minFrequencyHz;
                frequencyHz <= maxFrequencyHz;
                frequencyHz += stepHz
            )
            {
                var magnitudeSquared = ReaderIqGoertzelSubject
                    .Calculate(input, _sampleRate, frequencyHz)
                    .MagnitudeSquared;
                if (magnitudeSquared > bestMagnitudeSquared)
                {
                    bestMagnitudeSquared = magnitudeSquared;
                    bestFrequencyHz = frequencyHz;
                }
            }

            return bestFrequencyHz;
        }
    }

    public class ReaderIqGoertzelFrequencyOffsetFloatSubject : ReaderIqSimpleSubject<float, double>
    {
        private readonly double _sampleRate;
        private readonly double _frequencyHz;
        private readonly double _searchRangeHz;
        private bool _hasPreviousPhase;
        private double _previousPhase;
        private double _previousSearchFrequencyHz;

        public ReaderIqGoertzelFrequencyOffsetFloatSubject(
            IReaderIqSubject<float> input,
            double sampleRate,
            double frequencyHz
        )
            : this(input, sampleRate, frequencyHz, 0) { }

        public ReaderIqGoertzelFrequencyOffsetFloatSubject(
            IReaderIqSubject<float> input,
            double sampleRate,
            double frequencyHz,
            double searchRangeHz
        )
            : base(input)
        {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (frequencyHz < 0 || frequencyHz > sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(frequencyHz));
            if (
                searchRangeHz < 0
                || double.IsNaN(searchRangeHz)
                || double.IsInfinity(searchRangeHz)
            )
                throw new ArgumentOutOfRangeException(nameof(searchRangeHz));

            _sampleRate = sampleRate;
            _frequencyHz = frequencyHz;
            _searchRangeHz = searchRangeHz;
        }

        protected override double Process(ReadOnlySpan<float> input, out bool selfPublish)
        {
            var sampleCount = input.Length / 2;
            if (sampleCount <= 0)
            {
                selfPublish = true;
                return 0;
            }

            var searchFrequencyHz = FindPeakFrequency(input, sampleCount);
            var phase = ReaderIqGoertzelSubject
                .Calculate(input, _sampleRate, searchFrequencyHz)
                .Phase;
            if (_hasPreviousPhase == false)
            {
                _previousPhase = phase;
                _previousSearchFrequencyHz = searchFrequencyHz;
                _hasPreviousPhase = true;
                selfPublish = true;
                return 0;
            }

            if (Math.Abs(searchFrequencyHz - _previousSearchFrequencyHz) > double.Epsilon)
            {
                _previousPhase = phase;
                _previousSearchFrequencyHz = searchFrequencyHz;
                selfPublish = false;
                return searchFrequencyHz - _frequencyHz;
            }

            var phaseDelta = DspMathEx.GetDistanceAngleRad(phase, _previousPhase);
            _previousPhase = phase;
            _previousSearchFrequencyHz = searchFrequencyHz;
            selfPublish = false;
            var fineOffsetHz = phaseDelta * _sampleRate / (2.0 * Math.PI * sampleCount);
            return searchFrequencyHz - _frequencyHz + fineOffsetHz;
        }

        private double FindPeakFrequency(ReadOnlySpan<float> input, int sampleCount)
        {
            if (_searchRangeHz <= 0)
            {
                return _frequencyHz;
            }

            var stepHz = _sampleRate / sampleCount;
            var minFrequencyHz = Math.Max(-_sampleRate / 2.0, _frequencyHz - _searchRangeHz);
            var maxFrequencyHz = Math.Min(_sampleRate / 2.0, _frequencyHz + _searchRangeHz);
            var bestFrequencyHz = _frequencyHz;
            var bestMagnitudeSquared = double.MinValue;

            for (
                var frequencyHz = minFrequencyHz;
                frequencyHz <= maxFrequencyHz;
                frequencyHz += stepHz
            )
            {
                var magnitudeSquared = ReaderIqGoertzelSubject
                    .Calculate(input, _sampleRate, frequencyHz)
                    .MagnitudeSquared;
                if (magnitudeSquared > bestMagnitudeSquared)
                {
                    bestMagnitudeSquared = magnitudeSquared;
                    bestFrequencyHz = frequencyHz;
                }
            }

            return bestFrequencyHz;
        }
    }
}
