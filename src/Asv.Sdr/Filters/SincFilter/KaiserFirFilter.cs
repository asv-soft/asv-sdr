using System;

namespace Asv.Sdr
{
    /// <summary>
    /// Linear-phase FIR low-pass filter generated with a Kaiser-windowed sinc.
    /// </summary>
    public class KaiserLowPassFilter : CicSincFilter
    {
        public KaiserLowPassFilter(
            int sampleRate,
            double passFrequency,
            double stopFrequency,
            double attenuationDb
        )
            : base(
                KaiserFirDesigner.CreateLowPass(
                    sampleRate,
                    passFrequency,
                    stopFrequency,
                    attenuationDb
                )
            )
        {
        }
    }

    /// <summary>
    /// Linear-phase FIR band-pass filter generated with a Kaiser-windowed sinc.
    /// </summary>
    public class KaiserBandPassFilter : CicSincFilter
    {
        public KaiserBandPassFilter(
            int sampleRate,
            double lowerStopFrequency,
            double lowerPassFrequency,
            double upperPassFrequency,
            double upperStopFrequency,
            double attenuationDb
        )
            : base(
                KaiserFirDesigner.CreateBandPass(
                    sampleRate,
                    lowerStopFrequency,
                    lowerPassFrequency,
                    upperPassFrequency,
                    upperStopFrequency,
                    attenuationDb
                )
            )
        {
        }
    }

    internal static class KaiserFirDesigner
    {
        public static double[] CreateLowPass(
            int sampleRate,
            double passFrequency,
            double stopFrequency,
            double attenuationDb
        )
        {
            ValidateSampleRate(sampleRate);
            ValidateAttenuation(attenuationDb);
            ValidateFrequencyRange(nameof(passFrequency), passFrequency, sampleRate, includeZero: false);
            ValidateFrequencyRange(nameof(stopFrequency), stopFrequency, sampleRate, includeZero: false);

            if (passFrequency >= stopFrequency)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(passFrequency),
                    "Pass frequency must be lower than stop frequency."
                );
            }

            var cutoffFrequency = (passFrequency + stopFrequency) / 2.0;
            var order = EstimateEvenOrder(sampleRate, stopFrequency - passFrequency, attenuationDb);
            var beta = CalculateKaiserBeta(attenuationDb);
            var taps = CreateWindowedSinc(order, beta, n =>
            {
                var m = n - order / 2.0;
                if (Math.Abs(m) < double.Epsilon)
                {
                    return 2.0 * cutoffFrequency / sampleRate;
                }

                return Math.Sin(2.0 * Math.PI * cutoffFrequency * m / sampleRate) / (Math.PI * m);
            });

