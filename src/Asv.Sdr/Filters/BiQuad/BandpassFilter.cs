using System;
using Asv.Sdr;

namespace Asv.Tools
{
    /// <summary>
    /// Used to apply a bandpass-filter to a signal.
    /// https://github.com/filoe/cscore/blob/master/CSCore/DSP/BandpassFilter.cs
    /// </summary>
    public class BandpassFilter : BiQuad
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BandpassFilter"/> class.
        /// </summary>
        /// <param name="sampleRate">The sample rate.</param>
        /// <param name="frequency">The filter's corner frequency.</param>
        public BandpassFilter(int sampleRate, double frequency)
            : base(sampleRate, frequency)
        {
        }

        public BandpassFilter(int sampleRate, double frequency, double q)
            : base(sampleRate, frequency, q)
        {

        }

        /// <summary>
        /// Calculates all coefficients.
        /// </summary>
        protected override void CalculateBiQuadCoefficients()
        {
            double k = Math.Tan(Math.PI * Frequency / SampleRate);
            double norm = 1 / (1 + k / Q + k * k);
            A0 = k / Q * norm;
            A1 = 0;
            A2 = -A0;
            B1 = 2 * (k * k - 1) * norm;
            B2 = (1 - k / Q + k * k) * norm;
        }
    }
}