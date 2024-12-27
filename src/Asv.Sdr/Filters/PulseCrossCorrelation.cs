using System;
using System.Collections.Immutable;
using System.Linq;
using Asv.Common;

namespace Asv.Sdr
{
    /// <summary>
    /// Represents a pulse cross-correlation filter used for signal processing.
    /// </summary>
    public class PulseCrossCorrelation : IDspFilter
    {
        private readonly ImmutableArray<double> _template;
        private readonly CircularBuffer2<double> _buffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="PulseCrossCorrelation"/> class.
        /// </summary>
        /// <param name="sampleRate">The sample rate in Hz.</param>
        /// <param name="bitRate">The bit rate in Hz.</param>
        /// <param name="pulseTemplate">The pulse template.</param>
        public PulseCrossCorrelation(int sampleRate, int bitRate, byte[] pulseTemplate)
            : this(sampleRate / bitRate, pulseTemplate) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="PulseCrossCorrelation"/> class.
        /// </summary>
        /// <param name="pulseSize">Size of one pulse in samples</param>
        /// <param name="pulseTemplate">Pulse template (0 or 1). For example [1, 0, 1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0]</param>
        /// <exception cref="Exception">.</exception>
        public PulseCrossCorrelation(int pulseSize, byte[] pulseTemplate)
        {
            _buffer = new CircularBuffer2<double>(pulseTemplate.Length * pulseSize);
            var builder = ImmutableArray.CreateBuilder<double>(pulseTemplate.Length * pulseSize);

            foreach (var val in pulseTemplate)
            {
                if (val is not (0 or 1))
                {
                    throw new Exception("Invalid template value: must be 0 or 1");
                }

                builder.AddRange(Enumerable.Repeat(val == 0 ? -1.0 : 1.0, pulseSize));
            }

            _template = builder.ToImmutable();
        }

        public double Process(double input)
        {
            _buffer.PushFront(input);
            if (_buffer.IsFull == false)
            {
                return 0;
            }

            var summ = 0.0;
            for (var i = 0; i < _template.Length; i++)
            {
                summ += _template[i] * _buffer[i];
            }

            return summ;
        }

        public void Reset()
        {
            _buffer.Clear();
        }
    }
}
