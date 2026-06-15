using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using Xunit;

namespace Asv.Sdr.Test
{
    public class GoertzelSubjectTests
    {
        [Fact]
        public void GoertzelMeasuresExactComplexFrequency()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 16_384;
            const double frequencyHz = 30.0;
            const double amplitude = 3.0;
            const double phase = 0.7;

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            var received = default(ReaderIqGoertzelResult);
            using var subscription = source
                .Goertzel(sampleRate, frequencyHz)
                .Subscribe(x => received = x);

            source.Publish(
                CreateComplexTone(sampleCount, sampleRate, frequencyHz, amplitude, phase)
            );

            Assert.True(Math.Abs(amplitude * sampleCount - received.Magnitude) < 1e-6);
            Assert.True(Math.Abs(DspMathEx.GetDistanceAngleRad(received.Phase, phase)) < 1e-10);
        }

        [Fact]
        public void GoertzelPhaseMeasuresTwoFrequenciesFromOneBuffer()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 9_600;
            const double frequency1Hz = 90.0;
            const double frequency2Hz = 150.0;
            const double phase1 = 0.7;
            const double phase2 = -1.2;

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            var received = (0.0, 0.0);
            using var subscription = source
                .GetGoertzelPhase(sampleRate, frequency1Hz, frequency2Hz)
                .Subscribe(x => received = x);

            source.Publish(
                CreateComplexTonePair(
                    sampleCount,
                    sampleRate,
                    frequency1Hz,
                    2.0,
                    phase1,
                    frequency2Hz,
                    3.0,
                    phase2
                )
            );

