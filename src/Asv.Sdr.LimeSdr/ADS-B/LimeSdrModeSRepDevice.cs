using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Asv.Sdr.LimeSdr;


public interface ILimeSdrModeSRepDevice : ILimeSdrModeACRepDevice
{
    Task SetDf11ReplyIsEnabled(bool enabled, CancellationToken cancel = default);
    
    Task SetDf11BroadcastIsEnabled(bool enabled, CancellationToken cancel = default);
    
    Task SetDf4IsEnabled(bool enabled, CancellationToken cancel = default);
    
    Task SetDf5IsEnabled(bool enabled, CancellationToken cancel = default);
    
    Task SetDf20IsEnabled(bool enabled, CancellationToken cancel = default);
    
    Task SetDf21IsEnabled(bool enabled, CancellationToken cancel = default);
    
    /// <summary>
    /// Выбор вывода сообщения своего/всех в регистры UF4, UF5, UF11, UF20, UF21.
    /// </summary>
    /// <param name="enabled"></param>
    /// <param name="cancel"></param>
    /// <returns></returns>
    Task SetNotOurUFsOutput(bool enabled, CancellationToken cancel = default);
    
    /// <summary>
    /// Read UF ADS-B message
    /// </summary>
    /// <returns>Last received UF ADS-B message</returns>
    Task<byte[]> ReadAnyUFMessage(CancellationToken cancel = default);

    Task<byte[]> ReadUF4Message(CancellationToken cancel = default);
    
    Task<byte[]> ReadUF5Message(CancellationToken cancel = default);

    Task<byte[]> ReadUF20Message(CancellationToken cancel = default);

    Task<byte[]> ReadUF21Message(CancellationToken cancel = default);

    Task<byte[]> ReadUF11Message(CancellationToken cancel = default);
    
    Task<(byte[] UF4, byte[] UF5, byte[] UF20, byte[] UF21, byte[] UF11)> ReadAllUFMessage(CancellationToken cancel = default);

    Task SetAnyUFType(ushort type, CancellationToken cancel);
    
    Task<ushort> GetAnyUFType(CancellationToken cancel);
    
    /// <summary>
    /// Write DF4 ADS-B message
    /// </summary>
    /// <param name="message">DF4 message</param>
    Task WriteDF4Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default);
    
    /// <summary>
    /// Write DF5 ADS-B message
    /// </summary>
    /// <param name="message">DF5 message</param>
    Task WriteDF5Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default);

    Task WriteBDS10Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default);
    Task WriteBDS17Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default);
    Task WriteBDS20Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default);
    Task WriteBDS30Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default);
    Task WriteBDS40Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default);
    Task WriteBDS50Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default);
    Task WriteBDS60Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default);
    
    
    /// <summary>
    /// Write DF11 ADS-B message
    /// </summary>
    /// <param name="message">DF11 message</param>
    Task WriteDF11Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default);
    
#if DEBUG
    Task<byte[]> ReadDF4Message(CancellationToken cancel = default);
    Task<byte[]> ReadDF5Message(CancellationToken cancel = default);
    Task<byte[]> ReadBDS10Message(CancellationToken cancel = default);
    Task<byte[]> ReadBDS17Message(CancellationToken cancel = default);
    Task<byte[]> ReadBDS20Message(CancellationToken cancel = default);
    Task<byte[]> ReadBDS30Message(CancellationToken cancel = default);
    Task<byte[]> ReadBDS40Message(CancellationToken cancel = default);
    Task<byte[]> ReadBDS50Message(CancellationToken cancel = default);
    Task<byte[]> ReadBDS60Message(CancellationToken cancel = default);
    Task<byte[]> ReadDF11Message(CancellationToken cancel = default);
#endif
    
    /// <summary>
    /// Sets the DF delay reply value.
    /// </summary>
    /// <param name="delayUs">Delay DM reply in micro seconds</param>
    Task SetDfDelay(double delayUs, CancellationToken cancel = default);
    
    /// <summary>
    /// Gets the DF delay reply value.
    /// </summary>
    /// <returns>Delay DM reply in micro seconds</returns>
    Task<double> GetDfDelay(CancellationToken cancel = default);
    
    /// <summary>
    /// RX UF4/UF20 Message Statistic
    /// </summary>
    /// <param name="cancel"></param>
    /// <returns>[UF4, UF20] cnt</returns>
    Task<byte[]> GetUF4UF20Stat(CancellationToken cancel = default);
    
    /// <summary>
    /// RX UF5/UF21 Message Statistic
    /// </summary>
    /// <param name="cancel"></param>
    /// <returns>[UF5, UF21] cnt</returns>
    Task<byte[]> GetUF5UF21Stat(CancellationToken cancel = default);
    
    /// <summary>
    /// RX UF11/UFxx Message Statistic
    /// </summary>
    /// <param name="cancel"></param>
    /// <returns>[UF11, UFxx] cnt</returns>
    Task<byte[]> GetUF11AnyUFStat(CancellationToken cancel = default);
    
    /// <summary>
    /// TX DF4/DF5 Message Statistic
    /// </summary>
    /// <param name="cancel"></param>
    /// <returns>[DF4, DF5] cnt</returns>
    Task<byte[]> GetDF4DF5Stat(CancellationToken cancel = default);

    /// <summary>
    /// RX UF4/UF5/UF20/UF21/UF11 Message Statistic
    /// TX DF4/DF5/DF20/DF21/DF11 Message Statistic
    /// </summary>
    /// <param name="cancel"></param>
    /// <returns>[UF4, UF5, UF20, UF21, UF11, DF4, DF5, DF20, DF21, DF11] cnt</returns>
    Task<(byte UF4, byte UF5, byte UF20, byte UF21, byte UF11, byte UFxx, byte DF4, byte DF5, byte DF20, byte DF21, byte
        DF11)> GetAllStat(CancellationToken cancel = default);
    
    /// <summary>
    /// TX DF20/DF21 Message Statistic
    /// </summary>
    /// <param name="cancel"></param>
    /// <returns>[DF20, DF21] cnt</returns>
    Task<byte[]> GetDF20DF21Stat(CancellationToken cancel = default);
    
    /// <summary>
    /// TX DF11/Reserve Message Statistic
    /// </summary>
    /// <param name="cancel"></param>
    /// <returns>[DF11, 0x00] cnt</returns>
    Task<byte[]> GetDF11ReserveStat(CancellationToken cancel = default);

    /// <summary>
    /// Update All DF messages
    /// </summary>
    /// <param name="df11">New df11 message</param>
    /// <param name="df4">New df4 message</param>
    /// <param name="df5">New df5 message</param>
    /// <param name="bds10">New bds10 message</param>
    /// <param name="bds17">New bds17 message</param>
    /// <param name="bds20">New bds20 message</param>
    /// <param name="bds30">New bds30 message</param>
    /// <param name="bds40">New bds40 message</param>
    /// <param name="bds50">New bds50 message</param>
    /// <param name="bds60">New bds60 message</param>
    /// <param name="df17Id">New df17Id message</param>
    /// <param name="cancel"></param>
    
    /// <returns></returns>
    Task InitAllMessage(ReadOnlySpan<byte> df11, ReadOnlySpan<byte> df4, ReadOnlySpan<byte> df5,
        ReadOnlySpan<byte> bds10, ReadOnlySpan<byte> bds17, ReadOnlySpan<byte> bds20, ReadOnlySpan<byte> bds30, ReadOnlySpan<byte> bds40, ReadOnlySpan<byte> bds50, ReadOnlySpan<byte> bds60, CancellationToken cancel);
    
    Task<(byte, byte)> GetRecognizedAndAllRequestsCountPerSecond(CancellationToken cancel = default);
    
    // public bool IsDisposed { get; }
}



