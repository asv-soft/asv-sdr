using System;
using Xunit;

namespace Asv.Sdr.Test
{
    public class AdsbExtendedSquitterDeserializationTests
    {
        [Theory]
        [InlineData("8D40621D58C382D690C8AC2863A7", 17, 5)]
        [InlineData("9040621D58C382D690C8AC556F52", 18, 0)]
        public void Factory_CompleteValidFrame_DeserializesHeaderAndPayload(
            string hex,
            byte expectedDownlinkFormat,
            byte expectedCapabilityOrControlField)
        {
            var result = AdsbExtendedSquitterFactory.Deserialize(Convert.FromHexString(hex));

            Assert.IsType<AdsbAirbornePositionWithBaroAlt>(result);
            Assert.Equal(expectedDownlinkFormat, result.DownlinkFormat);
            Assert.Equal(expectedCapabilityOrControlField, result.CapabilityOrControlField);
            Assert.Equal(0x40621D, result.AircraftAddress);
        }

        [Fact]
        public void Factory_CompleteFrameWithInvalidCrc_ThrowsFormatException()
        {
            var frame = Convert.FromHexString("8D40621D58C382D690C8AC2863A7");
            frame[10] ^= 0x01;

            Assert.Throws<FormatException>(() => AdsbExtendedSquitterFactory.Deserialize(frame));
        }

        [Fact]
        public void Factory_CompleteFrameWithInvalidLength_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(
                () => AdsbExtendedSquitterFactory.Deserialize(new byte[13])
            );
        }

        [Theory]
        [InlineData(2, 0, typeof(AdsbAircraftIdentification))]
        [InlineData(5, 0, typeof(AdsbSurfacePosition))]
        [InlineData(9, 0, typeof(AdsbAirbornePositionWithBaroAlt))]
        [InlineData(19, 1, typeof(AdsbGroundSpeed))]
        [InlineData(19, 4, typeof(AdsbAirspeed))]
        [InlineData(20, 0, typeof(AdsbAirbornePositionWithGnssAlt))]
        [InlineData(23, 7, typeof(AdsbEventDriven))]
        [InlineData(28, 1, typeof(AdsbAircraftEmergencyStatus))]
        [InlineData(28, 2, typeof(AdsbAircraftAcasRaBroadcast))]
        [InlineData(29, 0, typeof(AdsbTargetStateAndStatusInformation))]
        [InlineData(29, 1, typeof(AdsbTargetStateAndStatusInformation))]
        [InlineData(31, 0, typeof(AdsbAircraftOperationStatusAirborne))]
        [InlineData(31, 1, typeof(AdsbAircraftOperationStatusSurface))]
        public void Factory_SupportedTypeCodeAndSubtype_ReturnsExpectedType(
            byte typeCode,
            byte subType,
            Type expectedType)
        {
            var me = new byte[7];
            SetBits(me, 0, 5, typeCode);
            SetBits(me, 5, typeCode == 29 ? 2 : 3, subType);

            var result = AdsbExtendedSquitterFactory.Deserialize(17, 5, 0x150777, me);

            Assert.Equal(expectedType, result.GetType());
            Assert.Equal(typeCode, result.TypeCode);
            Assert.Equal(5, result.CapabilityOrControlField);
            Assert.Equal(0x150777, result.AircraftAddress);
        }

        [Fact]
        public void Factory_Df18ControlFieldZero_DeserializesNonTransponderSquitter()
        {
            var me = new byte[7];
            SetBits(me, 0, 5, 2);

            var result = AdsbExtendedSquitterFactory.Deserialize(18, 0, 0xABCDEF, me);

            Assert.IsType<AdsbAircraftIdentification>(result);
            Assert.Equal(SquitterTypeEnum.NonTransponder, result.SquitterType);
            Assert.Equal(0, result.CapabilityOrControlField);
        }

        [Fact]
        public void Factory_Df18NonZeroControlField_RejectsUnsupportedAddressSemantics()
        {
            var me = new byte[7];
            SetBits(me, 0, 5, 2);

            Assert.Throws<NotSupportedException>(
                () => AdsbExtendedSquitterFactory.Deserialize(18, 1, 0xABCDEF, me));
        }

