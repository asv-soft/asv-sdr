using System;

namespace Asv.Sdr
{
    public class ReaderIqFreqShiftDouble:ReaderIqSubject<double, double>
    {
        private readonly double _argMultiplier;
        private uint _increment;
        private readonly int _size;

        public ReaderIqFreqShiftDouble(IReaderIqSubject<double> input, double sampleRate,int freqHz, bool useArrayPool) : base(input, input.OutputBufferSize, useArrayPool)
        {
            _argMultiplier = 2 * Math.PI * freqHz / sampleRate;
            _increment = 0;
            _size = input.OutputBufferSize / 2;
        }

        protected override void Process(ReadOnlySpan<double> input, Span<double> output)
        {
            for (var i = 0; i < _size; i++)
            {
                output[i * 2] = Math.Sin(_increment * _argMultiplier) * input[i * 2];
                output[i * 2 + 1] = Math.Cos(_increment * _argMultiplier) * input[i * 2 + 1];
                _increment++;
            }
        }
    }

    public class ReaderIqFreqShiftFloat : ReaderIqSubject<float, double>
    {
        private readonly double _argMultiplier;
        private uint _increment;
        private readonly int _size;

        public ReaderIqFreqShiftFloat(IReaderIqSubject<float> input, double sampleRate, int freqHz, bool useArrayPool) : base(input, input.OutputBufferSize, useArrayPool)
        {
            _argMultiplier = 2 * Math.PI * freqHz / sampleRate;
            _increment = 0;
            _size = input.OutputBufferSize / 2;
        }

        protected override void Process(ReadOnlySpan<float> input, Span<double> output)
        {
            for (var i = 0; i < _size; i++)
            {
                output[i * 2] = Math.Sin(_increment * _argMultiplier);
                output[i * 2] = Math.Cos(_increment * _argMultiplier);
                _increment++;
            }
        }
    }
}