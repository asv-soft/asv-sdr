using Asv.Common;
using Xunit;

namespace Asv.Sdr.LimeSdr.Tests;

public sealed class UvdInterrogatorDeviceTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;
    [Fact]
    public void UvdDevice_UsesAssignedCustomModeSix()
    {
        Assert.True(Enum.IsDefined(typeof(CustomWorkMode), 6));
        Assert.Equal("Uvd", ((CustomWorkMode)6).ToString());
    }

    [Fact]
    public async Task TurnOnAndOff_UseUvdSelectorAndExistingResetProtocol()
    {
        using var fixture = new Fixture();

        await fixture.Device.TurnOnAsync(Cancel);

        Assert.Equal(0x0061, fixture.Registers.NativeControl);
        Assert.Equal(new (ushort, ushort)[] { (0x00D0, 0x0063), (0x00D0, 0x0061) }, fixture.Registers.NativeWrites);
        await fixture.Device.TurnOffAsync(Cancel);
        Assert.Equal(0, fixture.Registers.NativeControl & 1);
    }

    [Theory]
    [InlineData(UvdRequestCode.Zk1, 0)]
    [InlineData(UvdRequestCode.Zk2, 1)]
    [InlineData(UvdRequestCode.Zk3, 2)]
    [InlineData(UvdRequestCode.Zk4, 3)]
    public async Task SetCodeEnabled_ActiveLowMaskPreservesEveryOtherControlBit(UvdRequestCode code, int bit)
    {
        using var fixture = new Fixture(0xFFFF);

        await fixture.Device.SetCodeEnabledAsync(code, true, Cancel);

        Assert.Equal((ushort)(0xFFFF & ~(1 << bit)), fixture.Registers.Control);
        await fixture.Device.SetCodeEnabledAsync(code, false, Cancel);
        Assert.Equal(0xFFFF, fixture.Registers.Control);
        AssertAppliedSingleWordFrames(fixture.Registers, 2);
    }

    [Theory]
    [InlineData(UvdRequestCode.Zk1, 4, 8)]
    [InlineData(UvdRequestCode.Zk2, 5, 9)]
    [InlineData(UvdRequestCode.Zk3, 6, 10)]
    [InlineData(UvdRequestCode.Zk4, 7, 11)]
    public async Task SlsFlags_CanBeSetAndClearedIndependently(UvdRequestCode code, int enableBit, int attenuationBit)
    {
        var original = (ushort)(0xAFFF & ~((1 << enableBit) | (1 << attenuationBit)));
        using var fixture = new Fixture(original);
        var device = fixture.Device;

        await device.SetSlsPulseEnabledAsync(code, true, Cancel);
        await device.SetSlsPulseAttenuatedAsync(code, true, Cancel);

        Assert.Equal((ushort)(original | (1 << enableBit) | (1 << attenuationBit)), fixture.Registers.Control);
        await device.SetSlsPulseEnabledAsync(code, false, Cancel);
        Assert.Equal((ushort)(original | (1 << attenuationBit)), fixture.Registers.Control);
        await device.SetSlsPulseAttenuatedAsync(code, false, Cancel);
        Assert.Equal(original, fixture.Registers.Control);
        AssertAppliedSingleWordFrames(fixture.Registers, 4);
    }

    [Fact]
    public async Task SetAllCodesEnabled_ChangesOnlyFourDisableBits()
    {
        using var fixture = new Fixture(0xBCD0);

        await fixture.Device.SetAllCodesEnabledAsync(false, Cancel);
        Assert.Equal(0xBCDF, fixture.Registers.Control);
        await fixture.Device.SetAllCodesEnabledAsync(true, Cancel);
        Assert.Equal(0xBCD0, fixture.Registers.Control);
    }

    [Theory]
    [InlineData(UvdRequestCode.Zk1, 0)]
    [InlineData(UvdRequestCode.Zk2, 1)]
    [InlineData(UvdRequestCode.Zk3, 2)]
    [InlineData(UvdRequestCode.Zk4, 3)]
    public async Task ResetStatistics_PausesOnlySelectedCodeAndRestoresControl(UvdRequestCode code, int bit)
    {
        using var fixture = new Fixture(0xBFF0);

        await fixture.Device.ResetStatisticsAsync(code, Cancel);

        Assert.Equal(new ushort[] { (ushort)(0xBFF0 | (1 << bit)), 0xBFF0 }, fixture.Registers.AppliedControls);
        AssertAppliedSingleWordFrames(fixture.Registers, 2);
    }

    [Fact]
    public async Task ResetAllStatistics_LeavesOriginallyDisabledCodesDisabled()
    {
        using var fixture = new Fixture(0x5AA5);

        await fixture.Device.ResetAllStatisticsAsync(Cancel);

        Assert.Equal(new ushort[] { 0x5AAF, 0x5AA5 }, fixture.Registers.AppliedControls);
        fixture.Registers.Control = 0x5AAF;
        fixture.Registers.Frames.Clear();
        await fixture.Device.ResetAllStatisticsAsync(Cancel);
        Assert.Empty(fixture.Registers.Frames);
    }

    [Fact]
    public async Task ResetDisabledCode_DoesNotToggleOtherEnabledCodes()
    {
        using var fixture = new Fixture(0xBA04);

        await fixture.Device.ResetStatisticsAsync(UvdRequestCode.Zk3, Cancel);

        Assert.Equal(0xBA04, fixture.Registers.Control);
        Assert.Empty(fixture.Registers.Frames);
    }

    [Fact]
    public async Task ResetStatistics_QueuedControlUpdateRunsAfterBothResetWrites()
    {
        using var fixture = new Fixture(0xA000);
        Task? queued = null;
        fixture.Registers.OnFrameApplied = () =>
        {
            if (queued == null)
                queued = fixture.Device.SetSlsPulseEnabledAsync(UvdRequestCode.Zk3, true, Cancel);
        };

        await fixture.Device.ResetAllStatisticsAsync(Cancel);
        Assert.NotNull(queued);
        await queued;

        Assert.Equal(new ushort[] { 0xA00F, 0xA000, 0xA040 }, fixture.Registers.AppliedControls);
        Assert.Equal(0xA040, fixture.Registers.Control);
    }

    [Fact]
    public async Task CancelledReset_LeavesCodePausedWithoutOverwritingLaterSettings()
    {
        using var fixture = new Fixture(0x1000);
        using var cancellation = new CancellationTokenSource();
        fixture.Registers.OnFrameApplied = cancellation.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Device.ResetStatisticsAsync(UvdRequestCode.Zk2, cancellation.Token));

        Assert.Equal(0x1002, fixture.Registers.Control);
        Assert.Single(fixture.Registers.Frames);
        fixture.Registers.OnFrameApplied = null;
        await fixture.Device.SetSlsPulseAttenuatedAsync(UvdRequestCode.Zk4, true, Cancel);
        Assert.Equal(0x1802, fixture.Registers.Control);
    }

    [Fact]
    public async Task ReadSnapshot_DecodesOneHeldFrameWithCorrectWordAndByteOrder()
    {
        using var fixture = new Fixture();
        SetSample(fixture.Registers);

        var snapshot = await fixture.Device.ReadSnapshotAsync(Cancel);

        Assert.Equal(Enumerable.Range(0x0501, 13).Select(x => (ushort)x), fixture.Registers.Reads);
        Assert.Equal(new[] { true, false }, fixture.Registers.HoldTransitions);
        Assert.Equal(0x40, fixture.Registers.HoldControl);
        Assert.Equal("12345", snapshot.Zk1.StoredReply!.BoardNumber);
        Assert.True(snapshot.Zk1.StoredReply.CoordinateDistress);
        Assert.Equal(1270, snapshot.Zk2.StoredReply!.CurrentInformation!.AltitudeMeters);
        Assert.True(snapshot.Zk2.StoredReply.CurrentInformation.Emergency);
        Assert.Equal((byte?)30, snapshot.Zk2.StoredReply.CurrentInformation.FuelRemainingPercent);
        Assert.Equal(0xABCDEU, snapshot.Zk3.StoredReply!.InformationWord);
        Assert.Null(snapshot.Zk4.StoredReply);
        Assert.Equal(new byte[] { 100, 200, 1, 0 },
            new[] { snapshot.Zk1.ReplyCount, snapshot.Zk2.ReplyCount, snapshot.Zk3.ReplyCount, snapshot.Zk4.ReplyCount });
        Assert.Equal(50.0, snapshot.Zk1.ReplyRatioPercent);
        Assert.Equal(100.0, snapshot.Zk2.ReplyRatioPercent);
        Assert.Equal(0.5, snapshot.Zk3.ReplyRatioPercent);
        Assert.Equal(-1.0, snapshot.Zk1.ReplyDelayMicroseconds);
        Assert.Equal(2.0, snapshot.Zk2.ReplyDelayMicroseconds);
        Assert.Equal(0.0, snapshot.Zk3.ReplyDelayMicroseconds);
        Assert.True(double.IsNaN(snapshot.Zk4.ReplyDelayMicroseconds));
        Assert.Equal((short)-23, snapshot.RawFrequencyOffsetKHz);
        Assert.Equal(739.977, snapshot.FrequencyMHz, 9);
        Assert.Same(snapshot.Zk3, snapshot[UvdRequestCode.Zk3]);
    }

    [Theory]
    [InlineData(UvdRequestCode.Zk1, 0x0501, 100, -1.0)]
    [InlineData(UvdRequestCode.Zk2, 0x0503, 200, 2.0)]
    [InlineData(UvdRequestCode.Zk3, 0x0505, 1, 0.0)]
    [InlineData(UvdRequestCode.Zk4, 0, 0, double.NaN)]
    public async Task ReadCodeSnapshot_UsesAssignedReplyAndMeasurementRegisters(
        UvdRequestCode code, ushort replyAddress, byte count, double delay)
    {
        using var fixture = new Fixture();
        SetSample(fixture.Registers);
        var index = (int)code - 1;

        var sample = await fixture.Device.ReadCodeSnapshotAsync(code, Cancel);

        var expected = new List<ushort>();
        if (code != UvdRequestCode.Zk4)
            expected.AddRange([replyAddress, (ushort)(replyAddress + 1)]);
        expected.Add((ushort)(0x0507 + index / 2));
        expected.Add((ushort)(0x0509 + index));
        Assert.Equal(expected, fixture.Registers.Reads);
        Assert.Equal(count, sample.ReplyCount);
        Assert.Equal(delay, sample.ReplyDelayMicroseconds);
    }

    [Theory]
    [InlineData(UvdRequestCode.Zk1, 0x0501)]
    [InlineData(UvdRequestCode.Zk2, 0x0503)]
    [InlineData(UvdRequestCode.Zk3, 0x0505)]
    public async Task ReadStoredReply_HoldsBothWordsWhileUnderlyingRegistersChange(UvdRequestCode code, ushort address)
    {
        using var fixture = new Fixture();
        fixture.Registers.Set(address, 0x0011);
        fixture.Registers.Set((ushort)(address + 1), 0x2345);
        fixture.Registers.OnRead = _ => fixture.Registers.Set((ushort)(address + 1), 0x9999);

        var reply = await fixture.Device.ReadStoredReplyAsync(code, Cancel);

        Assert.Equal(0x12345U, reply.InformationWord);
        Assert.True(reply.CoordinateDistress);
        Assert.Equal(new ushort[] { address, (ushort)(address + 1) }, fixture.Registers.Reads);
        Assert.Equal(new[] { true, false }, fixture.Registers.HoldTransitions);
    }

    [Fact]
    public async Task ReadStoredReply_Zk4DoesNotReadStatisticsAsInformation()
    {
        using var fixture = new Fixture();

        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.Device.ReadStoredReplyAsync(UvdRequestCode.Zk4, Cancel));

        Assert.Empty(fixture.Registers.Reads);
        Assert.Empty(fixture.Registers.HoldTransitions);
    }

    [Theory]
    [InlineData(0, double.NaN)]
    [InlineData(1, 0.025)]
    [InlineData(200, 0.025)]
    [InlineData(201, double.NaN)]
    [InlineData(255, double.NaN)]
    public async Task ReplyCounts_InvalidBatchOrNoReplyDoesNotProduceValidDelay(byte count, double expectedDelay)
    {
        using var fixture = new Fixture();
        fixture.Registers.Set(0x0507, count);
        fixture.Registers.Set(0x0509, 1);

        var sample = await fixture.Device.ReadCodeSnapshotAsync(UvdRequestCode.Zk1, Cancel);

        Assert.Equal(expectedDelay, sample.ReplyDelayMicroseconds);
        Assert.Equal(count <= 200 ? count * 0.5 : double.NaN, sample.ReplyRatioPercent);
    }

    [Theory]
    [InlineData(short.MinValue, -819.2)]
    [InlineData(short.MaxValue, 819.175)]
    public async Task ReplyDelay_SignedTickExtremesHaveNoInventedNominalOffset(short ticks, double expected)
    {
        using var fixture = new Fixture();
        fixture.Registers.Set(0x0507, 1);
        fixture.Registers.Set(0x0509, unchecked((ushort)ticks));

        Assert.Equal(expected, (await fixture.Device.ReadCodeSnapshotAsync(UvdRequestCode.Zk1, Cancel)).ReplyDelayMicroseconds, 9);
    }

    [Theory]
    [InlineData(0, 740.0)]
    [InlineData(-3, 739.997)]
    [InlineData(26, 740.026)]
    [InlineData(short.MinValue, 707.232)]
    [InlineData(short.MaxValue, 772.767)]
    public async Task Frequency_UsesAssignedRegisterAndSignedKilohertzOffset(short raw, double expected)
    {
        using var fixture = new Fixture();
        fixture.Registers.Set(0x050D, unchecked((ushort)raw));

        Assert.Equal(expected, await fixture.Device.ReadFrequencyMHzAsync(Cancel), 9);
        Assert.Equal(new ushort[] { 0x050D }, fixture.Registers.Reads);
    }

    [Fact]
    public async Task Snapshot_WithoutRepliesRetainsRawFrequencyButDoesNotClaimMeasuredFrequency()
    {
        using var fixture = new Fixture();
        fixture.Registers.Set(0x050D, 10);

        var snapshot = await fixture.Device.ReadSnapshotAsync(Cancel);

        Assert.Equal((short)10, snapshot.RawFrequencyOffsetKHz);
        Assert.True(double.IsNaN(snapshot.FrequencyMHz));
    }

    [Fact]
    public async Task Snapshot_CoordinateOnlyZk4CanEstablishFrequencyWithoutInformationWord()
    {
        using var fixture = new Fixture();
        fixture.Registers.Set(0x0508, 0x0100);
        fixture.Registers.Set(0x050D, 50);

        var snapshot = await fixture.Device.ReadSnapshotAsync(Cancel);

        Assert.Equal(740.05, snapshot.FrequencyMHz, 9);
        Assert.Equal((byte)1, snapshot.Zk4.ReplyCount);
        Assert.Null(snapshot.Zk4.StoredReply);
        Assert.Equal(0.5, snapshot.Zk4.ReplyRatioPercent);
    }

    [Fact]
    public async Task ReadError_ReleasesHoldAndPropagatesOriginalError()
    {
        using var fixture = new Fixture();
        var failure = new IOException("register read failed");
        fixture.Registers.ReadError = failure;

        var error = await Assert.ThrowsAsync<IOException>(() => fixture.Device.ReadSnapshotAsync(Cancel));

        Assert.Same(failure, error);
        Assert.Equal(0x40, fixture.Registers.HoldControl);
        Assert.Equal(new[] { true, false }, fixture.Registers.HoldTransitions);
    }

    [Fact]
    public async Task CancellationDuringFrame_ReleasesHoldAndStopsReading()
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Registers.OnRead = _ => cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Device.ReadSnapshotAsync(cancellation.Token));

        Assert.Single(fixture.Registers.Reads);
        Assert.Equal(new[] { true, false }, fixture.Registers.HoldTransitions);
        Assert.Equal(0x40, fixture.Registers.HoldControl);
    }

    [Fact]
    public async Task AlreadyCancelledOrDisposed_DoesNotReadOrWriteHardware()
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Device.SetAllCodesEnabledAsync(true, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Device.ReadSnapshotAsync(cancellation.Token));
        fixture.Device.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Device.ReadSnapshotAsync(Cancel));
        Assert.Empty(fixture.Registers.Reads);
        Assert.Empty(fixture.Registers.Frames);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(255)]
    public async Task UnknownCode_IsRejectedBeforeHardwareAccess(byte value)
    {
        using var fixture = new Fixture();
        var code = (UvdRequestCode)value;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Device.SetCodeEnabledAsync(code, true, Cancel));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Device.SetSlsPulseEnabledAsync(code, true, Cancel));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Device.SetSlsPulseAttenuatedAsync(code, true, Cancel));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Device.ResetStatisticsAsync(code, Cancel));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Device.ReadCodeSnapshotAsync(code, Cancel));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Device.ReadStoredReplyAsync(code, Cancel));
        Assert.Empty(fixture.Registers.Reads);
        Assert.Empty(fixture.Registers.Frames);
    }

    private static void SetSample(RegisterEditor registers)
    {
        ushort[] values = [0x0011, 0x2345, 0x0006, 0xC127, 0x000A, 0xBCDE,
            0xC864, 0x0001, unchecked((ushort)-40), 80, 0, 0x7FFF, unchecked((ushort)-23)];
        for (var index = 0; index < values.Length; index++)
            registers.Set((ushort)(0x0501 + index), values[index]);
    }

    private static void AssertAppliedSingleWordFrames(RegisterEditor registers, int expected)
    {
        Assert.Equal(expected, registers.Frames.Count);
        Assert.All(registers.Frames, frame => Assert.Equal((ushort)0x0500, Assert.Single(frame).Address));
        Assert.Equal(0x80, registers.WriteControl);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly SingleThreadTaskScheduler _scheduler = new("UVD register tests");
        public RegisterEditor Registers { get; }
        public LimeSdrUvdInterrogatorDevice Device { get; }

        public Fixture(ushort control = 0)
        {
            Registers = new RegisterEditor { Control = control };
            Device = new LimeSdrUvdInterrogatorDevice(new TaskFactory(_scheduler), Registers);
        }

        public void Dispose()
        {
            Device.Dispose();
            _scheduler.Dispose();
        }
    }

    private sealed class RegisterEditor : ILmsRegisterEditor
    {
        private readonly Dictionary<ushort, ushort> _registers = [];
        private Dictionary<ushort, ushort>? _held;
        private readonly List<(ushort Address, ushort Value)> _pending = [];
        private readonly Dictionary<ushort, ushort> _native = [];
        private ushort _writeAddress, _writeValue, _readAddress;
        public ushort WriteControl { get; private set; } = 0x80;
        public ushort HoldControl { get; private set; } = 0x40;
        public ushort NativeControl => _native.GetValueOrDefault((ushort)0x00D0);
        public ushort Control { get => _registers.GetValueOrDefault((ushort)0x0500); set => Set(0x0500, value); }
        public List<ushort> Reads { get; } = [];
        public List<bool> HoldTransitions { get; } = [];
        public List<(ushort Address, ushort Value)[]> Frames { get; } = [];
        public IEnumerable<ushort> AppliedControls => Frames.Select(frame => Assert.Single(frame).Value);
        public List<(ushort Address, ushort Value)> NativeWrites { get; } = [];
        public Action? OnFrameApplied { get; set; }
        public Action<ushort>? OnRead { get; set; }
        public Exception? ReadError { get; set; }

        public void Set(ushort address, ushort value) => _registers[address] = value;

        public ushort RaedFPGAReg(ushort address)
        {
            if (address == 0x00D3) return WriteControl;
            if (address == 0x00D6) return HoldControl;
            if (address != 0x00D5) return _native.GetValueOrDefault(address);
            Reads.Add(_readAddress);
            if (ReadError is not null) throw ReadError;
            OnRead?.Invoke(_readAddress);
            return (_held ?? _registers).GetValueOrDefault(_readAddress);
        }

        public void WriteFPGAReg(ushort address, ushort value)
        {
            switch (address)
            {
                case 0x00D1: _writeAddress = value; break;
                case 0x00D2: _writeValue = value; break;
                case 0x00D4: _readAddress = value; break;
                case 0x00D6:
                    if ((HoldControl & 1) != (value & 1))
                    {
                        HoldTransitions.Add((value & 1) != 0);
                        _held = (value & 1) != 0 ? new Dictionary<ushort, ushort>(_registers) : null;
                    }
                    HoldControl = value;
                    break;
                case 0x00D3:
                    if ((WriteControl & 1) == 0 && (value & 1) != 0)
                        _pending.Add((_writeAddress, _writeValue));
                    if ((WriteControl & 2) == 0 && (value & 2) != 0)
                    {
                        foreach (var word in _pending) Set(word.Address, word.Value);
                        Frames.Add(_pending.ToArray());
                        _pending.Clear();
                        OnFrameApplied?.Invoke();
                    }
                    WriteControl = value;
                    break;
                default:
                    _native[address] = value;
                    NativeWrites.Add((address, value));
                    break;
            }
        }
    }
}