            Normalize(taps, sampleRate, 0.0);
            return taps;
        }

        public static double[] CreateBandPass(
            int sampleRate,
            double lowerStopFrequency,
            double lowerPassFrequency,
            double upperPassFrequency,
            double upperStopFrequency,
            double attenuationDb
        )
        {
            ValidateSampleRate(sampleRate);
            ValidateAttenuation(attenuationDb);
            ValidateFrequencyRange(nameof(lowerStopFrequency), lowerStopFrequency, sampleRate, includeZero: true);
            ValidateFrequencyRange(nameof(lowerPassFrequency), lowerPassFrequency, sampleRate, includeZero: false);
            ValidateFrequencyRange(nameof(upperPassFrequency), upperPassFrequency, sampleRate, includeZero: false);
            ValidateFrequencyRange(nameof(upperStopFrequency), upperStopFrequency, sampleRate, includeZero: false);

            if (
                lowerStopFrequency >= lowerPassFrequency
                || lowerPassFrequency >= upperPassFrequency
                || upperPassFrequency >= upperStopFrequency
            )
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lowerStopFrequency),
                    "Frequencies must satisfy lower stop < lower pass < upper pass < upper stop."
                );
            }

            var lowerCutoffFrequency = (lowerStopFrequency + lowerPassFrequency) / 2.0;
            var upperCutoffFrequency = (upperPassFrequency + upperStopFrequency) / 2.0;
            var transitionWidth = Math.Min(
                lowerPassFrequency - lowerStopFrequency,
                upperStopFrequency - upperPassFrequency
            );
            var order = EstimateEvenOrder(sampleRate, transitionWidth, attenuationDb);
            var beta = CalculateKaiserBeta(attenuationDb);
            var taps = CreateWindowedSinc(order, beta, n =>
            {
                var m = n - order / 2.0;
                if (Math.Abs(m) < double.Epsilon)
                {
                    return 2.0 * (upperCutoffFrequency - lowerCutoffFrequency) / sampleRate;
                }

                return (
                    Math.Sin(2.0 * Math.PI * upperCutoffFrequency * m / sampleRate)
                    - Math.Sin(2.0 * Math.PI * lowerCutoffFrequency * m / sampleRate)
                ) / (Math.PI * m);
            });

            Normalize(taps, sampleRate, (lowerCutoffFrequency + upperCutoffFrequency) / 2.0);
            return taps;
        }

        private static double[] CreateWindowedSinc(
            int order,
            double beta,
            Func<int, double> calculateIdealTap
        )
        {
            var taps = new double[order + 1];
            var betaBessel = ModifiedBessel0(beta);
            for (var n = 0; n < taps.Length; n++)
            {
                var x = (2.0 * n / order) - 1.0;
                var window = ModifiedBessel0(beta * Math.Sqrt(Math.Max(0.0, 1.0 - x * x)))
                    / betaBessel;
                taps[n] = calculateIdealTap(n) * window;
            }

            return taps;
        }

        private static int EstimateEvenOrder(
            int sampleRate,
            double transitionWidth,
            double attenuationDb
        )
        {
            if (transitionWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(transitionWidth),
                    "Transition width must be greater than zero."
                );
            }

            var normalizedTransition = transitionWidth / sampleRate;
            var order = (int)Math.Ceiling(
                (attenuationDb - 8.0) / (2.285 * 2.0 * Math.PI * normalizedTransition)
            );
            order = Math.Max(2, order);
            return order % 2 == 0 ? order : order + 1;
        }

        private static double CalculateKaiserBeta(double attenuationDb)
        {
            if (attenuationDb > 50.0)
            {
                return 0.1102 * (attenuationDb - 8.7);
            }

            if (attenuationDb >= 21.0)
            {
                return 0.5842 * Math.Pow(attenuationDb - 21.0, 0.4)
                    + 0.07886 * (attenuationDb - 21.0);
            }

            return 0.0;
        }

        private static double ModifiedBessel0(double x)
        {
            var sum = 1.0;
            var term = 1.0;
            var halfSquared = x * x / 4.0;
            for (var k = 1; k <= 50; k++)
            {
                term *= halfSquared / (k * k);
                sum += term;
                if (term < sum * 1E-12)
                {
                    break;
                }
            }

            return sum;
        }

        private static void Normalize(double[] taps, int sampleRate, double frequency)
        {
            var gain = CalculateMagnitude(taps, sampleRate, frequency);
            if (gain <= 0)
            {
                throw new InvalidOperationException("Filter gain cannot be normalized.");
            }

            for (var i = 0; i < taps.Length; i++)
            {
                taps[i] /= gain;
            }
        }

        private static double CalculateMagnitude(double[] taps, int sampleRate, double frequency)
        {
            var real = 0.0;
            var imaginary = 0.0;
            for (var n = 0; n < taps.Length; n++)
            {
                var angle = -2.0 * Math.PI * frequency * n / sampleRate;
                real += taps[n] * Math.Cos(angle);
                imaginary += taps[n] * Math.Sin(angle);
            }

            return Math.Sqrt(real * real + imaginary * imaginary);
        }

        private static void ValidateSampleRate(int sampleRate)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleRate),
                    "Sample rate must be greater than zero."
                );
            }
        }

        private static void ValidateAttenuation(double attenuationDb)
        {
            if (!double.IsFinite(attenuationDb) || attenuationDb <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attenuationDb),
                    "Attenuation must be greater than zero."
                );
            }
        }

        private static void ValidateFrequencyRange(
            string paramName,
            double frequency,
            int sampleRate,
            bool includeZero
        )
        {
            var min = includeZero ? 0.0 : double.Epsilon;
            if (!double.IsFinite(frequency) || frequency < min || frequency > sampleRate / 2.0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    "Frequency must be within the range from zero to the Nyquist frequency."
                );
            }
        }
    }
}
