using System;
using System.Threading;
using System.Threading.Tasks;

namespace Asv.Sdr.LimeSdr;

public enum CustomWorkMode
{
    Normal = 0,
    DmeAir = 1,
    DmeGround = 2,
    AdsB = 3
}

public interface ILimeSdrCustomDevice : ILimeSdrDevice
{
    /// <summary>
    /// Get the Custom Mode.
    /// </summary>
    Task<bool> IsEnabled(CancellationToken cancel = default);

    /// <summary>
    /// Get Custom Mode Type
    /// </summary>
    Task<CustomWorkMode> GetMode(CancellationToken cancel = default);

    /// <summary>
    /// Turn on Custom Mode
    /// </summary>
    /// <returns></returns>
    Task TurnOnMode(CancellationToken cancel = default);
    
    /// <summary>
    /// Turn off Custom Mode
    /// </summary>
    /// <returns></returns>
    Task TurnOffMode(CancellationToken cancel = default);

    /// <summary>
    /// Resets the Custom Mode
    /// </summary>
    Task CustomModeReset(CancellationToken cancel = default);

    /// <summary>
    /// Is inverts the GPIO amplifier control signal
    /// </summary>
    Task<bool> GetInvertsGpioAmplifierControl(CancellationToken cancel = default);
    
    /// <summary>
    /// Inverts the GPIO amplifier control signal
    /// </summary>
    /// <param name="enabled">false - Non Invert, true - Invert</param>
    /// <param name="cancel"></param>
    Task SetInvertsGpioAmplifierControl(bool enabled, CancellationToken cancel = default);
    
    /// <summary>
    /// Is enable carrier suppression in silent mode by switching the internal RF key
    /// </summary>
    /// <returns></returns>
    Task<bool> GetInternalRfKeySwitchEnabled(CancellationToken cancel = default);
    
    /// <summary>
    /// Enable carrier suppression in silent mode by switching the internal RF key
    /// </summary>
    /// <param name="enabled"></param>
    /// <param name="cancel"></param>
    /// <returns></returns>
    Task SetInternalRfKeySwitchEnabled(bool enabled, CancellationToken cancel = default);

    Task<ushort> GetPeakAmplitude(CancellationToken cancel = default);

    // Task<ushort> ReadCustomRegister(ushort address, CancellationToken cancel = default);
    // Task WriteCustomRegister(ushort address, ushort value, CancellationToken cancel = default);
    //
    // Task WriteCustomRegistersFrame(ValueTuple<ushort, ushort>[] addressValuePairs, CancellationToken cancel = default);
    // Task<ushort[]> ReadCustomRegistersFrame(ushort[] address, CancellationToken cancel = default);
}