using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZLogger;

namespace Asv.Sdr.LimeSdr;

public abstract class LimeSdrCustomDevice : LimeSdrDevice, ILimeSdrCustomDevice
{
    private readonly ILogger _logger;
    private ushort MODE_OFF_MASK = 0xFF0C;

    private const ushort ControlAddress     = 0x00D0;
    
    private const ushort ADDRESS_WR_Address = 0x00D1;
    private const ushort DATA_WR_Address    = 0x00D2;
    private const ushort CONTROL_WR_Address = 0x00D3;
    private const ushort ADDRESS_RD_Address = 0x00D4;
    private const ushort DATA_RD_Address    = 0x00D5;
    private const ushort CONTROL_RD_Address = 0x00D6;
    private const ushort PEAK_AMP_Address   = 0x00D7;
    
    private const ushort DEBUG_WR_Address   = 0x00DE;
    private const ushort DEBUG_RD_Address   = 0x00DF;

    public LimeSdrCustomDevice(string deviceId, ILogger? logger = null) : base(deviceId,true,logger ?? NullLogger.Instance)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<bool> IsModeEnabled(CancellationToken cancel = default)
    {
        var reg = await this.ReadFpgaRegisterBits(ControlAddress, 0, 1, cancel).ConfigureAwait(false);
        return reg != 0;
    }

    public async Task<CustomWorkMode> GetMode(CancellationToken cancel = default)
    {
        var reg = await ReadFpgaRegister(ControlAddress, cancel).ConfigureAwait(false);
        return (CustomWorkMode)((reg >> 4) & 0xF);
    }

    protected Task TurnOnMode(CancellationToken cancel = default)
    {
        _logger.ZLogInformation($"Turn on custom mode");
        var mode = (ushort)InternalGetMode();
        return AtomicEditRegister(edit =>
        {
            // var reg = edit.RaedFPGAReg(ControlAddress);
            ushort reg = 0x0;
            var mask = (ushort)((mode << 4) | 0x3);
            var regReset = (ushort)(reg & MODE_OFF_MASK);
            regReset = (ushort)(regReset | mask);
            edit.WriteFPGAReg(ControlAddress, regReset);
            mask = (ushort)((mode << 4) | 0x1);
            reg = (ushort)(reg & MODE_OFF_MASK);
            reg = (ushort)(reg | mask);
            edit.WriteFPGAReg(ControlAddress, reg);
        }, cancel);
    }

    protected Task TurnOffMode(CancellationToken cancel = default)
    {
        _logger.ZLogInformation($"Turn off custom mode");
        return AtomicEditRegister(edit =>
        {
            var reg = edit.RaedFPGAReg(ControlAddress);
            reg = (ushort)(reg & MODE_OFF_MASK);
            edit.WriteFPGAReg(ControlAddress, reg);
        }, cancel);
    }
    protected abstract CustomWorkMode InternalGetMode();

    // public async Task CustomModeReset(CancellationToken cancel = default)
    // {
    //     _logger.ZLogInformation($"Resetting custom mode");
    //     await this.WriteFpgaRegisterBits(ControlAddress, 1, 1, 1, cancel);
    //     await Task.Delay(500, cancel);
    //     await this.WriteFpgaRegisterBits(ControlAddress, 1, 1, 0, cancel);
    // }
    
    public Task CustomModeReset(CancellationToken cancel = default)
    {
        _logger.ZLogInformation($"Resetting custom mode");
        return AtomicEditRegister(edit =>
        {
            edit.InternalWriteFpgaRegisterBits(ControlAddress, 1, 1, 1);
            edit.InternalWriteFpgaRegisterBits(ControlAddress, 1, 1, 0);
        }, cancel);
    }

    public Task SwitchMode(bool isCustom, CancellationToken cancel = default)
    {
        return this.WriteFpgaRegisterBits(ControlAddress, 0, 1, (ushort)(isCustom ? 1 : 0), cancel);
    }

    public async Task<bool> GetInvertsGpioAmplifierControl(CancellationToken cancel = default)
    {
        var reg = await this.ReadFpgaRegisterBits(ControlAddress, 8, 1, cancel).ConfigureAwait(false);
        return reg != 0;
    }

