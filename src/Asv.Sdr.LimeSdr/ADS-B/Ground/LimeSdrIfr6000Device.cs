using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZLogger;

namespace Asv.Sdr.LimeSdr;


public class LimeSdrIfr6000Device : LimeSdrCustomDevice, ILimeSdrIfr6000Device
{
    private readonly LimeSdrDeviceConfig _config;
    private readonly ILogger _logger;
    private int _readDfMsgFlag;
    private const ushort ModeAResp_15_0_InternAddr          = 0x0301; // (0,0,С1,А1,С2,А2,С4,А4,Х,В1,D1,В2,D2,В4,D4,SPI) -- RD
    private const ushort ModeCResp_15_0_InternAddr          = 0x0302; // (0,0,С1,А1,С2,А2,С4,А4,Х,В1,D1,В2,D2,В4,D4,0) -- RD
    private const ushort DelayOffsetAC_15_0_InternAddr      = 0x0303; // калибровочный коэффициент по дальности принятых сообщений A/C, signed -- WR
    private const ushort ReplyRatio_A_15_8_C_7_0_InternAddr = 0x0304; // процент ответов A/C	(по 0,5% т.е. количество ответов на 200 запросов) -- RD
    
    private const ushort P1_P3_SpacingOffset_A_15_8_C_7_0_InternAddr = 0x0305; // Отклонение от кодового расстояния 8/21 мкс кода A/C для запросов, signed, в тактах, один такт 0,025 мкс  -- WR
    
    private const ushort ModeA_C_Control = 0x0306; // 
    
    private const ushort Width_A_F1_15_8_F2_7_0 = 0x0307;  // средняя ширина ответных кадрирующих импульсов для кода A, в тактах, один такт 0,025 мкс	-- RD
    private const ushort Width_C_F1_15_8_F2_7_0 = 0x0308;  // ширина ответных кадрирующих импульсов для кода C, в тактах, один такт 0,025 мкс	-- RD
    private const ushort F1_F2_Spacing_A_15_8_C_7_0 = 0x0309;  // Отклонение ответных кадрирующих импульсов от кодового расстояния 20,3 мкс, signed, в тактах, один такт 0,025 мкс  -- RD
    private const ushort Reply_Delay_A_15_0 = 0x030A;  // Отклонение задержки ответов от нулевой дальности ModeA (нулевая дальность = 3 мкс), signed, в тактах, один такт 0,025 мкс	-- RD
    private const ushort Reply_Jitter_A_15_0 = 0x030B;  // Отклонение задержки ответов от нулевой дальности ModeC (нулевая дальность = 3 мкс), signed, в тактах, один такт 0,025 мкс	-- RD
    private const ushort Reply_Delay_C_15_0 = 0x030C;  // Джиттер задержки ответов ModeA (Разница между самой длинной и короткой задержкой в серии запрос-ответов(200шт например))-- RD
    private const ushort Reply_Jitter_C_15_0 = 0x030D;  // Джиттер задержки ответов ModeC (Разница между самой длинной и короткой задержкой в серии запрос-ответов(200шт например))-- RD

    private const ushort ReplyRatioS_7_0 = 0x030E;
    
    private const ushort DF_RX_111_96 = 0x030F;
    private const ushort DF_RX_95_80 = 0x0310;
    private const ushort DF_RX_79_64 = 0x0311;
    private const ushort DF_RX_63_48 = 0x0312;
    private const ushort DF_RX_47_32 = 0x0313;
    private const ushort DF_RX_31_16 = 0x0314;
    private const ushort DF_RX_15_0 = 0x0315;

    private const ushort UF_TX_111_96 = 0x0316;
    private const ushort UF_TX_95_80 = 0x0317;
    private const ushort UF_TX_79_64 = 0x0318;
    private const ushort UF_TX_63_48 = 0x0319;
    private const ushort UF_TX_47_32 = 0x031A;
    private const ushort UF_TX_31_16 = 0x031B;
    private const ushort UF_TX_15_0 = 0x031C;
    
    public LimeSdrIfr6000Device(string deviceId, LimeSdrDeviceConfig config, ILogger? logger = null) : base(deviceId, logger)
    {
        _config = config;
        _logger = logger ?? NullLogger.Instance;
    }


    protected override CustomWorkMode InternalGetMode()
    {
        return CustomWorkMode.Ifr6000;
    }

    public Task<bool> IsTurnOn()
    {
        return IsModeEnabled(DisposeCancel);
    }

