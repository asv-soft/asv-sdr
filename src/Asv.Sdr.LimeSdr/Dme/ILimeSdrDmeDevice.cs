using System.Threading;
using System.Threading.Tasks;

namespace Asv.Sdr.LimeSdr;

public enum DmeWorkMode
{
    Air = 0,
    Ground = 1
}

public enum DmeChannel
{
    XChannel = 0,
    YChannel = 1
}

public enum EnvelopeType
{
    Gauss = 0,
    Rectangle = 1
}


/// <summary>
/// Custom extension interface for LimeSdr mini device
/// WOrk only with custom FPGA firmware
/// </summary>
public interface ILimeSdrDmeDevice:ILimeSdrDevice
{
    /// <summary>
    /// Gets the DME mode.
    /// </summary>
    Task<bool> DmeIsEnabled(CancellationToken cancel = default);

    /// <summary>
    /// Gets the DME mode.
    /// </summary>
    Task DmeSetIsEnabled(bool enabled, CancellationToken cancel = default);

    /// <summary>
    /// Get DME Type
    /// </summary>
    Task<DmeWorkMode> DmeGetMode(CancellationToken cancel = default);

    /// <summary>
    /// Set DME Type
    /// </summary>
    /// <returns></returns>
    Task DmeSetMode(DmeWorkMode mode, CancellationToken cancel = default);

    /// <summary>
    /// Resets the DME mode
    /// </summary>
    Task DmeReset(CancellationToken cancel = default);

    /// <summary>
    /// Gets the Is ISO Enabled value for DME mode.
    /// </summary>
    Task<bool> DmeGetIsIsoEnabled(CancellationToken cancel = default);

    /// <summary>
    /// Sets the DME ISO mode.
    /// </summary>
    Task DmeSetIsoEnabled(bool enabled, CancellationToken cancel = default);

    /// <summary>
    /// Retrieves the current DME channel.
    /// </summary>
    Task<DmeChannel> DmeGetChannel(CancellationToken cancel = default);

    /// <summary>
    /// Sets the DME channel.
    /// </summary>
    Task DmeSetChannel(DmeChannel value, CancellationToken cancel = default);

    /// <summary>
    /// Gets the value indicating whether zero self calibration is enabled for DME (Distance Measuring Equipment).
    /// </summary>
    Task<bool> DmeGetIsZeroCalibrationEnabled(CancellationToken cancel = default);

    /// <summary>
    /// Sets the status of zero calibration for the DME device.
    /// </summary>
    Task DmeSetIsZeroCalibrationEnabled(bool value, CancellationToken cancel = default);

    /// <summary>
    /// Gets the envelope type for DME (Distance Measuring Equipment).
    /// </summary>
    Task<EnvelopeType> DmeGetEnvelopeType(CancellationToken cancel = default);
    /// <summary>
    /// Sets the envelope type for DME.
    /// </summary>
    Task DmeSetEnvelopeType(EnvelopeType value, CancellationToken cancel = default);

    Task DmeSetRelayInvert(bool isInvert, CancellationToken cancel = default);
    Task<bool> DmeGetRelayInvert(bool isInvert, CancellationToken cancel = default);
    /// <summary>
    /// Gets measured distance in meters (AIR mode).
    /// </summary>
    Task<int> DmeGetMeasuredDistance(CancellationToken cancel = default);
    /// <summary>
    /// Gets the HIP period in microseconds.
    /// Returns 0xFFFF if HIPs are disabled.
    /// </summary>
    Task<ushort> DmeGetHipPeriod(CancellationToken cancel = default);
    /// <summary>
    /// Sets the HIP period in microseconds.
    /// Set to 0xFFFF to disable HIPs.
    /// </summary>
    Task DmeSetHipPeriod(ushort periodMicroseconds, CancellationToken cancel = default);

    /// <summary>
    /// Gets the reply distance in meters (GROUND mode).
    /// </summary>
    Task<int> DmeGetReplyDistance(CancellationToken cancel = default);

    /// <summary>
    /// Sets reply distance in meters (GROUND mode).
    /// </summary>
    Task DmeSetReplyDistance(int distance, CancellationToken cancel = default);

    /// <summary>
    /// Gets the MAX_HITS value, which is the maximum number of hits out of 32 queries.
    /// </summary>
    Task<byte> DmeGetMaxHitsMeasure(CancellationToken cancel = default);

   

    /// <summary>
    /// Gets the PEAK_AMP value, which represents the maximum signal amplitude over the last 100 milliseconds.
    /// The gain should be adjusted to keep PEAK_AMP within the range 0x100 < PEAK_AMP < 0x800.
    /// </summary>
    Task<ushort> DmeGetPeakAmplitude(CancellationToken cancel = default);

    /// <summary>
    /// Gets the FREQ_ZD value, which represents the query frequency in Hz.
    /// The default is 100 Hz for AIR mode.
    /// </summary>
    Task<ushort> DmeGetRequestFrequency(CancellationToken cancel = default);

    /// <summary>
    /// Sets the FREQ_ZD value, which is the query frequency in Hz.
    /// Valid range: 1 Hz to 20,000 Hz.
    /// </summary>
    Task DmeSetRequestFrequency(ushort freqZd, CancellationToken cancel = default);

    /// <summary>
    /// Sets the DelayRx2Tx value, zero delay in meters.
    /// </summary>
    /// <param name="delayMeter">The delay value in meters.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DmeSetDelayRx2Tx(ushort delayMeter, CancellationToken cancel = default);

    /// <summary>
    /// Gets the DelayRx2Tx value, zero delay in meters.
    /// </summary>
    /// <param name="cancel">Cancellation token</param>
    /// <returns>The DelayRx2Tx value</returns>
    Task<ushort> DmeGetDelayRx2Tx(CancellationToken cancel = default);

    /// <summary>
    /// Gets the flag indicating the presence of active requests/responses.
    /// </summary>
    Task<bool> DmeGetSignalAvailableFlag(CancellationToken cancel = default);

    /// <summary>
    /// Reads data from the DEBUG_to_periphcfg register (0x00DE).
    /// </summary>
    Task<ushort> DmeReadDebugToPeriphCfg(CancellationToken cancel = default);

    /// <summary>
    /// Writes data to the DEBUG_from_periphcfg register (0x00DF).
    /// </summary>
    Task DmeWriteDebugFromPeriphCfg(ushort data, CancellationToken cancel = default);
}