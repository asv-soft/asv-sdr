using System;

namespace Asv.Sdr
{
    public class ReaderIqFreqShiftDouble:ReaderIqSubject<double, double>
    {
        private readonly double _argMultiplier;
        private uint _increment;
        private readonly int _complexSampleCount;

        public ReaderIqFreqShiftDouble(IReaderIqSubject<double> input, double sampleRate,int freqHz, bool useArrayPool) : base(input, input.OutputBufferSize, useArrayPool)
        {
            _argMultiplier = 2 * Math.PI * freqHz / sampleRate;
            _increment = 0;
            _complexSampleCount = input.OutputBufferSize / 2;
        }

        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            for (var i = 0; i < _complexSampleCount; i++)
            {
                var offset = i * 2;
                var phase = _increment * _argMultiplier;
                var cos = Math.Cos(phase);
                var sin = Math.Sin(phase);
                var inputI = input[offset];
                var inputQ = input[offset + 1];

                output[offset] = inputI * cos - inputQ * sin;
                output[offset + 1] = inputI * sin + inputQ * cos;
                _increment++;
            }
        }
    }

    public class ReaderIqFreqShiftFloat : ReaderIqSubject<float, double>
    {
        private readonly double _argMultiplier;
        private uint _increment;
        private readonly int _complexSampleCount;

        public ReaderIqFreqShiftFloat(IReaderIqSubject<float> input, double sampleRate, int freqHz, bool useArrayPool) : base(input, input.OutputBufferSize, useArrayPool)
        {
            _argMultiplier = 2 * Math.PI * freqHz / sampleRate;
            _increment = 0;
            _complexSampleCount = input.OutputBufferSize / 2;
        }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            for (var i = 0; i < _complexSampleCount; i++)
            {
                var offset = i * 2;
                var phase = _increment * _argMultiplier;
                var cos = Math.Cos(phase);
                var sin = Math.Sin(phase);
                var inputI = input[offset];
                var inputQ = input[offset + 1];

                output[offset] = inputI * cos - inputQ * sin;
                output[offset + 1] = inputI * sin + inputQ * cos;
                _increment++;
            }
        }
    }
}
