using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Asv.Sdr.LimeSdr;

/// <summary>Register adapter for the UVD interrogator's 0x0500..0x050D map.</summary>
public sealed class LimeSdrUvdInterrogatorDevice : LimeSdrCustomDevice, ILimeSdrUvdInterrogatorDevice
{
    private const ushort ControlRegister = 0x0500;
    private static readonly ushort[] SnapshotRegisters =
    [
        0x0501, 0x0502, 0x0503, 0x0504, 0x0505, 0x0506,
        0x0507, 0x0508, 0x0509, 0x050A, 0x050B, 0x050C, 0x050D
    ];

    public LimeSdrUvdInterrogatorDevice(string deviceId, ILogger? logger = null) : base(deviceId, logger)
    {
    }

    internal LimeSdrUvdInterrogatorDevice(TaskFactory taskFactory, ILmsRegisterEditor registerEditor)
        : base(taskFactory, registerEditor)
    {
    }

    protected override CustomWorkMode InternalGetMode() => CustomWorkMode.Uvd;

    public Task TurnOnAsync(CancellationToken cancel = default) => SetModuleEnabledAsync(true, cancel);
    public Task TurnOffAsync(CancellationToken cancel = default) => SetModuleEnabledAsync(false, cancel);

    public Task SetCodeEnabledAsync(UvdRequestCode code, bool enabled, CancellationToken cancel = default) =>
        UpdateControlAsync((ushort)(1 << GetCodeIndex(code)), !enabled, cancel);

    public Task SetAllCodesEnabledAsync(bool enabled, CancellationToken cancel = default) =>
        UpdateControlAsync(0x000F, !enabled, cancel);

    public Task SetSlsPulseEnabledAsync(UvdRequestCode code, bool enabled, CancellationToken cancel = default) =>
        UpdateControlAsync((ushort)(1 << (GetCodeIndex(code) + 4)), enabled, cancel);

    public Task SetSlsPulseAttenuatedAsync(UvdRequestCode code, bool attenuated, CancellationToken cancel = default) =>
        UpdateControlAsync((ushort)(1 << (GetCodeIndex(code) + 8)), attenuated, cancel);

    public Task ResetStatisticsAsync(UvdRequestCode code, CancellationToken cancel = default) =>
        ResetStatisticsCoreAsync((ushort)(1 << GetCodeIndex(code)), cancel);

    public Task ResetAllStatisticsAsync(CancellationToken cancel = default) => ResetStatisticsCoreAsync(0x000F, cancel);

    public async Task<UvdReply> ReadStoredReplyAsync(UvdRequestCode code, CancellationToken cancel = default)
    {
        var index = GetCodeIndex(code);
        if (code == UvdRequestCode.Zk4)
            throw new NotSupportedException("ZK4 does not define an information-word reply.");
        var address = (ushort)(0x0501 + index * 2);
        var words = await ReadFrameAsync([address, (ushort)(address + 1)], cancel).ConfigureAwait(false);
        return DecodeStoredReply(code, words[0], words[1]);
    }

    public async Task<UvdCodeSnapshot> ReadCodeSnapshotAsync(UvdRequestCode code, CancellationToken cancel = default)
    {
        var index = GetCodeIndex(code);
        var countAddress = (ushort)(0x0507 + index / 2);
        var delayAddress = (ushort)(0x0509 + index);
        if (code == UvdRequestCode.Zk4)
        {
            var words = await ReadFrameAsync([countAddress, delayAddress], cancel).ConfigureAwait(false);
            return new UvdCodeSnapshot(code, (byte)(words[0] >> 8), unchecked((short)words[1]), null);
        }

        var replyAddress = (ushort)(0x0501 + index * 2);
        var frame = await ReadFrameAsync([replyAddress, (ushort)(replyAddress + 1), countAddress, delayAddress], cancel)
            .ConfigureAwait(false);
        return new UvdCodeSnapshot(code, (byte)(frame[2] >> ((index % 2) * 8)), unchecked((short)frame[3]),
            DecodeStoredReply(code, frame[0], frame[1]));
    }

