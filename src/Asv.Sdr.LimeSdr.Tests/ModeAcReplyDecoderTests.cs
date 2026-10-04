using Asv.Sdr.LimeSdr;
using Xunit;

namespace Asv.Sdr.LimeSdr.Tests;

public class ModeAcReplyDecoderTests
{
    [Theory]
    [InlineData(0x2A7E, "0777", false)]
    [InlineData(0x2A7F, "0777", true)]
    [InlineData(0x0A7E, "0767", false)]
    [InlineData(0x2000, "0010", false)]
    [InlineData(0x4000, "0000", false)]
    [InlineData(0x8000, "0000", false)]
    public void DecodeSquawk_RawRegister_PreservesC1AndSeparatesSpi(
        ushort register, string expectedSquawk, bool expectedSpi)
    {
        var reply = ModeAcReplyDecoder.DecodeSquawk(register);
        Assert.Equal(expectedSquawk, reply.Squawk);
        Assert.Equal(expectedSpi, reply.Spi);
    }

    [Fact]
    public void DecodeSquawk_All4096Codes_RoundTripsWithoutDroppingC1()
    {
        for (var value = 0; value < 4096; value++)
        {
            var expected = Convert.ToString(value, 8).PadLeft(4, '0');
            var register = (ushort)(ModeSHelper.SetSquawk(expected) << 1);
            Assert.Equal(expected, ModeAcReplyDecoder.DecodeSquawk(register).Squawk);
        }
    }

    [Fact]
    public void DecodeAltitude_ValidGillhamAltitudes_PreservesC1()
    {
        var exercisedC1 = false;
        for (var altitude = -1000; altitude <= 50000; altitude += 100)
        {
            var code = ModeSHelper.GetModeCAltitudeCodeFromAltitude(altitude);
            exercisedC1 |= (code & 0x1000) != 0;
            Assert.Equal(altitude, ModeAcReplyDecoder.DecodeAltitude((ushort)(code << 1)));
        }
        Assert.True(exercisedC1);
    }
}
