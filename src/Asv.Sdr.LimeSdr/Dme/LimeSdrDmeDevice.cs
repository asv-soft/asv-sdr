using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZLogger;

namespace Asv.Sdr.LimeSdr;



public class LimeSdrDmeDevice:LimeSdrDevice, ILimeSdrDmeDevice
{
    private const ushort ControlAddress = 0x00D0;
    private const ushort DistanceAddressHigh = 0x00D1; // Address for high part of the distance
    private const ushort DistanceAddressLow = 0x00D2;  // Address for low part of the distance
    private const ushort HipPeriodAddress = 0x00D3;    // Address for HIP period
    private const ushort DelayOdAddressHigh = 0x00D4;  // Address for high part of DelayOD
    private const ushort DelayOdAddressLow = 0x00D5;   // Address for low part of DelayOD
    private const ushort MaxHitsAddress = 0x00D6;      // Address for MAX_HITS
    private const ushort PeakAmpAddress = 0x00D7;      // Address for PEAK_AMP
    private const ushort FreqZdAddress = 0x00D8;       // Address for FREQ_ZD
    private const ushort DelayRx2TxAddress = 0x00D9; // Address for DelayRx2Tx
    
    private const ushort FlagRequestResponseAddress = 0x00DB; // Address for request/response flag (bit 0)
    private const ushort DebugToPeriphcfgAddress = 0x00DE; // Address for DEBUG_to_periphcfg
    private const ushort DebugFromPeriphcfgAddress = 0x00DF; // Address for DEBUG_from_periphcfg
    
    private readonly ILogger _logger;

