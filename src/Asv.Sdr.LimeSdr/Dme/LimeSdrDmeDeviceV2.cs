using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Asv.Sdr.LimeSdr;

public interface ILimeSdrDmeDeviceV2 : ILimeSdrCustomDevice
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
    
    Task<bool> DmeGetRelayInvert(CancellationToken cancel = default);
    
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
    /// Frequency of received responses/requests in Air/Ground mode respectively (all pairs, including hips)
    /// </summary>
    /// <param name="cancel"></param>
    /// <returns></returns>
    Task<ushort> DmeGetAllPairsFrequency(CancellationToken cancel = default);
    
    Task<ushort> DmeGetPulseRisingMean(CancellationToken cancel = default);
    Task<ushort> DmeGetPulseRisingSigma(CancellationToken cancel = default);
    Task<ushort> DmeGetPulseFallingMean(CancellationToken cancel = default);
    Task<ushort> DmeGetPulseFallingSigma(CancellationToken cancel = default);
    Task<ushort> DmeGetPulseDurationMean(CancellationToken cancel = default);
    Task<ushort> DmeGetPulseDurationSigma(CancellationToken cancel = default);
    Task<ushort> DmeGetCodeTimeMean(CancellationToken cancel = default);
    Task<ushort> DmeGetCodeTimeSigma(CancellationToken cancel = default);
    
    Task<ushort> DmeGetResponseRate(CancellationToken cancel = default);
    Task<ushort> DmeGetPulsePairFrequency(CancellationToken cancel = default);
    
    Task DmeSetIso(string iso, CancellationToken cancel = default);
    
    Task<string> DmeGetIso(CancellationToken cancel = default);
    
    Task<double[]> DmeGetFirstPulseShape(CancellationToken cancel = default);
    Task<double[]> DmeGetSecondPulseShape(CancellationToken cancel = default);
    
}
public class LimeSdrDmeDeviceV2 : LimeSdrCustomDevice, ILimeSdrDmeDeviceV2
{
    private readonly DmeWorkMode _mode;
    private readonly ILogger? _logger;

    public LimeSdrDmeDeviceV2(string deviceId, DmeWorkMode mode, ILogger? logger = null) : base(deviceId, logger)
    {
        _mode = mode;
        _logger = logger;
    }


    public Task<bool> DmeIsEnabled(CancellationToken cancel = default)
    {
        return IsEnabled(cancel);
    }

    public Task DmeSetIsEnabled(bool enabled, CancellationToken cancel = default)
    {
        return enabled ? TurnOnMode(cancel) : TurnOffMode(cancel);
    }