    public async Task TurnOn()
    {
        await TurnOnMode(DisposeCancel);
        await Task.Delay(500, DisposeCancel).ConfigureAwait(false);
    }

    public Task TurnOff()
    {
        return TurnOffMode(DisposeCancel);
    }

    public async Task RfRelaySelectOutput(bool isTx)
    {
        var value = isTx ? !_config.RfRelayRxIsHigh : _config.RfRelayRxIsHigh;
        _logger?.ZLogDebug($"Setting RF relay to {(isTx ? "TX" : "RX")} (GPIO[{_config.RfRelayGpio}]={value})");
        var arr = new byte[1] ;
        await ReadGpioDirection(arr, DisposeCancel);
        var dir = arr[0];
        await ReadGpio(arr, DisposeCancel);
        var gpio = arr[0];
        var dirMask = (byte)(1 << _config.RfRelayGpio);
        arr[0] = (byte)(dir | dirMask);
        await WriteGpioDirection(arr, DisposeCancel);
        
        if (value)
        {
            arr[0] = (byte)(gpio | dirMask);
        }
        else
        {
            arr[0] = (byte)(gpio & ~dirMask);
        }
        await WriteGpio(arr, DisposeCancel);
    }

    public Task WriteDelayOffsetModeAC(double offset)
    {
        var tacts = (int)Math.Round(offset * 40);
        if (tacts > short.MaxValue) tacts = short.MaxValue;
        if (tacts < short.MinValue) tacts = short.MinValue;
        var del = (ushort)tacts;
        return WriteCustomRegister(DelayOffsetAC_15_0_InternAddr, del, DisposeCancel);
    }

    public async Task<(float ModeA, float ModeC)> ReadReplyRatioModeAC()
    {
        var rRatio = await ReadCustomRegister(ReplyRatio_A_15_8_C_7_0_InternAddr, DisposeCancel).ConfigureAwait(false);
        return (ModeA: (rRatio >> 8) * 0.5f, ModeC: (rRatio & 0xFF) * 0.5f);
    }

    public Task WriteP1P3SpacingOffset(float modeAOffset, float modeCOffset)
    {
        var aOffset = (int)Math.Round(modeAOffset * 40);
        var cOffset = (int)Math.Round(modeCOffset * 40);
        if (aOffset > sbyte.MaxValue) aOffset = sbyte.MaxValue;
        if (aOffset < sbyte.MinValue) aOffset = sbyte.MinValue;
        if (cOffset > sbyte.MaxValue) cOffset = sbyte.MaxValue;
        if (cOffset < sbyte.MinValue) cOffset = sbyte.MinValue;

        var reg = (ushort)((byte)cOffset | (byte)aOffset << 8);
        return WriteCustomRegister(P1_P3_SpacingOffset_A_15_8_C_7_0_InternAddr, reg,  DisposeCancel);
    }

    public Task WriteModeACControl(bool modeAP2SlsPulseEn, bool modeCP2SlsPulseEn, bool modeAP2SlsPulseAtt,
        bool modeCP2SlsPulseAtt, bool allCallModeAC_A, bool allCallModeAC_C, bool allCallModeS_A, bool allCallModeS_C)
    {
        var reg = (ushort)0;
        if (modeAP2SlsPulseEn) reg |= 0x1;
        if (modeCP2SlsPulseEn) reg |= 0x2;
        if (modeAP2SlsPulseAtt) reg |= 0x4;
        if (modeCP2SlsPulseAtt) reg |= 0x8;
        if (allCallModeAC_A) reg |= 0x10;
        if (allCallModeAC_C) reg |= 0x20;
        if (allCallModeS_A) reg |= 0x40;
        if (allCallModeS_C) reg |= 0x80;
        return WriteCustomRegister(ModeA_C_Control, reg, DisposeCancel);
    }

    public async Task<(float F1, float F2)> ReadModeAPulseWidth()
    {
        var reg = await ReadCustomRegister(Width_A_F1_15_8_F2_7_0, DisposeCancel).ConfigureAwait(false);
        var f1 = (reg >> 8) * 0.025f;
        var f2 = (reg & 0xFF) * 0.025f;
        return (f1, f2);
    }

    public async Task<(float F1, float F2)> ReadModeCPulseWidth()
    {
        var reg = await ReadCustomRegister(Width_C_F1_15_8_F2_7_0, DisposeCancel).ConfigureAwait(false);
        var f1 = (reg >> 8) * 0.025f;
        var f2 = (reg & 0xFF) * 0.025f;
        return (f1, f2);
    }

