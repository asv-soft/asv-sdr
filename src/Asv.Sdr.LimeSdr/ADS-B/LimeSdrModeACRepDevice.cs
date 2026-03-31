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
}

public class LimeSdrModeACRepDevice : LimeSdrAdsbTransponderDevice, ILimeSdrModeACRepDevice
{
    private bool _isAdsbEnabled;

    public LimeSdrModeACRepDevice(string deviceId, LimeSdrDeviceConfig config, bool isAdsbEnabled = true, ILogger? logger = null) : base(deviceId, config, logger)
    {
        _isAdsbEnabled = isAdsbEnabled;
    }

    public Task TurnOnOffAdsB(bool enabled, CancellationToken cancel = default)
    {
        throw new System.NotImplementedException();
    }
    
    private static uint SetAltitude(double alt)
    {
        if (double.IsNaN(alt)) return 0;
    
        alt *= 3.28084;
        var altNorm = alt switch
        {
            > 50187.5 => (int)Math.Round(alt / 100.0, 0) * 100,
            < -1000.0 => -1000,
            _ => (int)Math.Round(alt / 25.0) * 25
        };
    
        if (altNorm > 50175)
        {
            var nAlt = 0u; //SetAltitudeToModeC(altNorm);
            return (uint)(((nAlt & 0x7E0) << 2) | ((nAlt & 0x10) << 1) | (nAlt & 0xF));
        }
    
        altNorm = (altNorm + 1000) / 25;
        return (uint)(((altNorm & 0x7E0) << 2) | ((altNorm & 0x10) << 1) | 0x10 | (altNorm & 0xF));
    }
}