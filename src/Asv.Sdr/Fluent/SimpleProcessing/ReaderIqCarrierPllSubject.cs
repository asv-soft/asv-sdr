using System;

namespace Asv.Sdr
{
    public sealed class ReaderIqCarrierPllOptions
    {
        public ReaderIqCarrierPllOptions(double sampleRate, double nominalFrequencyHz)
        {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (Math.Abs(nominalFrequencyHz) > sampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(nominalFrequencyHz));

            SampleRate = sampleRate;
            NominalFrequencyHz = nominalFrequencyHz;
        }

        public double SampleRate { get; }

        public double NominalFrequencyHz { get; }

        public double LoopBandwidthHz { get; set; } = 100.0;

        public double DampingFactor { get; set; } = 0.707;

        public double InitialFrequencyOffsetHz { get; set; }

        public double MaxFrequencyOffsetHz { get; set; } = double.PositiveInfinity;

        public double MinMagnitude { get; set; }

        public double LockThresholdRad { get; set; } = Math.PI / 6.0;
    }

    public sealed class ReaderIqCarrierPllResult
    {
        public ReaderIqCarrierPllResult(
            double frequencyHz,
            double frequencyOffsetHz,
            double phaseRad,
            double meanAbsPhaseErrorRad,
            double meanMagnitude,
            int sampleCount,
            int trackedSampleCount,
            bool isLocked
        )
        {
            FrequencyHz = frequencyHz;
            FrequencyOffsetHz = frequencyOffsetHz;
            PhaseRad = phaseRad;
            MeanAbsPhaseErrorRad = meanAbsPhaseErrorRad;
            MeanMagnitude = meanMagnitude;
            SampleCount = sampleCount;
            TrackedSampleCount = trackedSampleCount;
            IsLocked = isLocked;
        }

        public double FrequencyHz { get; }

        public double FrequencyOffsetHz { get; }

        public double PhaseRad { get; }

        public double MeanAbsPhaseErrorRad { get; }

        public double MeanMagnitude { get; }

        public int SampleCount { get; }

        public int TrackedSampleCount { get; }

        public bool IsLocked { get; }
    }

    public sealed class ReaderIqCarrierPllDoubleSubject
        : ReaderIqSimpleSubject<double, ReaderIqCarrierPllResult>
    {
        private readonly CarrierPllCore _core;

        public ReaderIqCarrierPllDoubleSubject(
            IReaderIqSubject<double> input,
            ReaderIqCarrierPllOptions options
        )
            : base(input)
        {
            if (input.OutputBufferSize < 2 || input.OutputBufferSize % 2 != 0)
                throw new ArgumentException(
                    "Input buffer size must contain a whole number of IQ pairs."
                );

            _core = new CarrierPllCore(options);
        }

        protected override ReaderIqCarrierPllResult Process(
            ReadOnlySpan<double> input,
            out bool selfPublish
        )
        {
            selfPublish = false;
            return _core.Process(input);
        }
    }

    public sealed class ReaderIqCarrierPllFloatSubject
        : ReaderIqSimpleSubject<float, ReaderIqCarrierPllResult>
    {
        private readonly CarrierPllCore _core;

        public ReaderIqCarrierPllFloatSubject(
            IReaderIqSubject<float> input,
            ReaderIqCarrierPllOptions options
        )
            : base(input)
        {
            if (input.OutputBufferSize < 2 || input.OutputBufferSize % 2 != 0)
                throw new ArgumentException(
                    "Input buffer size must contain a whole number of IQ pairs."
                );

            _core = new CarrierPllCore(options);
        }

        protected override ReaderIqCarrierPllResult Process(
            ReadOnlySpan<float> input,
            out bool selfPublish
        )
        {
            selfPublish = false;
            return _core.Process(input);
        }
    }

    internal sealed class CarrierPllCore
    {
        private const double TwoPi = 2.0 * Math.PI;