    public async Task<(float ModeA, float ModeC)> ReadModeACPulseSpacing()
    {
        var reg = await ReadCustomRegister(F1_F2_Spacing_A_15_8_C_7_0, DisposeCancel).ConfigureAwait(false);
        var a = (sbyte)(reg >> 8) * 0.025f;
        var c = (sbyte)(reg & 0xFF) * 0.025f;
        return (a, c);
    }

    public async Task<float> ReadModeAReplyDelay()
    {
        var reg = await ReadCustomRegister(Reply_Delay_A_15_0, DisposeCancel).ConfigureAwait(false);
        return reg * 0.025f;
    }
    
    public async Task<float> ReadModeCReplyDelay()
    {
        var reg = await ReadCustomRegister(Reply_Delay_C_15_0, DisposeCancel).ConfigureAwait(false);
        return reg * 0.025f;
    }

    public async Task<float> ReadModeAReplyJitter()
    {
        var reg = await ReadCustomRegister(Reply_Jitter_A_15_0, DisposeCancel).ConfigureAwait(false);
        return reg * 0.025f;
    }
    
    public async Task<float> ReadModeCReplyJitter()
    {
        var reg = await ReadCustomRegister(Reply_Jitter_C_15_0, DisposeCancel).ConfigureAwait(false);
        return reg * 0.025f;
    }

    public async Task<(string Squawk, bool Spi)> ReadModeASquawkCode()
    {
        var code = await ReadCustomRegister(ModeAResp_15_0_InternAddr, DisposeCancel).ConfigureAwait(false);
        var spi = (code & 0x1) != 0;
        var squawk = ModeSHelper.GetSquawk((ushort)((code >> 1) & 0xFFF));
        return (squawk, spi);
    }

    public Task<ModeSDF20> ReadModeSDf20Altitude()
    {
        throw new System.NotImplementedException();
    }

    public Task<ModeSDF5> ReadModeSDf5IdentityCode()
    {
        return Task.FromResult(new ModeSDF5 { Squawk = "0000"});
    }

    public Task<ModeSDF21> ReadModeSDf21IdentityCode()
    {
        throw new System.NotImplementedException();
    }

    public Task<(ModeSDF0 Msg, byte Req, byte Resp)> ReadModeSDf0AirAir()
    {
        throw new System.NotImplementedException();
    }

    public Task<(ModeSDF16 Msg, byte Req, byte Resp)> ReadModeSDf16AirAir()
    {
        throw new System.NotImplementedException();
    }

    public Task<Bds10> ReadBds10()
    {
        throw new System.NotImplementedException();
    }

    public Task<Bds17> ReadBds17()
    {
        throw new System.NotImplementedException();
    }

    public Task<Bds20> ReadBds20()
    {
        throw new System.NotImplementedException();
    }

    public Task<Bds30> ReadBds30()
    {
        throw new System.NotImplementedException();
    }

    public Task<Bds40> ReadBds40()
    {
        throw new System.NotImplementedException();
    }

    public Task<Bds50> ReadBds50()
    {
        throw new System.NotImplementedException();
    }

    public Task<Bds60> ReadBds60()
    {
        throw new System.NotImplementedException();
    }

    public Task<AdsbAirbornePosition> ReadAdsbAirbornePosition()
    {
        throw new System.NotImplementedException();
    }

    public Task<AdsbSurfacePosition> ReadAdsbSurfacePosition()
    {
        throw new System.NotImplementedException();
    }

    public Task<AdsbAircraftIdentification> ReadAdsbAircraftIdentification()
    {
        throw new System.NotImplementedException();
    }

    public Task<AdsbGroundSpeed> ReadAdsbGroundSpeed()
    {
        throw new System.NotImplementedException();
    }

    public Task<AdsbAirspeed> ReadAdsbAirspeed()
    {
        throw new System.NotImplementedException();
    }

    public Task<AdsbAircraftOperationStatus> ReadAdsbAircraftOperationStatus()
    {
        throw new System.NotImplementedException();
    }

    public async Task<int> ReadModeCAltitude()
    {
        var code = await ReadCustomRegister(ModeCResp_15_0_InternAddr, DisposeCancel).ConfigureAwait(false);
        return ModeSHelper.GetAltitudeFromModeCAltitudeCode((ushort)((code >> 1) & 0xFFF)) ?? 0;
    }

