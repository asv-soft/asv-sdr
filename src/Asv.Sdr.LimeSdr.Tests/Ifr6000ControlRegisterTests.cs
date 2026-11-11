using Asv.Common;
using Xunit;

namespace Asv.Sdr.LimeSdr.Tests;

public sealed class Ifr6000ControlRegisterTests
{
    [Theory]
    [InlineData(0x0000, 0x2000)]
    [InlineData(0x2000, 0x0000)]
    [InlineData(0xD5A5, 0xF5A5)]
    [InlineData(0xF5A5, 0xD5A5)]
    [InlineData(0xFFFF, 0xDFFF)]
    public async Task ResetAdsBStatistics_TogglesDfBitAndRestoresOriginalRegister(
        ushort original, ushort opposite)
    {
        using var fixture = new Fixture(original);
        var device = fixture.Device;
        var registers = fixture.Registers;

        await device.ResetAdsBStatistics();

        AssertTransaction(registers, opposite, original);
        Assert.Equal(original, registers.Read(0x0306));
    }

    [Theory]
    [InlineData(0xD5A5, true, (ushort)0xF5A5, 0xD5A5)]
    [InlineData(0xF5A5, true, null, 0xD5A5)]
    [InlineData(0xD5A5, false, null, 0xF5A5)]
    [InlineData(0xF5A5, false, (ushort)0xD5A5, 0xF5A5)]
    public async Task SwitchDf17Df18_SelectsRequestedDfAndResetsEvenWhenAlreadySelected(
        ushort original, bool isDf17, ushort? intermediate, ushort expected)
    {
        using var fixture = new Fixture(original);
        var device = fixture.Device;
        var registers = fixture.Registers;

        await device.SwitchDf17Df18(isDf17);

        AssertTransaction(registers,
            intermediate.HasValue ? [intermediate.Value, expected] : [expected]);
        Assert.Equal(expected, registers.Read(0x0306));
    }

    [Theory]
    [InlineData(0, 0xC300)]
    [InlineData(1, 0xC300)]
    [InlineData(2, 0xC300)]
    [InlineData(3, 0xC300)]
    [InlineData(4, 0xC300)]
    [InlineData(5, 0xC300)]
    [InlineData(6, 0xC300)]
    [InlineData(7, 0xC300)]
    [InlineData(0, 0xE300)]
    [InlineData(1, 0xE300)]
    [InlineData(2, 0xE300)]
    [InlineData(3, 0xE300)]
    [InlineData(4, 0xE300)]
    [InlineData(5, 0xE300)]
    [InlineData(6, 0xE300)]
    [InlineData(7, 0xE300)]
    public async Task WriteModeACControl_EachFlag_CanBeEnabledThenDisabled(int bit, ushort unrelated)
    {
        using var fixture = new Fixture(unrelated);
        var device = fixture.Device;
        var registers = fixture.Registers;

        await WriteModeAc(device, (byte)(1 << bit));
        AssertTransaction(registers, (ushort)(unrelated | (1 << bit)));

        registers.ClearTrace();
        await WriteModeAc(device, 0);
        AssertTransaction(registers, unrelated);
    }

    [Theory]
    [InlineData(0x00, 0xFF00)]
    [InlineData(0x15, 0xFF15)]
    [InlineData(0x2A, 0xFF2A)]
    [InlineData(0x40, 0xEB40)]
    [InlineData(0x80, 0xE780)]
    [InlineData(0xC0, 0xE3C0)]
    [InlineData(0xFF, 0xE3FF)]
    public async Task WriteModeACControl_ReplacesFlagsAndEnablesRequiredIntermodeTransmitters(
        byte flags, ushort expected)
    {
        using var fixture = new Fixture(ushort.MaxValue);
        var device = fixture.Device;
        var registers = fixture.Registers;

        await WriteModeAc(device, flags);

        AssertTransaction(registers, expected);
    }