        [Fact]
        public void Factory_TypeCodeZero_DoesNotMisclassifyAsSurfacePosition()
        {
            Assert.Throws<NotSupportedException>(
                () => AdsbExtendedSquitterFactory.Deserialize(17, 5, 0x150777, new byte[7]));
        }

        [Fact]
        public void AirborneAndSurfacePosition_RawVersionDependentBits_ArePreserved()
        {
            var airborneMe = new byte[7];
            SetBits(airborneMe, 0, 5, 11);
            SetBits(airborneMe, 7, 1, 1);
            SetBits(airborneMe, 20, 1, 1);
            SetBits(airborneMe, 21, 1, 1);
            SetBits(airborneMe, 22, 17, 0x12345);
            SetBits(airborneMe, 39, 17, 0x0ABCD);

            var airborne = Assert.IsType<AdsbAirbornePositionWithBaroAlt>(
                AdsbExtendedSquitterFactory.Deserialize(17, 5, 0x150777, airborneMe));
            Assert.True(airborne.SingleAntennaOrNicSupplementB);
            Assert.True(airborne.IsSingleAntenna);
            Assert.True(airborne.NicSupplementB);
            Assert.True(airborne.TimeSynchronizedWithUtc);
            Assert.Equal(CprFormatEnum.Odd, airborne.CprFormat);
            AssertRoundTripMe(airborneMe, airborne);

            var surfaceMe = new byte[7];
            SetBits(surfaceMe, 0, 5, 5);
            SetBits(surfaceMe, 5, 7, 127);
            SetBits(surfaceMe, 12, 1, 1);
            SetBits(surfaceMe, 13, 7, 127);
            SetBits(surfaceMe, 20, 1, 1);

            var surface = Assert.IsType<AdsbSurfacePosition>(
                AdsbExtendedSquitterFactory.Deserialize(17, 5, 0x150777, surfaceMe));
            Assert.Equal(127, surface.MovementCode);
            Assert.True(surface.IsMovementReserved);
            Assert.False(surface.IsMovementAvailable);
            Assert.True(surface.TimeSynchronizedWithUtc);
            Assert.Equal(357.1875, surface.GroundTrack, 6);
            AssertRoundTripMe(surfaceMe, surface);
        }

        [Fact]
        public void Identification_ExposesRawCategoryCode()
        {
            var me = new byte[7];
            SetBits(me, 0, 5, 3);
            SetBits(me, 5, 3, 5);
            var encodedIdentification = TransponderHelper.AircraftIdEncoding("TEST123");
            Array.Copy(encodedIdentification, 0, me, 1, encodedIdentification.Length);

            var result = Assert.IsType<AdsbAircraftIdentification>(
                AdsbExtendedSquitterFactory.Deserialize(17, 5, 0x150777, me));

            Assert.Equal(3, result.TypeCode);
            Assert.Equal(5, result.CategoryCode);
            AssertRoundTripMe(me, result);
        }

        [Fact]
        public void Airspeed_UsesCompleteTenBitFieldAndPreservesReservedBits()
        {
            var me = new byte[7];
            SetBits(me, 0, 5, 19);
            SetBits(me, 5, 3, 4);
            SetBits(me, 8, 1, 1);
            SetBits(me, 9, 1, 1);
            SetBits(me, 10, 3, 5);
            SetBits(me, 13, 1, 1);
            SetBits(me, 14, 10, 512);
            SetBits(me, 24, 1, 1);
            SetBits(me, 25, 10, 513);
            SetBits(me, 37, 9, 2);
            SetBits(me, 46, 2, 3);

            var result = Assert.IsType<AdsbAirspeed>(
                AdsbExtendedSquitterFactory.Deserialize(17, 5, 0x150777, me));

            Assert.True(result.IntentChangeFlag);
            Assert.True(result.IFRCapabilityFlag);
            Assert.Equal(5, result.NavigationAccuracyCategoryVelocityCode);
            Assert.Equal(3, result.ReservedBits);
            Assert.True(result.MagneticHeadingAvailable);
            Assert.Equal(512, result.MagneticHeadingRaw);
            Assert.Equal(180.0, result.MagneticHeading, 6);
            Assert.Equal(AirspeedTypeEnum.TAS, result.AirspeedType);
            Assert.Equal(513, result.AirspeedRaw);
            Assert.Equal(2048.0 * 0.51444, result.Airspeed, 6);
            AssertRoundTripMe(me, result);
        }