    public Task<bool> WriteUfMessage(ModeSUFormatBase msg)
    {
        if (Interlocked.CompareExchange(ref _readDfMsgFlag, 1, 0) != 0) return Task.FromResult(false);
        try
        {
            return InternalWriteUfMessage(msg);
        }
        finally
        {
            Interlocked.Exchange(ref _readDfMsgFlag, 0);
        }
    }
    private async Task<bool> InternalWriteUfMessage(ModeSUFormatBase msg)
    {
        try
        {
            var buffer = new byte[14];
            var span = new Span<byte>(buffer);
            msg.Serialize(ref span);
            var frame = new ValueTuple<ushort, ushort>[7];
            frame[0] = new ValueTuple<ushort, ushort>(UF_TX_111_96, (ushort)((buffer[0] << 8) | buffer[1]));
            frame[1] = new ValueTuple<ushort, ushort>(UF_TX_95_80, (ushort)((buffer[2] << 8) | buffer[3]));
            frame[2] = new ValueTuple<ushort, ushort>(UF_TX_79_64, (ushort)((buffer[4] << 8) | buffer[5]));
            frame[3] = new ValueTuple<ushort, ushort>(UF_TX_63_48, (ushort)((buffer[6] << 8) | buffer[7]));
            frame[4] = new ValueTuple<ushort, ushort>(UF_TX_47_32, (ushort)((buffer[8] << 8) | buffer[9]));
            frame[5] = new ValueTuple<ushort, ushort>(UF_TX_31_16, (ushort)((buffer[10] << 8) | buffer[11]));
            frame[6] = new ValueTuple<ushort, ushort>(UF_TX_15_0, (ushort)((buffer[12] << 8) | buffer[13]));
            await WriteCustomRegistersFrame(frame, DisposeCancel).ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        
    }

    public async Task<ModeSDFormatBase?> ReadDfMessage(Func<ModeSDFormatBase> factory, int attempts = 3)
    {
        if (Interlocked.CompareExchange(ref _readDfMsgFlag, 1, 0) != 0) return null;
        try
        {
            return await InternalReadDfMessage(factory, attempts).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _readDfMsgFlag, 0);
        }
    }
    private async Task<ModeSDFormatBase?> InternalReadDfMessage(Func<ModeSDFormatBase> factory, int attempts = 3)
    {
        var msg = factory();
        var length = msg.GetByteSize();
        var addrFrame = new ushort[length % 2 == 0 ? length / 2 : (length / 2) + 1];
        for (var i = 0; i < addrFrame.Length; i++)
        {
            addrFrame[i] = (ushort)(DF_RX_111_96 + i);
        }
        while (attempts-- > 0)
        {
            try
            {
                var valueFrame = await ReadCustomRegistersFrame(addrFrame, DisposeCancel).ConfigureAwait(false);
                var buffer = new byte[length];
                for (var i = 0; i < length; i++)
                {
                    var index = i / 2;
                    buffer[i] = (byte)(i % 2 == 0 ? (valueFrame[index] >> 8) & 0xFF : valueFrame[index] & 0xFF);
                }
                var span = new ReadOnlySpan<byte>(buffer);
                msg.Deserialize(ref span);
                return msg;
            }
            catch (InvalidDataException)
            {
                // ignore
                await Task.Delay(20, DisposeCancel).ConfigureAwait(false);
            }
        }
        return null;
    }

    public async Task<ModeSDFormatBase?> ReadDfMessage(ModeSUFormatBase reqMsg, Func<ModeSDFormatBase> respFactory, int attempts = 3)
    {
        if (Interlocked.CompareExchange(ref _readDfMsgFlag, 1, 0) != 0) return null;
        try
        {
            await InternalWriteUfMessage(reqMsg).ConfigureAwait(false);
            return await InternalReadDfMessage(respFactory, attempts).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _readDfMsgFlag, 0);
        }
    }

    public async Task<float> ReadReplyRatioModeS()
    {
        return (await ReadCustomRegister(ReplyRatioS_7_0).ConfigureAwait(false) & 0xFF) * 0.5f;
    }

    public Task<(ModeSDF11 Msg, byte Req, byte Resp)> ReadModeSDf11IcaoAddress()
    {
        throw new System.NotImplementedException();
    }

    public Task<ModeSDF4> ReadModeSDf4Altitude()
    {
        return Task.FromResult(new ModeSDF4 { Altitude = 0});
    }
}

