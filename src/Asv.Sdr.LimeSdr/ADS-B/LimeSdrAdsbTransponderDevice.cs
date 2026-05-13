using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZLogger;

namespace Asv.Sdr.LimeSdr;

public interface ILimeSdrAdsbTransponderDevice : ILimeSdrCustomDevice
{
    /// <summary>
    /// Sets the mode.
    /// </summary>
    Task TurnOnOffMode(bool enabled, CancellationToken cancel = default);
    
    /// <summary>
    /// Resets the mode
    /// </summary>
    Task Reset(CancellationToken cancel = default);
    
    /// <summary>
    /// 
    /// </summary>
    Task RfRelaySelectOutput(bool isTx, CancellationToken cancel = default);

    /// <summary>
    /// Set transponder capability
    /// </summary>
    /// <param name="ca">Transponder capability (3 bit)</param>
    /// <param name="cancel"></param>
    Task SetCapability(CapabilityEnum ca, CancellationToken cancel = default);
    
    /// <summary>
    /// Get transponder capability
    /// </summary>
    /// <param name="cancel"></param>
    Task<CapabilityEnum> GetCapability(CancellationToken cancel = default);
    
    /// <summary>
    /// Set ICAO aircraft address
    /// </summary>
    /// <param name="address">ICAO aircraft address (24 bit)</param>
    Task SetIcaoAddress(uint address, CancellationToken cancel = default);
    
    /// <summary>
    /// DF17 Id message
    /// </summary>
    Task<bool> IsDF17IdEnabled(CancellationToken cancel = default);
    
    /// <summary>
    /// DF17 Position message
    /// </summary>
    Task<bool> IsDF17PositionEnabled(CancellationToken cancel = default);
    
    /// <summary>
    /// DF17 Velocity message
    /// </summary>
    Task<bool> IsDF17VelocityEnabled(CancellationToken cancel = default);

    /// <summary>
    /// Turn On/Off DF17 Id message
    /// </summary>
    Task TurnOnOffDF17Id(bool enabled, CancellationToken cancel = default);
    
    /// <summary>
    /// Turn On/Off DF17 Position message
    /// </summary>
    Task TurnOnOffDF17Position(bool enabled, CancellationToken cancel = default);
    
    /// <summary>
    /// Turn On/Off DF17 Velocity message
    /// </summary>
    Task TurnOnOffDF17Velocity(bool enabled, CancellationToken cancel = default);

#if DEBUG
    Task<byte[]> ReadDF17Id(CancellationToken cancel = default);
    Task<(byte[] Even, byte[] Odd)> ReadDF17Position(CancellationToken cancel = default);
    Task<byte[]> ReadDF17Velocity(CancellationToken cancel = default);
#endif
    
    Task WriteDF17Id(ReadOnlyMemory<byte> message, CancellationToken cancel = default);
    Task WriteDF17Position(ReadOnlyMemory<byte> evenMessage, ReadOnlyMemory<byte> oddMessage,
        CancellationToken cancel = default);
    Task WriteDF17Velocity(ReadOnlyMemory<byte> message, CancellationToken cancel = default);

}


public class LimeSdrAdsbTransponderDevice : LimeSdrCustomDevice, ILimeSdrAdsbTransponderDevice
{
    private readonly LimeSdrDeviceConfig _config;
    protected CapabilityEnum capability;
    protected readonly ILogger logger;

    private const ushort CONTROL_DF_Address = 0x0046;
    
    // DF (down)
    private const ushort DF11_55_40_InternAddr          = 0x0000; // DF11(55:40)
    private const ushort DF11_39_24_InternAddr          = 0x0001; // DF11(39:24)
    
    protected const ushort DF17_ID_79_64_InternAddr       = 0x0006; // DF17_ID(79:64)
    protected const ushort DF17_ID_63_48_InternAddr       = 0x0007; // DF17_ID(63:48)
    protected const ushort DF17_ID_47_32_InternAddr       = 0x0008; // DF17_ID(47:32)
    protected const ushort DF17_ID_31_24_InternAddr       = 0x0009; // DF17_ID(31:24) & "00"
    
    private const ushort DF17_POS_EVEN_79_64_InternAddr = 0x000A; // DF17_POS_EVEN(79:64)
    private const ushort DF17_POS_EVEN_63_48_InternAddr = 0x000B; // DF17_POS_EVEN(63:48)
    private const ushort DF17_POS_EVEN_47_32_InternAddr = 0x000C; // DF17_POS_EVEN(47:32)
    private const ushort DF17_POS_EVEN_31_24_InternAddr = 0x000D; // DF17_POS_EVEN(31:24) & "00"
    