public class LimeSdrModeSRepDevice : LimeSdrModeACRepDevice, ILimeSdrModeSRepDevice
{
    private const ushort ADSB_MASK = 0x0070;
    private const ushort MODES_LEVEL1_MASK = 0x000F;
    private const ushort MODES_LEVEL2_MASK = 0x0300;
    
    private const ushort CONTROL_DF_Address = 0x0046;
    private const ushort UF4_UF20_CNT_Address  = 0x0047; // Address for UF4 Cnt (7:0) | UF20 Cnt (7:0)
    private const ushort UF5_UF21_CNT_Address  = 0x0048; // Address for UF5 Cnt (7:0) | UF21 Cnt (7:0)
    private const ushort UF11_UFxx_CNT_Address = 0x0049; // Address for UF11 Cnt (7:0) | UFxx Cnt (7:0)
    private const ushort DF4_DF5_CNT_Address   = 0x004A; // Address for DF4 Cnt (7:0) | DF5 Cnt (7:0)
    private const ushort DF11_CNT_Address      = 0x004B; // Address for DF11 Cnt (7:0) | 0x00 (7:0)
    private const ushort DF20_DF21_CNT_Address = 0x004C; // Address for DF20 Cnt (7:0) | DF21 Cnt (7:0) 0x00DE
    
    #region Internal registers

    // Write
    private const ushort DF11_55_40_InternAddr          = 0x0000; // DF11(55:40)
    private const ushort DF11_39_24_InternAddr          = 0x0001; // DF11(39:24)
    
    private const ushort DF5_55_40_InternAddr           = 0x0002; // DF5 (55:40)
    private const ushort DF5_39_24_InternAddr           = 0x0003; // DF5 (39:24)
    
    private const ushort DF4_55_40_InternAddr           = 0x0004; // DF4 (55:40)
    private const ushort DF4_39_24_InternAddr           = 0x0005; // DF4 (39:24)
    
    
    private const ushort DelayDF_InternAddr             = 0x0016; // Delay DF Reply
    private const ushort UFxxType_InternAddr            = 0x0017; // UFxx output message type registers
    
    private const ushort RecReqCnt_7_0_AllReqCnt_7_0_InternAddr= 0x0045; // Recognized Request count per/sec(7:0) & All Request count per/sec(7:0)
    
    private const ushort BDS10_79_64_InternAddr         = 0x0039; // BDS10(79:64)
    private const ushort BDS10_63_48_InternAddr         = 0x003A; // BDS10(63:48)
    private const ushort BDS10_47_32_InternAddr         = 0x003B; // BDS10(47:32)
    private const ushort BDS10_31_24_InternAddr         = 0x003C; // BDS10(31:24)
    
    private const ushort BDS17_79_64_InternAddr         = 0x003D; // BDS17(79:64)
    private const ushort BDS17_63_48_InternAddr         = 0x003E; // BDS17(63:48)
    private const ushort BDS17_47_32_InternAddr         = 0x003F; // BDS17(47:32)
    private const ushort BDS17_31_24_InternAddr         = 0x0040; // BDS17(31:24)
    
    private const ushort BDS20_79_64_InternAddr         = 0x0041; // BDS20(79:64)
    private const ushort BDS20_63_48_InternAddr         = 0x0042; // BDS20(63:48)
    private const ushort BDS20_47_32_InternAddr         = 0x0043; // BDS20(47:32)
    private const ushort BDS20_31_24_InternAddr         = 0x0044; // BDS20(31:24)
    
    private const ushort BDS30_79_64_InternAddr         = 0x0052; // BDS30(79:64)
    private const ushort BDS30_63_48_InternAddr         = 0x0053; // BDS30(63:48)
    private const ushort BDS30_47_32_InternAddr         = 0x0054; // BDS30(47:32)
    private const ushort BDS30_31_24_InternAddr         = 0x0055; // BDS30(31:24)
    
    private const ushort BDS40_79_64_InternAddr         = 0x0056; // BDS40(79:64)
    private const ushort BDS40_63_48_InternAddr         = 0x0057; // BDS40(63:48)
    private const ushort BDS40_47_32_InternAddr         = 0x0058; // BDS40(47:32)
    private const ushort BDS40_31_24_InternAddr         = 0x0059; // BDS40(31:24)
    
    private const ushort BDS50_79_64_InternAddr         = 0x005A; // BDS50(79:64)
    private const ushort BDS50_63_48_InternAddr         = 0x005B; // BDS50(63:48)
    private const ushort BDS50_47_32_InternAddr         = 0x005C; // BDS50(47:32)
    private const ushort BDS50_31_24_InternAddr         = 0x005D; // BDS50(31:24)
    
    private const ushort BDS60_79_64_InternAddr         = 0x005E; // BDS60(79:64)
    private const ushort BDS60_63_48_InternAddr         = 0x005F; // BDS60(63:48)
    private const ushort BDS60_47_32_InternAddr         = 0x0060; // BDS60(47:32)
    private const ushort BDS60_31_24_InternAddr         = 0x0061; // BDS60(31:24)
    
    // Read
    private const ushort UF11_55_40_InternAddr          = 0x0018; // UF11(55:40)
    private const ushort UF11_39_24_InternAddr          = 0x0019; // UF11(39:24)
    private const ushort UF11_23_08_InternAddr          = 0x001A; // UF11(23:8)
    private const ushort UF11_07_00_InternAddr          = 0x001B; // UF11(7:0) & "00"
    