    public async Task<DmeWorkMode> DmeGetMode(CancellationToken cancel = default)
    {
        var customMode = await GetMode(cancel).ConfigureAwait(false);

        return customMode switch
        {
            CustomWorkMode.DmeAir => DmeWorkMode.Air,
            CustomWorkMode.DmeGround => DmeWorkMode.Ground,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public Task DmeReset(CancellationToken cancel = default)
    {
        return CustomModeReset(cancel);
    }

    public async Task<bool> DmeGetIsIsoEnabled(CancellationToken cancel = default)
    {
        var reg = await ReadCustomRegisterBits(0x0100, 0, 1, cancel).ConfigureAwait(false);
        return reg != 0;
    }

    public Task DmeSetIsoEnabled(bool enabled, CancellationToken cancel = default)
    {
        return WriteCustomRegisterBits(0x0100, 0, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public async Task<DmeChannel> DmeGetChannel(CancellationToken cancel = default)
    {
        var reg = await ReadCustomRegisterBits(0x0100, 1, 1, cancel).ConfigureAwait(false);
        return (DmeChannel)reg;
    }

    public Task DmeSetChannel(DmeChannel value, CancellationToken cancel = default)
    {
        return WriteCustomRegisterBits(0x0100, 1, 1, (ushort)value, cancel);
    }

    public async Task<bool> DmeGetIsZeroCalibrationEnabled(CancellationToken cancel = default)
    {
        var reg = await ReadCustomRegisterBits(0x0100, 2, 1, cancel).ConfigureAwait(false);
        return reg != 0;
    }

    public Task DmeSetIsZeroCalibrationEnabled(bool value, CancellationToken cancel = default)
    {
        return WriteCustomRegisterBits(0x0100, 2, 1, (ushort)(value ? 1 : 0), cancel);
    }

    public async Task<EnvelopeType> DmeGetEnvelopeType(CancellationToken cancel = default)
    {
        var reg = await ReadCustomRegisterBits(0x0100, 3, 1, cancel).ConfigureAwait(false);
        return (EnvelopeType)reg;
    }

    public Task DmeSetEnvelopeType(EnvelopeType value, CancellationToken cancel = default)
    {
        return WriteCustomRegisterBits(0x0100, 3, 1, (ushort)value, cancel);
    }

    public Task DmeSetRelayInvert(bool isInvert, CancellationToken cancel = default)
    {
        return SetInvertsGpioAmplifierControl(isInvert, cancel);
    }

    public Task<bool> DmeGetRelayInvert(CancellationToken cancel = default)
    {
        return GetInvertsGpioAmplifierControl(cancel);
    }

    public async Task<int> DmeGetMeasuredDistance(CancellationToken cancel = default)
    {
        var frame = await ReadCustomRegistersFrame([0x0103, 0x0104], cancel).ConfigureAwait(false);
        return (frame[0] << 16) | frame[1];
    }

    public Task<ushort> DmeGetHipPeriod(CancellationToken cancel = default)
    {
        return ReadCustomRegister(0x0107, cancel);
    }

    public Task DmeSetHipPeriod(ushort periodMicroseconds, CancellationToken cancel = default)
    {
        return WriteCustomRegister(0x0107, periodMicroseconds, cancel);
    }

    public async Task<int> DmeGetReplyDistance(CancellationToken cancel = default)
    {
        var frame = await ReadCustomRegistersFrame([0x0108, 0x0109], cancel).ConfigureAwait(false);
        return (frame[0] << 16) | frame[1];
    }

    public Task DmeSetReplyDistance(int distance, CancellationToken cancel = default)
    {
        var reg1 = (ushort)(distance >> 16);
        var reg2 = (ushort)(distance & 0xFFFF);
        return WriteCustomRegistersFrame([(0x0108, reg1), (0x0109, reg2)], cancel);
    }

    public Task<byte> DmeGetMaxHitsMeasure(CancellationToken cancel = default)
    {
        return ReadCustomRegisterBits(0x0105, 0, 8, cancel).ContinueWith(t => (byte)t.Result, cancel);
    }

    public Task<ushort> DmeGetPeakAmplitude(CancellationToken cancel = default)
    {
        return GetPeakAmplitude(cancel);
    }

    public Task<ushort> DmeGetRequestFrequency(CancellationToken cancel = default)
    {
        return ReadCustomRegister(0x0106, cancel);
    }

    public Task DmeSetRequestFrequency(ushort freqZd, CancellationToken cancel = default)
    {
        return WriteCustomRegister(0x0106, freqZd, cancel);
    }

    public Task DmeSetDelayRx2Tx(ushort delayMeter, CancellationToken cancel = default)
    {
        return WriteCustomRegister(0x0101, delayMeter, cancel);
    }

    public Task<ushort> DmeGetDelayRx2Tx(CancellationToken cancel = default)
    {
        return ReadCustomRegister(0x0101, cancel);
    }

    public Task<ushort> DmeGetAllPairsFrequency(CancellationToken cancel = default)
    {
        return ReadCustomRegister(0x0102, cancel);
    }

    public Task<ushort> DmeGetPulseRisingMean(CancellationToken cancel = default)
    {
        return ReadCustomRegisterBits(0x010A, 8, 8, cancel);
    }

    public Task<ushort> DmeGetPulseRisingSigma(CancellationToken cancel = default)
    {
        return ReadCustomRegisterBits(0x010A, 0, 8, cancel);
    }

    public Task<ushort> DmeGetPulseFallingMean(CancellationToken cancel = default)
    {
        return ReadCustomRegisterBits(0x010B, 8, 8, cancel);
    }

    public Task<ushort> DmeGetPulseFallingSigma(CancellationToken cancel = default)
    {
        return ReadCustomRegisterBits(0x010B, 0, 8, cancel);
    }

    public Task<ushort> DmeGetPulseDurationMean(CancellationToken cancel = default)
    {
        return ReadCustomRegisterBits(0x010C, 8, 8, cancel);
    }

    public Task<ushort> DmeGetPulseDurationSigma(CancellationToken cancel = default)
    {
        return ReadCustomRegisterBits(0x010C, 0, 8, cancel);
    }

    public Task<ushort> DmeGetCodeTimeMean(CancellationToken cancel = default)
    {
        return ReadCustomRegisterBits(0x010D, 8, 8, cancel);
    }

    public Task<ushort> DmeGetCodeTimeSigma(CancellationToken cancel = default)
    {
        return ReadCustomRegisterBits(0x010D, 0, 8, cancel);
    }

    public Task<ushort> DmeGetResponseRate(CancellationToken cancel = default)
    {
        return ReadCustomRegister(0x010E, cancel);
    }

    public Task<ushort> DmeGetPulsePairFrequency(CancellationToken cancel = default)
    {
        return ReadCustomRegister(0x010E, cancel);
    }

    public Task DmeSetIso(string iso, CancellationToken cancel = default)
    {
        var result = "    "u8.ToArray();
        var bytes = Encoding.ASCII.GetBytes(iso.ToUpper(), 0, Math.Min(iso.Length, 4));
        Array.Copy(bytes, 0, result, 0, bytes.Length);
        var reg1 = (ushort)((result[0] << 8) | result[1]);
        var reg2 = (ushort)((result[2] << 8) | result[3]);
        return WriteCustomRegistersFrame([(0x010F, reg1), (0x0110, reg2)], cancel);
    }

    public async Task<string> DmeGetIso(CancellationToken cancel = default)
    {
        var frame = await ReadCustomRegistersFrame([0x010F, 0x0110], cancel).ConfigureAwait(false);
        var buffer = new byte[4];
        buffer[0] = (byte)(frame[0] >> 8);
        buffer[1] = (byte)(frame[0] & 0xFF);
        buffer[2] = (byte)(frame[1] >> 8);
        buffer[3] = (byte)(frame[1] & 0xFF);
        return Encoding.ASCII.GetString(buffer);
    }

    public async Task<double[]> DmeGetFirstPulseShape(CancellationToken cancel = default)
    {
        var addr = new ushort[45];
        for (var i = 0; i < 45; i++)
        {
            addr[i] = (ushort)(0x0111 + i);
        }
        var frame = await ReadCustomRegistersFrame(addr, cancel).ConfigureAwait(false);
        return frame.Select(f => f / 2048.0).ToArray();
    }

    public async Task<double[]> DmeGetSecondPulseShape(CancellationToken cancel = default)
    {
        var addr = new ushort[45];
        for (var i = 0; i < 45; i++)
        {
            addr[i] = (ushort)(0x013E + i);
        }
        var frame = await ReadCustomRegistersFrame(addr, cancel).ConfigureAwait(false);
        return frame.Select(f => f / 2048.0).ToArray();
    }

    protected override CustomWorkMode InternalGetMode()
    {
        return _mode switch
        {
            DmeWorkMode.Air => CustomWorkMode.DmeAir,
            DmeWorkMode.Ground => CustomWorkMode.DmeGround,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}