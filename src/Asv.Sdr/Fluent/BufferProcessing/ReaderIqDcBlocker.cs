using System;

namespace Asv.Sdr
{
    /// <summary>
    /// Removes steady-state DC and LO leakage from interleaved I/Q samples with a first-order high-pass filter.
    /// </summary>
    public sealed class ReaderIqDcBlockerSubject : ReaderIqSubject<double, double>
    {
        private readonly double _pole;
        private bool _hasState;
        private double _previousInputI;
        private double _previousInputQ;
        private double _previousOutputI;
        private double _previousOutputQ;

        /// <summary>
        /// Initializes a DC blocker for double-precision interleaved I/Q samples.
        /// </summary>
        /// <param name="input">The source I/Q stream.</param>
        /// <param name="sampleRate">The source sample rate in hertz.</param>
        /// <param name="cutoffHz">The high-pass cutoff frequency in hertz.</param>
        /// <param name="useArrayPool">True to rent the output buffer from the shared array pool.</param>
        public ReaderIqDcBlockerSubject(
            IReaderIqSubject<double> input,
            double sampleRate,
            double cutoffHz,
            bool useArrayPool = true
        )
            : base(input, GetOutputBufferSize(input), useArrayPool)
        {
            ValidateFilterParameters(sampleRate, cutoffHz);

            _pole = Math.Exp(-2.0 * Math.PI * cutoffHz / sampleRate);
        }

        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            if (_hasState == false)
            {
                _previousInputI = input[0];
                _previousInputQ = input[1];
                _hasState = true;
            }

            for (var index = 0; index < OutputBufferSize; index += 2)
            {
                var inputI = input[index];
                var inputQ = input[index + 1];
                var outputI = (inputI - _previousInputI) + (_pole * _previousOutputI);
                var outputQ = (inputQ - _previousInputQ) + (_pole * _previousOutputQ);

                output[index] = outputI;
                output[index + 1] = outputQ;

                _previousInputI = inputI;
                _previousInputQ = inputQ;
                _previousOutputI = outputI;
                _previousOutputQ = outputQ;
            }
        }

        private static int GetOutputBufferSize(IReaderIqSubject<double> input)
        {
            ArgumentNullException.ThrowIfNull(input);
            ValidateIqBufferSize(input.OutputBufferSize);

            return input.OutputBufferSize;
        }

        internal static void ValidateFilterParameters(double sampleRate, double cutoffHz)
        {
            if (double.IsFinite(sampleRate) == false || sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }

            if (double.IsFinite(cutoffHz) == false || cutoffHz <= 0 || cutoffHz >= sampleRate / 2.0)
            {
                throw new ArgumentOutOfRangeException(nameof(cutoffHz));
            }
        }

        internal static void ValidateIqBufferSize(int outputBufferSize)
        {
            if ((outputBufferSize & 1) != 0)
            {
                throw new ArgumentException("IQ buffer size must contain complete I/Q pairs.");
            }
        }
    }

    /// <summary>
    /// Converts single-precision interleaved I/Q samples to double precision while removing steady-state DC and LO leakage.
    /// </summary>
    public sealed class ReaderIqDcBlockerFloatSubject : ReaderIqSubject<float, double>
    {
        private readonly double _pole;
        private bool _hasState;
        private double _previousInputI;
        private double _previousInputQ;
        private double _previousOutputI;
        private double _previousOutputQ;

        /// <summary>
        /// Initializes a DC blocker for single-precision interleaved I/Q samples.
        /// </summary>
        /// <param name="input">The source I/Q stream.</param>
        /// <param name="sampleRate">The source sample rate in hertz.</param>
        /// <param name="cutoffHz">The high-pass cutoff frequency in hertz.</param>
        /// <param name="useArrayPool">True to rent the output buffer from the shared array pool.</param>
        public ReaderIqDcBlockerFloatSubject(
            IReaderIqSubject<float> input,
            double sampleRate,
            double cutoffHz,
            bool useArrayPool = true
        )
            : base(input, GetOutputBufferSize(input), useArrayPool)
        {
            ReaderIqDcBlockerSubject.ValidateFilterParameters(sampleRate, cutoffHz);

            _pole = Math.Exp(-2.0 * Math.PI * cutoffHz / sampleRate);
        }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            if (_hasState == false)
            {
                _previousInputI = input[0];
                _previousInputQ = input[1];
                _hasState = true;
            }

            for (var index = 0; index < OutputBufferSize; index += 2)
            {
                var inputI = input[index];
                var inputQ = input[index + 1];
                var outputI = (inputI - _previousInputI) + (_pole * _previousOutputI);
                var outputQ = (inputQ - _previousInputQ) + (_pole * _previousOutputQ);

                output[index] = outputI;
                output[index + 1] = outputQ;

                _previousInputI = inputI;
                _previousInputQ = inputQ;
                _previousOutputI = outputI;
                _previousOutputQ = outputQ;
            }
        }

        private static int GetOutputBufferSize(IReaderIqSubject<float> input)
        {
            ArgumentNullException.ThrowIfNull(input);
            ReaderIqDcBlockerSubject.ValidateIqBufferSize(input.OutputBufferSize);

            return input.OutputBufferSize;
        }
    }

    /// <summary>
    /// Provides fluent helpers for inserting an I/Q DC blocker into reader pipelines.
    /// </summary>
    public static class ReaderIqDcBlockerSubjectExtensions
    {
        /// <summary>
        /// Adds an independent first-order DC blocker for the I and Q channels.
        /// </summary>
        /// <param name="input">The source I/Q stream.</param>
        /// <param name="sampleRate">The source sample rate in hertz.</param>
        /// <param name="cutoffHz">The high-pass cutoff frequency in hertz.</param>
        /// <param name="useArrayPool">True to rent the output buffer from the shared array pool.</param>
        /// <returns>A double-precision I/Q stream with DC and LO leakage suppressed.</returns>
        public static IReaderIqSubject<double> AddIqDcBlocker(
            this IReaderIqSubject<double> input,
            double sampleRate,
            double cutoffHz,
            bool useArrayPool = true
        )
        {
            return new ReaderIqDcBlockerSubject(input, sampleRate, cutoffHz, useArrayPool);
        }

        /// <summary>
        /// Adds an independent first-order DC blocker for the I and Q channels and converts output to double precision.
        /// </summary>
        /// <param name="input">The source I/Q stream.</param>
        /// <param name="sampleRate">The source sample rate in hertz.</param>
        /// <param name="cutoffHz">The high-pass cutoff frequency in hertz.</param>
        /// <param name="useArrayPool">True to rent the output buffer from the shared array pool.</param>
        /// <returns>A double-precision I/Q stream with DC and LO leakage suppressed.</returns>
        public static IReaderIqSubject<double> AddIqDcBlocker(
            this IReaderIqSubject<float> input,
            double sampleRate,
            double cutoffHz,
            bool useArrayPool = true
        )
        {
            return new ReaderIqDcBlockerFloatSubject(input, sampleRate, cutoffHz, useArrayPool);
        }
    }
}