    public LimeSdrDmeDevice(string deviceId,ILogger? logger = null)
        :base(deviceId,true,logger ?? NullLogger.Instance)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Gets the DME mode.
    /// </summary>
    public Task<bool> DmeIsEnabled(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 0, 1, cancel)
            .ContinueWith(x=>x.Result != 0, cancel);
    }
    /// <summary>
    /// Gets the DME mode.
    /// </summary>
    public Task DmeSetIsEnabled(bool enabled, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting DME mode to {enabled}");
        return this.WriteFpgaRegisterBits(ControlAddress, 0, 1, (ushort)(enabled ? 1 : 0), cancel);
    }
    /// <summary>
    /// Get DME Type
    /// </summary>
    public Task<DmeWorkMode> DmeGetMode(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 1, 1, cancel)
            .ContinueWith(t => (DmeWorkMode)t.Result, cancel);
    }
    /// <summary>
    /// Set DME Type
    /// </summary>
    /// <returns></returns>
    public Task DmeSetMode(DmeWorkMode mode, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting DME type to {mode}");
        return this.WriteFpgaRegisterBits(ControlAddress, 1, 1, (ushort)mode, cancel);
    }
    
    public async Task DmeReset(CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Resetting DME");
        await this.WriteFpgaRegisterBits(ControlAddress, 2, 1, 1, cancel);
        await Task.Delay(100, cancel);
        await this.WriteFpgaRegisterBits(ControlAddress, 2, 1, 0, cancel);
    }
    
    public Task<bool> DmeGetIsIsoEnabled(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 3, 1, cancel)
            .ContinueWith(t => t.Result != 0, cancel);
    }
    
    public Task DmeSetIsoEnabled(bool enabled, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting DME ISO to {enabled}");
        return this.WriteFpgaRegisterBits(ControlAddress, 3, 1, (ushort)(enabled ? 1 : 0), cancel);
    }
    
    public Task<DmeChannel> DmeGetChannel(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 4, 1, cancel)
            .ContinueWith(t => (DmeChannel)t.Result, cancel);
    }
    
    public Task DmeSetChannel(DmeChannel value, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting DME channel to {value}");
        return this.WriteFpgaRegisterBits(ControlAddress, 4, 1, (ushort)(value), cancel);
    }
    
    public Task<bool> DmeGetIsZeroCalibrationEnabled(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 5, 1, cancel)
            .ContinueWith(t => t.Result != 0, cancel);
    }
    
    public Task DmeSetIsZeroCalibrationEnabled(bool value, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting DME null distance request to {value}");
        return this.WriteFpgaRegisterBits(ControlAddress, 5, 1, (ushort)(value ? 1 : 0), cancel);
    }
    
    public Task<EnvelopeType> DmeGetEnvelopeType(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 6, 1, cancel)
            .ContinueWith(t => (EnvelopeType)t.Result, cancel);
    }
    
    public Task DmeSetEnvelopeType(EnvelopeType value, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting DME envelope type to {value}");
        return this.WriteFpgaRegisterBits(ControlAddress, 6, 1, (ushort)value, cancel);
    }
    
    public Task DmeSetRelayInvert(bool isInvert, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting DME invelop relay type to {isInvert}");
        return this.WriteFpgaRegisterBits(ControlAddress, 7, 1, (ushort)(isInvert ? 1:0), cancel);
    }
    
    public Task<bool> DmeGetRelayInvert(bool isInvert, CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 7, 1, cancel)
            .ContinueWith(t => t.Result!=0, cancel);
    }
    
    /// <summary>
    /// Gets the distance in meters by combining two registers.
    /// </summary>
    public async Task<int> DmeGetMeasuredDistance(CancellationToken cancel = default)
    {
        // Read the high 16 bits of the distance from 0x00D1 (bits 1:0)
        var highPart = await this.ReadFpgaRegister(DistanceAddressHigh, cancel);

        // Read the low 16 bits of the distance from 0x00D2 (all 16 bits)
        var lowPart = await this.ReadFpgaRegister(DistanceAddressLow, cancel);

        // Combine the two parts into a full 32-bit signed integer
        
        var distance = (highPart << 16) | lowPart;
        return distance;
    }
    
    /// <summary>
    /// Gets the HIP period in microseconds.
    /// Returns 0xFFFF if HIPs are disabled.
    /// </summary>
    public async Task<ushort> DmeGetHipPeriod(CancellationToken cancel = default)
    {
        // Read the HIP period from register 0x00D3 (16 bits)
        var hipPeriod = await this.ReadFpgaRegister(HipPeriodAddress, cancel);
        return hipPeriod;
    }

    /// <summary>
    /// Sets the HIP period in microseconds.
    /// Set to 0xFFFF to disable HIPs.
    /// </summary>
    public Task DmeSetHipPeriod(ushort periodMicroseconds, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting HIP period to {periodMicroseconds} microseconds");

        // Write the HIP period to register 0x00D3 (16 bits)
        return this.WriteFpgaRegister(HipPeriodAddress, periodMicroseconds, cancel);
    }
    
    /// <summary>
    /// Gets the DelayOD (delay distance) in meters by combining two registers.
    /// </summary>
    public async Task<int> DmeGetReplyDistance(CancellationToken cancel = default)
    {
        // Read the high 16 bits of DelayOD from 0x00D4
        var highPart = await this.ReadFpgaRegister(DelayOdAddressHigh, cancel);

        // Read the low 16 bits of DelayOD from 0x00D5
        var lowPart = await this.ReadFpgaRegister(DelayOdAddressLow, cancel);

        // Combine the two parts into a full 32-bit signed integer
        var delayOd = (highPart << 16) | lowPart;

        _logger.ZLogDebug($"Simulated delay distance: {delayOd} meters");
        return delayOd;
    }

    /// <summary>
    /// Sets the DelayOD (delay distance) in meters by splitting the integer into two parts.
    /// </summary>
    public async Task DmeSetReplyDistance(int distance, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting simulated delay distance to {distance} meters");

        // Split the delay distance into high and low parts
        var highPart = (ushort)((distance >> 16) & 0xFFFF); // High 16 bits
        var lowPart = (ushort)(distance & 0xFFFF);          // Low 16 bits

        // Write the high part to 0x00D4
        await this.WriteFpgaRegister(DelayOdAddressHigh, highPart, cancel);

        // Write the low part to 0x00D5
        await this.WriteFpgaRegister(DelayOdAddressLow, lowPart, cancel);
    }
    
    /// <summary>
    /// Gets the MAX_HITS value, which is the maximum number of hits out of 32 queries.
    /// </summary>
    public async Task<byte> DmeGetMaxHitsMeasure(CancellationToken cancel = default)
    {
        // Read the 8-bit MAX_HITS value from 0x00D6 (bits 7:0)
        var maxHits = await this.ReadFpgaRegisterBits(MaxHitsAddress, 0, 8, cancel);
        return (byte)maxHits; // Cast to byte since we only need 8 bits
    }

   
    /// <summary>
    /// Gets the PEAK_AMP value, which represents the maximum signal amplitude over the last 100 milliseconds.
    /// The gain should be adjusted to keep PEAK_AMP within the range 0x100 < PEAK_AMP < 0x800.
    /// </summary>
    public async Task<ushort> DmeGetPeakAmplitude(CancellationToken cancel = default)
    {
        // Read the 16-bit PEAK_AMP value from 0x00D7
        var peakAmp = await this.ReadFpgaRegister(PeakAmpAddress, cancel);
        return peakAmp;
    }
    
    /// <summary>
    /// Gets the FREQ_ZD value, which represents the query frequency in Hz.
    /// The default is 100 Hz for AIR mode.
    /// </summary>
    public async Task<ushort> DmeGetRequestFrequency(CancellationToken cancel = default)
    {
        // Read the 16-bit FREQ_ZD value from 0x00D8
        var freqZd = await this.ReadFpgaRegister(FreqZdAddress, cancel);
        return freqZd;
    }

    /// <summary>
    /// Sets the FREQ_ZD value, which is the query frequency in Hz.
    /// Valid range: 1 Hz to 20,000 Hz.
    /// </summary>
    public Task DmeSetRequestFrequency(ushort freqZd, CancellationToken cancel = default)
    {
        if (freqZd is < 1 or > 20000)
        {
            throw new ArgumentOutOfRangeException(nameof(freqZd), "Frequency must be between 1 Hz and 20,000 Hz.");
        }

        _logger.ZLogDebug($"Setting FREQ_ZD to {freqZd} Hz");

        // Write the 16-bit FREQ_ZD value to 0x00D8
        return this.WriteFpgaRegister(FreqZdAddress, freqZd, cancel);
    }
    
    /// <summary>
    /// Sets the DelayRx2Tx value, zero delay in meters.
    /// </summary>
    public Task DmeSetDelayRx2Tx(ushort delayMeter, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting DelayRx2Tx to {delayMeter} m");
        return this.WriteFpgaRegister(DelayRx2TxAddress, delayMeter, cancel);
    }
    /// <summary>
    /// Gets the DelayRx2Tx value, zero delay in meters.
    /// </summary>
    public Task<ushort> DmeGetDelayRx2Tx(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegister(DelayRx2TxAddress, cancel);
    }
    
    /// <summary>
    /// Gets the flag indicating the presence of active requests/responses.
    /// </summary>
    public async Task<bool> DmeGetSignalAvailableFlag(CancellationToken cancel = default)
    {
        // Read the 1-bit flag from 0x00DB (bit 0)
        var flag = await this.ReadFpgaRegisterBits(FlagRequestResponseAddress, 0, 1, cancel);
        return flag != 0;
    }

    /// <summary>
    /// Reads data from the DEBUG_to_periphcfg register (0x00DE).
    /// </summary>
    public async Task<ushort> DmeReadDebugToPeriphCfg(CancellationToken cancel = default)
    {
        // Read 16-bit data from 0x00DE
        var data = await this.ReadFpgaRegister(DebugToPeriphcfgAddress, cancel);
        return data;
    }

    /// <summary>
    /// Writes data to the DEBUG_from_periphcfg register (0x00DF).
    /// </summary>
    public Task DmeWriteDebugFromPeriphCfg(ushort data, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Writing {data:X4} to DEBUG_from_periphcfg");

        // Write 16-bit data to 0x00DF
        return this.WriteFpgaRegister(DebugFromPeriphcfgAddress, data, cancel);
    }

}
