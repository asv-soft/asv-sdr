using System;
using Xunit;

namespace Asv.Sdr.Test
{
    public class BdsInferenceTests
    {
        [Fact]
        public void Deserialize_Bds50ValuesOutsideCivilLimits_AcceptsFullRegisterRange()
        {
            var result = RoundTrip(new Bds50
            {
                RollAngle = 89.82421875,
                TrueTrackAngle = 179.82421875,
                GroundSpeed = 2046.0,
                TrackAngleRate = 15.96875,
                TrueAirspeed = 2046.0,
            });

            Assert.Equal(89.82421875, result.RollAngle);
            Assert.Equal(179.82421875, result.TrueTrackAngle);
            Assert.Equal(2046.0, result.GroundSpeed);
            Assert.Equal(15.96875, result.TrackAngleRate);
            Assert.Equal(2046.0, result.TrueAirspeed);
            Assert.False(BdsInference.Is50(result, BdsInferenceLimits.CivilSubsonic));
            Assert.True(BdsInference.Is50(result, BdsInferenceLimits.Unrestricted));
        }

        [Fact]
        public void Deserialize_Bds60ValuesOutsideCivilLimits_AcceptsFullRegisterRange()
        {
            var result = RoundTrip(new Bds60
            {
                MagneticHeading = 179.82421875,
                IndicatedAirspeed = 1023.0,
                Mach = 4.092,
                BarometricAltitudeRate = 16352.0,
                InertialVerticalVelocity = -16384.0,
            });

            Assert.Equal(179.82421875, result.MagneticHeading);
            Assert.Equal(1023.0, result.IndicatedAirspeed);
            Assert.Equal(4.092, result.Mach, 3);
            Assert.Equal(16352.0, result.BarometricAltitudeRate);
            Assert.Equal(-16384.0, result.InertialVerticalVelocity);
            Assert.False(BdsInference.Is60(result, BdsInferenceLimits.CivilSubsonic));
            Assert.True(BdsInference.Is60(result, BdsInferenceLimits.Unrestricted));
        }