    [Theory]
    [InlineData(0xFFFF, false, false, 0xFCFF)]
    [InlineData(0xFFFF, true, false, 0xFDFF)]
    [InlineData(0xFFFF, false, true, 0xFEFF)]
    [InlineData(0xFFFF, true, true, 0xFFFF)]
    [InlineData(0x84C5, false, false, 0x84C5)]
    [InlineData(0x84C5, true, false, 0x85C5)]
    [InlineData(0x84C5, false, true, 0x86C5)]
    [InlineData(0x84C5, true, true, 0x87C5)]
    public async Task WriteModeSControl_AllFlagCombinations_PreserveOtherBits(
        ushort original, bool enabled, bool attenuated, ushort expected)
    {
        using var fixture = new Fixture(original);
        var device = fixture.Device;
        var registers = fixture.Registers;

        await device.WriteModeSControl(enabled, attenuated);

        AssertTransaction(registers, expected);
    }

    [Theory]
    [InlineData("reset")]
    [InlineData("select")]
    [InlineData("ac")]
    [InlineData("s")]
    public async Task ControlOperations_DisposedDevice_DoesNotAccessRegisters(string operation)
    {
        using var fixture = new Fixture(0xF5A5);
        fixture.Device.Dispose();

        await Execute(operation, fixture.Device);

        Assert.Empty(fixture.Registers.Reads);
        Assert.Empty(fixture.Registers.Writes);
        Assert.Empty(fixture.Registers.Frames);
        Assert.Equal(0xF5A5, fixture.Registers.Read(0x0306));
    }

    [Theory]
    [InlineData("reset", false)]
    [InlineData("reset", true)]
    [InlineData("select", false)]
    [InlineData("select", true)]
    [InlineData("ac", false)]
    [InlineData("ac", true)]
    [InlineData("s", false)]
    [InlineData("s", true)]
    public async Task ControlOperations_DoNotHideRegisterIoErrors(string operation, bool failWrite)
    {
        using var fixture = new Fixture(0xF5A5);
        var device = fixture.Device;
        var registers = fixture.Registers;
        var failure = new IOException("Register I/O failed");
        registers.ReadError = failWrite ? null : failure;
        registers.WriteError = failWrite ? failure : null;

        var actual = await Assert.ThrowsAsync<IOException>(() =>
            Execute(operation, device));

        Assert.Same(failure, actual);
        Assert.Equal([0x0306], registers.Reads);
        Assert.Equal(failWrite ? 1 : 0, registers.Writes.Count);
        Assert.Empty(registers.Frames);
        Assert.Equal(0xF5A5, registers.Read(0x0306));
    }

    [Theory]
    [InlineData("all", false, 0xE31F, 0xFF1F)]
    [InlineData("a", false, 0xE31F, 0xE71F)]
    [InlineData("c", false, 0xE31F, 0xEB1F)]
    [InlineData("s", false, 0xE31F, 0xF31F)]
    [InlineData("all", true, 0xFF3F, 0xE33F)]
    [InlineData("a", true, 0xFF3F, 0xFB3F)]
    [InlineData("c", true, 0xFF3F, 0xF73F)]
    [InlineData("s", true, 0xFF3F, 0xEF3F)]
    public async Task ModeSwitches_ApplyDisableMaskAndPreserveOtherBits(
        string mode, bool isOn, ushort original, ushort expected)
    {
        using var fixture = new Fixture(original);
        var device = fixture.Device;

        await (mode switch
        {
            "all" => device.TurnOnOffAllModes(isOn),
            "a" => device.TurnOnOffModeA(isOn),
            "c" => device.TurnOnOffModeC(isOn),
            "s" => device.TurnOnOffModeS(isOn),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        });

        AssertTransaction(fixture.Registers, expected);
    }

    [Theory]
    [InlineData("all", 0x1C00)]
    [InlineData("a", 0x0400)]
    [InlineData("c", 0x0800)]
    [InlineData("s", 0x1000)]
    public async Task StatisticsResets_ToggleOnlySelectedModes(string mode, ushort mask)
    {
        using var fixture = new Fixture(0xD5A5);

        await ExecuteReset(mode, fixture.Device);

        AssertTransaction(fixture.Registers, (ushort)(0xD5A5 ^ mask), 0xD5A5);
    }