    public async Task<UvdInterrogatorSnapshot> ReadSnapshotAsync(CancellationToken cancel = default)
    {
        var words = await ReadFrameAsync(SnapshotRegisters, cancel).ConfigureAwait(false);
        var snapshots = new UvdCodeSnapshot[4];
        for (var index = 0; index < snapshots.Length; index++)
        {
            var code = (UvdRequestCode)(index + 1);
            var reply = index < 3 ? DecodeStoredReply(code, words[index * 2], words[index * 2 + 1]) : null;
            snapshots[index] = new UvdCodeSnapshot(code,
                (byte)(words[6 + index / 2] >> ((index % 2) * 8)), unchecked((short)words[8 + index]), reply);
        }
        return new UvdInterrogatorSnapshot(snapshots[0], snapshots[1], snapshots[2], snapshots[3],
            unchecked((short)words[12]));
    }

    public async Task<double> ReadFrequencyMHzAsync(CancellationToken cancel = default)
    {
        var words = await ReadFrameAsync([0x050D], cancel).ConfigureAwait(false);
        return UvdInterrogatorSnapshot.ReplyReferenceFrequencyMHz + unchecked((short)words[0]) / 1000.0;
    }

    private async Task SetModuleEnabledAsync(bool enabled, CancellationToken cancel)
    {
        CheckState(cancel);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(DisposeCancel, cancel);
        if (enabled)
            await TurnOnMode(linked.Token).ConfigureAwait(false);
        else
            await TurnOffMode(linked.Token).ConfigureAwait(false);
        CheckState(linked.Token);
    }

    private async Task UpdateControlAsync(ushort mask, bool set, CancellationToken cancel)
    {
        CheckState(cancel);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(DisposeCancel, cancel);
        await AtomicEditRegister(edit =>
        {
            CheckState(linked.Token);
            var value = ReadCustomRegister(edit, ControlRegister);
            CheckState(linked.Token);
            WriteCustomRegister(edit, ControlRegister, set ? (ushort)(value | mask) : (ushort)(value & ~mask));
        }, linked.Token).ConfigureAwait(false);
        CheckState(linked.Token);
    }

    private async Task ResetStatisticsCoreAsync(ushort mask, CancellationToken cancel)
    {
        CheckState(cancel);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(DisposeCancel, cancel);
        await AtomicEditRegister(edit =>
        {
            CheckState(linked.Token);
            var original = ReadCustomRegister(edit, ControlRegister);
            CheckState(linked.Token);
            var enabledMask = (ushort)(~original & mask);
            if (enabledMask == 0)
                return;
            WriteCustomRegister(edit, ControlRegister, (ushort)(original | enabledMask));
            // Keep both writes in the same scheduler operation, as for the existing IFR6000 resets.
            Thread.Sleep(10);
            CheckState(linked.Token);
            WriteCustomRegister(edit, ControlRegister, original);
        }, linked.Token).ConfigureAwait(false);
        CheckState(linked.Token);
    }

    private async Task<ushort[]> ReadFrameAsync(ushort[] addresses, CancellationToken cancel)
    {
        CheckState(cancel);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(DisposeCancel, cancel);
        var words = await ReadCustomRegistersFrame(addresses, linked.Token).ConfigureAwait(false);
        CheckState(linked.Token);
        return words;
    }

    private static UvdReply DecodeStoredReply(UvdRequestCode code, ushort high, ushort low) =>
        UvdReplyDecoder.Decode(code, ((uint)(high & 0xF) << 16) | low, (high & 0x10) != 0);

    private void CheckState(CancellationToken cancel)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        cancel.ThrowIfCancellationRequested();
    }

    private static int GetCodeIndex(UvdRequestCode code) => code switch
    {
        UvdRequestCode.Zk1 => 0,
        UvdRequestCode.Zk2 => 1,
        UvdRequestCode.Zk3 => 2,
        UvdRequestCode.Zk4 => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    };
}