        [Fact]
        public void EventDriven_ExposesSubtypeAndModeAIdentity()
        {
            var me = new byte[7];
            SetBits(me, 0, 5, 23);
            SetBits(me, 5, 3, 7);
            SetBits(me, 8, 13, 0x1234);
            SetBits(me, 21, 3, 5);
            SetBits(me, 24, 32, 0x89ABCDEF);

            var result = Assert.IsType<AdsbEventDriven>(
                AdsbExtendedSquitterFactory.Deserialize(17, 5, 0x150777, me));

            Assert.Equal(7, result.SubType);
            Assert.Equal(0x1234, result.ModeAIdentityRaw);
            Assert.Equal(5, result.ReservedBits21To23);
            Assert.Equal(0x89ABCDEFU, result.ReservedBits24To55);
            AssertRoundTripMe(me, result);
        }

        [Fact]
        public void AcasRaBroadcast_ExposesRawAdvisoryAndThreatFields()
        {
            var me = new byte[7];
            SetBits(me, 0, 5, 28);
            SetBits(me, 5, 3, 2);
            SetBits(me, 8, 14, 0x2AAA);
            SetBits(me, 22, 4, 0xA);
            SetBits(me, 26, 1, 1);
            SetBits(me, 27, 1, 1);
            SetBits(me, 28, 2, 1);
            SetBits(me, 30, 24, 0xABCDEF);
            SetBits(me, 54, 2, 2);

            var result = Assert.IsType<AdsbAircraftAcasRaBroadcast>(
                AdsbExtendedSquitterFactory.Deserialize(17, 5, 0x150777, me));

            Assert.Equal(0x2AAA, result.AcasRa.AdvisoryRaw);
            Assert.Equal(0xA, result.AcasRa.ComplementRaw);
            Assert.True(result.AcasRa.RaTerminated);
            Assert.True(result.AcasRa.MultipleThreat);
            Assert.Equal(1, result.AcasRa.ThreatTypeIndicator);
            Assert.Equal("ABCDEF", result.AcasRa.ThreatIcao);
            AssertRoundTripMe(me, result);
        }

        [Fact]
        public void TargetStateLegacy_DeserializesCompleteSubtypeLayout()
        {
            var me = new byte[7];
            SetBits(me, 0, 5, 29);
            SetBits(me, 5, 2, 0);
            SetBits(me, 7, 2, 2);
            SetBits(me, 9, 1, 1);
            SetBits(me, 10, 1, 1);
            SetBits(me, 11, 2, 3);
            SetBits(me, 13, 2, 2);
            SetBits(me, 15, 10, 118);
            SetBits(me, 25, 2, 1);
            SetBits(me, 27, 9, 270);
            SetBits(me, 36, 1, 1);
            SetBits(me, 37, 2, 3);
            SetBits(me, 39, 4, 9);
            SetBits(me, 43, 1, 1);
            SetBits(me, 44, 2, 2);
            SetBits(me, 46, 5, 0x15);
            SetBits(me, 51, 2, 3);
            SetBits(me, 53, 3, 5);

            var result = Assert.IsType<AdsbTargetStateAndStatusInformation>(
                AdsbExtendedSquitterFactory.Deserialize(17, 5, 0x150777, me));

            Assert.Equal(TargetStateStatusSubTypeEnum.Legacy, result.TargetStateStatusSubType);
            Assert.Equal(2, result.VerticalDataSource);
            Assert.Equal(1, result.TargetAltitudeType);
            Assert.Equal(3, result.TargetAltitudeCapability);
            Assert.Equal(2, result.VerticalModeIndicator);
            Assert.Equal(118, result.TargetAltitudeRaw);
            Assert.Equal(10800, result.TargetAltitudeFt);
            Assert.Equal(1, result.HorizontalDataSource);
            Assert.Equal(270, result.TargetHeadingDeg);
            Assert.Equal(3, result.HorizontalModeIndicator);
            Assert.Equal(9, result.LegacyNacPCode);
            Assert.True(result.LegacyNicBaro);
            Assert.Equal(2, result.LegacySilCode);
            Assert.Equal(3, result.CapabilityModeCode);
            Assert.Equal(5, result.EmergencyPriorityCode);
            AssertRoundTripMe(me, result);
        }

