using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Asv.Sdr.LimeSdr;


public class LimeSdrIfr6000DeviceConfig
{
    
}

public class LimeSdrIfr6000Device : LimeSdrCustomDevice, ILimeSdrIfr6000Device
{
    private const ushort ModeAResp_15_0_InternAddr          = 0x0301; // (0,0,С1,А1,С2,А2,С4,А4,Х,В1,D1,В2,D2,В4,D4,SPI) -- RD
    private const ushort ModeCResp_15_0_InternAddr          = 0x0302; // (0,0,С1,А1,С2,А2,С4,А4,Х,В1,D1,В2,D2,В4,D4,0) -- RD
    private const ushort DelayOffsetAC_15_0_InternAddr      = 0x0303; // калибровочный коэффициент по дальности принятых сообщений A/C, signed -- WR
    private const ushort ReplyRatio_A_7_0_C_15_8_InternAddr = 0x0304; // процент ответов A/C	(по 0,5% т.е. количество ответов на 200 запросов) -- RD
    
    private const ushort P1_P3_SpacingOffset_A_7_0_C_15_8_InternAddr = 0x0305; // Отклонение от кодового расстояния 8/21 мкс кода A/C для запросов, signed, в тактах, один такт 0,025 мкс  -- WR
    
    private const ushort ModeA_C_Control = 0x0306; // 
    
    private const ushort Width_A_F1_7_0_F2_15_8 = 0x0307;  // средняя ширина ответных кадрирующих импульсов для кода A, в тактах, один такт 0,025 мкс	-- RD
    private const ushort Width_C_F1_7_0_F2_15_8 = 0x0308;  // ширина ответных кадрирующих импульсов для кода C, в тактах, один такт 0,025 мкс	-- RD
    private const ushort F1_F2_Spacing_A_7_0_C_15_8 = 0x0309;  // Отклонение ответных кадрирующих импульсов от кодового расстояния 20,3 мкс, signed, в тактах, один такт 0,025 мкс  -- RD
    private const ushort Reply_Delay_A_7_0_C_15_8 = 0x030A;  // Отклонение задержки ответов от нулевой дальности (нулевая дальность = 3 мкс), signed, в тактах, один такт 0,025 мкс	-- RD
    private const ushort Reply_Jitter_A_7_0_C_15_8 = 0x030B;  // Джиттер задержки ответов (Разница между самой длинной и короткой задержкой в серии запрос-ответов(200шт например))-- RD
    
    public LimeSdrIfr6000Device(string deviceId, LimeSdrIfr6000DeviceConfig config, ILogger? logger = null) : base(deviceId, logger)
    {
        
    }


    protected override CustomWorkMode InternalGetMode()
    {
        return CustomWorkMode.Ifr6000;
    }

    public Task WriteDelayOffsetModeAC(double offset)
    {
        var tacts = (int)Math.Round(offset * 40);
        if (tacts > short.MaxValue) tacts = short.MaxValue;
        if (tacts < short.MinValue) tacts = short.MinValue;
        var del = (ushort)tacts;
        return WriteCustomRegister(DelayOffsetAC_15_0_InternAddr, del, DisposeCancel);
    }

    public async Task<(float ModeA, float ModeC)> ModeACCReadReplyRatio()
    {
        var rRatio = await ReadCustomRegister(ReplyRatio_A_7_0_C_15_8_InternAddr, DisposeCancel).ConfigureAwait(false);
        return (ModeA: (rRatio & 0xFF) * 0.5f, ModeC: (rRatio >> 8) * 0.5f);
    }

    public Task WriteP1P3SpacingOffset(float modeAOffset, float modeCOffset)
    {
        var aOffset = (int)Math.Round(modeAOffset * 40);
        var cOffset = (int)Math.Round(modeCOffset * 40);
        if (aOffset > sbyte.MaxValue) aOffset = sbyte.MaxValue;
        if (aOffset < sbyte.MinValue) aOffset = sbyte.MinValue;
        if (cOffset > sbyte.MaxValue) cOffset = sbyte.MaxValue;
        if (cOffset < sbyte.MinValue) cOffset = sbyte.MinValue;

        var reg = (ushort)((byte)aOffset | (byte)cOffset << 8);
        return WriteCustomRegister(P1_P3_SpacingOffset_A_7_0_C_15_8_InternAddr, reg,  DisposeCancel);
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
        var reg = await ReadCustomRegister(Width_A_F1_7_0_F2_15_8, DisposeCancel).ConfigureAwait(false);
        var f1 = (reg & 0xFF) * 0.025f;
        var f2 = (reg >> 8) * 0.025f;
        return (f1, f2);
    }

    public async Task<(float F1, float F2)> ReadModeCPulseWidth()
    {
        var reg = await ReadCustomRegister(Width_C_F1_7_0_F2_15_8, DisposeCancel).ConfigureAwait(false);
        var f1 = (reg & 0xFF) * 0.025f;
        var f2 = (reg >> 8) * 0.025f;
        return (f1, f2);
    }

    public async Task<(float ModeA, float ModeC)> ReadModeACPulseSpacing()
    {
        var reg = await ReadCustomRegister(F1_F2_Spacing_A_7_0_C_15_8, DisposeCancel).ConfigureAwait(false);
        var a = (sbyte)(reg & 0xFF) * 0.025f;
        var c = (sbyte)(reg >> 8) * 0.025f;
        return (a, c);
    }

    public async Task<(float ModeA, float ModeC)> ReadModeACReplayDelay()
    {
        var reg = await ReadCustomRegister(Reply_Delay_A_7_0_C_15_8, DisposeCancel).ConfigureAwait(false);
        var a = (sbyte)(reg & 0xFF) * 0.025f;
        var c = (sbyte)(reg >> 8) * 0.025f;
        return (a, c);
    }

    public async Task<(float ModeA, float ModeC)> ReadModeACReplayJitter()
    {
        var reg = await ReadCustomRegister(Reply_Jitter_A_7_0_C_15_8, DisposeCancel).ConfigureAwait(false);
        var a = (reg & 0xFF) * 0.025f;
        var c = (reg >> 8) * 0.025f;
        return (a, c);
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

    public Task<(ModeSDF11 Msg, byte Req, byte Resp)> ReadModeSDf11IcaoAddress()
    {
        throw new System.NotImplementedException();
    }

    public Task<ModeSDF4> ReadModeSDf4Altitude()
    {
        return Task.FromResult(new ModeSDF4 { Altitude = 0});
    }
}