        private readonly double _sampleRate;
        private readonly double _nominalFrequencyHz;
        private readonly double _nominalRadPerSample;
        private readonly double _proportionalGain;
        private readonly double _integralGain;
        private readonly double _maxFrequencyOffsetRadPerSample;
        private readonly double _minMagnitude;
        private readonly double _lockThresholdRad;

        private double _frequencyOffsetRadPerSample;
        private double _phaseRad;

        public CarrierPllCore(ReaderIqCarrierPllOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (
                options.LoopBandwidthHz <= 0
                || double.IsNaN(options.LoopBandwidthHz)
                || double.IsInfinity(options.LoopBandwidthHz)
            )
                throw new ArgumentOutOfRangeException(nameof(options.LoopBandwidthHz));
            if (options.LoopBandwidthHz >= options.SampleRate / 2.0)
                throw new ArgumentOutOfRangeException(nameof(options.LoopBandwidthHz));
            if (
                options.DampingFactor <= 0
                || double.IsNaN(options.DampingFactor)
                || double.IsInfinity(options.DampingFactor)
            )
                throw new ArgumentOutOfRangeException(nameof(options.DampingFactor));
            if (options.MaxFrequencyOffsetHz <= 0 || double.IsNaN(options.MaxFrequencyOffsetHz))
                throw new ArgumentOutOfRangeException(nameof(options.MaxFrequencyOffsetHz));
            if (
                options.MinMagnitude < 0
                || double.IsNaN(options.MinMagnitude)
                || double.IsInfinity(options.MinMagnitude)
            )
                throw new ArgumentOutOfRangeException(nameof(options.MinMagnitude));
            if (
                options.LockThresholdRad <= 0
                || options.LockThresholdRad > Math.PI
                || double.IsNaN(options.LockThresholdRad)
                || double.IsInfinity(options.LockThresholdRad)
            )
                throw new ArgumentOutOfRangeException(nameof(options.LockThresholdRad));

            _sampleRate = options.SampleRate;
            _nominalFrequencyHz = options.NominalFrequencyHz;
            _nominalRadPerSample = HzToRadPerSample(options.NominalFrequencyHz, options.SampleRate);
            _frequencyOffsetRadPerSample = HzToRadPerSample(
                options.InitialFrequencyOffsetHz,
                options.SampleRate
            );
            _maxFrequencyOffsetRadPerSample = double.IsInfinity(options.MaxFrequencyOffsetHz)
                ? double.PositiveInfinity
                : Math.Abs(HzToRadPerSample(options.MaxFrequencyOffsetHz, options.SampleRate));
            _minMagnitude = options.MinMagnitude;
            _lockThresholdRad = options.LockThresholdRad;

            var normalizedBandwidth = TwoPi * options.LoopBandwidthHz / options.SampleRate;
            var denominator =
                1.0
                + (2.0 * options.DampingFactor * normalizedBandwidth)
                + (normalizedBandwidth * normalizedBandwidth);
            _proportionalGain = 4.0 * options.DampingFactor * normalizedBandwidth / denominator;
            _integralGain = 4.0 * normalizedBandwidth * normalizedBandwidth / denominator;
            _frequencyOffsetRadPerSample = ClampFrequencyOffset(_frequencyOffsetRadPerSample);
        }

        public ReaderIqCarrierPllResult Process(ReadOnlySpan<double> input)
        {
            var sampleCount = input.Length / 2;
            var sumAbsPhaseError = 0.0;
            var sumMagnitude = 0.0;
            var trackedSampleCount = 0;

            for (var i = 0; i < sampleCount; i++)
            {
                ProcessSample(
                    input[i * 2],
                    input[i * 2 + 1],
                    ref sumAbsPhaseError,
                    ref sumMagnitude,
                    ref trackedSampleCount
                );
            }

            return CreateResult(sampleCount, trackedSampleCount, sumAbsPhaseError, sumMagnitude);
        }