        [Fact]
        public void TargetStateSubtypeOne_DecodesSignedSelectedHeading()
        {
            var me = new byte[7];
            SetBits(me, 0, 5, 29);
            SetBits(me, 5, 2, 1);
            SetBits(me, 7, 1, 1);
            SetBits(me, 8, 1, 1);
            SetBits(me, 9, 11, 101);
            SetBits(me, 20, 9, 251);
            SetBits(me, 29, 1, 1);
            SetBits(me, 30, 1, 1);
            SetBits(me, 31, 8, 64);
            SetBits(me, 39, 4, 10);
            SetBits(me, 43, 1, 1);
            SetBits(me, 44, 2, 3);
            SetBits(me, 46, 1, 1);
            SetBits(me, 47, 1, 1);
            SetBits(me, 48, 1, 1);
            SetBits(me, 49, 1, 1);
            SetBits(me, 51, 1, 1);
            SetBits(me, 52, 1, 1);
            SetBits(me, 53, 1, 1);
            SetBits(me, 54, 2, 2);

            var result = Assert.IsType<AdsbTargetStateAndStatusInformation>(
                AdsbExtendedSquitterFactory.Deserialize(17, 5, 0x150777, me));

            Assert.Equal(TargetStateStatusSubTypeEnum.TargetStateAndModeStatus, result.TargetStateStatusSubType);
            Assert.Equal(3200, result.SelectedAltitudeFt);
            Assert.NotNull(result.BarometricPressureSettingMbar);
            Assert.Equal(1000.0, result.BarometricPressureSettingMbar.Value, 6);
            Assert.True(result.SelectedHeadingStatus);
            Assert.True(result.SelectedHeadingIsNegative);
            Assert.Equal(0x140, result.SelectedHeadingRaw);
            Assert.NotNull(result.SelectedHeadingDeg);
            Assert.Equal(-45.0, result.SelectedHeadingDeg.Value, 6);
            Assert.Equal(10, result.NacPCode);
            Assert.True(result.ModeStatus);
            Assert.True(result.AutopilotEngaged);
            Assert.True(result.LnavMode);
            AssertRoundTripMe(me, result);
        }

        [Fact]
        public void OperationalStatusSurfaceV2_DecodesVersionSpecificFieldsAndGpsOffsets()
        {
            var me = new byte[7];
            SetBits(me, 0, 5, 31);
            SetBits(me, 5, 3, 1);
            SetBits(me, 8, 16, 0x13F6);
            SetBits(me, 24, 16, 0x2DAB);
            SetBits(me, 40, 3, 2);
            SetBits(me, 43, 1, 1);
            SetBits(me, 44, 4, 9);
            SetBits(me, 48, 2, 2);
            SetBits(me, 50, 2, 3);
            SetBits(me, 52, 1, 1);
            SetBits(me, 53, 1, 1);
            SetBits(me, 54, 1, 1);
            SetBits(me, 55, 1, 1);

            var result = Assert.IsType<AdsbAircraftOperationStatusSurface>(
                AdsbExtendedSquitterFactory.Deserialize(17, 5, 0x150777, me));

            Assert.Equal(AdsbVersionNumberEnum.AppendixC, result.AdsbVersionNumber);
            Assert.True(result.NicSupplementA);
            Assert.Equal(9, result.NacPCode);
            Assert.Equal(2, result.ReservedBits48_49);
            Assert.True(result.TrackAngleOrHeading);
            Assert.True(result.ReservedBit54);
            Assert.True(result.ReservedBit55);
            Assert.True(result.SilSupplement);
            Assert.NotNull(result.GpsAntennaOffsetRaw);
            Assert.Equal((byte)0xAB, result.GpsAntennaOffsetRaw.Value);
            Assert.NotNull(result.GpsAntennaOffset);
            Assert.Equal(5, result.GpsAntennaOffset.LateralCode);
            Assert.True(result.GpsAntennaOffset.LateralOffsetIsRight);
            Assert.NotNull(result.GpsAntennaOffset.LateralOffsetMeters);
            Assert.Equal(2.0, result.GpsAntennaOffset.LateralOffsetMeters.Value, 6);
            Assert.Equal(11, result.GpsAntennaOffset.LongitudinalCode);
            Assert.False(result.GpsAntennaOffset.PositionOffsetApplied);
            Assert.NotNull(result.GpsAntennaOffset.LongitudinalOffsetMeters);
            Assert.Equal(20.0, result.GpsAntennaOffset.LongitudinalOffsetMeters.Value, 6);
            AssertRoundTripMe(me, result);
        }

