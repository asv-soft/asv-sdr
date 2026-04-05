using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Asv.Sdr.LimeSdr;


public interface ILimeSdrModeACRepDevice : ILimeSdrAdsbTransponderDevice
{
    /// <summary>
    /// Turn On/Off ADS-B
    /// </summary>
    Task TurnOnOffAdsB(bool enabled, CancellationToken cancel = default);
    Task WriteModeASquawkCode(string squawk, bool spi);
    Task WriteModeCAltitude(double altitude);
    Task WriteDelayOffsetModeAc(double offset);
    Task<(byte ModeACnt, byte ModeCCnt)> ReadAcCount();
}

public class LimeSdrModeACRepDevice : LimeSdrAdsbTransponderDevice, ILimeSdrModeACRepDevice
{
    private bool _isAdsbEnabled;
    private const ushort ModeAResp_15_0_InternAddr          = 0x004E; // (0,0,С1,А1,С2,А2,С4,А4,Х,В1,D1,В2,D2,В4,D4,SPI) -- WR
    private const ushort ModeCResp_15_0_InternAddr          = 0x004F; // (0,0,С1,А1,С2,А2,С4,А4,Х,В1,D1,В2,D2,В4,D4,0) -- WR
    private const ushort DelayOffsetAC_15_0_InternAddr      = 0x0050; // калибровочный коэффициент по дальности принятых сообщений A/C, signed -- WR
    private const ushort A_CNT_15_8_C_CNT_7_0_InternAddr    = 0x0051; // счетчики принятого сообщения A и С соответственно		--RD

    public LimeSdrModeACRepDevice(string deviceId, LimeSdrDeviceConfig config, bool isAdsbEnabled = true, ILogger? logger = null) : base(deviceId, config, logger)
    {
        _isAdsbEnabled = isAdsbEnabled;
        if (_isAdsbEnabled)
        {
            WriteCustomRegister(0x0046, 0x70, DisposeCancel).Wait(DisposeCancel);
        }
        else
        {
            WriteCustomRegister(0x0046, 0x00, DisposeCancel).Wait(DisposeCancel);
        }
    }
    
    protected override CustomWorkMode InternalGetMode()
    {
        return CustomWorkMode.AdsBModeACRep;
    }
    public async Task TurnOnOffAdsB(bool enabled, CancellationToken cancel = default)
    {
        await TurnOnOffDF17Id(enabled, cancel).ConfigureAwait(false);
        await TurnOnOffDF17Position(enabled, cancel).ConfigureAwait(false);
        await TurnOnOffDF17Velocity(enabled, cancel).ConfigureAwait(false);
    }

    public Task WriteModeASquawkCode(string squawk, bool spi)
    {
        var code = (ushort)(ModeSHelper.SetSquawk(squawk) << 1);
        if (spi) code |= 0x1;
        return WriteCustomRegister(ModeAResp_15_0_InternAddr, code, DisposeCancel);
    }

    public Task WriteModeCAltitude(double altitude)
    {
        var altCode = ModeSHelper.GetModeCAltitudeCodeFromAltitude((int)Math.Round(altitude));
        return WriteCustomRegister(ModeCResp_15_0_InternAddr, (ushort)(altCode << 1), DisposeCancel);
    }


    public Task WriteDelayOffsetModeAc(double offset)
    {
        var tacts = (int)Math.Round(offset * 40);
        if (tacts > short.MaxValue) tacts = short.MaxValue;
        if (tacts < short.MinValue) tacts = short.MinValue;
        var del = (ushort)tacts;
        return WriteCustomRegister(DelayOffsetAC_15_0_InternAddr, del, DisposeCancel);
    }

    public async Task<(byte ModeACnt, byte ModeCCnt)> ReadAcCount()
    {
        var data = await ReadCustomRegister(A_CNT_15_8_C_CNT_7_0_InternAddr, DisposeCancel).ConfigureAwait(false);
        return ((byte)((data >> 8) & 0xFF), (byte)(data & 0xFF));
    }
}