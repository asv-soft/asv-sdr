using System;
using Xunit;

namespace Asv.Sdr.Test
{
    public class BdsStatusTests
    {
        [Fact]
        public void Deserialize_Bds40StatusBits_PreservesFieldAvailability()
        {
            var available = DeserializeSerialized<Bds40>();
            var unavailable = DeserializeZeros<Bds40>();

            Assert.True(available.HasMcpFcuSelectedAltitude);
            Assert.True(available.HasFmsSelectedAltitude);
            Assert.True(available.HasBarometricPressureSetting);
            Assert.True(available.HasVnavMode);
            Assert.True(available.HasAltHoldMode);
            Assert.True(available.HasApproachMode);
            Assert.True(available.HasTargetAltitudeSource);
            Assert.False(unavailable.HasMcpFcuSelectedAltitude);
            Assert.False(unavailable.HasFmsSelectedAltitude);
            Assert.False(unavailable.HasBarometricPressureSetting);
            Assert.False(unavailable.HasVnavMode);
            Assert.False(unavailable.HasAltHoldMode);
            Assert.False(unavailable.HasApproachMode);
            Assert.False(unavailable.HasTargetAltitudeSource);
        }

        [Fact]
        public void Deserialize_Bds50StatusBits_PreservesFieldAvailability()
        {
            var available = DeserializeSerialized<Bds50>();
            var unavailable = DeserializeZeros<Bds50>();

            Assert.True(available.HasRollAngle);
            Assert.True(available.HasTrueTrackAngle);
            Assert.True(available.HasGroundSpeed);
            Assert.True(available.HasTrackAngleRate);
            Assert.True(available.HasTrueAirspeed);
            Assert.False(unavailable.HasRollAngle);
            Assert.False(unavailable.HasTrueTrackAngle);
            Assert.False(unavailable.HasGroundSpeed);
            Assert.False(unavailable.HasTrackAngleRate);
            Assert.False(unavailable.HasTrueAirspeed);
        }

        [Fact]
        public void Deserialize_Bds60StatusBits_PreservesFieldAvailability()
        {
            var available = DeserializeSerialized<Bds60>();
            var unavailable = DeserializeZeros<Bds60>();

            Assert.True(available.HasMagneticHeading);
            Assert.True(available.HasIndicatedAirspeed);
            Assert.True(available.HasMach);
            Assert.True(available.HasBarometricAltitudeRate);
            Assert.True(available.HasInertialVerticalVelocity);
            Assert.False(unavailable.HasMagneticHeading);
            Assert.False(unavailable.HasIndicatedAirspeed);
            Assert.False(unavailable.HasMach);
            Assert.False(unavailable.HasBarometricAltitudeRate);
            Assert.False(unavailable.HasInertialVerticalVelocity);
        }

        private static T DeserializeSerialized<T>() where T : BdsBase, new()
        {
            var source = new T();
            var bytes = new byte[7];
            var writeBuffer = bytes.AsSpan();
            source.Serialize(ref writeBuffer);
            return Deserialize<T>(bytes);
        }

        private static T DeserializeZeros<T>() where T : BdsBase, new()
        {
            return Deserialize<T>(new byte[7]);
        }

        private static T Deserialize<T>(byte[] bytes) where T : BdsBase, new()
        {
            var result = new T();
            ReadOnlySpan<byte> readBuffer = bytes;
            result.Deserialize(ref readBuffer);
            return result;
        }
    }
}