    private const ushort UF5_55_40_InternAddr           = 0x001C; // UF5(55:40)
    private const ushort UF5_39_24_InternAddr           = 0x001D; // UF5(39:24)
    private const ushort UF5_23_08_InternAddr           = 0x001E; // UF5(23:8)
    private const ushort UF5_07_00_InternAddr           = 0x001F; // UF5(7:0) & "00"
    
    private const ushort UF4_55_40_InternAddr           = 0x0020; // UF4(55:40)
    private const ushort UF4_39_24_InternAddr           = 0x0021; // UF4(39:24)
    private const ushort UF4_23_08_InternAddr           = 0x0022; // UF4(23:8)
    private const ushort UF4_07_00_InternAddr           = 0x0023; // UF4(7:0) & "00"
    
    private const ushort UFxx_111_96_InternAddr         = 0x0024; // UFxx(111:96)
    private const ushort UFxx_95_80_InternAddr          = 0x0025; // UFxx(95:80)
    private const ushort UFxx_79_64_InternAddr          = 0x0026; // UFxx(79:64)
    private const ushort UFxx_63_48_InternAddr          = 0x0027; // UFxx(63:48)
    private const ushort UFxx_47_32_InternAddr          = 0x0028; // UFxx(47:32)
    private const ushort UFxx_31_16_InternAddr          = 0x0029; // UFxx(31:16)
    private const ushort UFxx_15_0_InternAddr           = 0x002A; // UFxx(15:0)
    
    private const ushort UF20_111_96_InternAddr         = 0x002B; // UF20(111:96)
    private const ushort UF20_95_80_InternAddr          = 0x002C; // UF20(95:80)
    private const ushort UF20_79_64_InternAddr          = 0x002D; // UF20(79:64)
    private const ushort UF20_63_48_InternAddr          = 0x002E; // UF20(63:48)
    private const ushort UF20_47_32_InternAddr          = 0x002F; // UF20(47:32)
    private const ushort UF20_31_16_InternAddr          = 0x0030; // UF20(31:16)
    private const ushort UF20_15_0_InternAddr           = 0x0031; // UF20(15:0)
    
    private const ushort UF21_111_96_InternAddr         = 0x0032; // UF21(111:96)
    private const ushort UF21_95_80_InternAddr          = 0x0033; // UF21(95:80)
    private const ushort UF21_79_64_InternAddr          = 0x0034; // UF21(79:64)
    private const ushort UF21_63_48_InternAddr          = 0x0035; // UF21(63:48)
    private const ushort UF21_47_32_InternAddr          = 0x0036; // UF21(47:32)
    private const ushort UF21_31_16_InternAddr          = 0x0037; // UF21(31:16)
    private const ushort UF21_15_0_InternAddr           = 0x0038; // UF21(15:0)
    
    #endregion
    
    public LimeSdrModeSRepDevice(string deviceId, LimeSdrDeviceConfig config, CapabilityEnum capability, bool isAdsbEnabled = true, ILogger? logger = null) : base(deviceId, config, capability, isAdsbEnabled, logger)
    {
    }
    
    protected override CustomWorkMode InternalGetMode()
    {
        return CustomWorkMode.AdsBModeSRep;
    }

    public override async Task TurnOnOffMode(bool enabled, CancellationToken cancel = default)
    {
        if (enabled)
        {
            var val = (ushort)(IsAdsbEnabled ? ADSB_MASK : 0x0);
            val = (ushort)(val | (ushort)(capability == CapabilityEnum.Level1
                ? MODES_LEVEL1_MASK
                : MODES_LEVEL1_MASK | MODES_LEVEL2_MASK));
            await TurnOnMode(cancel);
            await Task.Delay(1000, cancel).ConfigureAwait(false);
            // Включаем ответы на любые запросы DF11, DF4, DF5, если нужно включаем отправку ADS-B. Если уровень выше первого, включаем DF20, DF21
            await WriteCustomRegister(0x0046, val, cancel).ConfigureAwait(false);
            await SetCapability(capability, cancel).ConfigureAwait(false);
        }
        else
        {
            await TurnOffMode(cancel).ConfigureAwait(false);
        }
    }

    public override async Task SetCapability(CapabilityEnum ca, CancellationToken cancel = default)
    {
        await base.SetCapability(ca, cancel).ConfigureAwait(false);
        await SetDf20IsEnabled(ca != CapabilityEnum.Level1, cancel).ConfigureAwait(false);
        await SetDf21IsEnabled(ca != CapabilityEnum.Level1, cancel).ConfigureAwait(false);
    }