    private const ushort DF17_POS_ODD_79_64_InternAddr  = 0x000E; // DF17_POS_ODD(79:64)
    private const ushort DF17_POS_ODD_63_48_InternAddr  = 0x000F; // DF17_POS_ODD(63:48)
    private const ushort DF17_POS_ODD_47_32_InternAddr  = 0x0010; // DF17_POS_ODD(47:32)
    private const ushort DF17_POS_ODD_31_24_InternAddr  = 0x0011; // DF17_POS_ODD(31:24) & "00"
    
    private const ushort DF17_VLS_79_64_InternAddr      = 0x0012; // DF17_VLS(79:64)
    private const ushort DF17_VLS_63_48_InternAddr      = 0x0013; // DF17_VLS(63:48)
    private const ushort DF17_VLS_47_32_InternAddr      = 0x0014; // DF17_VLS(47:32)
    private const ushort DF17_VLS_31_24_InternAddr      = 0x0015; // DF17_VLS(31:24) & "00"

    public LimeSdrAdsbTransponderDevice(string deviceId, LimeSdrDeviceConfig config, CapabilityEnum capability = CapabilityEnum.Level1, ILogger? logger = null) : base(deviceId, logger)
    {
        _config = config;
        this.capability = capability;
        this.logger = logger ?? NullLogger.Instance;
    }

    protected override CustomWorkMode InternalGetMode()
    {
        return CustomWorkMode.AdsBModeSRep;
    }


    public virtual async Task TurnOnOffMode(bool enabled, CancellationToken cancel = default)
    {
        if (enabled)
        {
            // Выключаем ответы на любые запросы, оставляем включенным только отправку ADS-B
            await WriteCustomRegister(CONTROL_DF_Address, 0x70, cancel).ConfigureAwait(false);
            await TurnOnMode(cancel);
            await Task.Delay(500, cancel).ConfigureAwait(false);
            // На всякий пож еще раз: Выключаем ответы на любые запросы, оставляем включенным только отправку ADS-B
            await WriteCustomRegister(CONTROL_DF_Address, 0x70, cancel).ConfigureAwait(false);
            await SetCapability(capability, cancel).ConfigureAwait(false);
        }
        else
        {
            await TurnOffMode(cancel).ConfigureAwait(false);
        }
    }

    public async Task RfRelaySelectOutput(bool isTx, CancellationToken cancel = default)
    {
        var value = isTx ? !_config.RfRelayRxIsHigh : _config.RfRelayRxIsHigh;
        logger?.ZLogDebug($"Setting RF relay to {(isTx ? "TX" : "RX")} (GPIO[{_config.RfRelayGpio}]={value})");
        var arr = new byte[1] ;
        await ReadGpioDirection(arr, cancel);
        var dir = arr[0];
        await ReadGpio(arr, cancel);
        var gpio = arr[0];
        var dirMask = (byte)(1 << _config.RfRelayGpio);
        arr[0] = (byte)(dir | dirMask);
        await WriteGpioDirection(arr, cancel);
        
        if (value)
        {
            arr[0] = (byte)(gpio | dirMask);
        }
        else
        {
            arr[0] = (byte)(gpio & ~dirMask);
        }
        await WriteGpio(arr, cancel);
    }

    public Task SetCapability(CapabilityEnum ca, CancellationToken cancel = default)
    {
        capability = ca;
        var rawCa = (byte)TransponderHelper.SetCapability(ca);
        var reg = (ushort)((0xB << 3) | rawCa);
        return WriteCustomRegisterBits(DF11_55_40_InternAddr, 8, 8, reg, cancel);
    }

    public async Task<CapabilityEnum> GetCapability(CancellationToken cancel = default)
    {
        var reg = await ReadCustomRegisterBits(DF11_55_40_InternAddr, 8, 8, cancel);
        var ca = reg & 0x7;
        return TransponderHelper.GetCapability(ca);
    }

    public async Task SetIcaoAddress(uint address, CancellationToken cancel = default)
    {
        if ((address & 0x00FFFFFF) != 0) throw new ArgumentOutOfRangeException(nameof(address));
        
        var reg1 = await ReadCustomRegister(DF11_55_40_InternAddr, cancel).ConfigureAwait(false);
        reg1 &= 0x0700; // save previous capability
        reg1 |= 0xB << 11; // DF11 number
        reg1 |= (ushort)((address & 0x00FF0000) >> 16); // upper 8 bits of the address
        var reg2 = (ushort)(address & 0x0000FFFF); // lower 16 bits of the address
        await WriteCustomRegistersFrame(
        [
            new ValueTuple<ushort, ushort>(DF11_55_40_InternAddr, reg1),
            new ValueTuple<ushort, ushort>(DF11_39_24_InternAddr, reg2)
        ], cancel).ConfigureAwait(false);

    }

