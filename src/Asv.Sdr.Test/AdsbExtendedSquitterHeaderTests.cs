using System;
using Xunit;

namespace Asv.Sdr.Test
{
    public class AdsbExtendedSquitterHeaderTests
    {
        [Theory]
        [InlineData("8D40621D58C382D690C8AC2863A7", 17, 5)]
        [InlineData("9040621D58C382D690C8AC556F52", 18, 0)]
        public void ReadHeader_ValidExtendedSquitter_ReturnsHeader(
            string hex,
            byte expectedDownlinkFormat,
            byte expectedCapabilityOrControlField)
        {
            var header = AdsbExtendedSquitterFactory.ReadHeader(
                Convert.FromHexString(hex)
            );

            Assert.Equal(expectedDownlinkFormat, header.DownlinkFormat);
            Assert.Equal(
                expectedCapabilityOrControlField,
                header.CapabilityOrControlField
            );
            Assert.Equal(0x40621DU, header.AircraftAddress);
        }

        [Fact]
        public void TryReadHeader_UnsupportedTypedPayload_ReturnsHeader()
        {
            var frame = Convert.FromHexString("8D40621D58C382D690C8AC2863A7");
            frame[4] = 30 << 3;
            UpdateCrc(frame);

            var result = AdsbExtendedSquitterFactory.TryReadHeader(
                frame,
                out var header
            );

            Assert.True(result);
            Assert.Equal(17, header.DownlinkFormat);
            Assert.Equal(0x40621DU, header.AircraftAddress);
            Assert.Throws<NotSupportedException>(
                () => AdsbExtendedSquitterFactory.Deserialize(frame)
            );
        }

        [Fact]
        public void TryReadHeader_InvalidCrc_ReturnsFalse()
        {
            var frame = Convert.FromHexString("8D40621D58C382D690C8AC2863A7");
            frame[10] ^= 0x01;

            Assert.False(
                AdsbExtendedSquitterFactory.TryReadHeader(frame, out _)
            );
        }

        [Fact]
        public void ReadHeader_Df18WithNonZeroControlField_Throws()
        {
            var frame = Convert.FromHexString("9040621D58C382D690C8AC556F52");
            frame[0] |= 0x01;
            UpdateCrc(frame);

            Assert.Throws<NotSupportedException>(
                () => AdsbExtendedSquitterFactory.ReadHeader(frame)
            );
        }

        private static void UpdateCrc(byte[] frame)
        {
            var crc = ModeSHelper.CalcCrc24(frame, frame.Length - 3);
            frame[^3] = (byte)(crc >> 16);
            frame[^2] = (byte)(crc >> 8);
            frame[^1] = (byte)crc;
        }
    }
}