    public Task SetInvertsGpioAmplifierControl(bool enabled, CancellationToken cancel = default)
    {
        return this.WriteFpgaRegisterBits(ControlAddress, 8, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public async Task<bool> GetInternalRfKeySwitchEnabled(CancellationToken cancel = default)
    {
        var reg = await this.ReadFpgaRegisterBits(ControlAddress, 9, 1, cancel).ConfigureAwait(false);
        return reg != 0;
    }

    public Task SetInternalRfKeySwitchEnabled(bool enabled, CancellationToken cancel = default)
    {
        return this.WriteFpgaRegisterBits(ControlAddress, 9, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task<ushort> GetPeakAmplitude(CancellationToken cancel = default)
    {
        return ReadFpgaRegister(PEAK_AMP_Address, cancel);
    }
    
    protected Task<ushort> ReadDebugRegister(CancellationToken cancel = default)
    {
        return ReadFpgaRegister(DEBUG_RD_Address, cancel);
    }
    
    protected Task WriteDebugRegister(ushort value, CancellationToken cancel = default)
    {
        return WriteFpgaRegister(DEBUG_WR_Address, value, cancel);
    }
    
    protected async Task<ushort> ReadCustomRegisterBits(ushort address, ushort index, ushort length, CancellationToken cancel = default)
    {
        ushort result = 0;
        await AtomicEditRegister(edit =>
        {
            result = ReadCustomRegisterBits(edit, address, index, length);
        }, cancel).ConfigureAwait(false);
        return result;
    }

    protected Task WriteCustomRegisterBits(ushort address, ushort index, ushort length, ushort value, CancellationToken cancel = default)
    {
        return AtomicEditRegister(edit =>
        {
            WriteCustomRegisterBits(edit, address, index, length, value);
            edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 1, 1, 1);
            edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 1, 1, 0);
        }, cancel);
    }

    protected async Task<ushort> ReadCustomRegister(ushort address, CancellationToken cancel = default)
    {
        ushort result = 0;
        await AtomicEditRegister(edit =>
        {
            result = ReadCustomRegister(edit, address);
        }, cancel).ConfigureAwait(false);
        return result;
    }
    
    
    protected Task WriteCustomRegister(ushort address, ushort value, CancellationToken cancel = default)
    {
        return AtomicEditRegister(edit =>
        {
            WriteCustomRegister(edit, address, value);
            edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 1, 1, 1);
            edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 1, 1, 0);
        }, cancel);
    }
    
    protected Task WriteCustomRegistersFrame((ushort, ushort)[] addressValuePairs, CancellationToken cancel = default)
    {
        return AtomicEditRegister(edit =>
        {
            foreach (var addressValuePair in addressValuePairs)
            {
                WriteCustomRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }
            edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 1, 1, 1);
            edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 1, 1, 0);
        }, cancel);
    }

    protected async Task<ushort[]> ReadCustomRegistersFrame(ushort[] address, CancellationToken cancel = default)
    {
        var result = new ushort[address.Length];
        await AtomicEditRegister(edit =>
        {
            SetIsHoldingFrame(edit, true);
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = ReadCustomRegister(edit, address[i]);
            }
            SetIsHoldingFrame(edit, false);
        }, cancel).ConfigureAwait(false);
        
        return result;
    }

    private ushort ReadCustomRegister(ILmsRegisterEditor edit, ushort address)
    {
        edit.WriteFPGAReg(ADDRESS_RD_Address, address);
        return edit.RaedFPGAReg(DATA_RD_Address);
    }

    private ushort ReadCustomRegisterBits(ILmsRegisterEditor edit, ushort address, ushort index, ushort length)
    {
        if (length is <= 0 or > 16) throw new Exception("Error length");
        var reg = ReadCustomRegister(edit, address);
        var mask = (ushort)(unchecked((ushort)~0u) >> (sizeof(ushort) * 8 - length));
        return (ushort)((reg >> index) & mask);
    }

    private void WriteCustomRegister(ILmsRegisterEditor edit, ushort address, ushort value)
    {
        edit.WriteFPGAReg(ADDRESS_WR_Address, address);
        edit.WriteFPGAReg(DATA_WR_Address, value);
        edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 0, 1, 1);
        edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 0, 1, 0);
    }

    private void WriteCustomRegisterBits(ILmsRegisterEditor edit, ushort address, ushort index, ushort length,
        ushort value)
    {
        if (length is <= 0 or > 16) throw new Exception("Error length");
        var mask = (ushort)((unchecked((ushort)~0u) << (index + length)) | (unchecked((ushort)~0u) >> (sizeof(ushort) * 8 - index)));
        var reg = ReadCustomRegister(edit, address);
        reg = (ushort)((reg & mask) | ((value << index) & ~mask));
        WriteCustomRegister(edit, address, reg);
    }

    private void SetIsHoldingFrame(ILmsRegisterEditor edit, bool enabled)
    {
        edit.InternalWriteFpgaRegisterBits(CONTROL_RD_Address, 0, 1, (ushort)(enabled ? 1 : 0));
    }
    
    
}

