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
    private const int DefaultDfPollIntervalMs = 10;
    private const int DefaultDfResponseTimeoutMs = 100;
    
    private const ushort ModeAResp_15_0_InternAddr          = 0x0301; // (0,0,С1,А1,С2,А2,С4,А4,Х,В1,D1,В2,D2,В4,D4,SPI) -- RD
    private const ushort ModeCResp_15_0_InternAddr          = 0x0302; // (0,0,С1,А1,С2,А2,С4,А4,Х,В1,D1,В2,D2,В4,D4,0) -- RD
    private const ushort DelayOffsetAC_15_0_InternAddr      = 0x0303; // калибровочный коэффициент по дальности принятых сообщений A/C, signed -- WR
    private const ushort ReplyRatio_A_15_8_C_7_0_InternAddr = 0x0304; // процент ответов A/C	(по 0,5% т.е. количество ответов на 200 запросов) -- RD
    
    private const ushort P1_P3_SpacingOffset_A_15_8_C_7_0_InternAddr = 0x0305; // Отклонение от кодового расстояния 8/21 мкс кода A/C для запросов, signed, в тактах, один такт 0,025 мкс  -- WR
    
    private const ushort ModeA_C_Control = 0x0306; // 
    private const ushort ModeS_Control = 0x0306; // 
    
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
    
    private const ushort ReplyDelayS = 0x031D;       // Нулевая дальность (нулевая дальность = 128 мкс), signed, в тактах, один такт 0,025 мкс	-- RD
    private const ushort ReplyJitterS = 0x031E;      // Джиттер задержки ответов (Разница между самой длинной и короткой задержкой в серии запрос-ответов(200шт например))-- RD

    private const ushort BDS_05_Ev_CNT_Period_15_0 = 0x031F;   // --RD -- BDS_05_Ev_CNT(7:0) & BDS_05_Ev_Period(7:0); period step is 0.2s, 0xFF means infinity
    private const ushort BDS_05_Odd_CNT_Period_15_0 = 0x0320;  // --RD -- BDS_05_Odd_CNT(7:0) & BDS_05_Odd_Period(7:0)
    private const ushort BDS_06_Ev_CNT_Period_15_0 = 0x0321;   // --RD -- BDS_06_Ev_CNT(7:0) & BDS_06_Ev_Period(7:0)
    private const ushort BDS_06_Odd_CNT_Period_15_0 = 0x0322;  // --RD -- BDS_06_Odd_CNT(7:0) & BDS_06_Odd_Period(7:0)
    private const ushort BDS_08_CNT_Period_15_0 = 0x0323;      // --RD -- BDS_08_CNT(7:0) & BDS_08_Period(7:0)
    private const ushort BDS_09_Vel_CNT_Period_15_0 = 0x0324;  // --RD -- BDS_09_VEL_CNT(7:0) & BDS_09_VEL_Period(7:0)
    private const ushort BDS_09_Air_CNT_Period_15_0 = 0x0325;  // --RD -- BDS_09_AIR_CNT(7:0) & BDS_09_AIR_Period(7:0)
    private const ushort BDS_0A_CNT_Period_15_0 = 0x0326;      // --RD -- BDS_0A_CNT(7:0) & BDS_0A_Period(7:0)
    private const ushort BDS_61_E_CNT_Period_15_0 = 0x0327;    // --RD -- BDS_61_E_CNT(7:0) & BDS_61_E_Period(7:0)
    private const ushort BDS_61_T_CNT_Period_15_0 = 0x0328;    // --RD -- BDS_61_T_CNT(7:0) & BDS_61_T_Period(7:0)
    private const ushort BDS_62_Old_CNT_Period_15_0 = 0x0329;  // --RD -- BDS_62_Old_CNT(7:0) & BDS_62_Old_Period(7:0)
    private const ushort BDS_62_New_CNT_Period_15_0 = 0x032A;  // --RD -- BDS_62_New_CNT(7:0) & BDS_62_New_Period(7:0)
    private const ushort BDS_65_Air_CNT_Period_15_0 = 0x032B;  // --RD -- BDS_65_Air_CNT(7:0) & BDS_65_Air_Period(7:0)
    private const ushort BDS_65_Sur_CNT_Period_15_0 = 0x032C;  // --RD -- BDS_65_Sur_CNT(7:0) & BDS_65_Sur_Period(7:0)
    private const ushort DF11_SKW_CNT_Period_15_0 = 0x032D;    // --RD -- DF11_SKW_CNT(7:0) & DF11_SKW_Period(7:0)
    private const ushort DF_RX_CNT = 0x032E;                    // --RD -- DFxx_CNT(15:0)

    private const ushort BDS_05_Ev_111_96 = 0x0400;  // --RD -- BDS_05_Ev
    private const ushort BDS_05_Ev_95_80 = 0x0401;   // --RD
    private const ushort BDS_05_Ev_79_64 = 0x0402;   // --RD
    private const ushort BDS_05_Ev_63_48 = 0x0403;   // --RD
    private const ushort BDS_05_Ev_47_32 = 0x0404;   // --RD
    private const ushort BDS_05_Ev_31_16 = 0x0405;   // --RD
    private const ushort BDS_05_Ev_15_0 = 0x0406;    // --RD

    private const ushort BDS_05_Odd_111_96 = 0x0407; // --RD -- BDS_05_Odd
    private const ushort BDS_05_Odd_95_80 = 0x0408;  // --RD
    private const ushort BDS_05_Odd_79_64 = 0x0409;  // --RD
    private const ushort BDS_05_Odd_63_48 = 0x040A;  // --RD
    private const ushort BDS_05_Odd_47_32 = 0x040B;  // --RD
    private const ushort BDS_05_Odd_31_16 = 0x040C;  // --RD
    private const ushort BDS_05_Odd_15_0 = 0x040D;   // --RD

    private const ushort BDS_06_Ev_111_96 = 0x040E;  // --RD -- BDS_06_Ev
    private const ushort BDS_06_Ev_95_80 = 0x040F;   // --RD
    private const ushort BDS_06_Ev_79_64 = 0x0410;   // --RD
    private const ushort BDS_06_Ev_63_48 = 0x0411;   // --RD
    private const ushort BDS_06_Ev_47_32 = 0x0412;   // --RD
    private const ushort BDS_06_Ev_31_16 = 0x0413;   // --RD
    private const ushort BDS_06_Ev_15_0 = 0x0414;    // --RD

    private const ushort BDS_06_Odd_111_96 = 0x0415; // --RD -- BDS_06_Odd
    private const ushort BDS_06_Odd_95_80 = 0x0416;  // --RD
    private const ushort BDS_06_Odd_79_64 = 0x0417;  // --RD
    private const ushort BDS_06_Odd_63_48 = 0x0418;  // --RD
    private const ushort BDS_06_Odd_47_32 = 0x0419;  // --RD
    private const ushort BDS_06_Odd_31_16 = 0x041A;  // --RD
    private const ushort BDS_06_Odd_15_0 = 0x041B;   // --RD

    private const ushort BDS_08_111_96 = 0x041C;     // --RD -- BDS_08
    private const ushort BDS_08_95_80 = 0x041D;      // --RD
    private const ushort BDS_08_79_64 = 0x041E;      // --RD
    private const ushort BDS_08_63_48 = 0x041F;      // --RD
    private const ushort BDS_08_47_32 = 0x0420;      // --RD
    private const ushort BDS_08_31_16 = 0x0421;      // --RD
    private const ushort BDS_08_15_0 = 0x0422;       // --RD

    private const ushort BDS_09_Vel_111_96 = 0x0423; // --RD -- BDS_09_VEL
    private const ushort BDS_09_Vel_95_80 = 0x0424;  // --RD
    private const ushort BDS_09_Vel_79_64 = 0x0425;  // --RD
    private const ushort BDS_09_Vel_63_48 = 0x0426;  // --RD
    private const ushort BDS_09_Vel_47_32 = 0x0427;  // --RD
    private const ushort BDS_09_Vel_31_16 = 0x0428;  // --RD
    private const ushort BDS_09_Vel_15_0 = 0x0429;   // --RD

    private const ushort BDS_09_Air_111_96 = 0x042A; // --RD -- BDS_09_AIR
    private const ushort BDS_09_Air_95_80 = 0x042B;  // --RD
    private const ushort BDS_09_Air_79_64 = 0x042C;  // --RD
    private const ushort BDS_09_Air_63_48 = 0x042D;  // --RD
    private const ushort BDS_09_Air_47_32 = 0x042E;  // --RD
    private const ushort BDS_09_Air_31_16 = 0x042F;  // --RD
    private const ushort BDS_09_Air_15_0 = 0x0430;   // --RD

    private const ushort BDS_0A_111_96 = 0x0431;     // --RD -- BDS_0A
    private const ushort BDS_0A_95_80 = 0x0432;      // --RD
    private const ushort BDS_0A_79_64 = 0x0433;      // --RD
    private const ushort BDS_0A_63_48 = 0x0434;      // --RD
    private const ushort BDS_0A_47_32 = 0x0435;      // --RD
    private const ushort BDS_0A_31_16 = 0x0436;      // --RD
    private const ushort BDS_0A_15_0 = 0x0437;       // --RD

    private const ushort BDS_61_E_111_96 = 0x0438;   // --RD -- BDS_61_E
    private const ushort BDS_61_E_95_80 = 0x0439;    // --RD
    private const ushort BDS_61_E_79_64 = 0x043A;    // --RD
    private const ushort BDS_61_E_63_48 = 0x043B;    // --RD
    private const ushort BDS_61_E_47_32 = 0x043C;    // --RD
    private const ushort BDS_61_E_31_16 = 0x043D;    // --RD
    private const ushort BDS_61_E_15_0 = 0x043E;     // --RD

    private const ushort BDS_61_T_111_96 = 0x043F;   // --RD -- BDS_61_T
    private const ushort BDS_61_T_95_80 = 0x0440;    // --RD
    private const ushort BDS_61_T_79_64 = 0x0441;    // --RD
    private const ushort BDS_61_T_63_48 = 0x0442;    // --RD
    private const ushort BDS_61_T_47_32 = 0x0443;    // --RD
    private const ushort BDS_61_T_31_16 = 0x0444;    // --RD
    private const ushort BDS_61_T_15_0 = 0x0445;     // --RD

    private const ushort BDS_62_Old_111_96 = 0x0446; // --RD -- BDS_62_Old
    private const ushort BDS_62_Old_95_80 = 0x0447;  // --RD
    private const ushort BDS_62_Old_79_64 = 0x0448;  // --RD
    private const ushort BDS_62_Old_63_48 = 0x0449;  // --RD
    private const ushort BDS_62_Old_47_32 = 0x044A;  // --RD
    private const ushort BDS_62_Old_31_16 = 0x044B;  // --RD
    private const ushort BDS_62_Old_15_0 = 0x044C;   // --RD

    private const ushort BDS_62_New_111_96 = 0x044D; // --RD -- BDS_62_New
    private const ushort BDS_62_New_95_80 = 0x044E;  // --RD
    private const ushort BDS_62_New_79_64 = 0x044F;  // --RD
    private const ushort BDS_62_New_63_48 = 0x0450;  // --RD
    private const ushort BDS_62_New_47_32 = 0x0451;  // --RD
    private const ushort BDS_62_New_31_16 = 0x0452;  // --RD
    private const ushort BDS_62_New_15_0 = 0x0453;   // --RD

    private const ushort BDS_65_Air_111_96 = 0x0454; // --RD -- BDS_65_Air
    private const ushort BDS_65_Air_95_80 = 0x0455;  // --RD
    private const ushort BDS_65_Air_79_64 = 0x0456;  // --RD
    private const ushort BDS_65_Air_63_48 = 0x0457;  // --RD
    private const ushort BDS_65_Air_47_32 = 0x0458;  // --RD
    private const ushort BDS_65_Air_31_16 = 0x0459;  // --RD
    private const ushort BDS_65_Air_15_0 = 0x045A;   // --RD

    private const ushort BDS_65_Sur_111_96 = 0x045B; // --RD -- BDS_65_Sur
    private const ushort BDS_65_Sur_95_80 = 0x045C;  // --RD
    private const ushort BDS_65_Sur_79_64 = 0x045D;  // --RD
    private const ushort BDS_65_Sur_63_48 = 0x045E;  // --RD
    private const ushort BDS_65_Sur_47_32 = 0x045F;  // --RD
    private const ushort BDS_65_Sur_31_16 = 0x0460;  // --RD
    private const ushort BDS_65_Sur_15_0 = 0x0461;   // --RD

    private const ushort DF11_SKW_55_40 = 0x0462;    // --RD -- DF11_SKW
    private const ushort DF11_SKW_39_24 = 0x0463;    // --RD
    private const ushort DF11_SKW_23_8  = 0x0464;    // --RD
    private const ushort DF11_SKW_7_0   = 0x0465;    // --RD
    
    
    
    
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

    public async Task WriteModeACControl(bool modeAP2SlsPulseEn, bool modeCP2SlsPulseEn, bool modeAP2SlsPulseAtt,
        bool modeCP2SlsPulseAtt, bool allCallModeAC_A, bool allCallModeAC_C, bool allCallModeS_A, bool allCallModeS_C)
    {
        var reg = await ReadCustomRegister(ModeA_C_Control, DisposeCancel).ConfigureAwait(false);
        if (modeAP2SlsPulseEn) reg |= 0x1;
        if (modeCP2SlsPulseEn) reg |= 0x2;
        if (modeAP2SlsPulseAtt) reg |= 0x4;
        if (modeCP2SlsPulseAtt) reg |= 0x8;
        if (allCallModeAC_A) reg |= 0x10;
        if (allCallModeAC_C) reg |= 0x20;
        if (allCallModeS_A) reg |= 0x40;
        if (allCallModeS_C) reg |= 0x80;
        await WriteCustomRegister(ModeA_C_Control, reg, DisposeCancel).ConfigureAwait(false);
    }

    public async Task WriteModeSControl(bool modeSP5SlsPulseEn, bool modeSP5SlsPulseAtt)
    {
        var reg = await ReadCustomRegister(ModeS_Control, DisposeCancel).ConfigureAwait(false);
        if (modeSP5SlsPulseEn) reg |= 0x100;
        if (modeSP5SlsPulseAtt) reg |= 0x200;
        await WriteCustomRegister(ModeS_Control, reg, DisposeCancel).ConfigureAwait(false);
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

    public async Task<int> ReadModeCAltitude()
    {
        var code = await ReadCustomRegister(ModeCResp_15_0_InternAddr, DisposeCancel).ConfigureAwait(false);
        return ModeSHelper.GetAltitudeFromModeCAltitudeCode((ushort)((code >> 1) & 0xFFF)) ?? 0;
    }

    public async Task<bool> WriteUfMessage(ModeSUFormatBase msg)
    {
        if (Interlocked.CompareExchange(ref _readDfMsgFlag, 1, 0) != 0) return false;
        try
        {
            return await InternalWriteUfMessage(msg).ConfigureAwait(false);
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
            await WriteUfFrame(CreateUfFrame(msg)).ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        
    }

    private async Task<(bool Success, ushort CounterBefore)> InternalWriteUfMessageWithCounterSnapshot(ModeSUFormatBase msg)
    {
        try
        {
            var frame = CreateUfFrame(msg);
            ushort counterBefore = 0;
            await AtomicEditRegister(edit =>
            {
                foreach (var addressValuePair in frame)
                {
                    WriteCustomRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
                }

                counterBefore = ReadCustomRegister(edit, DF_RX_CNT);
                edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 1, 1, 1);
                edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 1, 1, 0);
            }, DisposeCancel).ConfigureAwait(false);

            return (true, counterBefore);
        }
        catch (Exception)
        {
            return (false, 0);
        }
    }

    private Task WriteUfFrame(ValueTuple<ushort, ushort>[] frame)
    {
        return WriteCustomRegistersFrame(frame, DisposeCancel);
    }

    private static ValueTuple<ushort, ushort>[] CreateUfFrame(ModeSUFormatBase msg)
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
        return frame;
    }

    public async Task<ModeSDFormatBase?> ReadDfMessage(Func<ModeSDFormatBase> factory, int attempts = 3)
    {
        if (Interlocked.CompareExchange(ref _readDfMsgFlag, 1, 0) != 0) return null;
        try
        {
            return await InternalReadDfMessage(factory, GetDfResponseTimeoutFromAttempts(attempts)).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _readDfMsgFlag, 0);
        }
    }

    private async Task<T?> InternalReadDfMessage<T>(Func<T> factory, int timeoutMs = DefaultDfResponseTimeoutMs,
        int pollIntervalMs = DefaultDfPollIntervalMs, ushort? counterBefore = null, Func<T, bool>? isValid = null)
        where T : ModeSDFormatBase
    {
        var length = factory().GetByteSize();
        var dfRegisterCount = length % 2 == 0 ? length / 2 : (length / 2) + 1;
        var addrFrame = new ushort[dfRegisterCount + (counterBefore.HasValue ? 1 : 0)];
        for (var i = 0; i < dfRegisterCount; i++)
        {
            addrFrame[i] = (ushort)(DF_RX_111_96 + i);
        }
        if (counterBefore.HasValue)
        {
            addrFrame[^1] = DF_RX_CNT;
        }

        var timeoutAt = Environment.TickCount64 + Math.Max(1, timeoutMs);
        var lastCounter = counterBefore;
        do
        {
            try
            {
                if (pollIntervalMs > 0)
                {
                    await Task.Delay(pollIntervalMs, DisposeCancel).ConfigureAwait(false);
                }

                var valueFrame = await ReadCustomRegistersFrame(addrFrame, DisposeCancel).ConfigureAwait(false);
                if (lastCounter.HasValue)
                {
                    var counter = valueFrame[^1];
                    if (counter == lastCounter.Value)
                    {
                        continue;
                    }

                    lastCounter = counter;
                }

                var buffer = ConvertRegisterFrameToBytes(valueFrame, length);
                var span = new ReadOnlySpan<byte>(buffer);
                var msg = factory();
                msg.Deserialize(ref span);
                if (isValid == null || isValid(msg))
                {
                    return msg;
                }
            }
            catch (InvalidDataException)
            {
                // ignore
            }
            catch (FormatException)
            {
                // ignore
            }
        } while (Environment.TickCount64 < timeoutAt);

        return null;
    }

    public async Task<ModeSDFormatBase?> ReadDfMessage(ModeSUFormatBase reqMsg, Func<ModeSDFormatBase> respFactory, int attempts = 3)
    {
        if (Interlocked.CompareExchange(ref _readDfMsgFlag, 1, 0) != 0) return null;
        try
        {
            var writeResult = await InternalWriteUfMessageWithCounterSnapshot(reqMsg).ConfigureAwait(false);
            if (!writeResult.Success)
            {
                return null;
            }

            return await InternalReadDfMessage(respFactory, GetDfResponseTimeoutFromAttempts(attempts),
                counterBefore: writeResult.CounterBefore).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _readDfMsgFlag, 0);
        }
    }

    private async Task<T?> RequestDfMessage<T>(ModeSUFormatBase reqMsg, Func<T> respFactory, uint expectedIcao,
        Func<T, bool>? isValid = null, int timeoutMs = DefaultDfResponseTimeoutMs)
        where T : ModeSDFormatBase
    {
        if (Interlocked.CompareExchange(ref _readDfMsgFlag, 1, 0) != 0) return null;
        try
        {
            var writeResult = await InternalWriteUfMessageWithCounterSnapshot(reqMsg).ConfigureAwait(false);
            if (!writeResult.Success)
            {
                return null;
            }

            return await InternalReadDfMessage(
                respFactory,
                timeoutMs,
                counterBefore: writeResult.CounterBefore,
                isValid: msg => msg.IcaoAddress == expectedIcao && isValid?.Invoke(msg) != false).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _readDfMsgFlag, 0);
        }
    }

    private static int GetDfResponseTimeoutFromAttempts(int attempts)
    {
        return Math.Max(1, attempts) * DefaultDfPollIntervalMs;
    }

    public async Task<(byte Counter, float Period)> ReadDf11SquitterStatistics()
    {
        var reg = await ReadCustomRegister(DF11_SKW_CNT_Period_15_0, DisposeCancel).ConfigureAwait(false);
        var cnt = (byte)(reg >> 8);
        var per = ReadSquitterPeriodSeconds(reg);
        return (Counter: cnt, Period: per);
    }

    public async Task<ExSquitterStatistics> ReadExSquitterStatistics()
    {
        var addrFrame = new ushort[14];
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
                Bds62New = (byte)(valueFrame[11] >> 8),
                Bds65Airborne = (byte)(valueFrame[12] >> 8),
                Bds65Surface = (byte)(valueFrame[13] >> 8),
            },
            Periods =
            {
                Bds05Even = ReadSquitterPeriodSeconds(valueFrame[0]),
                Bds05Odd = ReadSquitterPeriodSeconds(valueFrame[1]),
                Bds06Even = ReadSquitterPeriodSeconds(valueFrame[2]),
                Bds06Odd = ReadSquitterPeriodSeconds(valueFrame[3]),
                Bds08 = ReadSquitterPeriodSeconds(valueFrame[4]),
                Bds09GroundSpeed = ReadSquitterPeriodSeconds(valueFrame[5]),
                Bds09Airspeed = ReadSquitterPeriodSeconds(valueFrame[6]),
                Bds0A = ReadSquitterPeriodSeconds(valueFrame[7]),
                Bds61EmergencyPriorityStatus = ReadSquitterPeriodSeconds(valueFrame[8]),
                Bds61TcasRaBroadcast = ReadSquitterPeriodSeconds(valueFrame[9]),
                Bds62Old = ReadSquitterPeriodSeconds(valueFrame[10]),
                Bds62New = ReadSquitterPeriodSeconds(valueFrame[11]),
                Bds65Airborne = ReadSquitterPeriodSeconds(valueFrame[12]),
                Bds65Surface = ReadSquitterPeriodSeconds(valueFrame[13]),
            }
        };
        return result;
    }

    private static float ReadSquitterPeriodSeconds(ushort value)
    {
        var period = value & 0xFF;
        return period == 0xFF ? float.PositiveInfinity : period * 0.2f;
    }

    public Task<AdsbAirbornePosition?> ReadExBds05Even()
    {
        return ReadExMessage(BDS_05_Ev_111_96, CreateAirbornePositionMessage);
    }

    public Task<AdsbAirbornePosition?> ReadExBds05Odd()
    {
        return ReadExMessage(BDS_05_Odd_111_96, CreateAirbornePositionMessage);
    }

    public Task<AdsbSurfacePosition?> ReadExBds06Even()
    {
        return ReadExMessage(BDS_06_Ev_111_96, () => new AdsbSurfacePosition());
    }

    public Task<AdsbSurfacePosition?> ReadExBds06Odd()
    {
        return ReadExMessage(BDS_06_Odd_111_96, () => new AdsbSurfacePosition());
    }

    public Task<AdsbAircraftIdentification?> ReadExBds08Id()
    {
        return ReadExMessage(BDS_08_111_96, () => new AdsbAircraftIdentification());
    }

    public Task<AdsbGroundSpeed?> ReadExBds09GroundSpeed()
    {
        return ReadExMessage(BDS_09_Vel_111_96, () => new AdsbGroundSpeed(), IsGroundSpeedMessage);
    }

    public Task<AdsbAirspeed?> ReadExBds09Airspeed()
    {
        return ReadExMessage(BDS_09_Air_111_96, () => new AdsbAirspeed(), IsAirspeedMessage);
    }

    public Task<AdsbAircraftEmergencyStatus?> ReadExBds61EmergencyPriorityStatus()
    {
        return ReadExMessage(BDS_61_E_111_96, () => new AdsbAircraftEmergencyStatus());
    }

    public Task<AdsbAircraftAcasRaBroadcast?> ReadExBds61TcasRaBroadcast()
    {
        return ReadExMessage(BDS_61_T_111_96, () => new AdsbAircraftAcasRaBroadcast());
    }

    public Task<AdsbTargetStateAndStatusInformation?> ReadExBds62Old()
    {
        return ReadExMessage(BDS_62_Old_111_96, () => new AdsbTargetStateAndStatusInformation());
    }

    public Task<AdsbTargetStateAndStatusInformation?> ReadExBds62New()
    {
        return ReadExMessage(BDS_62_New_111_96, () => new AdsbTargetStateAndStatusInformation());
    }

    public Task<AdsbAircraftOperationStatus?> ReadExBds65Airborne()
    {
        return ReadExMessage<AdsbAircraftOperationStatus>(BDS_65_Air_111_96, () => new AdsbAircraftOperationStatusV0());
    }

    public Task<AdsbAircraftOperationStatus?> ReadExBds65Surface()
    {
        return ReadExMessage<AdsbAircraftOperationStatus>(BDS_65_Sur_111_96, () => new AdsbAircraftOperationStatusV1());
    }

    private Task<T?> ReadExMessage<T>(ushort startAddress, Func<T> factory)
        where T : AdsbExtendedSquitterBase
    {
        return ReadExMessage(startAddress, _ => factory());
    }

    private Task<T?> ReadExMessage<T>(ushort startAddress, Func<T> factory, Func<T, bool> isValid)
        where T : AdsbExtendedSquitterBase
    {
        return ReadExMessage(startAddress, _ => factory(), isValid);
    }

    private async Task<T?> ReadExMessage<T>(ushort startAddress, Func<byte[], T?> factory, Func<T, bool>? isValid = null)
        where T : AdsbExtendedSquitterBase
    {
        var addrFrame = new ushort[7];
        for (ushort i = 0; i < addrFrame.Length; i++)
        {
            addrFrame[i] = (ushort)(startAddress + i);
        }

        var valueFrame = await ReadCustomRegistersFrame(addrFrame, DisposeCancel).ConfigureAwait(false);
        var buffer = ConvertRegisterFrameToBytes(valueFrame, addrFrame.Length * 2);

        var span = new ReadOnlySpan<byte>(buffer);
        try
        {
            var msg = factory(buffer);
            if (msg == null)
            {
                return null;
            }

            msg.Deserialize(ref span);
            return isValid == null || isValid(msg) ? msg : null;
        }
        catch (Exception)
        {
            // ignored
        }

        return null;
    }

    private static AdsbAirbornePosition? CreateAirbornePositionMessage(byte[] buffer)
    {
        var span = new ReadOnlySpan<byte>(buffer);
        return TransponderHelper.GetMessageType(span) switch
        {
            AdsbMessageTypeEnum.AirborneBarometricPosition => new AdsbAirbornePositionWithBaroAlt(),
            AdsbMessageTypeEnum.AirborneGnssPosition => new AdsbAirbornePositionWithGnssAlt(),
            _ => null
        };
    }

    private static bool IsGroundSpeedMessage(AdsbGroundSpeed msg)
    {
        return msg.SubType is VelocitySubTypeEnum.SubType1 or VelocitySubTypeEnum.SubType2;
    }

    private static bool IsAirspeedMessage(AdsbAirspeed msg)
    {
        return msg.SubType is VelocitySubTypeEnum.SubType3 or VelocitySubTypeEnum.SubType4;
    }
    
    public async Task<ModeSDF11?> ReadDf11Squitter()
    {
        var addrFrame = new ushort[4];
        for (ushort i = 0; i < addrFrame.Length; i++)
        {
            addrFrame[i] = (ushort)(DF11_SKW_55_40 + i);
        }
        var valueFrame = await ReadCustomRegistersFrame(addrFrame, DisposeCancel).ConfigureAwait(false);
        var buffer = ConvertRegisterFrameToBytes(valueFrame, 7);
        
        var span = new ReadOnlySpan<byte>(buffer);
        ModeSDF11? msg = null;
        try
        {
            msg = new ModeSDF11(0, 0);
            msg.Deserialize(ref span);
            return msg;
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

    public async Task<ushort> ReadSelectiveDfCounter()
    {
        return await ReadCustomRegister(DF_RX_CNT, DisposeCancel).ConfigureAwait(false);
    }

    public Task<ModeSDF4?> ReadModeSDf4(uint icao)
    {
        return RequestDfMessage(new ModeSUF4 { IcaoAddress = icao, RR = 0, DI = 0 }, () => new ModeSDF4(), icao);
    }

    public Task<ModeSDF20?> ReadModeSDf20(uint icao, byte bds)
    {
        ValidateBdsRegister(bds);
        var rr = (byte)(16 + (bds >> 4));
        var rrs = (byte)(bds & 0xF);
        return RequestDfMessage(
            new ModeSUF4 { IcaoAddress = icao, RR = rr, DI = 7, RRS = rrs },
            () => new ModeSDF20 { Bds = GetBdsRegister(bds) },
            icao,
            msg => HasBdsDataSelector(msg, bds));
    }

    public Task<ModeSDF20?> ReadModeSDf20(uint icao, byte ads, byte bds)
    {
        ValidateBdsRegister(bds);
        var adsMsg = GetAdsRegister(ads);
        var rr = (byte)(16 + (bds >> 4));
        var rrs = (byte)(bds & 0xF);
        return RequestDfMessage(
            new ModeSUF20 { IcaoAddress = icao, RR = rr, DI = 7, RRS = rrs, Ads = adsMsg },
            () => new ModeSDF20 { Bds = GetBdsRegister(bds) },
            icao,
            msg => HasBdsDataSelector(msg, bds));
    }

    public Task<ModeSDF4?> ReadModeSDf4(uint icao, byte ads)
    {
        var adsMsg = GetAdsRegister(ads);
        return RequestDfMessage(
            new ModeSUF20 { IcaoAddress = icao, RR = 0, DI = 0, Ads = adsMsg },
            () => new ModeSDF4(),
            icao);
    }

    public Task<ModeSDF5?> ReadModeSDf5(uint icao)
    {
        return RequestDfMessage(new ModeSUF5 { IcaoAddress = icao, RR = 0, DI = 0 }, () => new ModeSDF5(), icao);
    }

    public Task<ModeSDF21?> ReadModeSDf21(uint icao, byte bds)
    {
        ValidateBdsRegister(bds);
        var rr = (byte)(16 + (bds >> 4));
        var rrs = (byte)(bds & 0xF);
        return RequestDfMessage(
            new ModeSUF5 { IcaoAddress = icao, RR = rr, DI = 7, RRS = rrs },
            () => new ModeSDF21 { Bds = GetBdsRegister(bds) },
            icao,
            msg => HasBdsDataSelector(msg, bds));
    }

    public Task<ModeSDF21?> ReadModeSDf21(uint icao, byte ads, byte bds)
    {
        ValidateBdsRegister(bds);
        var adsMsg = GetAdsRegister(ads);
        var rr = (byte)(16 + (bds >> 4));
        var rrs = (byte)(bds & 0xF);
        return RequestDfMessage(
            new ModeSUF21 { IcaoAddress = icao, RR = rr, DI = 7, RRS = rrs, Ads = adsMsg },
            () => new ModeSDF21 { Bds = GetBdsRegister(bds) },
            icao,
            msg => HasBdsDataSelector(msg, bds));
    }

    public Task<ModeSDF5?> ReadModeSDf5(uint icao, byte ads)
    {
        var adsMsg = GetAdsRegister(ads);
        return RequestDfMessage(
            new ModeSUF21 { IcaoAddress = icao, RR = 0, DI = 0, Ads = adsMsg },
            () => new ModeSDF5(),
            icao);
    }

    public Task<ModeSDF0?> ReadModeSDf0(uint icao)
    {
        return RequestDfMessage(
            new ModeSUF0 { IcaoAddress = icao, ReplyLength = 0, Acquisition = 1 },
            () => new ModeSDF0(),
            icao);
    }

    public Task<ModeSDF16?> ReadModeSDf16(uint icao, byte bds)
    {
        ValidateBdsRegister(bds);
        return RequestDfMessage(
            new ModeSUF0 { IcaoAddress = icao, ReplyLength = 1, Acquisition = 0, DataSelector = bds },
            () => new ModeSDF16 { Bds = GetBdsRegister(bds) },
            icao,
            msg => HasBdsDataSelector(msg, bds));
    }
    
    public Task<ModeSDF16?> ReadModeSDf16(uint icao, byte ads, byte bds)
    {
        ValidateBdsRegister(bds);
        var adsMsg = GetAdsRegister(ads);
        return RequestDfMessage(
            new ModeSUF16 { IcaoAddress = icao, ReplyLength = 1, Acquisition = 0, DataSelector = bds, Ads = adsMsg },
            () => new ModeSDF16 { Bds = GetBdsRegister(bds) },
            icao,
            msg => HasBdsDataSelector(msg, bds));
    }
    
    public Task<ModeSDF0?> ReadModeSDf0(uint icao, byte ads)
    {
        var adsMsg = GetAdsRegister(ads);
        return RequestDfMessage(
            new ModeSUF16 { IcaoAddress = icao, ReplyLength = 0, Acquisition = 1, Ads = adsMsg },
            () => new ModeSDF0(),
            icao);
    }

    private static bool HasBdsDataSelector(ModeSDFormatBase msg, byte bds)
    {
        return msg.Bds is BdsAny || msg.Bds?.DataSelector == bds;
    }

    private static void ValidateBdsRegister(byte bds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bds, 0x10, nameof(bds));
    }
    
    private static AdsBase GetAdsRegister(byte ads)
    {
        switch (ads)
        {
            case 0x05: return new Ads05();
        }
        var ads1 = (byte)(ads >> 4);
        var ads2 = (byte)(ads & 0xF);
        return new AdsAny(ads1, ads2);
    }
    private static BdsBase GetBdsRegister(byte bds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bds, 0x10, nameof(bds));
        switch (bds)
        {
            case 0x10:
                return new Bds10();
            case 0x17:
                return new Bds17();
            case 0x20:
                return new Bds20();
            case 0x30:
                return new Bds30();
            case 0x40:
                return new Bds40();
            case 0x50:
                return new Bds50();
            case 0x60:
                return new Bds60();
        }
        var bds1 = (byte)(bds >> 4);
        var bds2 = (byte)(bds & 0xF);
        return new BdsAny(bds1, bds2);
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