        [Theory]
        [InlineData(49.921875, true)]
        [InlineData(-49.921875, true)]
        [InlineData(50.09765625, false)]
        [InlineData(-50.09765625, false)]
        public void Is50_RollAngleAtCivilBoundary_ReturnsExpectedResult(double rollAngle, bool expected)
        {
            var candidate = RoundTrip(new Bds50 { RollAngle = rollAngle });

            var result = BdsInference.Is50(candidate, BdsInferenceLimits.CivilSubsonic);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(600.0, true)]
        [InlineData(602.0, false)]
        public void Is50_GroundSpeedAtCivilBoundary_ReturnsExpectedResult(double groundSpeed, bool expected)
        {
            var candidate = RoundTrip(new Bds50
            {
                GroundSpeed = groundSpeed,
                TrueAirspeed = 500.0,
            });

            var result = BdsInference.Is50(candidate, BdsInferenceLimits.CivilSubsonic);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(500.0, true)]
        [InlineData(502.0, false)]
        public void Is50_TrueAirspeedAtCivilBoundary_ReturnsExpectedResult(double trueAirspeed, bool expected)
        {
            var candidate = RoundTrip(new Bds50
            {
                GroundSpeed = 500.0,
                TrueAirspeed = trueAirspeed,
            });

            var result = BdsInference.Is50(candidate, BdsInferenceLimits.CivilSubsonic);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(400.0, 200.0, true)]
        [InlineData(402.0, 200.0, false)]
        public void Is50_SpeedDifferenceAtCivilBoundary_ReturnsExpectedResult(
            double groundSpeed,
            double trueAirspeed,
            bool expected
        )
        {
            var candidate = RoundTrip(new Bds50
            {
                GroundSpeed = groundSpeed,
                TrueAirspeed = trueAirspeed,
            });

            var result = BdsInference.Is50(candidate, BdsInferenceLimits.CivilSubsonic);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(500.0, true)]
        [InlineData(501.0, false)]
        public void Is60_IndicatedAirspeedAtCivilBoundary_ReturnsExpectedResult(
            double indicatedAirspeed,
            bool expected
        )
        {
            var candidate = RoundTrip(new Bds60 { IndicatedAirspeed = indicatedAirspeed });

            var result = BdsInference.Is60(candidate, BdsInferenceLimits.CivilSubsonic);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(1.0, true)]
        [InlineData(1.004, false)]
        public void Is60_MachAtCivilBoundary_ReturnsExpectedResult(double mach, bool expected)
        {
            var candidate = RoundTrip(new Bds60 { Mach = mach });

            var result = BdsInference.Is60(candidate, BdsInferenceLimits.CivilSubsonic);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(6016.0, true)]
        [InlineData(-6016.0, true)]
        [InlineData(6048.0, false)]
        [InlineData(-6048.0, false)]
        public void Is60_VerticalRateAtCivilBoundary_ReturnsExpectedResult(double verticalRate, bool expected)
        {
            var candidate = RoundTrip(new Bds60
            {
                BarometricAltitudeRate = verticalRate,
                InertialVerticalVelocity = verticalRate,
            });

            var result = BdsInference.Is60(candidate, BdsInferenceLimits.CivilSubsonic);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetBds_ImplausibleCivilValue_UsesSelectedInferenceProfile()
        {
            var bytes = Serialize(new Bds50 { RollAngle = 50.09765625 });
            ReadOnlySpan<byte> civilBuffer = bytes;
            ReadOnlySpan<byte> unrestrictedBuffer = bytes;

            var civilCandidates = BdsFactory.GetBds(ref civilBuffer, BdsInferenceLimits.CivilSubsonic);
            var unrestrictedCandidates = BdsFactory.GetBds(ref unrestrictedBuffer, BdsInferenceLimits.Unrestricted);

            Assert.DoesNotContain(civilCandidates, candidate => candidate is Bds50);
            Assert.Contains(unrestrictedCandidates, candidate => candidate is Bds50);
        }

        [Fact]
        public void DeserializeBds_KnownBds50OutsideCivilLimits_BypassesInference()
        {
            var bytes = Serialize(new Bds50
            {
                RollAngle = 89.82421875,
                GroundSpeed = 2046.0,
                TrueAirspeed = 2046.0,
            });
            var sut = new ModeSDF21Probe
            {
                Bds = new Bds50(),
                InferenceLimits = BdsInferenceLimits.CivilSubsonic,
            };

            sut.DeserializeBdsPayload(bytes);

            var result = Assert.IsType<Bds50>(sut.Bds);
            Assert.Equal(89.82421875, result.RollAngle);
            Assert.Equal(2046.0, result.GroundSpeed);
            Assert.Equal(2046.0, result.TrueAirspeed);
        }

        [Fact]
        public void Deserialize_Bds50StatusValueMismatch_Throws()
        {
            var bytes = new byte[7];
            var buffer = bytes.AsSpan();
            var pos = 0;
            ModeSHelper.SetBitU(buffer, ref pos, 1, 0);
            ModeSHelper.SetBitS(buffer, ref pos, 10, 1);

            Assert.Throws<Exception>(() => Deserialize<Bds50>(bytes));
        }

        [Fact]
        public void Deserialize_Bds60StatusValueMismatch_Throws()
        {
            var bytes = new byte[7];
            var buffer = bytes.AsSpan();
            var pos = 0;
            ModeSHelper.SetBitU(buffer, ref pos, 1, 0);
            ModeSHelper.SetBitS(buffer, ref pos, 11, 1);

            Assert.Throws<Exception>(() => Deserialize<Bds60>(bytes));
        }

        [Fact]
        public void Is60_CustomProfile_ReplacesCivilLimit()
        {
            var candidate = RoundTrip(new Bds60 { Mach = 1.2 });
            var customLimits = BdsInferenceLimits.CivilSubsonic with { MaximumMach = 1.2 };

            Assert.False(BdsInference.Is60(candidate, BdsInferenceLimits.CivilSubsonic));
            Assert.True(BdsInference.Is60(candidate, customLimits));
        }

        private static T RoundTrip<T>(T source) where T : BdsBase, new()
        {
            return Deserialize<T>(Serialize(source));
        }

        private static byte[] Serialize(BdsBase source)
        {
            var bytes = new byte[7];
            var buffer = bytes.AsSpan();
            source.Serialize(ref buffer);
            return bytes;
        }

        private static T Deserialize<T>(byte[] bytes) where T : BdsBase, new()
        {
            var result = new T();
            ReadOnlySpan<byte> buffer = bytes;
            result.Deserialize(ref buffer);
            return result;
        }

        private sealed class ModeSDF21Probe : ModeSDF21
        {
            public void DeserializeBdsPayload(byte[] bytes)
            {
                var pos = 0;
                DeserializeBds(bytes, ref pos);
            }
        }
    }
}
