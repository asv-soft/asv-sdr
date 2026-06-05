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
    private double _delayOffsetAc = 0;
    
    private const ushort ModeAResp_15_0_InternAddr          = 0x0301; // (0,0,С1,А1,С2,А2,С4,А4,Х,В1,D1,В2,D2,В4,D4,SPI) -- RD
    private const ushort ModeCResp_15_0_InternAddr          = 0x0302; // (0,0,С1,А1,С2,А2,С4,А4,Х,В1,D1,В2,D2,В4,D4,0) -- RD
    private const ushort DelayOffsetAC_15_0_InternAddr      = 0x0303; // калибровочный коэффициент по дальности принятых сообщений A/C, signed -- WR
    private const ushort ReplyRatio_A_15_8_C_7_0_InternAddr = 0x0304; // процент ответов A/C	(по 0,5% т.е. количество ответов на 200 запросов) -- RD
    
    private const ushort P1_P3_SpacingOffset_A_15_8_C_7_0_InternAddr = 0x0305; // Отклонение от кодового расстояния 8/21 мкс кода A/C для запросов, signed, в тактах, один такт 0,025 мкс  -- WR
    
    private const ushort ModeA_C_Control = 0x0306; // 
    
    private const ushort Width_A_F1_15_8_F2_7_0 = 0x0307;  // средняя ширина ответных кадрирующих импульсов для кода A, в тактах, один такт 0,025 мкс	-- RD
    private const ushort Width_C_F1_15_8_F2_7_0 = 0x0308;  // ширина ответных кадрирующих импульсов для кода C, в тактах, один такт 0,025 мкс	-- RD
    private const ushort F1_F2_Spacing_A_15_8_C_7_0 = 0x0309;  // Отклонение ответных кадрирующих импульсов от кодового расстояния 20,3 мкс, signed, в тактах, один такт 0,025 мкс  -- RD
    
    private const ushort Reply_Delay_A_15_0 = 0x030A;  // Задержка ответов А, на откалиброванном приборе нулевая дальность должна быть: 3 мкс для А/С и 128 мкс для межрижимого ответа ответчика S, signed, в тактах, один такт 0,025 мкс	-- RD
    private const ushort Reply_Jitter_A_15_0 = 0x030B;  // Джиттер задержки ответов (Разница между самой длинной и короткой задержкой в серии запрос-ответов(200шт например))-- RD
    private const ushort Reply_Delay_C_15_0 = 0x030C;  // Задержка ответов C, на откалиброванном приборе нулевая дальность должна быть: 3 мкс для А/С и 128 мкс для межрижимого ответа ответчика S, signed, в тактах, один такт 0,025 мкс	-- RD
    private const ushort Reply_Jitter_C_15_0 = 0x030D;  // Джиттер задержки ответов (Разница между самой длинной и короткой задержкой в серии запрос-ответов(200шт например))-- RD

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

    private const ushort BDS_05_Ev_111_96 = 0x031F;  // --RD -- BDS_05_Ev
    private const ushort BDS_05_Ev_95_80 = 0x0320;   // --RD
    private const ushort BDS_05_Ev_79_64 = 0x0321;   // --RD
    private const ushort BDS_05_Ev_63_48 = 0x0322;   // --RD
    private const ushort BDS_05_Ev_47_32 = 0x0323;   // --RD
    private const ushort BDS_05_Ev_31_16 = 0x0324;   // --RD
    private const ushort BDS_05_Ev_15_0 = 0x0325;    // --RD

    private const ushort BDS_05_Odd_111_96 = 0x0326; // --RD -- BDS_05_Odd
    private const ushort BDS_05_Odd_95_80 = 0x0327;  // --RD
    private const ushort BDS_05_Odd_79_64 = 0x0328;  // --RD
    private const ushort BDS_05_Odd_63_48 = 0x0329;  // --RD
    private const ushort BDS_05_Odd_47_32 = 0x032A;  // --RD
    private const ushort BDS_05_Odd_31_16 = 0x032B;  // --RD
    private const ushort BDS_05_Odd_15_0 = 0x032C;   // --RD

    private const ushort BDS_06_Ev_111_96 = 0x032D;  // --RD -- BDS_06_Ev
    private const ushort BDS_06_Ev_95_80 = 0x032E;   // --RD
    private const ushort BDS_06_Ev_79_64 = 0x032F;   // --RD
    private const ushort BDS_06_Ev_63_48 = 0x0330;   // --RD
    private const ushort BDS_06_Ev_47_32 = 0x0331;   // --RD
    private const ushort BDS_06_Ev_31_16 = 0x0332;   // --RD
    private const ushort BDS_06_Ev_15_0 = 0x0333;    // --RD

    private const ushort BDS_06_Odd_111_96 = 0x0334; // --RD -- BDS_06_Odd
    private const ushort BDS_06_Odd_95_80 = 0x0335;  // --RD
    private const ushort BDS_06_Odd_79_64 = 0x0336;  // --RD
    private const ushort BDS_06_Odd_63_48 = 0x0337;  // --RD
    private const ushort BDS_06_Odd_47_32 = 0x0338;  // --RD
    private const ushort BDS_06_Odd_31_16 = 0x0339;  // --RD
    private const ushort BDS_06_Odd_15_0 = 0x033A;   // --RD

    private const ushort BDS_08_111_96 = 0x033B;     // --RD -- BDS_08
    private const ushort BDS_08_95_80 = 0x033C;      // --RD
    private const ushort BDS_08_79_64 = 0x033D;      // --RD
    private const ushort BDS_08_63_48 = 0x033E;      // --RD
    private const ushort BDS_08_47_32 = 0x033F;      // --RD
    private const ushort BDS_08_31_16 = 0x0340;      // --RD
    private const ushort BDS_08_15_0 = 0x0341;       // --RD

    private const ushort BDS_09_Vel_111_96 = 0x0342; // --RD -- BDS_09_VEL
    private const ushort BDS_09_Vel_95_80 = 0x0343;  // --RD
    private const ushort BDS_09_Vel_79_64 = 0x0344;  // --RD
    private const ushort BDS_09_Vel_63_48 = 0x0345;  // --RD
    private const ushort BDS_09_Vel_47_32 = 0x0346;  // --RD
    private const ushort BDS_09_Vel_31_16 = 0x0347;  // --RD
    private const ushort BDS_09_Vel_15_0 = 0x0348;   // --RD

    private const ushort BDS_09_Air_111_96 = 0x0349; // --RD -- BDS_09_AIR
    private const ushort BDS_09_Air_95_80 = 0x034A;  // --RD
    private const ushort BDS_09_Air_79_64 = 0x034B;  // --RD
    private const ushort BDS_09_Air_63_48 = 0x034C;  // --RD
    private const ushort BDS_09_Air_47_32 = 0x034D;  // --RD
    private const ushort BDS_09_Air_31_16 = 0x034E;  // --RD
    private const ushort BDS_09_Air_15_0 = 0x034F;   // --RD

    private const ushort BDS_0A_111_96 = 0x0350;     // --RD -- BDS_0A
    private const ushort BDS_0A_95_80 = 0x0351;      // --RD
    private const ushort BDS_0A_79_64 = 0x0352;      // --RD
    private const ushort BDS_0A_63_48 = 0x0353;      // --RD
    private const ushort BDS_0A_47_32 = 0x0354;      // --RD
    private const ushort BDS_0A_31_16 = 0x0355;      // --RD
    private const ushort BDS_0A_15_0 = 0x0356;       // --RD

    private const ushort BDS_61_E_111_96 = 0x0357;   // --RD -- BDS_61_E
    private const ushort BDS_61_E_95_80 = 0x0358;    // --RD
    private const ushort BDS_61_E_79_64 = 0x0359;    // --RD
    private const ushort BDS_61_E_63_48 = 0x035A;    // --RD
    private const ushort BDS_61_E_47_32 = 0x035B;    // --RD
    private const ushort BDS_61_E_31_16 = 0x035C;    // --RD
    private const ushort BDS_61_E_15_0 = 0x035D;     // --RD

    private const ushort BDS_61_T_111_96 = 0x035E;   // --RD -- BDS_61_T
    private const ushort BDS_61_T_95_80 = 0x035F;    // --RD
    private const ushort BDS_61_T_79_64 = 0x0360;    // --RD
    private const ushort BDS_61_T_63_48 = 0x0361;    // --RD
    private const ushort BDS_61_T_47_32 = 0x0362;    // --RD
    private const ushort BDS_61_T_31_16 = 0x0363;    // --RD
    private const ushort BDS_61_T_15_0 = 0x0364;     // --RD

    private const ushort BDS_62_Old_111_96 = 0x0365; // --RD -- BDS_62_Old
    private const ushort BDS_62_Old_95_80 = 0x0366;  // --RD
    private const ushort BDS_62_Old_79_64 = 0x0367;  // --RD
    private const ushort BDS_62_Old_63_48 = 0x0368;  // --RD
    private const ushort BDS_62_Old_47_32 = 0x0369;  // --RD
    private const ushort BDS_62_Old_31_16 = 0x036A;  // --RD
    private const ushort BDS_62_Old_15_0 = 0x036B;   // --RD

    private const ushort BDS_62_New_111_96 = 0x036C; // --RD -- BDS_62_New
    private const ushort BDS_62_New_95_80 = 0x036D;  // --RD
    private const ushort BDS_62_New_79_64 = 0x036E;  // --RD
    private const ushort BDS_62_New_63_48 = 0x036F;  // --RD
    private const ushort BDS_62_New_47_32 = 0x0370;  // --RD
    private const ushort BDS_62_New_31_16 = 0x0371;  // --RD
    private const ushort BDS_62_New_15_0 = 0x0372;   // --RD

    private const ushort BDS_65_Air_111_96 = 0x0373; // --RD -- BDS_65_Air
    private const ushort BDS_65_Air_95_80 = 0x0374;  // --RD
    private const ushort BDS_65_Air_79_64 = 0x0375;  // --RD
    private const ushort BDS_65_Air_63_48 = 0x0376;  // --RD
    private const ushort BDS_65_Air_47_32 = 0x0377;  // --RD
    private const ushort BDS_65_Air_31_16 = 0x0378;  // --RD
    private const ushort BDS_65_Air_15_0 = 0x0379;   // --RD

    private const ushort BDS_65_Sur_111_96 = 0x037A; // --RD -- BDS_65_Sur
    private const ushort BDS_65_Sur_95_80 = 0x037B;  // --RD
    private const ushort BDS_65_Sur_79_64 = 0x037C;  // --RD
    private const ushort BDS_65_Sur_63_48 = 0x037D;  // --RD
    private const ushort BDS_65_Sur_47_32 = 0x037E;  // --RD
    private const ushort BDS_65_Sur_31_16 = 0x037F;  // --RD
    private const ushort BDS_65_Sur_15_0 = 0x0380;   // --RD

    private const ushort BDS_05_Ev_CNT_Period_15_0 = 0x0381;   // --RD -- BDS_05_Ev_CNT(7:0) & BDS_05_Ev_Period(7:0); Period step 0.2s, FF = infinity
    private const ushort BDS_05_Odd_CNT_Period_15_0 = 0x0382;  // --RD -- BDS_05_Odd_CNT(7:0) & BDS_05_Odd_Period(7:0)
    private const ushort BDS_06_Ev_CNT_Period_15_0 = 0x0383;   // --RD -- BDS_06_Ev_CNT(7:0) & BDS_06_Ev_Period(7:0)
    private const ushort BDS_06_Odd_CNT_Period_15_0 = 0x0384;  // --RD -- BDS_06_Odd_CNT(7:0) & BDS_06_Odd_Period(7:0)
    private const ushort BDS_08_CNT_Period_15_0 = 0x0385;      // --RD -- BDS_08_CNT(7:0) & BDS_08_Period(7:0)
    private const ushort BDS_09_CNT_Period_15_0 = 0x0386;      // --RD -- BDS_09_CNT(7:0) & BDS_09_Period(7:0)
    private const ushort BDS_0A_CNT_Period_15_0 = 0x0387;      // --RD -- BDS_0A_CNT(7:0) & BDS_0A_Period(7:0)
    private const ushort BDS_61_E_CNT_Period_15_0 = 0x0388;    // --RD -- BDS_61_E_CNT(7:0) & BDS_61_E_Period(7:0)
    private const ushort BDS_61_T_CNT_Period_15_0 = 0x0389;    // --RD -- BDS_61_T_CNT(7:0) & BDS_61_T_Period(7:0)
    private const ushort BDS_62_Old_CNT_Period_15_0 = 0x038A;  // --RD -- BDS_62_Old_CNT(7:0) & BDS_62_Old_Period(7:0)
    private const ushort BDS_62_New_CNT_Period_15_0 = 0x038B;  // --RD -- BDS_62_New_CNT(7:0) & BDS_62_New_Period(7:0)
    private const ushort BDS_65_Air_CNT_Period_15_0 = 0x038C;  // --RD -- BDS_65_Air_CNT(7:0) & BDS_65_Air_Period(7:0)
    private const ushort BDS_65_Sur_CNT_Period_15_0 = 0x038D;  // --RD -- BDS_65_Sur_CNT(7:0) & BDS_65_Sur_Period(7:0)
    
    
    
    
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
        _delayOffsetAc = offset;
        return Task.CompletedTask;
        
        var tacts = (int)Math.Round(offset * 40);
        if (tacts > short.MaxValue) tacts = short.MaxValue;
        if (tacts < short.MinValue) tacts = short.MinValue;
        var del = (ushort)tacts;
        // return WriteCustomRegister(DelayOffsetAC_15_0_InternAddr, del, DisposeCancel);
        
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

    public async Task<ExSquitterStatistics> ReadExSquitterStatistics()
    {
        var addrFrame = new ushort[13];
        for (ushort i = 0; i < addrFrame.Length; i++)
        {
            addrFrame[i] = (ushort)(BDS_05_Ev_CNT_Period_15_0 + i);
        }
        var valueFrame = await ReadCustomRegistersFrame(addrFrame, DisposeCancel).ConfigureAwait(false);

        var result = new ExSquitterStatistics
        {
            Counters =
            {
                Bds05Even = (byte)(valueFrame[0] >> 8),
                Bds05Odd = (byte)(valueFrame[1] >> 8),
                Bds06Even = (byte)(valueFrame[2] >> 8),
                Bds06Odd = (byte)(valueFrame[3] >> 8),
                Bds08 = (byte)(valueFrame[4] >> 8),
                Bds09GroundSpeed = (byte)(valueFrame[5] >> 8),
                Bds09Airspeed = (byte)(valueFrame[6] >> 8),
                Bds0A = (byte)(valueFrame[7] >> 8),
                Bds61EmergencyPriorityStatus = (byte)(valueFrame[8] >> 8),
                Bds61TcasRaBroadcast = (byte)(valueFrame[9] >> 8),
                Bds62Old = (byte)(valueFrame[10] >> 8),
                Bds62New = (byte)(valueFrame[10] >> 8),
                Bds65Airborne = (byte)(valueFrame[11] >> 8),
                Bds65Surface = (byte)(valueFrame[12] >> 8)
            },
            Periods =
            {
                Bds05Even = (valueFrame[0] & 0xFF) * 0.2f,
                Bds05Odd = (valueFrame[1] & 0xFF) * 0.2f,
                Bds06Even = (valueFrame[2] & 0xFF) * 0.2f,
                Bds06Odd = (valueFrame[3] & 0xFF) * 0.2f,
                Bds08 = (valueFrame[4] & 0xFF) * 0.2f,
                Bds09GroundSpeed = (valueFrame[5] & 0xFF) * 0.2f,
                Bds09Airspeed = (valueFrame[6] & 0xFF) * 0.2f,
                Bds0A = (valueFrame[7] & 0xFF) * 0.2f,
                Bds61EmergencyPriorityStatus = (valueFrame[8] & 0xFF) * 0.2f,
                Bds61TcasRaBroadcast = (valueFrame[9] & 0xFF) * 0.2f,
                Bds62Old = (valueFrame[10] & 0xFF) * 0.2f,
                Bds62New = (valueFrame[10] & 0xFF) * 0.2f,
                Bds65Airborne = (valueFrame[11] & 0xFF) * 0.2f,
                Bds65Surface = (valueFrame[12] & 0xFF) * 0.2f
            }
        };
        return result;
    }

    public async Task<AdsbAirbornePosition?> ReadExBds05Even()
    {
        var addrFrame = new ushort[7];
        for (ushort i = 0; i < addrFrame.Length; i++)
        {
            addrFrame[i] = (ushort)(BDS_05_Ev_111_96 + i);
        }
        var valueFrame = await ReadCustomRegistersFrame(addrFrame, DisposeCancel).ConfigureAwait(false);
        var buffer = new byte[addrFrame.Length * 2];
        var span = new ReadOnlySpan<byte>(buffer);
        for (var i = 0; i < buffer.Length; i++)
        {
            var index = i / 2;
            buffer[i] = (byte)(i % 2 == 0 ? (valueFrame[index] >> 8) & 0xFF : valueFrame[index] & 0xFF);
        }

        AdsbAirbornePosition? msgEven = null;
        try
        {
            var type = TransponderHelper.GetMessageType(span);
            AdsbAirbornePosition? even = type switch
            {
                AdsbMessageTypeEnum.AirborneBarometricPosition => new AdsbAirbornePositionWithBaroAlt(),
                AdsbMessageTypeEnum.AirborneGnssPosition => new AdsbAirbornePositionWithGnssAlt(),
                _ => null
            };
            if (even != null)
            {
                even.Deserialize(ref span);
                msgEven = even;
            }
        }
        catch (Exception)
        {
            // ignored
        }
        
        return msgEven;
    }
    
    public async Task<AdsbAirbornePosition?> ReadExBds05Odd()
    {
        var addrFrame = new ushort[7];
        for (ushort i = 0; i < addrFrame.Length; i++)
        {
            addrFrame[i] = (ushort)(BDS_05_Odd_111_96 + i);
        }
        var valueFrame = await ReadCustomRegistersFrame(addrFrame, DisposeCancel).ConfigureAwait(false);
        var buffer = new byte[addrFrame.Length * 2];
        var span = new ReadOnlySpan<byte>(buffer);
        for (var i = 0; i < buffer.Length; i++)
        {
            var index = i / 2;
            buffer[i] = (byte)(i % 2 == 0 ? (valueFrame[index] >> 8) & 0xFF : valueFrame[index] & 0xFF);
        }

        AdsbAirbornePosition? msgOdd = null;
        try
        {
            var type = TransponderHelper.GetMessageType(span);
            AdsbAirbornePosition? odd = type switch
            {
                AdsbMessageTypeEnum.AirborneBarometricPosition => new AdsbAirbornePositionWithBaroAlt(),
                AdsbMessageTypeEnum.AirborneGnssPosition => new AdsbAirbornePositionWithGnssAlt(),
                _ => null
            };
            if (odd != null)
            {
                odd.Deserialize(ref span);
                msgOdd = odd;
            }
        }
        catch (Exception)
        {
            // ignored
        }
        
        return msgOdd;
    }
    
    public async Task<AdsbSurfacePosition?> ReadExBds06Even()
    {
        var addrFrame = new ushort[7];
        for (ushort i = 0; i < addrFrame.Length; i++)
        {
            addrFrame[i] = (ushort)(BDS_06_Ev_111_96 + i);
        }
        var valueFrame = await ReadCustomRegistersFrame(addrFrame, DisposeCancel).ConfigureAwait(false);
        var buffer = new byte[addrFrame.Length * 2];
        var span = new ReadOnlySpan<byte>(buffer);
        for (var i = 0; i < buffer.Length; i++)
        {
            var index = i / 2;
            buffer[i] = (byte)(i % 2 == 0 ? (valueFrame[index] >> 8) & 0xFF : valueFrame[index] & 0xFF);
        }

        AdsbSurfacePosition? msgEven = null;
        try
        {
            var type = TransponderHelper.GetMessageType(span);
            var even = type switch
            {
                AdsbMessageTypeEnum.SurfacePosition => new AdsbSurfacePosition(),
                _ => null
            };
            if (even != null)
            {
                even.Deserialize(ref span);
                msgEven = even;
            }
        }
        catch (Exception)
        {
            // ignored
        }
        
        return msgEven;
    }
    
    public async Task<AdsbSurfacePosition?> ReadExBds06Odd()
    {
        var addrFrame = new ushort[7];
        for (ushort i = 0; i < addrFrame.Length; i++)
        {
            addrFrame[i] = (ushort)(BDS_06_Odd_111_96 + i);
        }
        var valueFrame = await ReadCustomRegistersFrame(addrFrame, DisposeCancel).ConfigureAwait(false);
        var buffer = new byte[addrFrame.Length * 2];
        var span = new ReadOnlySpan<byte>(buffer);
        for (var i = 0; i < buffer.Length; i++)
        {
            var index = i / 2;
            buffer[i] = (byte)(i % 2 == 0 ? (valueFrame[index] >> 8) & 0xFF : valueFrame[index] & 0xFF);
        }

        AdsbSurfacePosition? msgOdd = null;
        try
        {
            var type = TransponderHelper.GetMessageType(span);
            var even = type switch
            {
                AdsbMessageTypeEnum.SurfacePosition => new AdsbSurfacePosition(),
                _ => null
            };
            if (even != null)
            {
                even.Deserialize(ref span);
                msgOdd = even;
            }
        }
        catch (Exception)
        {
            // ignored
        }
        
        return msgOdd;
    }
    
    public async Task<AdsbAircraftIdentification?> ReadExBds08Id()
    {
        var addrFrame = new ushort[7];
        for (ushort i = 0; i < addrFrame.Length; i++)
        {
            addrFrame[i] = (ushort)(BDS_08_111_96 + i);
        }
        var valueFrame = await ReadCustomRegistersFrame(addrFrame, DisposeCancel).ConfigureAwait(false);
        var buffer = new byte[addrFrame.Length * 2];
        for (var i = 0; i < buffer.Length; i++)
        {
            var index = i / 2;
            buffer[i] = (byte)(i % 2 == 0 ? (valueFrame[index] >> 8) & 0xFF : valueFrame[index] & 0xFF);
        }
        
        var span = new ReadOnlySpan<byte>(buffer);
        AdsbAircraftIdentification? msg = null;
        try
        {
            var type = TransponderHelper.GetMessageType(span);
            msg = type switch
            {
                AdsbMessageTypeEnum.AircraftIdentification => new AdsbAircraftIdentification(),
                _ => null
            };
            msg?.Deserialize(ref span);
        }
        catch (Exception)
        {
            // ignored
        }
        
        return msg;
    }
    
    public async Task<AdsbGroundSpeed?> ReadExBds09GroundSpeed()
    {
        var addrFrame = new ushort[7];
        for (ushort i = 0; i < addrFrame.Length; i++)
        {
            addrFrame[i] = (ushort)(BDS_09_Vel_111_96 + i);
        }
        var valueFrame = await ReadCustomRegistersFrame(addrFrame, DisposeCancel).ConfigureAwait(false);
        var buffer = new byte[addrFrame.Length * 2];
        for (var i = 0; i < buffer.Length; i++)
        {
            var index = i / 2;
            buffer[i] = (byte)(i % 2 == 0 ? (valueFrame[index] >> 8) & 0xFF : valueFrame[index] & 0xFF);
        }
        
        var span = new ReadOnlySpan<byte>(buffer);
        AdsbGroundSpeed? msg = null;
        try
        {
            var type = TransponderHelper.GetMessageType(span);
            var subType = (VelocitySubTypeEnum)TransponderHelper.GetMessageSybType(span);
            
            if (type == AdsbMessageTypeEnum.AirborneVelocities && subType is VelocitySubTypeEnum.SubType1 or VelocitySubTypeEnum.SubType2)
            {
                msg = new AdsbGroundSpeed();
                msg.Deserialize(ref span);
                return msg;
            }
        }
        catch (Exception)
        {
            // ignored
        }
        
        return msg;
    }
    
    public async Task<AdsbAirspeed?> ReadExBds09Airspeed()
    {
        var addrFrame = new ushort[7];
        for (ushort i = 0; i < addrFrame.Length; i++)
        {
            addrFrame[i] = (ushort)(BDS_09_Air_111_96 + i);
        }
        var valueFrame = await ReadCustomRegistersFrame(addrFrame, DisposeCancel).ConfigureAwait(false);
        var buffer = new byte[addrFrame.Length * 2];
        for (var i = 0; i < buffer.Length; i++)
        {
            var index = i / 2;
            buffer[i] = (byte)(i % 2 == 0 ? (valueFrame[index] >> 8) & 0xFF : valueFrame[index] & 0xFF);
        }
        
        var span = new ReadOnlySpan<byte>(buffer);
        AdsbAirspeed? msg = null;
        try
        {
            var type = TransponderHelper.GetMessageType(span);
            var subType = (VelocitySubTypeEnum)TransponderHelper.GetMessageSybType(span);
            
            if (type == AdsbMessageTypeEnum.AirborneVelocities && subType is VelocitySubTypeEnum.SubType3 or VelocitySubTypeEnum.SubType4)
            {
                msg = new AdsbAirspeed();
                msg.Deserialize(ref span);
                return msg;
            }
        }
        catch (Exception)
        {
            // ignored
        }
        
        return msg;
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

public class ExSquitterStatistics
{
    public ByteCounters Counters { get; } = new();
    public Periods Periods { get; } = new();
}
public class ByteCounters
{
    public byte Bds05Even { get; set; }
    public byte Bds05Odd { get; set; }
    public byte Bds06Even { get; set; }
    public byte Bds06Odd { get; set; }
    public byte Bds08 { get; set; }
    public byte Bds09GroundSpeed { get; set; }
    public byte Bds09Airspeed { get; set; }
    public byte Bds0A { get; set; }
    public byte Bds61EmergencyPriorityStatus { get; set; }
    public byte Bds61TcasRaBroadcast { get; set; }
    public byte Bds62Old { get; set; }
    public byte Bds62New { get; set; }
    public byte Bds65Airborne { get; set; }
    public byte Bds65Surface { get; set; }
}

public class Periods
{
    public float Bds05Even { get; set; } = float.NaN;
    public float Bds05Odd { get; set; } = float.NaN;
    public float Bds06Even { get; set; } = float.NaN;
    public float Bds06Odd { get; set; } = float.NaN;
    public float Bds08 { get; set; } = float.NaN;
    public float Bds09GroundSpeed { get; set; } = float.NaN;
    public float Bds09Airspeed { get; set; } = float.NaN;
    public float Bds0A { get; set; } = float.NaN;
    public float Bds61EmergencyPriorityStatus { get; set; } = float.NaN;
    public float Bds61TcasRaBroadcast { get; set; } = float.NaN;
    public float Bds62Old { get; set; } = float.NaN;
    public float Bds62New { get; set; } = float.NaN;
    public float Bds65Airborne { get; set; } = float.NaN;
    public float Bds65Surface { get; set; } = float.NaN;
}

