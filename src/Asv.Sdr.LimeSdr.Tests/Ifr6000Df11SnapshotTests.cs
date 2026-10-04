using Asv.Sdr.LimeSdr;
using Xunit;

namespace Asv.Sdr.LimeSdr.Tests;

public sealed class Ifr6000Df11SnapshotTests
{
    [Fact]
    public void TryDecode_CapturedHardwareDf11_ReturnsObservedAddressAndInterrogatorCode()
    {
        var sample = CreateSample("5E15077728731F", 42);

        Assert.True(Ifr6000Df11Snapshot.TryDecode(sample, sample, 41, out var message, out var reason));

        Assert.Equal("validated", reason);
        Assert.Equal(0x150777U, message!.IcaoAddress);
        Assert.Equal(1, message.IC);
        Assert.Equal(0, message.CL);
    }

    [Fact]
    public void TryDecode_FreshSharedSlotWithZeroInterrogatorCode_PreservesObservedFrame()
    {
        // This frame can be unsolicited; freshness alone must not imply ITM causality.
        var sample = CreateSample("5E15077728731E", 116);

        Assert.True(Ifr6000Df11Snapshot.TryDecode(sample, sample, 115, out var message, out var reason));

        Assert.Equal("validated", reason);
        Assert.Equal(0x150777U, message!.IcaoAddress);
        Assert.Equal(0, message.IC);
        Assert.Equal(0, message.CL);
    }

    [Fact]
    public void TryDecode_UnchangedBaseline_ReturnsNoFreshFrame()
    {
        var sample = CreateSample("5E15077728731F", 42);

        Assert.False(Ifr6000Df11Snapshot.TryDecode(sample, sample, 42, out var message, out var reason));

        Assert.Null(message);
        Assert.Equal("no-fresh-frame", reason);
    }

    [Fact]
    public void TryDecode_CounterWrap_IsFresh()
    {
        var sample = CreateSample("5E15077728731F", 0);

        Assert.True(Ifr6000Df11Snapshot.TryDecode(sample, sample, 255, out var message, out _));

        Assert.NotNull(message);
    }

    [Fact]
    public void TryDecode_MonitorWithoutBaseline_AcceptsLatestFrame()
    {
        var sample = CreateSample("5E15077728731F", 0);

        Assert.True(Ifr6000Df11Snapshot.TryDecode(sample, sample, null, out _, out _));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 0)]
    [InlineData(1, 5)]
    public void TryDecode_CounterChangesDuringEitherRead_RejectsSnapshot(int sampleIndex, int wordIndex)
    {
        var first = CreateSample("5E15077728731F", 42);
        var second = (ushort[])first.Clone();
        (sampleIndex == 0 ? first : second)[wordIndex] = 43 << 8;

        Assert.False(Ifr6000Df11Snapshot.TryDecode(first, second, 41, out var message, out var reason));

        Assert.Null(message);
        Assert.Equal("counter-changed", reason);
    }

    [Fact]
    public void TryDecode_RawChangesWithoutCounter_RejectsInterruptedRamWrite()
    {
        var first = CreateSample("5E15077728731F", 42);
        var second = (ushort[])first.Clone();
        second[2] ^= 0x0400;

        Assert.False(Ifr6000Df11Snapshot.TryDecode(first, second, 41, out var message, out var reason));

        Assert.Null(message);
        Assert.Equal("raw-changed", reason);
    }

    [Fact]
    public void TryDecode_SameCorruptedIcaoInBothReads_RejectsParityError()
    {
        var sample = CreateSample("5E15037728731F", 42);

        Assert.False(Ifr6000Df11Snapshot.TryDecode(sample, sample, 41, out var message, out var reason));

        Assert.Null(message);
        Assert.Equal("crc-invalid", reason);
    }

    [Fact]
    public void TryDecode_ValidDifferentAddress_PreservesObservedValueForCallerComparison()
    {
        var sample = CreateSample(0x150377, 42);

        Assert.True(Ifr6000Df11Snapshot.TryDecode(sample, sample, 41, out var message, out _));

        Assert.Equal(0x150377U, message!.IcaoAddress);
        Assert.NotEqual(0x150777U, message.IcaoAddress);
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(0xFFFFFFU)]
    public void TryDecode_InvalidAddressWithValidParity_ReturnsNull(uint icao)
    {
        var sample = CreateSample(icao, 42);

        Assert.False(Ifr6000Df11Snapshot.TryDecode(sample, sample, 41, out var message, out var reason));

        Assert.Null(message);
        Assert.Equal("icao-invalid", reason);
    }

    [Fact]
    public void TryDecode_WrongDf_ReturnsNullAfterParserFailure()
    {
        var sample = CreateSample("8D15077728731F", 42);

        Assert.False(Ifr6000Df11Snapshot.TryDecode(sample, sample, 41, out var message, out var reason));

        Assert.Null(message);
        Assert.Equal("parse-failed:InvalidDataException", reason);
    }

    [Fact]
    public void TryDecode_PeriodAndTrailingPaddingDiffer_DoesNotRejectSameMessage()
    {
        var first = CreateSample("5E15077728731F", 42);
        var second = (ushort[])first.Clone();
        first[0] |= 1;
        first[5] |= 2;
        second[0] |= 3;
        second[5] |= 4;
        second[4] |= 0x00FF;

        Assert.True(Ifr6000Df11Snapshot.TryDecode(first, second, 41, out _, out _));
    }

    [Fact]
    public void TryDecode_TruncatedSample_ReturnsNull()
    {
        var sample = CreateSample("5E15077728731F", 42);

        Assert.False(Ifr6000Df11Snapshot.TryDecode(sample.AsSpan(0, 5), sample, 41, out var message, out var reason));

        Assert.Null(message);
        Assert.Equal("sample-length", reason);
    }

    private static ushort[] CreateSample(uint icao, byte counter)
    {
        var bytes = new byte[7];
        var buffer = bytes.AsSpan();
        new ModeSDF11 { IcaoAddress = icao, IC = 1 }.Serialize(ref buffer);
        return CreateSample(Convert.ToHexString(bytes), counter);
    }

    private static ushort[] CreateSample(string raw, byte counter)
    {
        var bytes = Convert.FromHexString(raw);
        var sample = new ushort[Ifr6000Df11Snapshot.SampleLength];
        sample[0] = sample[^1] = (ushort)(counter << 8);
        for (var index = 0; index < bytes.Length; index++)
        {
            sample[index / 2 + 1] |= (ushort)(index % 2 == 0 ? bytes[index] << 8 : bytes[index]);
        }

        return sample;
    }
}