        [Fact]
        public void OperationalStatusAirborneV1_DecodesLegacyCapabilitySemantics()
        {
            var me = new byte[7];
            SetBits(me, 0, 5, 31);
            SetBits(me, 5, 3, 0);
            SetBits(me, 8, 16, 0x3300);
            SetBits(me, 24, 16, 0x3800);
            SetBits(me, 40, 3, 1);
            SetBits(me, 43, 1, 1);
            SetBits(me, 44, 4, 8);
            SetBits(me, 48, 2, 3);
            SetBits(me, 50, 2, 2);
            SetBits(me, 52, 1, 1);
            SetBits(me, 53, 1, 1);
            SetBits(me, 54, 1, 1);

            var result = Assert.IsType<AdsbAircraftOperationStatusAirborne>(
                AdsbExtendedSquitterFactory.Deserialize(17, 5, 0x150777, me));

            Assert.Equal(AdsbVersionNumberEnum.AppendixB, result.AdsbVersionNumber);
            Assert.NotNull(result.AirborneCapability);
            Assert.False(result.AirborneCapability.TcasOperational);
            Assert.True(result.AirborneCapability.AcasNotOperational);
            Assert.True(result.AirborneCapability.CdtiOperational);
            Assert.False(result.AirborneCapability.Is1090EsInApplicable);
            Assert.Null(result.AirborneCapability.HasUatIn);
            Assert.Equal(3, result.BarometricAltitudeQuality);
            Assert.Null(result.GeometricVerticalAccuracy);
            Assert.True(result.BarometricAltitudeIntegrity);
            Assert.Null(result.SilSupplement);
            Assert.True(result.ReservedBit54);
            Assert.True(result.OperationalMode.ReceivingAtcServices);
            Assert.Null(result.OperationalMode.SingleAntenna);
            Assert.Null(result.OperationalMode.Sda);
            AssertRoundTripMe(me, result);
        }

        private static void AssertRoundTripMe(byte[] expectedMe, AdsbExtendedSquitterBase message)
        {
            var frame = new byte[TransponderHelper.LongFrameLengthBytes];
            var buffer = frame.AsSpan();
            message.Serialize(ref buffer);
            Assert.Equal(expectedMe, frame.AsSpan(4, 7).ToArray());
        }

        private static void SetBits(byte[] buffer, int bitOffset, int bitCount, uint value)
        {
            for (var i = 0; i < bitCount; i++)
            {
                var sourceBit = (value >> (bitCount - i - 1)) & 1U;
                var targetBit = bitOffset + i;
                var byteIndex = targetBit / 8;
                var bitInByte = 7 - targetBit % 8;
                var mask = (byte)(1 << bitInByte);
                buffer[byteIndex] = sourceBit == 0
                    ? (byte)(buffer[byteIndex] & ~mask)
                    : (byte)(buffer[byteIndex] | mask);
            }
        }
    }
}
