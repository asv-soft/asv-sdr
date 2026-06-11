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
            var dc = ReaderIqGoertzelSubject.Calculate(input, _sampleRate, 0);
            if (dc.Magnitude <= double.Epsilon)
            {
                return 0;
            }

            var target = ReaderIqGoertzelSubject.CalculateCentered(
                input,
                _sampleRate,
                _frequencyHz,
                dc
            );
            return 2.0 * target.Magnitude / dc.Magnitude;
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
            var targetDc = ReaderIqGoertzelSubject.Calculate(input, _sampleRate, 0);
            var target = ReaderIqGoertzelSubject.CalculateCentered(
                input,
                _sampleRate,
                _frequencyHz,
                targetDc
            );
            return 2.0
                * target.Magnitude
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
            var bestMagnitude = double.MinValue;

            for (
                var frequencyHz = minFrequencyHz;
                frequencyHz <= maxFrequencyHz;
                frequencyHz += stepHz
            )
            {
                var magnitude = ReaderIqGoertzelSubject
                    .Calculate(input, _sampleRate, frequencyHz)
                    .Magnitude;
                if (magnitude > bestMagnitude)
                {
                    bestMagnitude = magnitude;
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
            var bestMagnitude = double.MinValue;

            for (
                var frequencyHz = minFrequencyHz;
                frequencyHz <= maxFrequencyHz;
                frequencyHz += stepHz
            )
            {
                var magnitude = ReaderIqGoertzelSubject
                    .Calculate(input, _sampleRate, frequencyHz)
                    .Magnitude;
                if (magnitude > bestMagnitude)
                {
                    bestMagnitude = magnitude;
                    bestFrequencyHz = frequencyHz;
                }
            }

            return bestFrequencyHz;
        }
    }
}
