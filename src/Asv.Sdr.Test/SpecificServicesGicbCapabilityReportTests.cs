using System;
using Xunit;

namespace Asv.Sdr.Test
{
    public class SpecificServicesGicbCapabilityReportTests
    {
        [Theory]
        [InlineData(0x18, 0x01, 0x38)]
        [InlineData(0x19, 0x39, 0x70)]
        [InlineData(0x1A, 0x71, 0xA8)]
        [InlineData(0x1B, 0xA9, 0xE0)]
        public void Serialize_FirstAndLastCapability_SetsBoundaryBits(
            byte register,
            byte firstCapability,
            byte lastCapability
        )
        {
            var sut = Create(register);
            sut.Gicbs.Add(CreateGicb(firstCapability));
            sut.Gicbs.Add(CreateGicb(lastCapability));
            var bytes = new byte[7];
            var buffer = bytes.AsSpan();

            sut.Serialize(ref buffer);

            Assert.Equal(new byte[] { 0x80, 0, 0, 0, 0, 0, 0x01 }, bytes);
        }

        [Fact]
        public void Serialize_Bds1C_FirstAndLastCapability_LeavesReservedBitsClear()
        {
            var sut = new Bds1C();
            sut.Gicbs.Add(CreateGicb(0xE1));
            sut.Gicbs.Add(CreateGicb(0xFF));
            var bytes = new byte[7];
            var buffer = bytes.AsSpan();

            sut.Serialize(ref buffer);

            Assert.Equal(new byte[] { 0x80, 0, 0, 0x02, 0, 0, 0 }, bytes);
        }

        [Fact]
        public void DeserializeAndSerialize_Bds1C_PreservesReservedBits()
        {
            var sut = new Bds1C();
            var bytes = new byte[] { 0, 0, 0, 0x01, 0xAB, 0xCD, 0xEF };
            ReadOnlySpan<byte> readBuffer = bytes;

            sut.Deserialize(ref readBuffer);
            var result = new byte[7];
            var writeBuffer = result.AsSpan();
            sut.Serialize(ref writeBuffer);

            Assert.Equal(0x01ABCDEFU, sut.ReservedBits);
            Assert.Equal(bytes, result);
        }

        [Theory]
        [InlineData(0x18, 0x01, 0x38)]
        [InlineData(0x19, 0x39, 0x70)]
        [InlineData(0x1A, 0x71, 0xA8)]
        [InlineData(0x1B, 0xA9, 0xE0)]
        [InlineData(0x1C, 0xE1, 0xFF)]
        public void Deserialize_BoundaryBits_ReturnsMappedCapabilities(
            byte register,
            byte firstCapability,
            byte lastCapability
        )
        {
            var sut = Create(register);
            var bytes = register == 0x1C
                ? new byte[] { 0x80, 0, 0, 0x02, 0, 0, 0 }
                : new byte[] { 0x80, 0, 0, 0, 0, 0, 0x01 };
            ReadOnlySpan<byte> buffer = bytes;

            sut.Deserialize(ref buffer);

            Assert.Collection(
                sut.Gicbs,
                item => Assert.Equal(firstCapability, item.DataSelector),
                item => Assert.Equal(lastCapability, item.DataSelector)
            );
        }

        [Fact]
        public void Serialize_Bds17AdsbCapabilities_PreservesSelectedBits()
        {
            var sut = new Bds17();
            sut.Gicbs.Add(CreateGicb(0x05));
            sut.Gicbs.Add(CreateGicb(0x0A));
            var bytes = new byte[7];
            var buffer = bytes.AsSpan();

            sut.Serialize(ref buffer);

            Assert.Equal(0x84, bytes[0]);
        }

        private static SpecificServicesGicbCapabilityReport Create(byte register)
        {
            return register switch
            {
                0x18 => new Bds18(),
                0x19 => new Bds19(),
                0x1A => new Bds1A(),
                0x1B => new Bds1B(),
                0x1C => new Bds1C(),
                _ => throw new ArgumentOutOfRangeException(nameof(register)),
            };
        }

        private static Gicb CreateGicb(byte dataSelector)
        {
            return new Gicb((byte)(dataSelector >> 4), (byte)(dataSelector & 0x0F));
        }
    }
}