    public async Task<bool> IsDF17IdEnabled(CancellationToken cancel = default)
    {
        var reg = await ReadCustomRegisterBits(0x0046, 4, 1, cancel).ConfigureAwait(false);
        return reg != 0;
    }

    public async Task<bool> IsDF17PositionEnabled(CancellationToken cancel = default)
    {
        var reg = await ReadCustomRegisterBits(0x0046, 5, 1, cancel).ConfigureAwait(false);
        return reg != 0;
    }

    public async Task<bool> IsDF17VelocityEnabled(CancellationToken cancel = default)
    {
        var reg = await ReadCustomRegisterBits(0x0046, 6, 1, cancel).ConfigureAwait(false);
        return reg != 0;
    }

    public Task TurnOnOffDF17Id(bool enabled, CancellationToken cancel = default)
    {
        return WriteCustomRegisterBits(0x0046, 4, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task TurnOnOffDF17Position(bool enabled, CancellationToken cancel = default)
    {
        return WriteCustomRegisterBits(0x0046, 5, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task TurnOnOffDF17Velocity(bool enabled, CancellationToken cancel = default)
    {
        return WriteCustomRegisterBits(0x0046, 6, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    #region Internal

    protected async Task<byte[]> InternalReadDF11(CancellationToken cancel = default)
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
    
    protected Task InternalWriteDF11(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
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

    #endregion
    
    public async Task<byte[]> ReadDF17Id(CancellationToken cancel = default)
    {
        var df11 = await InternalReadDF11(cancel).ConfigureAwait(false);
        df11[0] &= 0x7;
        df11[0] |= 0x11 << 3; // change DF11 to DF17

        var addrs = new[] { DF17_ID_79_64_InternAddr, DF17_ID_63_48_InternAddr, DF17_ID_47_32_InternAddr, DF17_ID_31_24_InternAddr };
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[11];

        for (var i = 0; i < df11.Length; i++)
        {
            result[i] = df11[i];
        }
        for (var i = 0; i < 3; i++)
        {
            result[2 * (i + 2)] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * (i + 2) + 1] = (byte)(values[i] & 0xFF);
        }

        result[10] = (byte)((values[3] >> 8) & 0xFF);;

        return result;
    }

    public async Task<(byte[] Even, byte[] Odd)> ReadDF17Position(CancellationToken cancel = default)
    {
        var df11 = await InternalReadDF11(cancel).ConfigureAwait(false);
        df11[0] &= 0x7;
        df11[0] |= 0x11 << 3; // change DF11 to DF17

        var addrs = new[]
        {
            DF17_POS_EVEN_79_64_InternAddr, DF17_POS_EVEN_63_48_InternAddr, DF17_POS_EVEN_47_32_InternAddr,
            DF17_POS_EVEN_31_24_InternAddr, DF17_POS_ODD_79_64_InternAddr, DF17_POS_ODD_63_48_InternAddr,
            DF17_POS_ODD_47_32_InternAddr, DF17_POS_ODD_31_24_InternAddr
        };
        
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        var resultEven = new byte[11];
        var resultOdd = new byte[11];

        for (var i = 0; i < df11.Length; i++)
        {
            resultEven[i] = df11[i];
            resultOdd[i] = df11[i];
        }
        for (var i = 0; i < 3; i++)
        {
            resultEven[2 * (i + 2)] = (byte)((values[i] >> 8) & 0xFF);
            resultEven[2 * (i + 2) + 1] = (byte)(values[i] & 0xFF);
            
            resultOdd[2 * (i + 2)] = (byte)((values[i + 4] >> 8) & 0xFF);
            resultOdd[2 * (i + 2) + 1] = (byte)(values[i + 4] & 0xFF);
        }

        resultEven[10] = (byte)((values[3] >> 8) & 0xFF);
        resultOdd[10] = (byte)((values[7] >> 8) & 0xFF);

        return (Even: resultEven, Odd: resultOdd);
    }

    public async Task<byte[]> ReadDF17Velocity(CancellationToken cancel = default)
    {
        var df11 = await InternalReadDF11(cancel).ConfigureAwait(false);
        df11[0] &= 0x7;
        df11[0] |= 0x11 << 3; // change DF11 to DF17

        var addrs = new[] { DF17_VLS_79_64_InternAddr, DF17_VLS_63_48_InternAddr, DF17_VLS_47_32_InternAddr, DF17_VLS_31_24_InternAddr };
        var values = await ReadCustomRegistersFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[11];

        for (var i = 0; i < df11.Length; i++)
        {
            result[i] = df11[i];
        }
        for (var i = 0; i < 3; i++)
        {
            result[2 * (i + 2)] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * (i + 2) + 1] = (byte)(values[i] & 0xFF);
        }

        result[10] = (byte)((values[3] >> 8) & 0xFF);

        return result;
    }

    public async Task WriteDF17Id(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 14)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B DF17 Identification message. Expected length greater than or equal to 14 bytes, but length {message.Length} bytes.");
            return;
        }
        
        var frame = new ValueTuple<ushort, ushort>[4];
        frame[0] = new ValueTuple<ushort, ushort>(DF17_ID_79_64_InternAddr, (ushort)((message.Span[4] << 8) | message.Span[5]));
        frame[1] = new ValueTuple<ushort, ushort>(DF17_ID_63_48_InternAddr, (ushort)((message.Span[6] << 8) | message.Span[7]));
        frame[2] = new ValueTuple<ushort, ushort>(DF17_ID_47_32_InternAddr, (ushort)((message.Span[8] << 8) | message.Span[9]));
        frame[3] = new ValueTuple<ushort, ushort>(DF17_ID_31_24_InternAddr, (ushort)(message.Span[10] << 8));
        await WriteCustomRegistersFrame(frame, cancel).ConfigureAwait(false);
    }

    public async Task WriteDF17Position(ReadOnlyMemory<byte> evenMessage, ReadOnlyMemory<byte> oddMessage, CancellationToken cancel = default)
    {
        if (evenMessage.Length != 14)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B DF17 Position Even message. Expected length greater than or equal to 14 bytes, but length {evenMessage.Length} bytes.");
            return;
        }
        
        if (oddMessage.Length != 14)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B DF17 Position Odd message. Expected length greater than or equal to 14 bytes, but length {oddMessage.Length} bytes.");
            return;
        }
        
        var frame = new ValueTuple<ushort, ushort>[8];
        frame[0] = new ValueTuple<ushort, ushort>(DF17_POS_EVEN_79_64_InternAddr, (ushort)((evenMessage.Span[4] << 8) | evenMessage.Span[5]));
        frame[1] = new ValueTuple<ushort, ushort>(DF17_POS_EVEN_63_48_InternAddr, (ushort)((evenMessage.Span[6] << 8) | evenMessage.Span[7]));
        frame[2] = new ValueTuple<ushort, ushort>(DF17_POS_EVEN_47_32_InternAddr, (ushort)((evenMessage.Span[8] << 8) | evenMessage.Span[9]));
        frame[3] = new ValueTuple<ushort, ushort>(DF17_POS_EVEN_31_24_InternAddr, (ushort)(evenMessage.Span[10] << 8));
        
        frame[4] = new ValueTuple<ushort, ushort>(DF17_POS_ODD_79_64_InternAddr, (ushort)((oddMessage.Span[4] << 8) | oddMessage.Span[5]));
        frame[5] = new ValueTuple<ushort, ushort>(DF17_POS_ODD_63_48_InternAddr, (ushort)((oddMessage.Span[6] << 8) | oddMessage.Span[7]));
        frame[6] = new ValueTuple<ushort, ushort>(DF17_POS_ODD_47_32_InternAddr, (ushort)((oddMessage.Span[8] << 8) | oddMessage.Span[9]));
        frame[7] = new ValueTuple<ushort, ushort>(DF17_POS_ODD_31_24_InternAddr, (ushort)(oddMessage.Span[10] << 8));
        
        await WriteCustomRegistersFrame(frame, cancel).ConfigureAwait(false);
    }

    public async Task WriteDF17Velocity(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 14)
        {
            logger.ZLogDebug(
                $"Error writing ADS-B DF17 Velocity message. Expected length greater than or equal to 14 bytes, but length {message.Length} bytes.");
            return;
        }
        
        var frame = new ValueTuple<ushort, ushort>[4];
        frame[0] = new ValueTuple<ushort, ushort>(DF17_VLS_79_64_InternAddr, (ushort)((message.Span[4] << 8) | message.Span[5]));
        frame[1] = new ValueTuple<ushort, ushort>(DF17_VLS_63_48_InternAddr, (ushort)((message.Span[6] << 8) | message.Span[7]));
        frame[2] = new ValueTuple<ushort, ushort>(DF17_VLS_47_32_InternAddr, (ushort)((message.Span[8] << 8) | message.Span[9]));
        frame[3] = new ValueTuple<ushort, ushort>(DF17_VLS_31_24_InternAddr, (ushort)(message.Span[10] << 8));
        await WriteCustomRegistersFrame(frame, cancel).ConfigureAwait(false);
    }
}
