using System;
using Xunit;

namespace Asv.Sdr.Test
{
    public class UvdReplyDecoderTests
    {
        [Fact]
        public void UvdReplyDecoder_BelongsToHardwareIndependentCore()
        {
            var assembly = typeof(ModeSHelper).Assembly;

            Assert.NotNull(assembly.GetType("Asv.Sdr.UvdReplyDecoder"));
        }

        [Theory]
        [InlineData(0U, "00000")]
        [InlineData(1U, "00001")]
        [InlineData(0x10000U, "10000")]
        [InlineData(0x12345U, "12345")]
        [InlineData(0x99999U, "99999")]
        public void DecodeZk1_PreservesFiveBcdDigits(uint information, string expected)
        {
            var reply = UvdReplyDecoder.Decode(UvdRequestCode.Zk1, information, true);

            Assert.Equal(expected, reply.BoardNumber);
            Assert.Equal(information, reply.InformationWord);
            Assert.True(reply.CoordinateDistress);
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(1, 11)]
        [InlineData(2, 12)]
        [InlineData(3, 13)]
        [InlineData(4, 15)]
        public void DecodeZk1_InvalidDigitRetainsRawWordWithoutInventingNumber(int position, int digit)
        {
            var word = (uint)digit << (position * 4);

            var reply = UvdReplyDecoder.Decode(UvdRequestCode.Zk1, word);

            Assert.Null(reply.BoardNumber);
            Assert.Equal(word, reply.InformationWord);
            Assert.False(reply.CoordinateDistress);
        }

        [Theory]
        [InlineData(UvdRequestCode.Zk2)]
        [InlineData(UvdRequestCode.Zk3)]
        public void DecodeOtherCodes_PreservesInformationAndCoordinateDistress(UvdRequestCode code)
        {
            var reply = UvdReplyDecoder.Decode(code, 0xABCDE, true);

            Assert.Equal(code, reply.RequestCode);
            Assert.Equal(0xABCDEU, reply.InformationWord);
            Assert.True(reply.CoordinateDistress);
            Assert.Null(reply.BoardNumber);
        }

        [Theory]
        [InlineData(0x100000U)]
        [InlineData(uint.MaxValue)]
        public void Decode_WordOutsideTwentyBitsIsRejected(uint word)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UvdReplyDecoder.Decode(UvdRequestCode.Zk1, word));
            Assert.False(UvdReplyDecoder.TryDecodeBoardNumber(word, out var number));
            Assert.Null(number);
        }

        [Fact]
        public void DecodeZk4_RejectsNonexistentInformationWord()
        {
            Assert.Throws<NotSupportedException>(() => UvdReplyDecoder.Decode(UvdRequestCode.Zk4, 0));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(255)]
        public void Decode_UnknownRequestCodeIsRejected(byte code)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UvdReplyDecoder.Decode((UvdRequestCode)code, 0));
        }

        [Theory]
        [InlineData(0x0000U, 0)]
        [InlineData(0x0127U, 1270)]
        [InlineData(0x1000U, 10000)]
        [InlineData(0x3999U, 39990)]
        [InlineData(0x4F00U, 0)]
        [InlineData(0x4F01U, -10)]
        [InlineData(0x4F12U, -120)]
        [InlineData(0x4F30U, -300)]
        public void DecodeAltitude_UsesDecimalDecadesAndNegativeAbsoluteMarker(uint word, int expected)
        {
            Assert.True(UvdReplyDecoder.TryDecodeAltitude(word, out var altitude));
            Assert.Equal(expected, altitude);
        }

        [Theory]
        [InlineData(0x000AU)]
        [InlineData(0x00A0U)]
        [InlineData(0x0A00U)]
        [InlineData(0x0F12U)]
        [InlineData(0x4F1AU)]
        [InlineData(0x100000U)]
        public void InvalidAltitude_DoesNotEraseOtherCurrentInformationFields(uint word)
        {
            if (word > UvdReplyDecoder.InformationWordMask)
            {
                Assert.False(UvdReplyDecoder.TryDecodeAltitude(word, out _));
                return;
            }
            var reply = UvdReplyDecoder.Decode(UvdRequestCode.Zk2, word | 0x68000);

            Assert.NotNull(reply.CurrentInformation);
            Assert.Null(reply.CurrentInformation.AltitudeMeters);
            Assert.True(reply.CurrentInformation.Emergency);
            Assert.Equal((byte)6, reply.CurrentInformation.FuelCode);
            Assert.Equal((byte?)30, reply.CurrentInformation.FuelRemainingPercent);
            Assert.Equal(word | 0x68000U, reply.InformationWord);
        }

        [Fact]
        public void DecodeTi_KeepsAltitudeReferenceAndBothDistressIndicatorsIndependent()
        {
            var reply = UvdReplyDecoder.Decode(UvdRequestCode.Zk2, 0x64127, true);
            var ti = reply.CurrentInformation;

            Assert.NotNull(ti);
            Assert.Equal(1270, ti.AltitudeMeters);
            Assert.True(ti.IsAbsoluteAltitude);
            Assert.False(ti.Emergency);
            Assert.True(reply.CoordinateDistress);
            Assert.Equal((byte?)30, ti.FuelRemainingPercent);
            reply = UvdReplyDecoder.Decode(UvdRequestCode.Zk2, 0x68127, false);
            var otherTi = Assert.IsType<UvdCurrentInformation>(reply.CurrentInformation);
            Assert.False(otherTi.IsAbsoluteAltitude);
            Assert.True(otherTi.Emergency);
            Assert.False(reply.CoordinateDistress);
        }

        [Theory]
        [InlineData(0, null)]
        [InlineData(1, 5)]
        [InlineData(2, 10)]
        [InlineData(3, 15)]
        [InlineData(4, 20)]
        [InlineData(5, 25)]
        [InlineData(6, 30)]
        [InlineData(7, 35)]
        [InlineData(8, 40)]
        [InlineData(9, 45)]
        [InlineData(10, 50)]
        [InlineData(11, 60)]
        [InlineData(12, 70)]
        [InlineData(13, 80)]
        [InlineData(14, 90)]
        [InlineData(15, 100)]
        public void DecodeSo2010Fuel_AllSixteenCodesMatchTableFour(byte code, int? expected)
        {
            var ti = UvdReplyDecoder.Decode(UvdRequestCode.Zk2, (uint)code << 16).CurrentInformation;

            Assert.NotNull(ti);
            Assert.Equal(code, ti.FuelCode);
            Assert.Equal(expected.HasValue ? (byte?)expected.Value : null, ti.FuelRemainingPercent);
        }

        [Theory]
        [InlineData(16)]
        [InlineData(255)]
        public void DecodeSo2010Fuel_RejectsCodesOutsideFourBits(byte code)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UvdReplyDecoder.DecodeSo2010FuelPercent(code));
        }
    }
}
