using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Asv.Sdr.LimeSdr;


public interface ILimeSdrModeSRepDevice
{
    /// <summary>
    /// Turn On/Off ADS-B
    /// </summary>
    Task TurnOnOffAdsB(bool enabled, CancellationToken cancel = default);
}



public class LimeSdrModeSRepDevice : LimeSdrAdsbTransponderDevice, ILimeSdrModeSRepDevice
{
    private bool _isAdsbEnabled;

    private const ushort ADSB_MASK = 0x0070;
    private const ushort MODES_LEVEL1_MASK = 0x000F;
    private const ushort MODES_LEVEL2_MASK = 0x0300;
    
    public LimeSdrModeSRepDevice(string deviceId, LimeSdrAdsbRepDeviceConfig config, bool isAdsbEnabled = true, ILogger? logger = null) : base(deviceId, config, logger)
    {
        _isAdsbEnabled = isAdsbEnabled;
    }

    public override async Task TurnOnOffMode(bool enabled, CancellationToken cancel = default)
    {
        if (enabled)
        {
            var val = _isAdsbEnabled ? (ushort)(ADSB_MASK | MODES_LEVEL1_MASK) : MODES_LEVEL1_MASK;
            // Включаем ответы на любые запросы DF11, DF4, DF5, если нужно включаем отправку ADS-B
            await WriteCustomRegister(0x0046, val, cancel).ConfigureAwait(false);
            await TurnOnMode(cancel);
            await Task.Delay(1500, cancel).ConfigureAwait(false);
            // На всякий пож еще раз: Включаем ответы на любые запросы DF11, DF4, DF5, если нужно включаем отправку ADS-B
            await WriteCustomRegister(0x0046, val, cancel).ConfigureAwait(false);
        }
        else
        {
            await TurnOffMode(cancel).ConfigureAwait(false);
        }
    }

    public Task TurnOnOffAdsB(bool enabled, CancellationToken cancel = default)
    {
        return enabled
            ? WriteCustomRegisterBits(0x0046, 4, 3, 0x7, cancel)
            : WriteCustomRegisterBits(0x0046, 4, 3, 0x0, cancel);
    }
}