            Assert.True(Math.Abs(DspMathEx.GetDistanceAngleRad(received.Item1, phase1)) < 1e-10);
            Assert.True(Math.Abs(DspMathEx.GetDistanceAngleRad(received.Item2, phase2)) < 1e-10);
        }

        [Fact]
        public void GoertzelAmRemovesDcBeforeMeasuringTargetFrequency()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 16_384;
            const double frequencyHz = sampleRate * 16.0 / sampleCount;
            const double modulationDepth = 0.35;

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            var received = 0.0;
            using var subscription = source
                .GetGoertzelAm(sampleRate, frequencyHz)
                .Subscribe(x => received = x);

            source.Publish(CreateRealAm(sampleCount, sampleRate, frequencyHz, modulationDepth));

            Assert.True(Math.Abs(modulationDepth - received) < 1e-10);
        }

        [Fact]
        public void GoertzelAmRemovesDcLeakageAtNonBinFrequency()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 10_000;
            const double frequencyHz = 1_234.5;

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            var received = 1.0;
            using var subscription = source
                .GetGoertzelAm(sampleRate, frequencyHz)
                .Subscribe(x => received = x);

            source.Publish(CreateConstantIq(sampleCount, 1.25, -0.75));

            Assert.True(received < 1e-10);
        }

        [Fact]
        public void GoertzelAmMeasuresThreeFrequenciesFromOneBuffer()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 9_600;
            const double frequency1Hz = 90.0;
            const double frequency2Hz = 150.0;
            const double frequency3Hz = 1_020.0;
            const double modulationDepth1 = 0.30;
            const double modulationDepth2 = 0.20;
            const double modulationDepth3 = 0.10;

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            var received = (0.0, 0.0, 0.0);
            using var subscription = source
                .GetGoertzelAm(sampleRate, frequency1Hz, frequency2Hz, frequency3Hz)
                .Subscribe(x => received = x);

            source.Publish(
                CreateRealAm(
                    sampleCount,
                    sampleRate,
                    frequency1Hz,
                    modulationDepth1,
                    frequency2Hz,
                    modulationDepth2,
                    frequency3Hz,
                    modulationDepth3
                )
            );

            Assert.True(Math.Abs(modulationDepth1 - received.Item1) < 1e-10);
            Assert.True(Math.Abs(modulationDepth2 - received.Item2) < 1e-10);
            Assert.True(Math.Abs(modulationDepth3 - received.Item3) < 1e-10);
        }

        [Fact]
        public void GoertzelAmStableCarrierNormalizesAgainstLongCarrierWindow()
        {
            const int sampleRate = 96_000;
            const int shortSampleCount = 1_788;
            const int carrierSampleCount = 9_600;
            const double codeIdFrequencyHz = 1_020.0;
            const double modulationDepth = 0.10;
            const double am30Depth = 0.30;
            var received = new List<double>();

            using var source = new TestReaderIqSubject<double>(shortSampleCount * 2);
            using var subscription = source
                .GetGoertzelAmStableCarrier(sampleRate, codeIdFrequencyHz, carrierSampleCount)
                .Subscribe(received.Add);

            for (var window = 0; window < 24; window++)
            {
                source.Publish(
                    CreateRealAm(
                        shortSampleCount,
                        sampleRate,
                        30.0,
                        am30Depth,
                        codeIdFrequencyHz,
                        modulationDepth,
                        window * shortSampleCount
                    )
                );
            }

            Assert.True(received.Count > 0);
            foreach (var value in received)
            {
                Assert.InRange(value, 0.095, 0.105);
            }
        }

        [Fact]
        public void GoertzelFrequencyOffsetTracksToneOffset()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 9_600;
            const double nominalFrequencyHz = 30.0;
            const double offsetHz = 1.25;
            const double amplitude = 3.0;
            var received = new List<double>();

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            using var subscription = source
                .GetGoertzelFrequencyOffset(sampleRate, nominalFrequencyHz)
                .Subscribe(received.Add);

            source.Publish(
                CreateComplexTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    0
                )
            );
            source.Publish(
                CreateComplexTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    sampleCount
                )
            );
            source.Publish(
                CreateComplexTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    sampleCount * 2
                )
            );

            Assert.Equal(2, received.Count);
            Assert.All(received, x => Assert.True(Math.Abs(offsetHz - x) < 1e-9));
        }

        [Fact]
        public void GoertzelFrequencyOffsetIsIndependentFromWindowSize()
        {
            const int sampleRate = 96_000;
            const double offsetHz = -1.75;
            const double amplitude = 2.0;

            var shortWindowOffset = MeasureFrequencyOffset(
                sampleRate,
                9_600,
                0,
                offsetHz,
                amplitude
            );
            var longWindowOffset = MeasureFrequencyOffset(
                sampleRate,
                16_000,
                0,
                offsetHz,
                amplitude
            );

            Assert.True(Math.Abs(offsetHz - shortWindowOffset) < 1e-9);
            Assert.True(Math.Abs(offsetHz - longWindowOffset) < 1e-9);
        }

        [Fact]
        public void GoertzelFrequencyOffsetTracksRealToneOffset()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 9_600;
            const double nominalFrequencyHz = 30.0;
            const double offsetHz = 0.25;
            const double amplitude = 0.3;
            var received = new List<double>();

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            using var subscription = source
                .GetGoertzelFrequencyOffset(sampleRate, nominalFrequencyHz)
                .Subscribe(received.Add);

            source.Publish(
                CreateRealTone(sampleCount, sampleRate, nominalFrequencyHz + offsetHz, amplitude, 0)
            );
            source.Publish(
                CreateRealTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    sampleCount
                )
            );
            source.Publish(
                CreateRealTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    sampleCount * 2
                )
            );

            Assert.Equal(2, received.Count);
            Assert.All(received, x => Assert.True(Math.Abs(offsetHz - x) < 0.02));
        }

        [Fact]
        public void GoertzelFrequencyOffsetTracksFloatToneOffset()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 9_600;
            const double nominalFrequencyHz = 0.0;
            const double offsetHz = -1.25;
            const double amplitude = 1.5;
            var received = new List<double>();

            using var source = new TestReaderIqSubject<float>(sampleCount * 2);
            using var subscription = source
                .GetGoertzelFrequencyOffset(sampleRate, nominalFrequencyHz)
                .Subscribe(received.Add);

            source.Publish(
                CreateComplexToneFloat(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    0
                )
            );
            source.Publish(
                CreateComplexToneFloat(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    sampleCount
                )
            );
            source.Publish(
                CreateComplexToneFloat(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    sampleCount * 2
                )
            );

            Assert.Equal(2, received.Count);
            Assert.All(received, x => Assert.True(Math.Abs(offsetHz - x) < 1e-6));
        }

        [Fact]
        public void GoertzelFrequencyOffsetCoarseSearchTracksLargeToneOffset()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 9_600;
            const double nominalFrequencyHz = 0.0;
            const double offsetHz = 1_001.25;
            const double searchRangeHz = 1_500.0;
            const double amplitude = 3.0;
            var received = new List<double>();

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            using var subscription = source
                .GetGoertzelFrequencyOffset(sampleRate, nominalFrequencyHz, searchRangeHz)
                .Subscribe(received.Add);

            source.Publish(
                CreateComplexTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    0
                )
            );
            source.Publish(
                CreateComplexTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    sampleCount
                )
            );
            source.Publish(
                CreateComplexTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    sampleCount * 2
                )
            );

            Assert.Equal(2, received.Count);
            Assert.All(received, x => Assert.True(Math.Abs(offsetHz - x) < 1e-9));
        }

        [Fact]
        public void GoertzelFrequencyOffsetCoarseSearchTracksOffsetAroundNonZeroNominal()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 9_600;
            const double nominalFrequencyHz = 1_000.0;
            const double offsetHz = 1_000.0;
            const double searchRangeHz = 2_000.0;
            const double amplitude = 3.0;
            var received = new List<double>();

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            using var subscription = source
                .GetGoertzelFrequencyOffset(sampleRate, nominalFrequencyHz, searchRangeHz)
                .Subscribe(received.Add);

            source.Publish(
                CreateComplexTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    0
                )
            );
            source.Publish(
                CreateComplexTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    sampleCount
                )
            );
            source.Publish(
                CreateComplexTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    sampleCount * 2
                )
            );

            Assert.Equal(2, received.Count);
            Assert.All(received, x => Assert.True(Math.Abs(offsetHz - x) < 1e-9));
        }

        [Fact]
        public void VorReferenceMeasuresFmReferencePhase()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 9_600;
            const double subcarrierHz = 9_960.0;
            const double referenceHz = 30.0;
            const double sidebandBandwidthHz = 1_050.0;
            const double referencePhase = 0.8;

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            var received = default(ReaderIqVorReferenceResult);
            using var subscription = source
                .GetVorReference(sampleRate, subcarrierHz, referenceHz, sidebandBandwidthHz)
                .Subscribe(x => received = x);

            source.Publish(
                CreateVorEnvelope(
                    sampleCount,
                    sampleRate,
                    subcarrierHz,
                    referenceHz,
                    referencePhase,
                    0,
                    0
                )
            );

            Assert.Equal(sampleCount - 1, received.SampleCount);
            Assert.True(received.Magnitude > 0);
            Assert.True(
                Math.Abs(DspMathEx.GetDistanceAngleRad(received.Phase, referencePhase)) < 1e-3
            );
        }

        [Fact]
        public void VorReferenceAndVariablePhaseMeasureAzimuth()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 9_600;
            const double subcarrierHz = 9_960.0;
            const double referenceHz = 30.0;
            const double sidebandBandwidthHz = 1_050.0;
            const double referencePhase = 0.8;
            const double azimuthRad = Math.PI * 1.5;
            var variablePhase = referencePhase - azimuthRad;

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            var reference = default(ReaderIqVorReferenceResult);
            var variable = 0.0;
            using var referenceSubscription = source
                .GetVorReference(sampleRate, subcarrierHz, referenceHz, sidebandBandwidthHz)
                .Subscribe(x => reference = x);
            using var variableSubscription = source
                .GetGoertzelPhase(sampleRate, referenceHz)
                .Subscribe(x => variable = x);

            source.Publish(
                CreateVorEnvelope(
                    sampleCount,
                    sampleRate,
                    subcarrierHz,
                    referenceHz,
                    referencePhase,
                    0.3,
                    variablePhase
                )
            );

            var measuredAzimuth = NormalizeAngleRad(
                DspMathEx.GetDistanceAngleRad(reference.Phase, variable)
            );
            Assert.True(
                Math.Abs(DspMathEx.GetDistanceAngleRad(measuredAzimuth, azimuthRad)) < 1e-3
            );
        }

        [Fact]
        public void GoertzelFmSubcarrierAmMeasuresFullFmSubcarrierDepth()
        {
            const int sampleRate = 96_000;
            const int sampleCount = 9_600;
            const double subcarrierHz = 9_960.0;
            const double referenceHz = 30.0;
            const double sidebandBandwidthHz = 1_050.0;

            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            var received = 0.0;
            using var subscription = source
                .GetGoertzelFmSubcarrierAm(
                    sampleRate,
                    subcarrierHz,
                    referenceHz,
                    sidebandBandwidthHz
                )
                .Subscribe(x => received = x);

            source.Publish(
                CreateVorEnvelope(sampleCount, sampleRate, subcarrierHz, referenceHz, 0.8, 0, 0)
            );

            Assert.True(Math.Abs(0.3 - received) < 1e-4);
        }

        private static double[] CreateComplexTone(
            int sampleCount,
            int sampleRate,
            double frequencyHz,
            double amplitude,
            double phase
        )
        {
            return CreateComplexTone(sampleCount, sampleRate, frequencyHz, amplitude, phase, 0);
        }

        private static double[] CreateComplexTone(
            int sampleCount,
            int sampleRate,
            double frequencyHz,
            double amplitude,
            double phase,
            int sampleOffset
        )
        {
            var buffer = new double[sampleCount * 2];
            var angleStep = 2.0 * Math.PI * frequencyHz / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                var angle = angleStep * (i + sampleOffset) + phase;
                buffer[i * 2] = amplitude * Math.Cos(angle);
                buffer[i * 2 + 1] = amplitude * Math.Sin(angle);
            }

            return buffer;
        }

        private static double[] CreateConstantIq(int sampleCount, double real, double imaginary)
        {
            var buffer = new double[sampleCount * 2];

            for (var i = 0; i < sampleCount; i++)
            {
                buffer[i * 2] = real;
                buffer[i * 2 + 1] = imaginary;
            }

            return buffer;
        }

        private static double[] CreateComplexTonePair(
            int sampleCount,
            int sampleRate,
            double frequency1Hz,
            double amplitude1,
            double phase1,
            double frequency2Hz,
            double amplitude2,
            double phase2
        )
        {
            var buffer = new double[sampleCount * 2];
            var angleStep1 = 2.0 * Math.PI * frequency1Hz / sampleRate;
            var angleStep2 = 2.0 * Math.PI * frequency2Hz / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                var angle1 = angleStep1 * i + phase1;
                var angle2 = angleStep2 * i + phase2;
                buffer[i * 2] = amplitude1 * Math.Cos(angle1) + amplitude2 * Math.Cos(angle2);
                buffer[i * 2 + 1] = amplitude1 * Math.Sin(angle1) + amplitude2 * Math.Sin(angle2);
            }

            return buffer;
        }

        private static float[] CreateComplexToneFloat(
            int sampleCount,
            int sampleRate,
            double frequencyHz,
            double amplitude,
            double phase,
            int sampleOffset
        )
        {
            var buffer = new float[sampleCount * 2];
            var angleStep = 2.0 * Math.PI * frequencyHz / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                var angle = angleStep * (i + sampleOffset) + phase;
                buffer[i * 2] = (float)(amplitude * Math.Cos(angle));
                buffer[i * 2 + 1] = (float)(amplitude * Math.Sin(angle));
            }

            return buffer;
        }

        private static double MeasureFrequencyOffset(
            int sampleRate,
            int sampleCount,
            double nominalFrequencyHz,
            double offsetHz,
            double amplitude
        )
        {
            var received = 0.0;
            using var source = new TestReaderIqSubject<double>(sampleCount * 2);
            using var subscription = source
                .GetGoertzelFrequencyOffset(sampleRate, nominalFrequencyHz)
                .Subscribe(x => received = x);

            source.Publish(
                CreateComplexTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    0
                )
            );
            source.Publish(
                CreateComplexTone(
                    sampleCount,
                    sampleRate,
                    nominalFrequencyHz + offsetHz,
                    amplitude,
                    0,
                    sampleCount
                )
            );
            return received;
        }

        private static double[] CreateVorEnvelope(
            int sampleCount,
            int sampleRate,
            double subcarrierHz,
            double referenceHz,
            double referencePhase,
            double variableDepth,
            double variablePhase
        )
        {
            const double subcarrierDepth = 0.3;
            const double fmIndex = 16.0;
            var buffer = new double[sampleCount * 2];
            var subcarrierAngleStep = 2.0 * Math.PI * subcarrierHz / sampleRate;
            var referenceAngleStep = 2.0 * Math.PI * referenceHz / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                var referenceAngle = referenceAngleStep * i + referencePhase;
                var variableAngle = referenceAngleStep * i + variablePhase;
                buffer[i * 2] =
                    1.0
                    + variableDepth * Math.Cos(variableAngle)
                    + subcarrierDepth
                        * Math.Cos(subcarrierAngleStep * i + (fmIndex * Math.Sin(referenceAngle)));
            }

            return buffer;
        }

        private static double[] CreateRealAm(
            int sampleCount,
            int sampleRate,
            double frequencyHz,
            double modulationDepth
        )
        {
            var buffer = new double[sampleCount * 2];
            var angleStep = 2.0 * Math.PI * frequencyHz / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                buffer[i * 2] = 1.0 + modulationDepth * Math.Cos(angleStep * i);
            }

            return buffer;
        }

        private static double[] CreateRealAm(
            int sampleCount,
            int sampleRate,
            double frequency1Hz,
            double modulationDepth1,
            double frequency2Hz,
            double modulationDepth2,
            double frequency3Hz,
            double modulationDepth3
        )
        {
            var buffer = new double[sampleCount * 2];
            var angleStep1 = 2.0 * Math.PI * frequency1Hz / sampleRate;
            var angleStep2 = 2.0 * Math.PI * frequency2Hz / sampleRate;
            var angleStep3 = 2.0 * Math.PI * frequency3Hz / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                buffer[i * 2] =
                    1.0
                    + modulationDepth1 * Math.Cos(angleStep1 * i)
                    + modulationDepth2 * Math.Cos(angleStep2 * i)
                    + modulationDepth3 * Math.Cos(angleStep3 * i);
            }

            return buffer;
        }

        private static double[] CreateRealAm(
            int sampleCount,
            int sampleRate,
            double slowFrequencyHz,
            double slowModulationDepth,
            double fastFrequencyHz,
            double fastModulationDepth,
            int sampleOffset
        )
        {
            var buffer = new double[sampleCount * 2];
            var slowAngleStep = 2.0 * Math.PI * slowFrequencyHz / sampleRate;
            var fastAngleStep = 2.0 * Math.PI * fastFrequencyHz / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                var sampleIndex = i + sampleOffset;
                buffer[i * 2] =
                    1.0
                    + slowModulationDepth * Math.Cos(slowAngleStep * sampleIndex)
                    + fastModulationDepth * Math.Cos(fastAngleStep * sampleIndex);
            }

            return buffer;
        }

        private static double[] CreateRealTone(
            int sampleCount,
            int sampleRate,
            double frequencyHz,
            double amplitude,
            int sampleOffset
        )
        {
            var buffer = new double[sampleCount * 2];
            var angleStep = 2.0 * Math.PI * frequencyHz / sampleRate;

            for (var i = 0; i < sampleCount; i++)
            {
                buffer[i * 2] = amplitude * Math.Cos(angleStep * (i + sampleOffset));
            }

            return buffer;
        }

        private static double NormalizeAngleRad(double value)
        {
            var result = value % (2.0 * Math.PI);
            return result < 0 ? result + (2.0 * Math.PI) : result;
        }

        private sealed class TestReaderIqSubject<T> : IReaderIqSubject<T>
        {
            private readonly Subject<Memory<T>> _subject = new();

            public TestReaderIqSubject(int outputBufferSize)
            {
                OutputBufferSize = outputBufferSize;
            }

            public int OutputBufferSize { get; }

            public IDisposable Subscribe(IObserver<Memory<T>> observer)
            {
                return _subject.Subscribe(observer);
            }

            public void Publish(T[] buffer)
            {
                _subject.OnNext(buffer);
            }

            public void Dispose()
            {
                _subject.OnCompleted();
                _subject.Dispose();
            }
        }
    }
}