    [Theory]
    [InlineData("adsb", 0xD5A5, 0xF5A5)]
    [InlineData("df17", 0xD5A5, 0xF5A5)]
    [InlineData("df18", 0xF5A5, 0xD5A5)]
    [InlineData("all", 0xD5A5, 0xC9A5)]
    [InlineData("a", 0xD5A5, 0xD1A5)]
    [InlineData("c", 0xD5A5, 0xDDA5)]
    [InlineData("s", 0xD5A5, 0xC5A5)]
    public async Task StatisticsReset_QueuedControlUpdate_RunsAfterBothResetWrites(
        string mode, ushort original, ushort opposite)
    {
        using var fixture = new Fixture(original);
        Task? update = null;
        fixture.Registers.FirstWordStaged = () => update = fixture.Device.WriteModeSControl(true, true);

        await ExecuteReset(mode, fixture.Device);
        Assert.NotNull(update);
        await update;

        AssertTransaction(fixture.Registers, opposite, original, (ushort)(original | 0x0300));
    }

    [Theory]
    [InlineData(0x000001u)]
    [InlineData(0x010000u)]
    [InlineData(0xABCDEFu)]
    [InlineData(0xFFFFFEu)]
    public async Task UpdateModeSExpectedIcao_WritesBothAddressWordsAndCachesSuccessfulWrite(uint address)
    {
        using var fixture = new Fixture(0xF5A5);
        ILimeSdrIfr6000Device device = fixture.Device;

        await device.UpdateModeSExpectedIcao(address);

        var expected = new (ushort, ushort)[]
        {
            (0x032F, (ushort)(address >> 16)), (0x0330, (ushort)(address & 0xFFFF))
        };
        Assert.Equal(expected, fixture.Registers.Writes);
        Assert.Equal(expected, Assert.Single(fixture.Registers.Frames));
        Assert.Equal((ushort)(address >> 16), fixture.Registers.Read(0x032F));
        Assert.Equal((ushort)(address & 0xFFFF), fixture.Registers.Read(0x0330));
        Assert.Equal(0xF5A5, fixture.Registers.Read(0x0306));
        fixture.Registers.ClearTrace();

        await device.UpdateModeSExpectedIcao(address);

        Assert.Empty(fixture.Registers.Writes);
        Assert.Empty(fixture.Registers.Frames);
        await device.UpdateModeSExpectedIcao(address == 1 ? 2 : address - 1);
        Assert.Equal(2, fixture.Registers.Writes.Count);
        Assert.Single(fixture.Registers.Frames);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0xFFFFFFu)]
    [InlineData(0x1000000u)]
    [InlineData(uint.MaxValue)]
    public async Task UpdateModeSExpectedIcao_InvalidOrBroadcastAddress_DoesNotWrite(uint address)
    {
        using var fixture = new Fixture(0xF5A5);

        await fixture.Device.UpdateModeSExpectedIcao(address);

        Assert.Empty(fixture.Registers.Writes);
        Assert.Empty(fixture.Registers.Frames);
        Assert.Equal(0xF5A5, fixture.Registers.Read(0x0306));
    }

    [Fact]
    public async Task UpdateModeSExpectedIcao_FailedWrite_IsRetriedInsteadOfCached()
    {
        using var fixture = new Fixture(0xF5A5);
        var failure = new IOException("Address write failed");
        fixture.Registers.WriteError = failure;
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() =>
            fixture.Device.UpdateModeSExpectedIcao(0xABCDEF)));
        fixture.Registers.WriteError = null;
        fixture.Registers.ClearTrace();

        await fixture.Device.UpdateModeSExpectedIcao(0xABCDEF);

        Assert.Equal(new (ushort, ushort)[] { (0x032F, 0xAB), (0x0330, 0xCDEF) },
            Assert.Single(fixture.Registers.Frames));
    }

    [Fact]
    public async Task PublicSingleRegisterWrite_AppliesExactlyOnce()
    {
        using var fixture = new Fixture(0xF5A5);

        await fixture.Device.WriteCustomRegister(0x0306, 0x1234, TestContext.Current.CancellationToken);

        AssertTransaction(fixture.Registers, 0x1234);
    }

    [Fact]
    public async Task ProtectedSingleRegisterWrite_AppliesBeforeReadingBackInSameTransaction()
    {
        using var fixture = new Fixture(0xF5A5);
        using var probe = new CustomDeviceProbe(fixture.TaskFactory, fixture.Registers);

        var readBack = await probe.WriteAndReadBack(0x0306, 0x1234);

        Assert.Equal(0x1234, readBack);
        AssertTransaction(fixture.Registers, 0x1234);
    }

    [Fact]
    public async Task RegisterBitsWrite_AppliesExactlyOnceAndPreservesOtherBits()
    {
        using var fixture = new Fixture(0xF5A5);
        using var probe = new CustomDeviceProbe(fixture.TaskFactory, fixture.Registers);

        await probe.WriteBit(0x0306, 13, false);

        AssertTransaction(fixture.Registers, 0xD5A5);
    }

    [Fact]
    public async Task RegisterFrameWrite_StagesEveryWordBeforeApplyingOnce()
    {
        using var fixture = new Fixture(0xF5A5);
        (ushort Address, ushort Value)[] words = [(0x0306, 0x1234), (0x032F, 0xAB), (0x0330, 0xCDEF)];
        var observedControl = new List<ushort>();
        fixture.Registers.WordStaged = () => observedControl.Add(fixture.Registers.Read(0x0306));

        await fixture.Device.WriteCustomRegistersFrame(words, TestContext.Current.CancellationToken);

        Assert.Equal(new ushort[] { 0xF5A5, 0xF5A5, 0xF5A5 }, observedControl);
        Assert.Equal(words, fixture.Registers.Writes);
        Assert.Equal(words, Assert.Single(fixture.Registers.Frames));
        Assert.All(words, word => Assert.Equal(word.Value, fixture.Registers.Read(word.Address)));
        Assert.Equal(0x80, fixture.Registers.Control);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UfRequest_BothWritePaths_ApplyAllSevenWordsAsOneFrame(bool readReply)
    {
        using var fixture = new Fixture(0xF5A5);
        var request = new ModeSUF4 { IcaoAddress = 0xABCDEF, IsShortReply = true };

        if (readReply)
        {
            Assert.Null(await fixture.Device.ReadDfMessage(request, () => new ModeSDF4(), attempts: 1));
        }
        else
        {
            Assert.True(await fixture.Device.WriteUfMessage(request));
        }

        Assert.Equal(2, fixture.Registers.Frames.Count);
        Assert.Equal(new (ushort, ushort)[] { (0x032F, 0xAB), (0x0330, 0xCDEF) }, fixture.Registers.Frames[0]);
        var ufFrame = fixture.Registers.Frames[1];
        Assert.Equal(Enumerable.Range(0x0316, 7).Select(address => (ushort)address), ufFrame.Select(word => word.Address));
        Assert.Equal(0x2000, ufFrame[0].Value);
        Assert.All(ufFrame, word => Assert.Equal(word.Value, fixture.Registers.Read(word.Address)));
        Assert.Equal(0x80, fixture.Registers.Control);
    }

    private static Task ExecuteReset(string mode, ILimeSdrIfr6000Device device) => mode switch
    {
        "adsb" => device.ResetAdsBStatistics(),
        "df17" => device.SwitchDf17Df18(true),
        "df18" => device.SwitchDf17Df18(false),
        "all" => device.ResetAllStatistics(),
        "a" => device.ResetModeAStatistics(),
        "c" => device.ResetModeCStatistics(),
        "s" => device.ResetModeSStatistics(),
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private static Task Execute(string operation, ILimeSdrIfr6000Device device) => operation switch
    {
        "reset" => device.ResetAdsBStatistics(),
        "select" => device.SwitchDf17Df18(false),
        "ac" => WriteModeAc(device, 0),
        "s" => device.WriteModeSControl(false, false),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static Task WriteModeAc(ILimeSdrIfr6000Device device, byte flags) =>
        device.WriteModeACControl(
            (flags & 0x01) != 0, (flags & 0x02) != 0, (flags & 0x04) != 0, (flags & 0x08) != 0,
            (flags & 0x10) != 0, (flags & 0x20) != 0, (flags & 0x40) != 0, (flags & 0x80) != 0);

    private static void AssertTransaction(RegisterEditor registers, params ushort[] values)
    {
        Assert.Equal(values.Select(value => ((ushort)0x0306, value)), registers.Writes);
        Assert.Equal(values.Length, registers.Frames.Count);
        for (var index = 0; index < values.Length; index++)
        {
            Assert.Equal(((ushort)0x0306, values[index]), Assert.Single(registers.Frames[index]));
        }
        Assert.Equal(values[^1], registers.Read(0x0306));
        Assert.Equal(0x80, registers.Control);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly SingleThreadTaskScheduler _scheduler = new("IFR6000 register tests");
        public TaskFactory TaskFactory { get; }
        public RegisterEditor Registers { get; }
        public LimeSdrIfr6000Device Device { get; }

        public Fixture(ushort value)
        {
            Registers = new RegisterEditor(value);
            TaskFactory = new TaskFactory(_scheduler);
            Device = new LimeSdrIfr6000Device(TaskFactory, Registers);
        }

        public void Dispose()
        {
            Device.Dispose();
            _scheduler.Dispose();
        }
    }

    private sealed class CustomDeviceProbe(TaskFactory taskFactory, ILmsRegisterEditor registers)
        : LimeSdrCustomDevice(taskFactory, registers)
    {
        protected override CustomWorkMode InternalGetMode() => CustomWorkMode.Ifr6000;

        public async Task<ushort> WriteAndReadBack(ushort address, ushort value)
        {
            ushort readBack = 0;
            await AtomicEditRegister(edit =>
            {
                WriteCustomRegister(edit, address, value);
                readBack = ReadCustomRegister(edit, address);
            }, DisposeCancel);
            return readBack;
        }

        public Task WriteBit(ushort address, ushort bit, bool value) =>
            WriteCustomRegisterBits(address, bit, 1, (ushort)(value ? 1 : 0), DisposeCancel);
    }

    private sealed class RegisterEditor : ILmsRegisterEditor
    {
        private readonly Dictionary<ushort, ushort> _applied = [];
        private readonly Dictionary<ushort, ushort> _staged = [];
        private readonly List<(ushort Address, ushort Value)> _pending = [];
        private ushort _writeAddress;
        private ushort _writeValue;
        private ushort _readAddress;
        private ushort _holdFrame;
        public ushort Control { get; private set; } = 0x80;
        public List<ushort> Reads { get; } = [];
        public List<(ushort Address, ushort Value)> Writes { get; } = [];
        public List<(ushort Address, ushort Value)[]> Frames { get; } = [];
        public Exception? ReadError { get; set; }
        public Exception? WriteError { get; set; }
        public Action? FirstWordStaged { get; set; }
        public Action? WordStaged { get; set; }

        public RegisterEditor(ushort value) => _applied[0x0306] = value;
        public ushort Read(ushort address) => _applied.GetValueOrDefault(address);

        public void ClearTrace()
        {
            Reads.Clear();
            Writes.Clear();
            Frames.Clear();
        }

        public ushort RaedFPGAReg(ushort address)
        {
            if (address == 0x00D3) return Control;
            if (address == 0x00D6) return _holdFrame;
            if (address != 0x00D5) throw new NotSupportedException($"Read {address:X4}");
            Reads.Add(_readAddress);
            if (ReadError is not null) throw ReadError;
            return Read(_readAddress);
        }

        public void WriteFPGAReg(ushort address, ushort value)
        {
            switch (address)
            {
                case 0x00D1: _writeAddress = value; break;
                case 0x00D2: _writeValue = value; break;
                case 0x00D4: _readAddress = value; break;
                case 0x00D6: _holdFrame = value; break;
                case 0x00D3:
                    var previous = Control;
                    if ((previous & 1) == 0 && (value & 1) != 0)
                    {
                        Writes.Add((_writeAddress, _writeValue));
                        if (WriteError is not null) throw WriteError;
                        _staged[_writeAddress] = _writeValue;
                        _pending.Add((_writeAddress, _writeValue));
                        if (Writes.Count == 1) FirstWordStaged?.Invoke();
                        WordStaged?.Invoke();
                    }
                    if ((previous & 2) == 0 && (value & 2) != 0)
                    {
                        foreach (var word in _staged) _applied[word.Key] = word.Value;
                        Frames.Add(_pending.ToArray());
                        _pending.Clear();
                    }
                    Control = value;
                    break;
                default: throw new NotSupportedException($"Write {address:X4}");
            }
        }
    }
}