        public ReaderIqCarrierPllResult Process(ReadOnlySpan<float> input)
        {
            var sampleCount = input.Length / 2;
            var sumAbsPhaseError = 0.0;
            var sumMagnitude = 0.0;
            var trackedSampleCount = 0;

            for (var i = 0; i < sampleCount; i++)
            {
                ProcessSample(
                    input[i * 2],
                    input[i * 2 + 1],
                    ref sumAbsPhaseError,
                    ref sumMagnitude,
                    ref trackedSampleCount
                );
            }

            return CreateResult(sampleCount, trackedSampleCount, sumAbsPhaseError, sumMagnitude);
        }

        private void ProcessSample(
            double real,
            double imaginary,
            ref double sumAbsPhaseError,
            ref double sumMagnitude,
            ref int trackedSampleCount
        )
        {
            var oscillatorReal = Math.Cos(_phaseRad);
            var oscillatorImaginary = Math.Sin(_phaseRad);
            var mixedReal = (real * oscillatorReal) + (imaginary * oscillatorImaginary);
            var mixedImaginary = (imaginary * oscillatorReal) - (real * oscillatorImaginary);
            var magnitude = Math.Sqrt((mixedReal * mixedReal) + (mixedImaginary * mixedImaginary));
            sumMagnitude += magnitude;

            if (magnitude >= _minMagnitude)
            {
                var phaseError = Math.Atan2(mixedImaginary, mixedReal);
                _frequencyOffsetRadPerSample = ClampFrequencyOffset(
                    _frequencyOffsetRadPerSample + (_integralGain * phaseError)
                );
                _phaseRad +=
                    _nominalRadPerSample
                    + _frequencyOffsetRadPerSample
                    + (_proportionalGain * phaseError);
                sumAbsPhaseError += Math.Abs(phaseError);
                trackedSampleCount++;
            }
            else
            {
                _phaseRad += _nominalRadPerSample + _frequencyOffsetRadPerSample;
            }

            _phaseRad = NormalizeRadians(_phaseRad);
        }

        private ReaderIqCarrierPllResult CreateResult(
            int sampleCount,
            int trackedSampleCount,
            double sumAbsPhaseError,
            double sumMagnitude
        )
        {
            var frequencyOffsetHz = RadPerSampleToHz(_frequencyOffsetRadPerSample, _sampleRate);
            var meanAbsPhaseError =
                trackedSampleCount > 0 ? sumAbsPhaseError / trackedSampleCount : double.NaN;
            var meanMagnitude = sampleCount > 0 ? sumMagnitude / sampleCount : double.NaN;
            var isLocked =
                trackedSampleCount > 0
                && meanMagnitude >= _minMagnitude
                && meanAbsPhaseError <= _lockThresholdRad;

            return new ReaderIqCarrierPllResult(
                _nominalFrequencyHz + frequencyOffsetHz,
                frequencyOffsetHz,
                NormalizeRadians(_phaseRad),
                meanAbsPhaseError,
                meanMagnitude,
                sampleCount,
                trackedSampleCount,
                isLocked
            );
        }

        private double ClampFrequencyOffset(double value)
        {
            if (double.IsInfinity(_maxFrequencyOffsetRadPerSample))
            {
                return value;
            }

            if (value > _maxFrequencyOffsetRadPerSample)
            {
                return _maxFrequencyOffsetRadPerSample;
            }

            return value < -_maxFrequencyOffsetRadPerSample
                ? -_maxFrequencyOffsetRadPerSample
                : value;
        }

        private static double HzToRadPerSample(double frequencyHz, double sampleRate)
        {
            return TwoPi * frequencyHz / sampleRate;
        }

        private static double RadPerSampleToHz(double value, double sampleRate)
        {
            return value * sampleRate / TwoPi;
        }

        private static double NormalizeRadians(double value)
        {
            var result = value % TwoPi;
            if (result > Math.PI)
            {
                result -= TwoPi;
            }
            else if (result < -Math.PI)
            {
                result += TwoPi;
            }

            return result;
        }
    }
}