    public Task SetDf11ReplyIsEnabled(bool enabled, CancellationToken cancel = default)
    {
        logger.ZLogDebug($"Setting ADS-B DF11 Reply to {enabled}");
        return this.WriteCustomRegisterBits(CONTROL_DF_Address, 0, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task SetDf11BroadcastIsEnabled(bool enabled, CancellationToken cancel = default)
    {
        logger.ZLogDebug($"Setting ADS-B DF11 Squitter to {enabled}");
        return this.WriteCustomRegisterBits(CONTROL_DF_Address, 1, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task SetDf4IsEnabled(bool enabled, CancellationToken cancel = default)
    {
        logger.ZLogDebug($"Setting ADS-B DF4 Reply to {enabled}");
        return this.WriteCustomRegisterBits(CONTROL_DF_Address, 3, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task SetDf5IsEnabled(bool enabled, CancellationToken cancel = default)
    {
        logger.ZLogDebug($"Setting ADS-B DF5 Reply to {enabled}");
        return this.WriteCustomRegisterBits(CONTROL_DF_Address, 2, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task SetDf20IsEnabled(bool enabled, CancellationToken cancel = default)
    {
        logger.ZLogDebug($"Setting ADS-B DF20 Reply to {enabled}");
        return this.WriteCustomRegisterBits(CONTROL_DF_Address, 8, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task SetDf21IsEnabled(bool enabled, CancellationToken cancel = default)
    {
        logger.ZLogDebug($"Setting ADS-B DF21 Reply to {enabled}");
        return this.WriteCustomRegisterBits(CONTROL_DF_Address, 9, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task SetNotOurUFsOutput(bool enabled, CancellationToken cancel = default)
    {
        return this.WriteCustomRegisterBits(CONTROL_DF_Address, 7, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public async Task<byte[]> ReadAnyUFMessage(CancellationToken cancel = default)
    {
        var result = new byte[14];

        var addrs = new[]
        {
            UFxx_111_96_InternAddr, UFxx_95_80_InternAddr, UFxx_79_64_InternAddr, UFxx_63_48_InternAddr,
            UFxx_47_32_InternAddr, UFxx_31_16_InternAddr, UFxx_15_0_InternAddr
        };
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);

        for (var i = 0; i < values.Length; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }

        return result;
    }

    public async Task<byte[]> ReadUF20Message(CancellationToken cancel = default)
    {
        var result = new byte[14];

        var addrs = new[]
        {
            UF20_111_96_InternAddr, UF20_95_80_InternAddr, UF20_79_64_InternAddr, UF20_63_48_InternAddr,
            UF20_47_32_InternAddr, UF20_31_16_InternAddr, UF20_15_0_InternAddr
        };
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);

        for (var i = 0; i < values.Length; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }

        return result;
    }

    public async Task<byte[]> ReadUF21Message(CancellationToken cancel = default)
    {
        var result = new byte[14];

        var addrs = new[]
        {
            UF21_111_96_InternAddr, UF21_95_80_InternAddr, UF21_79_64_InternAddr, UF21_63_48_InternAddr,
            UF21_47_32_InternAddr, UF21_31_16_InternAddr, UF21_15_0_InternAddr
        };
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);

        for (var i = 0; i < values.Length; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }

        return result;
    }

    public async Task<byte[]> ReadUF4Message(CancellationToken cancel = default)
    {
        var result = new byte[7];

        var addrs = new[]
            { UF4_55_40_InternAddr, UF4_39_24_InternAddr, UF4_23_08_InternAddr, UF4_07_00_InternAddr };
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        result[0] = (byte)((values[0] >> 8) & 0xFF);
        result[1] = (byte)(values[0] & 0xFF);
        result[2] = (byte)((values[1] >> 8) & 0xFF);
        result[3] = (byte)(values[1] & 0xFF);
        result[4] = (byte)((values[2] >> 8) & 0xFF);
        result[5] = (byte)(values[2] & 0xFF);
        result[6] = (byte)((values[3] >> 8) & 0xFF);

        return result;
    }

    public async Task<byte[]> ReadUF5Message(CancellationToken cancel = default)
    {
        var result = new byte[7];

        var addrs = new[]
            { UF5_55_40_InternAddr, UF5_39_24_InternAddr, UF5_23_08_InternAddr, UF5_07_00_InternAddr };
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        result[0] = (byte)((values[0] >> 8) & 0xFF);
        result[1] = (byte)(values[0] & 0xFF);
        result[2] = (byte)((values[1] >> 8) & 0xFF);
        result[3] = (byte)(values[1] & 0xFF);
        result[4] = (byte)((values[2] >> 8) & 0xFF);
        result[5] = (byte)(values[2] & 0xFF);
        result[6] = (byte)((values[3] >> 8) & 0xFF);

        return result;
    }

    public async Task<byte[]> ReadUF11Message(CancellationToken cancel = default)
    {
        var result = new byte[7];

        var addrs = new[]
            { UF11_55_40_InternAddr, UF11_39_24_InternAddr, UF11_23_08_InternAddr, UF11_07_00_InternAddr };
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        result[0] = (byte)((values[0] >> 8) & 0xFF);
        result[1] = (byte)(values[0] & 0xFF);
        result[2] = (byte)((values[1] >> 8) & 0xFF);
        result[3] = (byte)(values[1] & 0xFF);
        result[4] = (byte)((values[2] >> 8) & 0xFF);
        result[5] = (byte)(values[2] & 0xFF);
        result[6] = (byte)((values[3] >> 8) & 0xFF);

        return result;
    }

    public async Task<(byte[] UF4, byte[] UF5, byte[] UF20, byte[] UF21, byte[] UF11)> ReadAllUFMessage(CancellationToken cancel = default)
    {
        var addrs = new[]
        {
            UF4_55_40_InternAddr, UF4_39_24_InternAddr, UF4_23_08_InternAddr, UF4_07_00_InternAddr,
            UF5_55_40_InternAddr, UF5_39_24_InternAddr, UF5_23_08_InternAddr, UF5_07_00_InternAddr,
            UF20_111_96_InternAddr, UF20_95_80_InternAddr, UF20_79_64_InternAddr, UF20_63_48_InternAddr,
            UF20_47_32_InternAddr, UF20_31_16_InternAddr, UF20_15_0_InternAddr,
            UF21_111_96_InternAddr, UF21_95_80_InternAddr, UF21_79_64_InternAddr, UF21_63_48_InternAddr,
            UF21_47_32_InternAddr, UF21_31_16_InternAddr, UF21_15_0_InternAddr,
            UF11_55_40_InternAddr, UF11_39_24_InternAddr, UF11_23_08_InternAddr, UF11_07_00_InternAddr
        };
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        
        var uf4 = new byte[7];
        var uf5 = new byte[7];
        var uf20 = new byte[14];
        var uf21 = new byte[14];
        var uf11 = new byte[7];
        uf4[0] = (byte)((values[0] >> 8) & 0xFF);
        uf4[1] = (byte)(values[0] & 0xFF);
        uf4[2] = (byte)((values[1] >> 8) & 0xFF);
        uf4[3] = (byte)(values[1] & 0xFF);
        uf4[4] = (byte)((values[2] >> 8) & 0xFF);
        uf4[5] = (byte)(values[2] & 0xFF);
        uf4[6] = (byte)((values[3] >> 8) & 0xFF);

        uf5[0] = (byte)((values[4] >> 8) & 0xFF);
        uf5[1] = (byte)(values[4] & 0xFF);
        uf5[2] = (byte)((values[5] >> 8) & 0xFF);
        uf5[3] = (byte)(values[5] & 0xFF);
        uf5[4] = (byte)((values[6] >> 8) & 0xFF);
        uf5[5] = (byte)(values[6] & 0xFF);
        uf5[6] = (byte)((values[7] >> 8) & 0xFF);
        
        for (var i = 0; i < 7; i++)
        {
            uf20[2 * i] = (byte)((values[i + 8] >> 8) & 0xFF);
            uf20[2 * i + 1] = (byte)(values[i + 8] & 0xFF);
        }
        
        for (var i = 0; i < 7; i++)
        {
            uf21[2 * i] = (byte)((values[i + 15] >> 8) & 0xFF);
            uf21[2 * i + 1] = (byte)(values[i + 15] & 0xFF);
        }
        
        uf11[0] = (byte)((values[22] >> 8) & 0xFF);
        uf11[1] = (byte)(values[22] & 0xFF);
        uf11[2] = (byte)((values[23] >> 8) & 0xFF);
        uf11[3] = (byte)(values[23] & 0xFF);
        uf11[4] = (byte)((values[24] >> 8) & 0xFF);
        uf11[5] = (byte)(values[24] & 0xFF);
        uf11[6] = (byte)((values[25] >> 8) & 0xFF);
        
        return (uf4, uf5, uf20, uf21, uf11);
    }

    public Task SetAnyUFType(ushort type, CancellationToken cancel)
    {
        var frame = new[] { new ValueTuple<ushort, ushort>(UFxxType_InternAddr, type)};
        return WriteCustomRegistersFrame(frame, cancel);
    }

    public Task<ushort> GetAnyUFType(CancellationToken cancel)
    {
        return ReadCustomRegister(UFxxType_InternAddr, cancel);
    }

    public Task WriteDF4Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B DF4 message. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }

        var frame = new ValueTuple<ushort, ushort>[2];
        frame[0] = new ValueTuple<ushort, ushort>(DF4_55_40_InternAddr, (ushort)((message.Span[0] << 8) | message.Span[1]));
        frame[1] = new ValueTuple<ushort, ushort>(DF4_39_24_InternAddr, (ushort)((message.Span[2] << 8) | message.Span[3]));
        return WriteCustomRegistersFrame(frame, cancel);
    }

    public async Task<byte[]> ReadDF4Message(CancellationToken cancel = default)
    {
        var addrs = new[] { DF4_55_40_InternAddr, DF4_39_24_InternAddr};
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[values.Length * 2];
        for (var i = 0; i < values.Length; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }
        return result;
    }

    public Task WriteDF5Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B DF5 message. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }

        var frame = new ValueTuple<ushort, ushort>[2];
        frame[0] = new ValueTuple<ushort, ushort>(DF5_55_40_InternAddr, (ushort)((message.Span[0] << 8) | message.Span[1]));
        frame[1] = new ValueTuple<ushort, ushort>(DF5_39_24_InternAddr, (ushort)((message.Span[2] << 8) | message.Span[3]));
        return WriteCustomRegistersFrame(frame, cancel);
    }
    
    public async Task<byte[]> ReadDF5Message(CancellationToken cancel = default)
    {
        var addrs = new[] { DF5_55_40_InternAddr, DF5_39_24_InternAddr};
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[values.Length * 2];
        for (var i = 0; i < values.Length; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }
        return result;
    }

    public Task WriteBDS10Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B BDS10 register. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }

        var frame = new ValueTuple<ushort, ushort>[4];
        frame[0] = new ValueTuple<ushort, ushort>(BDS10_79_64_InternAddr, (ushort)((message.Span[0] << 8) | message.Span[1]));
        frame[1] = new ValueTuple<ushort, ushort>(BDS10_63_48_InternAddr, (ushort)((message.Span[2] << 8) | message.Span[3]));
        frame[2] = new ValueTuple<ushort, ushort>(BDS10_47_32_InternAddr, (ushort)((message.Span[4] << 8) | message.Span[5]));
        frame[4] = new ValueTuple<ushort, ushort>(BDS10_31_24_InternAddr, (ushort)((message.Span[6] << 8) | 0x0));
        return WriteCustomRegistersFrame(frame, cancel);
    }

    public Task WriteBDS17Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B BDS17 register. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }

        var frame = new ValueTuple<ushort, ushort>[4];
        frame[0] = new ValueTuple<ushort, ushort>(BDS17_79_64_InternAddr, (ushort)((message.Span[0] << 8) | message.Span[1]));
        frame[1] = new ValueTuple<ushort, ushort>(BDS17_63_48_InternAddr, (ushort)((message.Span[2] << 8) | message.Span[3]));
        frame[2] = new ValueTuple<ushort, ushort>(BDS17_47_32_InternAddr, (ushort)((message.Span[4] << 8) | message.Span[5]));
        frame[4] = new ValueTuple<ushort, ushort>(BDS17_31_24_InternAddr, (ushort)((message.Span[6] << 8) | 0x0));
        return WriteCustomRegistersFrame(frame, cancel);
    }

    public async Task WriteBDS20Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B BDS20 register. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return;
        }
        
        var frame = new ValueTuple<ushort, ushort>[4];
        frame[0] = new ValueTuple<ushort, ushort>(BDS20_79_64_InternAddr, (ushort)((message.Span[0] << 8) | message.Span[1]));
        frame[1] = new ValueTuple<ushort, ushort>(BDS20_63_48_InternAddr, (ushort)((message.Span[2] << 8) | message.Span[3]));
        frame[2] = new ValueTuple<ushort, ushort>(BDS20_47_32_InternAddr, (ushort)((message.Span[4] << 8) | message.Span[5]));
        frame[3] = new ValueTuple<ushort, ushort>(BDS20_31_24_InternAddr, (ushort)(message.Span[6] << 8));
        await WriteCustomRegistersFrame(frame, cancel);
    }

    public async Task WriteBDS30Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B BDS30 register. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return;
        }
        
        var frame = new ValueTuple<ushort, ushort>[4];
        frame[0] = new ValueTuple<ushort, ushort>(BDS30_79_64_InternAddr, (ushort)((message.Span[0] << 8) | message.Span[1]));
        frame[1] = new ValueTuple<ushort, ushort>(BDS30_63_48_InternAddr, (ushort)((message.Span[2] << 8) | message.Span[3]));
        frame[2] = new ValueTuple<ushort, ushort>(BDS30_47_32_InternAddr, (ushort)((message.Span[4] << 8) | message.Span[5]));
        frame[3] = new ValueTuple<ushort, ushort>(BDS30_31_24_InternAddr, (ushort)(message.Span[6] << 8));
        await WriteCustomRegistersFrame(frame, cancel);
    }

    public async Task<byte[]> ReadBDS10Message(CancellationToken cancel = default)
    {
        var addrs = new[]
        {
            BDS10_79_64_InternAddr, BDS10_63_48_InternAddr, BDS10_47_32_InternAddr, BDS10_31_24_InternAddr,
        };
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[7];
        for (var i = 0; i < 3; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }
        result[6] = (byte)((values[3] >> 8) & 0xFF);
        return result;
    }

    public async Task<byte[]> ReadBDS17Message(CancellationToken cancel = default)
    {
        var addrs = new[]
        {
            BDS17_79_64_InternAddr, BDS17_63_48_InternAddr, BDS17_47_32_InternAddr, BDS17_31_24_InternAddr,
        };
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[7];
        for (var i = 0; i < 3; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }
        result[6] = (byte)((values[3] >> 8) & 0xFF);
        return result;
    }

    public async Task<byte[]> ReadBDS20Message(CancellationToken cancel = default)
    {
        var addrs = new[]
        {
            BDS20_79_64_InternAddr, BDS20_63_48_InternAddr, BDS20_47_32_InternAddr, BDS20_31_24_InternAddr,
        };
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[7];

        for (var i = 0; i < 3; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }
        result[6] = (byte)((values[3] >> 8) & 0xFF);
        return result;
    }

    public async Task<byte[]> ReadBDS30Message(CancellationToken cancel = default)
    {
        var addrs = new[]
        {
            BDS30_79_64_InternAddr, BDS30_63_48_InternAddr, BDS30_47_32_InternAddr, BDS30_31_24_InternAddr,
        };
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[7];

        for (var i = 0; i < 3; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }
        result[6] = (byte)((values[3] >> 8) & 0xFF);
        return result;
    }

    public Task WriteBDS40Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B BDS40 register. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }

        var frame = new ValueTuple<ushort, ushort>[4];
        frame[0] = new ValueTuple<ushort, ushort>(BDS40_79_64_InternAddr, (ushort)((message.Span[0] << 8) | message.Span[1]));
        frame[1] = new ValueTuple<ushort, ushort>(BDS40_63_48_InternAddr, (ushort)((message.Span[2] << 8) | message.Span[3]));
        frame[2] = new ValueTuple<ushort, ushort>(BDS40_47_32_InternAddr, (ushort)((message.Span[4] << 8) | message.Span[5]));
        frame[3] = new ValueTuple<ushort, ushort>(BDS40_31_24_InternAddr, (ushort)(message.Span[6] << 8));
        return WriteCustomRegistersFrame(frame, cancel);
    }
    
    public async Task<byte[]> ReadBDS40Message(CancellationToken cancel = default)
    {
        var addrs = new[]
        {
            BDS40_79_64_InternAddr, BDS40_63_48_InternAddr, BDS40_47_32_InternAddr, BDS40_31_24_InternAddr,
        };
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[7];

        for (var i = 0; i < 3; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }
        result[6] = (byte)((values[3] >> 8) & 0xFF);
        return result;
    }
    
    public Task WriteBDS50Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B BDS50 register. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }

        var frame = new ValueTuple<ushort, ushort>[4];
        frame[0] = new ValueTuple<ushort, ushort>(BDS50_79_64_InternAddr, (ushort)((message.Span[0] << 8) | message.Span[1]));
        frame[1] = new ValueTuple<ushort, ushort>(BDS50_63_48_InternAddr, (ushort)((message.Span[2] << 8) | message.Span[3]));
        frame[2] = new ValueTuple<ushort, ushort>(BDS50_47_32_InternAddr, (ushort)((message.Span[4] << 8) | message.Span[5]));
        frame[3] = new ValueTuple<ushort, ushort>(BDS50_31_24_InternAddr, (ushort)(message.Span[6] << 8));
        return WriteCustomRegistersFrame(frame, cancel);
    }
    
    public async Task<byte[]> ReadBDS50Message(CancellationToken cancel = default)
    {
        var addrs = new[]
        {
            BDS50_79_64_InternAddr, BDS50_63_48_InternAddr, BDS50_47_32_InternAddr, BDS50_31_24_InternAddr,
        };
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[7];

        for (var i = 0; i < 3; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }
        result[6] = (byte)((values[3] >> 8) & 0xFF);
        return result;
    }
    
    public Task WriteBDS60Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B BDS60 register. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }

        var frame = new ValueTuple<ushort, ushort>[4];
        frame[0] = new ValueTuple<ushort, ushort>(BDS60_79_64_InternAddr, (ushort)((message.Span[0] << 8) | message.Span[1]));
        frame[1] = new ValueTuple<ushort, ushort>(BDS60_63_48_InternAddr, (ushort)((message.Span[2] << 8) | message.Span[3]));
        frame[2] = new ValueTuple<ushort, ushort>(BDS60_47_32_InternAddr, (ushort)((message.Span[4] << 8) | message.Span[5]));
        frame[3] = new ValueTuple<ushort, ushort>(BDS60_31_24_InternAddr, (ushort)(message.Span[6] << 8));
        return WriteCustomRegistersFrame(frame, cancel);
    }
    
    public async Task<byte[]> ReadBDS60Message(CancellationToken cancel = default)
    {
        var addrs = new[]
        {
            BDS60_79_64_InternAddr, BDS60_63_48_InternAddr, BDS60_47_32_InternAddr, BDS60_31_24_InternAddr,
        };
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[7];

        for (var i = 0; i < 3; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }
        result[6] = (byte)((values[3] >> 8) & 0xFF);
        return result;
    }

    public Task WriteDF11Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B DF11 message. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }

        var frame = new ValueTuple<ushort, ushort>[2];
        frame[0] = new ValueTuple<ushort, ushort>(DF11_55_40_InternAddr, (ushort)((message.Span[0] << 8) | message.Span[1]));
        frame[1] = new ValueTuple<ushort, ushort>(DF11_39_24_InternAddr, (ushort)((message.Span[2] << 8) | message.Span[3]));
        return WriteCustomRegistersFrame(frame, cancel);
    }

    public async Task<byte[]> ReadDF11Message(CancellationToken cancel = default)
    {
        var addrs = new[] { DF11_55_40_InternAddr, DF11_39_24_InternAddr};
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[values.Length * 2];
        for (var i = 0; i < values.Length; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }
        return result;
    }
    
    public Task SetDfDelay(double delayUs, CancellationToken cancel = default)
    {
        var delNorm = (int)Math.Round(delayUs * 40.0);
        if (delNorm > short.MaxValue) delNorm = short.MaxValue;
        if (delNorm < short.MinValue) delNorm = short.MinValue;
        var del = (ushort)delNorm;
        return WriteCustomRegister(DelayDF_InternAddr, del, cancel);
    }
    
    public async Task<double> GetDfDelay(CancellationToken cancel = default)
    {
        return await ReadCustomRegister(DelayDF_InternAddr, cancel) * 0.025;
    }

    public async Task<byte[]> GetUF4UF20Stat(CancellationToken cancel = default)
    {
        var reg = await ReadCustomRegister(UF4_UF20_CNT_Address, cancel).ConfigureAwait(false);
        return [(byte)((reg >> 8) & 0xFF), (byte)(reg & 0xFF)];
    }

    public async Task<byte[]> GetUF5UF21Stat(CancellationToken cancel = default)
    {
        var reg = await ReadCustomRegister(UF5_UF21_CNT_Address, cancel).ConfigureAwait(false);
        return [(byte)((reg >> 8) & 0xFF), (byte)(reg & 0xFF)];
    }

    public async Task<byte[]> GetUF11AnyUFStat(CancellationToken cancel = default)
    {
        var reg = await ReadCustomRegister(UF11_UFxx_CNT_Address, cancel).ConfigureAwait(false);
        return [(byte)((reg >> 8) & 0xFF), (byte)(reg & 0xFF)];
    }

    public async Task<byte[]> GetDF4DF5Stat(CancellationToken cancel = default)
    {
        var reg = await ReadCustomRegister(DF4_DF5_CNT_Address, cancel).ConfigureAwait(false);
        return [(byte)((reg >> 8) & 0xFF), (byte)(reg & 0xFF)];
    }

    public async Task<(byte UF4, byte UF5, byte UF20, byte UF21, byte UF11, byte UFxx, byte DF4, byte DF5, byte DF20, byte DF21, byte
        DF11)> GetAllStat(CancellationToken cancel = default)
    {
        var frame = new[]
        {
            UF4_UF20_CNT_Address, UF5_UF21_CNT_Address, UF11_UFxx_CNT_Address, DF4_DF5_CNT_Address, DF11_CNT_Address, DF20_DF21_CNT_Address
        };
        var regs = await ReadCustomRegistersFrame(frame, cancel).ConfigureAwait(false);
        
        var uf4 = (byte)((regs[0] >> 8) & 0xFF);
        var uf5 = (byte)((regs[1] >> 8) & 0xFF);
        var uf20 = (byte)(regs[0] & 0xFF);
        var uf21 = (byte)(regs[1] & 0xFF);
        var uf11 = (byte)((regs[2] >> 8) & 0xFF);
        var ufxx = (byte)(regs[2] & 0xFF);
        var df4 = (byte)((regs[3] >> 8) & 0xFF);
        var df5 = (byte)(regs[3] & 0xFF);
        var df20 = (byte)((regs[5] >> 8) & 0xFF);
        var df21 = (byte)(regs[5] & 0xFF);
        var df11 = (byte)((regs[4] >> 8) & 0xFF);

        return (uf4, uf5, uf20, uf21, uf11, ufxx, df4, df5, df20, df21, df11);
    }

    public async Task<byte[]> GetDF20DF21Stat(CancellationToken cancel = default)
    {
        var reg = await ReadCustomRegister(DF20_DF21_CNT_Address, cancel).ConfigureAwait(false);
        return [(byte)((reg >> 8) & 0xFF), (byte)(reg & 0xFF)];
    }

    public async Task<byte[]> GetDF11ReserveStat(CancellationToken cancel = default)
    {
        var reg = await ReadCustomRegister(DF11_CNT_Address, cancel).ConfigureAwait(false);
        return [(byte)((reg >> 8) & 0xFF), (byte)(reg & 0xFF)];
    }

    public Task InitAllMessage(ReadOnlySpan<byte> df11, ReadOnlySpan<byte> df4, ReadOnlySpan<byte> df5,
        ReadOnlySpan<byte> bds10, ReadOnlySpan<byte> bds17, ReadOnlySpan<byte> bds20, ReadOnlySpan<byte> bds30,
        ReadOnlySpan<byte> bds40, ReadOnlySpan<byte> bds50, ReadOnlySpan<byte> bds60, CancellationToken cancel)
    {
        if (df11.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B DF11 message. Expected length greater than or equal to 7 bytes, but length {df11.Length} bytes.");
            return Task.CompletedTask;
        }

        var df11Frame = new ValueTuple<ushort, ushort>[2];
        df11Frame[0] = new ValueTuple<ushort, ushort>(DF11_55_40_InternAddr, (ushort)((df11[0] << 8) | df11[1]));
        df11Frame[1] = new ValueTuple<ushort, ushort>(DF11_39_24_InternAddr, (ushort)((df11[2] << 8) | df11[3]));

        if (df4.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B DF4 message. Expected length greater than or equal to 7 bytes, but length {df4.Length} bytes.");
            return Task.CompletedTask;
        }

        var df4Frame = new ValueTuple<ushort, ushort>[2];
        df4Frame[0] = new ValueTuple<ushort, ushort>(DF4_55_40_InternAddr, (ushort)((df4[0] << 8) | df4[1]));
        df4Frame[1] = new ValueTuple<ushort, ushort>(DF4_39_24_InternAddr, (ushort)((df4[2] << 8) | df4[3]));

        if (df5.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B DF5 message. Expected length greater than or equal to 7 bytes, but length {df5.Length} bytes.");
            return Task.CompletedTask;
        }

        var df5Frame = new ValueTuple<ushort, ushort>[2];
        df5Frame[0] = new ValueTuple<ushort, ushort>(DF5_55_40_InternAddr, (ushort)((df5[0] << 8) | df5[1]));
        df5Frame[1] = new ValueTuple<ushort, ushort>(DF5_39_24_InternAddr, (ushort)((df5[2] << 8) | df5[3]));

        if (bds10.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B BDS10 register. Expected length greater than or equal to 7 bytes, but length {bds10.Length} bytes.");
            return Task.CompletedTask;
        }

        var bds10Frame = new ValueTuple<ushort, ushort>[4];
        bds10Frame[0] = new ValueTuple<ushort, ushort>(BDS10_79_64_InternAddr, (ushort)((bds10[0] << 8) | bds10[1]));
        bds10Frame[1] = new ValueTuple<ushort, ushort>(BDS10_63_48_InternAddr, (ushort)((bds10[2] << 8) | bds10[3]));
        bds10Frame[2] = new ValueTuple<ushort, ushort>(BDS10_47_32_InternAddr, (ushort)((bds10[4] << 8) | bds10[5]));
        bds10Frame[3] = new ValueTuple<ushort, ushort>(BDS10_31_24_InternAddr, (ushort)((bds10[6] << 8) | 0x0));

        if (bds17.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B BDS17 register. Expected length greater than or equal to 7 bytes, but length {bds17.Length} bytes.");
            return Task.CompletedTask;
        }
        var bds17Frame = new ValueTuple<ushort, ushort>[4];
        bds17Frame[0] = new ValueTuple<ushort, ushort>(BDS17_79_64_InternAddr, (ushort)((bds17[0] << 8) | bds17[1]));
        bds17Frame[1] = new ValueTuple<ushort, ushort>(BDS17_63_48_InternAddr, (ushort)((bds17[2] << 8) | bds17[3]));
        bds17Frame[2] = new ValueTuple<ushort, ushort>(BDS17_47_32_InternAddr, (ushort)((bds17[4] << 8) | bds17[5]));
        bds17Frame[3] = new ValueTuple<ushort, ushort>(BDS17_31_24_InternAddr, (ushort)((bds17[6] << 8) | 0x0));

        if (bds20.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B BDS20 register. Expected length greater than or equal to 7 bytes, but length {bds20.Length} bytes.");
            return Task.CompletedTask;
        }
        var bds20Frame = new ValueTuple<ushort, ushort>[4];
        bds20Frame[0] = new ValueTuple<ushort, ushort>(BDS20_79_64_InternAddr, (ushort)((bds20[0] << 8) | bds20[1]));
        bds20Frame[1] = new ValueTuple<ushort, ushort>(BDS20_63_48_InternAddr, (ushort)((bds20[2] << 8) | bds20[3]));
        bds20Frame[2] = new ValueTuple<ushort, ushort>(BDS20_47_32_InternAddr, (ushort)((bds20[4] << 8) | bds20[5]));
        bds20Frame[3] = new ValueTuple<ushort, ushort>(BDS20_31_24_InternAddr, (ushort)((bds20[6] << 8) | 0x0));
        
        if (bds30.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B BDS30 register. Expected length greater than or equal to 7 bytes, but length {bds30.Length} bytes.");
            return Task.CompletedTask;
        }
        var bds30Frame = new ValueTuple<ushort, ushort>[4];
        bds30Frame[0] = new ValueTuple<ushort, ushort>(BDS30_79_64_InternAddr, (ushort)((bds30[0] << 8) | bds30[1]));
        bds30Frame[1] = new ValueTuple<ushort, ushort>(BDS30_63_48_InternAddr, (ushort)((bds30[2] << 8) | bds30[3]));
        bds30Frame[2] = new ValueTuple<ushort, ushort>(BDS30_47_32_InternAddr, (ushort)((bds30[4] << 8) | bds30[5]));
        bds30Frame[3] = new ValueTuple<ushort, ushort>(BDS30_31_24_InternAddr, (ushort)((bds30[6] << 8) | 0x0));
        
        if (bds40.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B BDS40 register. Expected length greater than or equal to 7 bytes, but length {bds40.Length} bytes.");
            return Task.CompletedTask;
        }
        var bds40Frame = new ValueTuple<ushort, ushort>[4];
        bds40Frame[0] = new ValueTuple<ushort, ushort>(BDS40_79_64_InternAddr, (ushort)((bds40[0] << 8) | bds40[1]));
        bds40Frame[1] = new ValueTuple<ushort, ushort>(BDS40_63_48_InternAddr, (ushort)((bds40[2] << 8) | bds40[3]));
        bds40Frame[2] = new ValueTuple<ushort, ushort>(BDS40_47_32_InternAddr, (ushort)((bds40[4] << 8) | bds40[5]));
        bds40Frame[3] = new ValueTuple<ushort, ushort>(BDS40_31_24_InternAddr, (ushort)((bds40[6] << 8) | 0x0));

        if (bds50.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B BDS50 register. Expected length greater than or equal to 7 bytes, but length {bds50.Length} bytes.");
            return Task.CompletedTask;
        }
        var bds50Frame = new ValueTuple<ushort, ushort>[4];
        bds50Frame[0] = new ValueTuple<ushort, ushort>(BDS50_79_64_InternAddr, (ushort)((bds50[0] << 8) | bds50[1]));
        bds50Frame[1] = new ValueTuple<ushort, ushort>(BDS50_63_48_InternAddr, (ushort)((bds50[2] << 8) | bds50[3]));
        bds50Frame[2] = new ValueTuple<ushort, ushort>(BDS50_47_32_InternAddr, (ushort)((bds50[4] << 8) | bds50[5]));
        bds50Frame[3] = new ValueTuple<ushort, ushort>(BDS50_31_24_InternAddr, (ushort)((bds50[6] << 8) | 0x0));

        if (bds60.Length != 7)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B BDS60 register. Expected length greater than or equal to 7 bytes, but length {bds60.Length} bytes.");
            return Task.CompletedTask;
        }
        var bds60Frame = new ValueTuple<ushort, ushort>[4];
        bds60Frame[0] = new ValueTuple<ushort, ushort>(BDS60_79_64_InternAddr, (ushort)((bds60[0] << 8) | bds60[1]));
        bds60Frame[1] = new ValueTuple<ushort, ushort>(BDS60_63_48_InternAddr, (ushort)((bds60[2] << 8) | bds60[3]));
        bds60Frame[2] = new ValueTuple<ushort, ushort>(BDS60_47_32_InternAddr, (ushort)((bds60[4] << 8) | bds60[5]));
        bds60Frame[3] = new ValueTuple<ushort, ushort>(BDS60_31_24_InternAddr, (ushort)((bds60[6] << 8) | 0x0));

        // if (df17Id.Length != 14)
        // {
        //     logger.ZLogDebug(
        //         $"Error writing ADS-B DF17 Identification message. Expected length greater than or equal to 14 bytes, but length {df17Id.Length} bytes.");
        //     return Task.CompletedTask;
        // }
        //
        // var df17IdFrame = new ValueTuple<ushort, ushort>[4];
        // df17IdFrame[0] =
        //     new ValueTuple<ushort, ushort>(DF17_ID_79_64_InternAddr, (ushort)((df17Id[4] << 8) | df17Id[5]));
        // df17IdFrame[1] =
        //     new ValueTuple<ushort, ushort>(DF17_ID_63_48_InternAddr, (ushort)((df17Id[6] << 8) | df17Id[7]));
        // df17IdFrame[2] =
        //     new ValueTuple<ushort, ushort>(DF17_ID_47_32_InternAddr, (ushort)((df17Id[8] << 8) | df17Id[9]));
        // df17IdFrame[3] = new ValueTuple<ushort, ushort>(DF17_ID_31_24_InternAddr, (ushort)(df17Id[10] << 8));

        return AtomicEditRegister(edit =>
        {
            foreach (var addressValuePair in df11Frame)
            {
                WriteCustomRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }

            foreach (var addressValuePair in df4Frame)
            {
                WriteCustomRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }

            foreach (var addressValuePair in df5Frame)
            {
                WriteCustomRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }

            foreach (var addressValuePair in bds10Frame)
            {
                WriteCustomRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }
            
            foreach (var addressValuePair in bds17Frame)
            {
                WriteCustomRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }
            
            foreach (var addressValuePair in bds20Frame)
            {
                WriteCustomRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }

            foreach (var addressValuePair in bds30Frame)
            {
                WriteCustomRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }
            
            foreach (var addressValuePair in bds40Frame)
            {
                WriteCustomRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }

            foreach (var addressValuePair in bds50Frame)
            {
                WriteCustomRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }

            foreach (var addressValuePair in bds60Frame)
            {
                WriteCustomRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }

            // foreach (var addressValuePair in df17IdFrame)
            // {
            //     WriteCustomRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            // }

            edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 1, 1, 1);
            edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 1, 1, 0);
        }, cancel);
    }

    public async Task<(byte, byte)> GetRecognizedAndAllRequestsCountPerSecond(CancellationToken cancel = default)
    {
        var reg = await ReadCustomRegister(RecReqCnt_7_0_AllReqCnt_7_0_InternAddr, cancel).ConfigureAwait(false);
        var result = ((byte)(reg >> 8), (byte)(reg & 0xFF));
        return result;
    }

}

