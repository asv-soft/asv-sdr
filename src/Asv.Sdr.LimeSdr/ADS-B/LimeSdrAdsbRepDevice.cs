using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZLogger;

namespace Asv.Sdr.LimeSdr;

public class LimeSdrDeviceConfig
{
    public int RfRelayGpio { get; set; } = 0;
    public bool RfRelayRxIsHigh { get; set; } = true;
    public string SerialNumber { get; set; }
    
    public SignalThreshold[] SignalLevels { get; set; } = [];
    public double RxLevelOffset { get; set; }
    public uint TxLevelOffset { get; set; }
}


public class LimeSdrAdsbRepDevice : LimeSdrDevice, ILimeSdrAdsbDevice
{
    private readonly LimeSdrDeviceConfig _config;
    private const ushort ControlAddress        = 0x00D0;
    
    private const ushort MODE_OFF_MASK         = 0xF901;
    private const ushort ADS_B_MASK            = 0xE0;
    private const ushort MODE_S_MASK           = 0x61E;

    private const ushort ADDRESS_WR_Address    = 0x00D1; // Address for internal write memory address
    private const ushort DATA_WR_Address       = 0x00D2; // Address for internal write data address
    private const ushort CONTROL_WR_Address    = 0x00D3; // Address for internal write pair/page control

    private const ushort ADDRESS_RD_Address    = 0x00D4; // Address for internal read memory address
    private const ushort DATA_RD_Address       = 0x00D5; // Address for internal read data address
    private const ushort CONTROL_RD_Address    = 0x00D6; // Address for internal read page control

    private const ushort UF4_UF20_CNT_Address  = 0x00D7; // Address for UF4 Cnt (7:0) | UF20 Cnt (7:0)
    private const ushort UF5_UF21_CNT_Address  = 0x00D8; // Address for UF5 Cnt (7:0) | UF21 Cnt (7:0)
    private const ushort UF11_UFxx_CNT_Address = 0x00D9; // Address for UF11 Cnt (7:0) | UFxx Cnt (7:0)
    private const ushort DF4_DF5_CNT_Address   = 0x00DA; // Address for DF4 Cnt (7:0) | DF5 Cnt (7:0)
    private const ushort DF11_CNT_Address      = 0x00DB; // Address for DF11 Cnt (7:0) | 0x00 (7:0)

    private const ushort PEAK_AMP_Address      = 0x00DC; // Address Rx PEAK_AMP (15:0)
    
    private const ushort DF20_DF21_CNT_Address = 0x00DD; // Address for DF20 Cnt (7:0) | DF21 Cnt (7:0) 0x00DE
    
    private const ushort PEAK_AMP2_Address      = 0x00DE; // Address Rx PEAK_AMP2 (15:0), received request amplitude


    #region Internal registers

    // Write
    private const ushort DF11_55_40_InternAddr          = 0x0000; // DF11(55:40)
    private const ushort DF11_39_24_InternAddr          = 0x0001; // DF11(39:24)
    
    private const ushort DF5_55_40_InternAddr           = 0x0002; // DF5 (55:40)
    private const ushort DF5_39_24_InternAddr           = 0x0003; // DF5 (39:24)
    
    private const ushort DF4_55_40_InternAddr           = 0x0004; // DF4 (55:40)
    private const ushort DF4_39_24_InternAddr           = 0x0005; // DF4 (39:24)
    
    private const ushort DF17_ID_79_64_InternAddr       = 0x0006; // DF17_ID(79:64)
    private const ushort DF17_ID_63_48_InternAddr       = 0x0007; // DF17_ID(63:48)
    private const ushort DF17_ID_47_32_InternAddr       = 0x0008; // DF17_ID(47:32)
    private const ushort DF17_ID_31_24_InternAddr       = 0x0009; // DF17_ID(31:24) & "00"
    
    private const ushort DF17_POS_EVEN_79_64_InternAddr = 0x000A; // DF17_POS_EVEN(79:64)
    private const ushort DF17_POS_EVEN_63_48_InternAddr = 0x000B; // DF17_POS_EVEN(63:48)
    private const ushort DF17_POS_EVEN_47_32_InternAddr = 0x000C; // DF17_POS_EVEN(47:32)
    private const ushort DF17_POS_EVEN_31_24_InternAddr = 0x000D; // DF17_POS_EVEN(31:24) & "00"
    
    private const ushort DF17_POS_ODD_79_64_InternAddr  = 0x000E; // DF17_POS_ODD(79:64)
    private const ushort DF17_POS_ODD_63_48_InternAddr  = 0x000F; // DF17_POS_ODD(63:48)
    private const ushort DF17_POS_ODD_47_32_InternAddr  = 0x0010; // DF17_POS_ODD(47:32)
    private const ushort DF17_POS_ODD_31_24_InternAddr  = 0x0011; // DF17_POS_ODD(31:24) & "00"
    
    private const ushort DF17_VLS_79_64_InternAddr      = 0x0012; // DF17_VLS(79:64)
    private const ushort DF17_VLS_63_48_InternAddr      = 0x0013; // DF17_VLS(63:48)
    private const ushort DF17_VLS_47_32_InternAddr      = 0x0014; // DF17_VLS(47:32)
    private const ushort DF17_VLS_31_24_InternAddr      = 0x0015; // DF17_VLS(31:24) & "00"
    
    private const ushort DelayDF_InternAddr             = 0x0016; // Delay DF Reply
    private const ushort UFxxType_InternAddr            = 0x0017; // UFxx output message type registers
    
    private const ushort BDS10_71_56_InternAddr         = 0x0039; // BDS10(71:56)
    private const ushort BDS10_55_40_InternAddr         = 0x003A; // BDS10(55:40)
    private const ushort BDS10_39_24_InternAddr         = 0x003B; // BDS10(39:24)
    
    private const ushort BDS40_71_56_InternAddr         = 0x003C; // BDS40(71:56)
    private const ushort BDS40_55_40_InternAddr         = 0x003D; // BDS40(55:40)
    private const ushort BDS40_39_24_InternAddr         = 0x003E; // BDS40(39:24)
    
    private const ushort BDS50_71_56_InternAddr         = 0x003F; // BDS50(71:56)
    private const ushort BDS50_55_40_InternAddr         = 0x0040; // BDS50(55:40)
    private const ushort BDS50_39_24_InternAddr         = 0x0041; // BDS50(39:24)
    
    private const ushort BDS60_71_56_InternAddr         = 0x0042; // BDS60(71:56)
    private const ushort BDS60_55_40_InternAddr         = 0x0043; // BDS60(55:40)
    private const ushort BDS60_39_24_InternAddr         = 0x0044; // BDS60(39:24)
    
    private const ushort Time_15_8_RecReqCnt_7_0_InternAddr= 0x0045; // Timestamp(15:8) & Recognized Request count per/sec(7:0)
    private const ushort Time_15_8_AllReqCnt_7_0_InternAddr= 0x0046; // Timestamp(15:8) & All Request count per/sec(7:0)
    
    
    // Read
    private const ushort UF11_55_40_InternAddr          = 0x0018; // UF11(55:40)
    private const ushort UF11_39_24_InternAddr          = 0x0019; // UF11(39:24)
    private const ushort UF11_23_08_InternAddr          = 0x001A; // UF11(23:8)
    private const ushort UF11_07_00_InternAddr          = 0x001B; // UF11(7:0) & "00"
    
    private const ushort UF5_55_40_InternAddr           = 0x001C; // UF5(55:40)
    private const ushort UF5_39_24_InternAddr           = 0x001D; // UF5(39:24)
    private const ushort UF5_23_08_InternAddr           = 0x001E; // UF5(23:8)
    private const ushort UF5_07_00_InternAddr           = 0x001F; // UF5(7:0) & "00"
    
    private const ushort UF4_55_40_InternAddr           = 0x0020; // UF4(55:40)
    private const ushort UF4_39_24_InternAddr           = 0x0021; // UF4(39:24)
    private const ushort UF4_23_08_InternAddr           = 0x0022; // UF4(23:8)
    private const ushort UF4_07_00_InternAddr           = 0x0023; // UF4(7:0) & "00"
    
    private const ushort UFxx_111_96_InternAddr         = 0x0024; // UFxx(111:96)
    private const ushort UFxx_95_80_InternAddr          = 0x0025; // UFxx(95:80)
    private const ushort UFxx_79_64_InternAddr          = 0x0026; // UFxx(79:64)
    private const ushort UFxx_63_48_InternAddr          = 0x0027; // UFxx(63:48)
    private const ushort UFxx_47_32_InternAddr          = 0x0028; // UFxx(47:32)
    private const ushort UFxx_31_16_InternAddr          = 0x0029; // UFxx(31:16)
    private const ushort UFxx_15_0_InternAddr           = 0x002A; // UFxx(15:0)
    
    private const ushort UF20_111_96_InternAddr         = 0x002B; // UF20(111:96)
    private const ushort UF20_95_80_InternAddr          = 0x002C; // UF20(95:80)
    private const ushort UF20_79_64_InternAddr          = 0x002D; // UF20(79:64)
    private const ushort UF20_63_48_InternAddr          = 0x002E; // UF20(63:48)
    private const ushort UF20_47_32_InternAddr          = 0x002F; // UF20(47:32)
    private const ushort UF20_31_16_InternAddr          = 0x0030; // UF20(31:16)
    private const ushort UF20_15_0_InternAddr           = 0x0031; // UF20(15:0)
    
    private const ushort UF21_111_96_InternAddr         = 0x0032; // UF21(111:96)
    private const ushort UF21_95_80_InternAddr          = 0x0033; // UF21(95:80)
    private const ushort UF21_79_64_InternAddr          = 0x0034; // UF21(79:64)
    private const ushort UF21_63_48_InternAddr          = 0x0035; // UF21(63:48)
    private const ushort UF21_47_32_InternAddr          = 0x0036; // UF21(47:32)
    private const ushort UF21_31_16_InternAddr          = 0x0037; // UF21(31:16)
    private const ushort UF21_15_0_InternAddr           = 0x0038; // UF21(15:0)
    
    #endregion
    

    private readonly ILogger _logger;
    
    public LimeSdrAdsbRepDevice(string deviceId, LimeSdrDeviceConfig config, ILogger? logger = null)
        : base(deviceId, true, logger ?? NullLogger.Instance, LimeSdrParams.LMS7_CAPSEL,LimeSdrParams.LMS7_CAPTURE)
    {
        _config = config;
        _logger = logger ?? NullLogger.Instance; 
    }

    public Task<bool> AdsbIsEnabled(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 0, 1, cancel)
            .ContinueWith(x => x.Result != 0, cancel);
    }

    public async Task AdsbSetIsEnabled(bool enabled, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting ADS-B mode to {enabled}");
        await this.WriteFpgaRegisterBits(ControlAddress, 0, 1, (ushort)(enabled ? 1 : 0), cancel).ConfigureAwait(false);
        await Task.Delay(1000, cancel);
    }

    public Task AdsbSetMode(byte mode, CancellationToken cancel = default)
    {
        if (mode > 2) throw new ArgumentOutOfRangeException(nameof(mode));
        return AtomicEditRegister(edit =>
        {
            var reg = edit.RaedFPGAReg(ControlAddress);
            ushort mask = mode switch
            {
                0 => ADS_B_MASK,
                1 => MODE_S_MASK,
                2 => ADS_B_MASK | MODE_S_MASK,
                _ => reg
            };

            reg = (ushort)(reg & MODE_OFF_MASK);
            reg = (ushort)(reg | mask);
            edit.WriteFPGAReg(ControlAddress, reg);
        }, cancel);
    }

    public Task<bool> AdsbDf11ReplyIsEnabled(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 1, 1, cancel)
            .ContinueWith(x => x.Result != 0, cancel);
    }

    public Task<bool> AdsbDf11BroadcastIsEnabled(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 2, 1, cancel)
            .ContinueWith(x => x.Result != 0, cancel);
    }

    public Task<bool> AdsbDf4IsEnabled(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 4, 1, cancel)
            .ContinueWith(x => x.Result != 0, cancel);
    }

    public Task<bool> AdsbDf5IsEnabled(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 3, 1, cancel)
            .ContinueWith(x => x.Result != 0, cancel);
    }

    public Task<bool> AdsbDf20IsEnabled(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 9, 1, cancel)
            .ContinueWith(x => x.Result != 0, cancel);
    }

    public Task<bool> AdsbDf21IsEnabled(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 10, 1, cancel)
            .ContinueWith(x => x.Result != 0, cancel);
    }

    public Task<bool> AdsbDf17IdIsEnabled(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 5, 1, cancel)
            .ContinueWith(x => x.Result != 0, cancel);
    }

    public Task<bool> AdsbDf17PositionIsEnabled(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 6, 1, cancel)
            .ContinueWith(x => x.Result != 0, cancel);
    }

    public Task<bool> AdsbDf17VelocityIsEnabled(CancellationToken cancel = default)
    {
        return this.ReadFpgaRegisterBits(ControlAddress, 7, 1, cancel)
            .ContinueWith(x => x.Result != 0, cancel);
    }

    public Task SetDf11ReplyIsEnabled(bool enabled, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting ADS-B DF11 Reply to {enabled}");
        return this.WriteFpgaRegisterBits(ControlAddress, 1, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task SetDf11BroadcastIsEnabled(bool enabled, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting ADS-B DF11 Squitter to {enabled}");
        return this.WriteFpgaRegisterBits(ControlAddress, 2, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task SetDf4IsEnabled(bool enabled, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting ADS-B DF4 Reply to {enabled}");
        return this.WriteFpgaRegisterBits(ControlAddress, 4, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task SetDf5IsEnabled(bool enabled, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting ADS-B DF5 Reply to {enabled}");
        return this.WriteFpgaRegisterBits(ControlAddress, 3, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task SetDf20IsEnabled(bool enabled, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting ADS-B DF20 Reply to {enabled}");
        return this.WriteFpgaRegisterBits(ControlAddress, 9, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task SetDf21IsEnabled(bool enabled, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting ADS-B DF21 Reply to {enabled}");
        return this.WriteFpgaRegisterBits(ControlAddress, 10, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task SetDf17IdIsEnabled(bool enabled, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting ADS-B DF17 Id Squitter to {enabled}");
        return this.WriteFpgaRegisterBits(ControlAddress, 5, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task SetDf17PositionIsEnabled(bool enabled, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting ADS-B DF17 Position Squitter to {enabled}");
        return this.WriteFpgaRegisterBits(ControlAddress, 6, 1, (ushort)(enabled ? 1 : 0), cancel);
    }

    public Task SetDf17VelocityIsEnabled(bool enabled, CancellationToken cancel = default)
    {
        _logger.ZLogDebug($"Setting ADS-B DF17 Velocity Squitter to {enabled}");
        return this.WriteFpgaRegisterBits(ControlAddress, 7, 1, (ushort)(enabled ? 1 : 0), cancel);
    }


    public async Task<byte[]> ReadAnyUFMessage(CancellationToken cancel = default)
    {
        var result = new byte[14];

        var addrs = new[]
        {
            UFxx_111_96_InternAddr, UFxx_95_80_InternAddr, UFxx_79_64_InternAddr, UFxx_63_48_InternAddr,
            UFxx_47_32_InternAddr, UFxx_31_16_InternAddr, UFxx_15_0_InternAddr
        };
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);

        for (var i = 0; i < values.Length; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }

        return result;
    }

    public async Task<byte[]> ReadUF20Message(CancellationToken cancel = default)
    {
        var result = new byte[14];

        var addrs = new[]
        {
            UF20_111_96_InternAddr, UF20_95_80_InternAddr, UF20_79_64_InternAddr, UF20_63_48_InternAddr,
            UF20_47_32_InternAddr, UF20_31_16_InternAddr, UF20_15_0_InternAddr
        };
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);

        for (var i = 0; i < values.Length; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }

        return result;
    }

    public async Task<byte[]> ReadUF21Message(CancellationToken cancel = default)
    {
        var result = new byte[14];

        var addrs = new[]
        {
            UF21_111_96_InternAddr, UF21_95_80_InternAddr, UF21_79_64_InternAddr, UF21_63_48_InternAddr,
            UF21_47_32_InternAddr, UF21_31_16_InternAddr, UF21_15_0_InternAddr
        };
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);

        for (var i = 0; i < values.Length; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }

        return result;
    }

    public async Task<byte[]> ReadUF4Message(CancellationToken cancel = default)
    {
        var result = new byte[7];

        var addrs = new[]
            { UF4_55_40_InternAddr, UF4_39_24_InternAddr, UF4_23_08_InternAddr, UF4_07_00_InternAddr };
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);
        result[0] = (byte)((values[0] >> 8) & 0xFF);
        result[1] = (byte)(values[0] & 0xFF);
        result[2] = (byte)((values[1] >> 8) & 0xFF);
        result[3] = (byte)(values[1] & 0xFF);
        result[4] = (byte)((values[2] >> 8) & 0xFF);
        result[5] = (byte)(values[2] & 0xFF);
        result[6] = (byte)((values[3] >> 8) & 0xFF);

        return result;
    }

    public async Task<byte[]> ReadUF5Message(CancellationToken cancel = default)
    {
        var result = new byte[7];

        var addrs = new[]
            { UF5_55_40_InternAddr, UF5_39_24_InternAddr, UF5_23_08_InternAddr, UF5_07_00_InternAddr };
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);
        result[0] = (byte)((values[0] >> 8) & 0xFF);
        result[1] = (byte)(values[0] & 0xFF);
        result[2] = (byte)((values[1] >> 8) & 0xFF);
        result[3] = (byte)(values[1] & 0xFF);
        result[4] = (byte)((values[2] >> 8) & 0xFF);
        result[5] = (byte)(values[2] & 0xFF);
        result[6] = (byte)((values[3] >> 8) & 0xFF);

        return result;
    }

    public async Task<byte[]> ReadUF11Message(CancellationToken cancel = default)
    {
        var result = new byte[7];

        var addrs = new[]
            { UF11_55_40_InternAddr, UF11_39_24_InternAddr, UF11_23_08_InternAddr, UF11_07_00_InternAddr };
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);
        result[0] = (byte)((values[0] >> 8) & 0xFF);
        result[1] = (byte)(values[0] & 0xFF);
        result[2] = (byte)((values[1] >> 8) & 0xFF);
        result[3] = (byte)(values[1] & 0xFF);
        result[4] = (byte)((values[2] >> 8) & 0xFF);
        result[5] = (byte)(values[2] & 0xFF);
        result[6] = (byte)((values[3] >> 8) & 0xFF);

        return result;
    }

    public async Task<(byte[] UF4, byte[] UF5, byte[] UF20, byte[] UF21, byte[] UF11)> ReadAllUFMessage(CancellationToken cancel = default)
    {
        var addrs = new[]
        {
            UF4_55_40_InternAddr, UF4_39_24_InternAddr, UF4_23_08_InternAddr, UF4_07_00_InternAddr,
            UF5_55_40_InternAddr, UF5_39_24_InternAddr, UF5_23_08_InternAddr, UF5_07_00_InternAddr,
            UF20_111_96_InternAddr, UF20_95_80_InternAddr, UF20_79_64_InternAddr, UF20_63_48_InternAddr,
            UF20_47_32_InternAddr, UF20_31_16_InternAddr, UF20_15_0_InternAddr,
            UF21_111_96_InternAddr, UF21_95_80_InternAddr, UF21_79_64_InternAddr, UF21_63_48_InternAddr,
            UF21_47_32_InternAddr, UF21_31_16_InternAddr, UF21_15_0_InternAddr,
            UF11_55_40_InternAddr, UF11_39_24_InternAddr, UF11_23_08_InternAddr, UF11_07_00_InternAddr
        };
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);
        
        var uf4 = new byte[7];
        var uf5 = new byte[7];
        var uf20 = new byte[14];
        var uf21 = new byte[14];
        var uf11 = new byte[7];
        uf4[0] = (byte)((values[0] >> 8) & 0xFF);
        uf4[1] = (byte)(values[0] & 0xFF);
        uf4[2] = (byte)((values[1] >> 8) & 0xFF);
        uf4[3] = (byte)(values[1] & 0xFF);
        uf4[4] = (byte)((values[2] >> 8) & 0xFF);
        uf4[5] = (byte)(values[2] & 0xFF);
        uf4[6] = (byte)((values[3] >> 8) & 0xFF);

        uf5[0] = (byte)((values[4] >> 8) & 0xFF);
        uf5[1] = (byte)(values[4] & 0xFF);
        uf5[2] = (byte)((values[5] >> 8) & 0xFF);
        uf5[3] = (byte)(values[5] & 0xFF);
        uf5[4] = (byte)((values[6] >> 8) & 0xFF);
        uf5[5] = (byte)(values[6] & 0xFF);
        uf5[6] = (byte)((values[7] >> 8) & 0xFF);
        
        for (var i = 0; i < 7; i++)
        {
            uf20[2 * i] = (byte)((values[i + 8] >> 8) & 0xFF);
            uf20[2 * i + 1] = (byte)(values[i + 8] & 0xFF);
        }
        
        for (var i = 0; i < 7; i++)
        {
            uf21[2 * i] = (byte)((values[i + 15] >> 8) & 0xFF);
            uf21[2 * i + 1] = (byte)(values[i + 15] & 0xFF);
        }
        
        uf11[0] = (byte)((values[22] >> 8) & 0xFF);
        uf11[1] = (byte)(values[22] & 0xFF);
        uf11[2] = (byte)((values[23] >> 8) & 0xFF);
        uf11[3] = (byte)(values[23] & 0xFF);
        uf11[4] = (byte)((values[24] >> 8) & 0xFF);
        uf11[5] = (byte)(values[24] & 0xFF);
        uf11[6] = (byte)((values[25] >> 8) & 0xFF);
        
        return (uf4, uf5, uf20, uf21, uf11);
    }

    public Task SetAnyUFType(ushort type, CancellationToken cancel)
    {
        var frame = new[] { new ValueTuple<ushort, ushort>(UFxxType_InternAddr, type)};
        return WriteAdsbFrame(frame, cancel);
    }

    public async Task<ushort> GetAnyUFType(CancellationToken cancel)
    {
        ushort type = 0;
        await AtomicEditRegister(edit =>
        {
            type = ReadAdsbRegister(edit, UFxxType_InternAddr);
        }, cancel);

        return type;
    }

    public Task WriteDF4Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B DF4 message. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }

        var frame = new ValueTuple<ushort, ushort>[2];
        frame[0] = new ValueTuple<ushort, ushort>(DF4_55_40_InternAddr, (ushort)((message.Span[0] << 8) | message.Span[1]));
        frame[1] = new ValueTuple<ushort, ushort>(DF4_39_24_InternAddr, (ushort)((message.Span[2] << 8) | message.Span[3]));
        return WriteAdsbFrame(frame, cancel);
    }

    public async Task<byte[]> ReadDF4Message(CancellationToken cancel = default)
    {
        var addrs = new[] { DF4_55_40_InternAddr, DF4_39_24_InternAddr};
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[values.Length * 2];
        for (var i = 0; i < values.Length; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }
        return result;
    }

    public Task WriteDF5Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B DF5 message. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }

        var frame = new ValueTuple<ushort, ushort>[2];
        frame[0] = new ValueTuple<ushort, ushort>(DF5_55_40_InternAddr, (ushort)((message.Span[0] << 8) | message.Span[1]));
        frame[1] = new ValueTuple<ushort, ushort>(DF5_39_24_InternAddr, (ushort)((message.Span[2] << 8) | message.Span[3]));
        return WriteAdsbFrame(frame, cancel);
    }
    
    public async Task<byte[]> ReadDF5Message(CancellationToken cancel = default)
    {
        var addrs = new[] { DF5_55_40_InternAddr, DF5_39_24_InternAddr};
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[values.Length * 2];
        for (var i = 0; i < values.Length; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }
        return result;
    }

    public Task WriteBDS10Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B BDS10 register. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }

        var frame = new ValueTuple<ushort, ushort>[3];
        frame[0] = new ValueTuple<ushort, ushort>(BDS10_71_56_InternAddr, (ushort)((message.Span[1] << 8) | message.Span[2]));
        frame[1] = new ValueTuple<ushort, ushort>(BDS10_55_40_InternAddr, (ushort)((message.Span[3] << 8) | message.Span[4]));
        frame[2] = new ValueTuple<ushort, ushort>(BDS10_39_24_InternAddr, (ushort)((message.Span[5] << 8) | message.Span[6]));
        return WriteAdsbFrame(frame, cancel);
    }

    public async Task<byte[]> ReadBDS10Message(CancellationToken cancel = default)
    {
        var addrs = new[]
        {
            BDS10_71_56_InternAddr, BDS10_55_40_InternAddr, BDS10_39_24_InternAddr
        };
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[values.Length * 2 + 1];
        result[0] = (1 << 4) | 0;
        for (var i = 0; i < values.Length; i++)
        {
            result[2 * i + 1] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 2] = (byte)(values[i] & 0xFF);
        }
        return result;
    }

    public async Task WriteBDS20Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B BDS20 register. Expected length greater than or equal to 14 bytes, but length {message.Length} bytes.");
            return;
        }
        
        byte df17FirstByte = 0;
        await AtomicEditRegister(edit =>
        {
            df17FirstByte = (byte)((ReadAdsbRegister(edit, DF17_ID_79_64_InternAddr) >> 8) & 0xFF);
        }, cancel).ConfigureAwait(false);
        
        var frame = new ValueTuple<ushort, ushort>[4];
        frame[0] = new ValueTuple<ushort, ushort>(DF17_ID_79_64_InternAddr, (ushort)((df17FirstByte << 8) | message.Span[1]));
        frame[1] = new ValueTuple<ushort, ushort>(DF17_ID_63_48_InternAddr, (ushort)((message.Span[2] << 8) | message.Span[3]));
        frame[2] = new ValueTuple<ushort, ushort>(DF17_ID_47_32_InternAddr, (ushort)((message.Span[4] << 8) | message.Span[5]));
        frame[3] = new ValueTuple<ushort, ushort>(DF17_ID_31_24_InternAddr, (ushort)(message.Span[6] << 8));
        await WriteAdsbFrame(frame, cancel);
    }
    public async Task<byte[]> ReadBDS20Message(CancellationToken cancel = default)
    {
        var addrs = new[] { DF17_ID_79_64_InternAddr, DF17_ID_63_48_InternAddr, DF17_ID_47_32_InternAddr, DF17_ID_31_24_InternAddr };
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[7];

        for (var i = 0; i < 3; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }
        result[6] = (byte)((values[3] >> 8) & 0xFF);
        result[0] = 2 << 4 | 0;

        return result;
    }

    public Task WriteBDS40Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B BDS40 register. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }

        var frame = new ValueTuple<ushort, ushort>[3];
        frame[0] = new ValueTuple<ushort, ushort>(BDS40_71_56_InternAddr, (ushort)((message.Span[1] << 8) | message.Span[2]));
        frame[1] = new ValueTuple<ushort, ushort>(BDS40_55_40_InternAddr, (ushort)((message.Span[3] << 8) | message.Span[4]));
        frame[2] = new ValueTuple<ushort, ushort>(BDS40_39_24_InternAddr, (ushort)((message.Span[5] << 8) | message.Span[6]));
        return WriteAdsbFrame(frame, cancel);
    }
    
    public async Task<byte[]> ReadBDS40Message(CancellationToken cancel = default)
    {
        var addrs = new[]
        {
            BDS40_71_56_InternAddr, BDS40_55_40_InternAddr, BDS40_39_24_InternAddr
        };
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[values.Length * 2 + 1];
        result[0] = (4 << 4) | 0;
        for (var i = 0; i < values.Length; i++)
        {
            result[2 * i + 1] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 2] = (byte)(values[i] & 0xFF);
        }
        return result;
    }
    
    public Task WriteBDS50Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B BDS50 register. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }

        var frame = new ValueTuple<ushort, ushort>[3];
        frame[0] = new ValueTuple<ushort, ushort>(BDS50_71_56_InternAddr, (ushort)((message.Span[1] << 8) | message.Span[2]));
        frame[1] = new ValueTuple<ushort, ushort>(BDS50_55_40_InternAddr, (ushort)((message.Span[3] << 8) | message.Span[4]));
        frame[2] = new ValueTuple<ushort, ushort>(BDS50_39_24_InternAddr, (ushort)((message.Span[5] << 8) | message.Span[6]));
        return WriteAdsbFrame(frame, cancel);
    }
    
    public async Task<byte[]> ReadBDS50Message(CancellationToken cancel = default)
    {
        var addrs = new[]
        {
            BDS50_71_56_InternAddr, BDS50_55_40_InternAddr, BDS50_39_24_InternAddr
        };
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[values.Length * 2 + 1];
        result[0] = (5 << 4) | 0;
        for (var i = 0; i < values.Length; i++)
        {
            result[2 * i + 1] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 2] = (byte)(values[i] & 0xFF);
        }
        return result;
    }
    
    public Task WriteBDS60Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B BDS60 register. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }

        var frame = new ValueTuple<ushort, ushort>[3];
        frame[0] = new ValueTuple<ushort, ushort>(BDS60_71_56_InternAddr, (ushort)((message.Span[1] << 8) | message.Span[2]));
        frame[1] = new ValueTuple<ushort, ushort>(BDS60_55_40_InternAddr, (ushort)((message.Span[3] << 8) | message.Span[4]));
        frame[2] = new ValueTuple<ushort, ushort>(BDS60_39_24_InternAddr, (ushort)((message.Span[5] << 8) | message.Span[6]));
        return WriteAdsbFrame(frame, cancel);
    }
    
    public async Task<byte[]> ReadBDS60Message(CancellationToken cancel = default)
    {
        var addrs = new[]
        {
            BDS60_71_56_InternAddr, BDS60_55_40_InternAddr, BDS60_39_24_InternAddr
        };
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[values.Length * 2 + 1];
        result[0] = (6 << 4) | 0;
        for (var i = 0; i < values.Length; i++)
        {
            result[2 * i + 1] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 2] = (byte)(values[i] & 0xFF);
        }
        return result;
    }

    public Task WriteDF11Message(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 7)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B DF11 message. Expected length greater than or equal to 7 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }

        var frame = new ValueTuple<ushort, ushort>[2];
        frame[0] = new ValueTuple<ushort, ushort>(DF11_55_40_InternAddr, (ushort)((message.Span[0] << 8) | message.Span[1]));
        frame[1] = new ValueTuple<ushort, ushort>(DF11_39_24_InternAddr, (ushort)((message.Span[2] << 8) | message.Span[3]));
        return WriteAdsbFrame(frame, cancel);
    }

    public async Task<byte[]> ReadDF11Message(CancellationToken cancel = default)
    {
        var addrs = new[] { DF11_55_40_InternAddr, DF11_39_24_InternAddr};
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[values.Length * 2];
        for (var i = 0; i < values.Length; i++)
        {
            result[2 * i] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * i + 1] = (byte)(values[i] & 0xFF);
        }
        return result;
    }

    public Task WriteDF17IdMessage(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 14)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B DF17 Identification message. Expected length greater than or equal to 14 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }
        
        var frame = new ValueTuple<ushort, ushort>[4];
        frame[0] = new ValueTuple<ushort, ushort>(DF17_ID_79_64_InternAddr, (ushort)((message.Span[4] << 8) | message.Span[5]));
        frame[1] = new ValueTuple<ushort, ushort>(DF17_ID_63_48_InternAddr, (ushort)((message.Span[6] << 8) | message.Span[7]));
        frame[2] = new ValueTuple<ushort, ushort>(DF17_ID_47_32_InternAddr, (ushort)((message.Span[8] << 8) | message.Span[9]));
        frame[3] = new ValueTuple<ushort, ushort>(DF17_ID_31_24_InternAddr, (ushort)(message.Span[10] << 8));
        return WriteAdsbFrame(frame, cancel);
    }

    public async Task<byte[]> ReadDF17IdMessage(CancellationToken cancel = default)
    {
        var df11 = await ReadDF11Message(cancel).ConfigureAwait(false);
        df11[0] &= 0x7;
        df11[0] |= 0x11 << 3; // change DF11 to DF17

        var addrs = new[] { DF17_ID_79_64_InternAddr, DF17_ID_63_48_InternAddr, DF17_ID_47_32_InternAddr, DF17_ID_31_24_InternAddr };
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[11];

        for (var i = 0; i < df11.Length; i++)
        {
            result[i] = df11[i];
        }
        for (var i = 0; i < 3; i++)
        {
            result[2 * (i + 2)] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * (i + 2) + 1] = (byte)(values[i] & 0xFF);
        }

        result[10] = (byte)((values[3] >> 8) & 0xFF);;

        return result;
    }
    
    public Task WriteDF17PositionMessage(ReadOnlyMemory<byte> evenMessage, ReadOnlyMemory<byte> oddMessage, CancellationToken cancel = default)
    {
        if (evenMessage.Length != 14)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B DF17 Position Even message. Expected length greater than or equal to 14 bytes, but length {evenMessage.Length} bytes.");
            return Task.CompletedTask;
        }
        
        if (oddMessage.Length != 14)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B DF17 Position Odd message. Expected length greater than or equal to 14 bytes, but length {oddMessage.Length} bytes.");
            return Task.CompletedTask;
        }
        
        var frame = new ValueTuple<ushort, ushort>[8];
        frame[0] = new ValueTuple<ushort, ushort>(DF17_POS_EVEN_79_64_InternAddr, (ushort)((evenMessage.Span[4] << 8) | evenMessage.Span[5]));
        frame[1] = new ValueTuple<ushort, ushort>(DF17_POS_EVEN_63_48_InternAddr, (ushort)((evenMessage.Span[6] << 8) | evenMessage.Span[7]));
        frame[2] = new ValueTuple<ushort, ushort>(DF17_POS_EVEN_47_32_InternAddr, (ushort)((evenMessage.Span[8] << 8) | evenMessage.Span[9]));
        frame[3] = new ValueTuple<ushort, ushort>(DF17_POS_EVEN_31_24_InternAddr, (ushort)(evenMessage.Span[10] << 8));
        
        frame[4] = new ValueTuple<ushort, ushort>(DF17_POS_ODD_79_64_InternAddr, (ushort)((oddMessage.Span[4] << 8) | oddMessage.Span[5]));
        frame[5] = new ValueTuple<ushort, ushort>(DF17_POS_ODD_63_48_InternAddr, (ushort)((oddMessage.Span[6] << 8) | oddMessage.Span[7]));
        frame[6] = new ValueTuple<ushort, ushort>(DF17_POS_ODD_47_32_InternAddr, (ushort)((oddMessage.Span[8] << 8) | oddMessage.Span[9]));
        frame[7] = new ValueTuple<ushort, ushort>(DF17_POS_ODD_31_24_InternAddr, (ushort)(oddMessage.Span[10] << 8));
        
        return WriteAdsbFrame(frame, cancel);
    }

    public async Task<(byte[] Even, byte[] Odd)> ReadDF17PositionMessage(CancellationToken cancel = default)
    {
        var df11 = await ReadDF11Message(cancel).ConfigureAwait(false);
        df11[0] &= 0x7;
        df11[0] |= 0x11 << 3; // change DF11 to DF17

        var addrs = new[]
        {
            DF17_POS_EVEN_79_64_InternAddr, DF17_POS_EVEN_63_48_InternAddr, DF17_POS_EVEN_47_32_InternAddr,
            DF17_POS_EVEN_31_24_InternAddr, DF17_POS_ODD_79_64_InternAddr, DF17_POS_ODD_63_48_InternAddr,
            DF17_POS_ODD_47_32_InternAddr, DF17_POS_ODD_31_24_InternAddr
        };
        
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);
        var resultEven = new byte[11];
        var resultOdd = new byte[11];

        for (var i = 0; i < df11.Length; i++)
        {
            resultEven[i] = df11[i];
            resultOdd[i] = df11[i];
        }
        for (var i = 0; i < 3; i++)
        {
            resultEven[2 * (i + 2)] = (byte)((values[i] >> 8) & 0xFF);
            resultEven[2 * (i + 2) + 1] = (byte)(values[i] & 0xFF);
            
            resultOdd[2 * (i + 2)] = (byte)((values[i + 4] >> 8) & 0xFF);
            resultOdd[2 * (i + 2) + 1] = (byte)(values[i + 4] & 0xFF);
        }

        resultEven[10] = (byte)((values[3] >> 8) & 0xFF);
        resultOdd[10] = (byte)((values[7] >> 8) & 0xFF);

        return (Even: resultEven, Odd: resultOdd);
    }
    
    public Task WriteDF17VelocityMessage(ReadOnlyMemory<byte> message, CancellationToken cancel = default)
    {
        if (message.Length != 14)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B DF17 Velocity message. Expected length greater than or equal to 14 bytes, but length {message.Length} bytes.");
            return Task.CompletedTask;
        }
        
        var frame = new ValueTuple<ushort, ushort>[4];
        frame[0] = new ValueTuple<ushort, ushort>(DF17_VLS_79_64_InternAddr, (ushort)((message.Span[4] << 8) | message.Span[5]));
        frame[1] = new ValueTuple<ushort, ushort>(DF17_VLS_63_48_InternAddr, (ushort)((message.Span[6] << 8) | message.Span[7]));
        frame[2] = new ValueTuple<ushort, ushort>(DF17_VLS_47_32_InternAddr, (ushort)((message.Span[8] << 8) | message.Span[9]));
        frame[3] = new ValueTuple<ushort, ushort>(DF17_VLS_31_24_InternAddr, (ushort)(message.Span[10] << 8));
        return WriteAdsbFrame(frame, cancel);
    }

    public async Task<byte[]> ReadDF17VelocityMessage(CancellationToken cancel = default)
    {
        var df11 = await ReadDF11Message(cancel).ConfigureAwait(false);
        df11[0] &= 0x7;
        df11[0] |= 0x11 << 3; // change DF11 to DF17

        var addrs = new[] { DF17_VLS_79_64_InternAddr, DF17_VLS_63_48_InternAddr, DF17_VLS_47_32_InternAddr, DF17_VLS_31_24_InternAddr };
        var values = await ReadAdsbFrame(addrs, cancel).ConfigureAwait(false);
        var result = new byte[11];

        for (var i = 0; i < df11.Length; i++)
        {
            result[i] = df11[i];
        }
        for (var i = 0; i < 3; i++)
        {
            result[2 * (i + 2)] = (byte)((values[i] >> 8) & 0xFF);
            result[2 * (i + 2) + 1] = (byte)(values[i] & 0xFF);
        }

        result[10] = (byte)((values[3] >> 8) & 0xFF);

        return result;
    }

    /// <summary>
    /// Gets the PEAK_AMP value, which represents the maximum signal amplitude over the last 100 milliseconds.
    /// The gain should be adjusted to keep PEAK_AMP within the range from 0x100 PEAK_AMP to 0x800
    /// </summary>
    public Task<ushort> AdsbGetPeakAmplitude(CancellationToken cancel = default)
    {
        return ReadFpgaRegister(PEAK_AMP_Address, cancel);
    }

    public Task<ushort> AdsbGetReceivedPeakAmplitude(CancellationToken cancel = default)
    {
        return ReadFpgaRegister(PEAK_AMP2_Address, cancel);
    }

    public Task SetDfDelay(double delayUs, CancellationToken cancel = default)
    {
        var delNorm = (int)Math.Round(delayUs * 40.0);
        if (delNorm > short.MaxValue) delNorm = short.MaxValue;
        if (delNorm < short.MinValue) delNorm = short.MinValue;
        var del = (ushort)delNorm;
        return WriteAdsbFrame([new ValueTuple<ushort, ushort>(DelayDF_InternAddr, del)], cancel);
    }

    

    public async Task<double> AdsbGetDfDelay(CancellationToken cancel = default)
    {
        var result = 0.0;
        await AtomicEditRegister(edit =>
        {
            result = (short)ReadAdsbRegister(edit, DelayDF_InternAddr) * 0.025;
        }, cancel).ConfigureAwait(false);
        return result;
    }

    public async Task<byte[]> GetUF4UF20Stat(CancellationToken cancel = default)
    {
        var reg = await ReadFpgaRegister(UF4_UF20_CNT_Address, cancel).ConfigureAwait(false);
        return [(byte)((reg >> 8) & 0xFF), (byte)(reg & 0xFF)];
    }

    public async Task<byte[]> GetUF5UF21Stat(CancellationToken cancel = default)
    {
        var reg = await ReadFpgaRegister(UF5_UF21_CNT_Address, cancel).ConfigureAwait(false);
        return [(byte)((reg >> 8) & 0xFF), (byte)(reg & 0xFF)];
    }

    public async Task<byte[]> GetUF11AnyUFStat(CancellationToken cancel = default)
    {
        var reg = await ReadFpgaRegister(UF11_UFxx_CNT_Address, cancel).ConfigureAwait(false);
        return [(byte)((reg >> 8) & 0xFF), (byte)(reg & 0xFF)];
    }

    public async Task<byte[]> GetDF4DF5Stat(CancellationToken cancel = default)
    {
        var reg = await ReadFpgaRegister(DF4_DF5_CNT_Address, cancel).ConfigureAwait(false);
        return [(byte)((reg >> 8) & 0xFF), (byte)(reg & 0xFF)];
    }

    public async Task<(byte UF4, byte UF5, byte UF20, byte UF21, byte UF11, byte DF4, byte DF5, byte DF20, byte DF21, byte
        DF11)> GetAllStat(CancellationToken cancel = default)
    {
        var regUF420 = await ReadFpgaRegister(UF4_UF20_CNT_Address, cancel).ConfigureAwait(false);
        var regUF521 = await ReadFpgaRegister(UF5_UF21_CNT_Address, cancel).ConfigureAwait(false);
        var regfUF11 = await ReadFpgaRegister(UF11_UFxx_CNT_Address, cancel).ConfigureAwait(false);
        var regDF45 = await ReadFpgaRegister(DF4_DF5_CNT_Address, cancel).ConfigureAwait(false);
        var regDF2021 = await ReadFpgaRegister(DF20_DF21_CNT_Address, cancel).ConfigureAwait(false);
        var regDF11 = await ReadFpgaRegister(DF11_CNT_Address, cancel).ConfigureAwait(false);
        
        var uf4 = (byte)((regUF420 >> 8) & 0xFF);
        var uf5 = (byte)((regUF521 >> 8) & 0xFF);
        var uf20 = (byte)(regUF420 & 0xFF);
        var uf21 = (byte)(regUF521 & 0xFF);
        var uf11 = (byte)((regfUF11 >> 8) & 0xFF);
        var df4 = (byte)((regDF45 >> 8) & 0xFF);
        var df5 = (byte)(regDF45 & 0xFF);
        var df20 = (byte)((regDF2021 >> 8) & 0xFF);
        var df21 = (byte)(regDF2021 & 0xFF);
        var df11 = (byte)((regDF11 >> 8) & 0xFF);

        return (uf4, uf5, uf20, uf21, uf11, df4, df5, df20, df21, df11);
    }

    public async Task<byte[]> GetDF20DF21Stat(CancellationToken cancel = default)
    {
        var reg = await ReadFpgaRegister(DF20_DF21_CNT_Address, cancel).ConfigureAwait(false);
        return [(byte)((reg >> 8) & 0xFF), (byte)(reg & 0xFF)];
    }

    public async Task<byte[]> GetDF11ReserveStat(CancellationToken cancel = default)
    {
        var reg = await ReadFpgaRegister(DF11_CNT_Address, cancel).ConfigureAwait(false);
        return [(byte)((reg >> 8) & 0xFF), (byte)(reg & 0xFF)];
    }

    public Task InitAllMessage(ReadOnlySpan<byte> df11, ReadOnlySpan<byte> df4, ReadOnlySpan<byte> df5, ReadOnlySpan<byte> bds10, ReadOnlySpan<byte> bds20,
        ReadOnlySpan<byte> bds40, ReadOnlySpan<byte> bds50, ReadOnlySpan<byte> bds60, ReadOnlySpan<byte> df17Id, CancellationToken cancel)
    {
        if (df11.Length != 7)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B DF11 message. Expected length greater than or equal to 7 bytes, but length {df11.Length} bytes.");
            return Task.CompletedTask;
        }

        var df11Frame = new ValueTuple<ushort, ushort>[2];
        df11Frame[0] = new ValueTuple<ushort, ushort>(DF11_55_40_InternAddr, (ushort)((df11[0] << 8) | df11[1]));
        df11Frame[1] = new ValueTuple<ushort, ushort>(DF11_39_24_InternAddr, (ushort)((df11[2] << 8) | df11[3]));

        if (df4.Length != 7)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B DF4 message. Expected length greater than or equal to 7 bytes, but length {df4.Length} bytes.");
            return Task.CompletedTask;
        }

        var df4Frame = new ValueTuple<ushort, ushort>[2];
        df4Frame[0] = new ValueTuple<ushort, ushort>(DF4_55_40_InternAddr, (ushort)((df4[0] << 8) | df4[1]));
        df4Frame[1] = new ValueTuple<ushort, ushort>(DF4_39_24_InternAddr, (ushort)((df4[2] << 8) | df4[3]));

        if (df5.Length != 7)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B DF5 message. Expected length greater than or equal to 7 bytes, but length {df5.Length} bytes.");
            return Task.CompletedTask;
        }

        var df5Frame = new ValueTuple<ushort, ushort>[2];
        df5Frame[0] = new ValueTuple<ushort, ushort>(DF5_55_40_InternAddr, (ushort)((df5[0] << 8) | df5[1]));
        df5Frame[1] = new ValueTuple<ushort, ushort>(DF5_39_24_InternAddr, (ushort)((df5[2] << 8) | df5[3]));

        if (bds10.Length != 7)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B BDS10 register. Expected length greater than or equal to 7 bytes, but length {bds10.Length} bytes.");
            return Task.CompletedTask;
        }

        var bds10Frame = new ValueTuple<ushort, ushort>[3];
        bds10Frame[0] = new ValueTuple<ushort, ushort>(BDS10_71_56_InternAddr, (ushort)((bds10[1] << 8) | bds10[2]));
        bds10Frame[1] = new ValueTuple<ushort, ushort>(BDS10_55_40_InternAddr, (ushort)((bds10[3] << 8) | bds10[4]));
        bds10Frame[2] = new ValueTuple<ushort, ushort>(BDS10_39_24_InternAddr, (ushort)((bds10[5] << 8) | bds10[6]));

        // ToDo Если у нас будут отдельные регистры для BDS20, записываем их тоже
        // ToDo Пока пользуемся регистрами DF17 Id
        
        if (bds40.Length != 7)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B BDS40 register. Expected length greater than or equal to 7 bytes, but length {bds40.Length} bytes.");
            return Task.CompletedTask;
        }

        var bds40Frame = new ValueTuple<ushort, ushort>[3];
        bds40Frame[0] = new ValueTuple<ushort, ushort>(BDS40_71_56_InternAddr, (ushort)((bds40[1] << 8) | bds40[2]));
        bds40Frame[1] = new ValueTuple<ushort, ushort>(BDS40_55_40_InternAddr, (ushort)((bds40[3] << 8) | bds40[4]));
        bds40Frame[2] = new ValueTuple<ushort, ushort>(BDS40_39_24_InternAddr, (ushort)((bds40[5] << 8) | bds40[6]));
        
        if (bds50.Length != 7)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B BDS50 register. Expected length greater than or equal to 7 bytes, but length {bds50.Length} bytes.");
            return Task.CompletedTask;
        }

        var bds50Frame = new ValueTuple<ushort, ushort>[3];
        bds50Frame[0] = new ValueTuple<ushort, ushort>(BDS50_71_56_InternAddr, (ushort)((bds50[1] << 8) | bds50[2]));
        bds50Frame[1] = new ValueTuple<ushort, ushort>(BDS50_55_40_InternAddr, (ushort)((bds50[3] << 8) | bds50[4]));
        bds50Frame[2] = new ValueTuple<ushort, ushort>(BDS50_39_24_InternAddr, (ushort)((bds50[5] << 8) | bds50[6]));
        
        if (bds60.Length != 7)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B BDS60 register. Expected length greater than or equal to 7 bytes, but length {bds60.Length} bytes.");
            return Task.CompletedTask;
        }

        var bds60Frame = new ValueTuple<ushort, ushort>[3];
        bds60Frame[0] = new ValueTuple<ushort, ushort>(BDS60_71_56_InternAddr, (ushort)((bds60[1] << 8) | bds60[2]));
        bds60Frame[1] = new ValueTuple<ushort, ushort>(BDS60_55_40_InternAddr, (ushort)((bds60[3] << 8) | bds60[4]));
        bds60Frame[2] = new ValueTuple<ushort, ushort>(BDS60_39_24_InternAddr, (ushort)((bds60[5] << 8) | bds60[6]));
        
        if (df17Id.Length != 14)
        {
            _logger.ZLogDebug(
                $"Error writing ADS-B DF17 Identification message. Expected length greater than or equal to 14 bytes, but length {df17Id.Length} bytes.");
            return Task.CompletedTask;
        }

        var df17IdFrame = new ValueTuple<ushort, ushort>[4];
        df17IdFrame[0] = new ValueTuple<ushort, ushort>(DF17_ID_79_64_InternAddr, (ushort)((df17Id[4] << 8) | df17Id[5]));
        df17IdFrame[1] = new ValueTuple<ushort, ushort>(DF17_ID_63_48_InternAddr, (ushort)((df17Id[6] << 8) | df17Id[7]));
        df17IdFrame[2] = new ValueTuple<ushort, ushort>(DF17_ID_47_32_InternAddr, (ushort)((df17Id[8] << 8) | df17Id[9]));
        df17IdFrame[3] = new ValueTuple<ushort, ushort>(DF17_ID_31_24_InternAddr, (ushort)(df17Id[10] << 8));

        return AtomicEditRegister(edit =>
        {
            foreach (var addressValuePair in df11Frame)
            {
                WriteAdsbRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }

            foreach (var addressValuePair in df4Frame)
            {
                WriteAdsbRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }

            foreach (var addressValuePair in df5Frame)
            {
                WriteAdsbRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }

            foreach (var addressValuePair in bds10Frame)
            {
                WriteAdsbRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }

            foreach (var addressValuePair in bds40Frame)
            {
                WriteAdsbRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }
            
            foreach (var addressValuePair in bds50Frame)
            {
                WriteAdsbRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }

            foreach (var addressValuePair in bds60Frame)
            {
                WriteAdsbRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }

            foreach (var addressValuePair in df17IdFrame)
            {
                WriteAdsbRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }

            edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 1, 1, 1);
            edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 1, 1, 0);
        }, cancel);
    }

    public async Task<(byte, byte)> GetRecognizedRequestsCountPerSecond(CancellationToken cancel = default)
    {
        var reg = await ReadAdsbRegister(Time_15_8_RecReqCnt_7_0_InternAddr, cancel).ConfigureAwait(false);
        var result = ((byte)(reg >> 8), (byte)(reg & 0xFF));
        return result;
    }

    public async Task<(byte, byte)> GetAllRequestsCountPerSecond(CancellationToken cancel = default)
    {
        var reg = await ReadAdsbRegister(Time_15_8_AllReqCnt_7_0_InternAddr, cancel).ConfigureAwait(false);
        var result = ((byte)(reg >> 8), (byte)(reg & 0xFF));
        return result;
    }

    public new bool IsDisposed => base.IsDisposed;
    public async Task<ushort> ReadAdsbRegister(ushort address, CancellationToken cancel = default)
    {
        ushort result = 0;
        await AtomicEditRegister(edit =>
        {
            result = ReadAdsbRegister(edit, address);
        }, cancel).ConfigureAwait(false);
        return result;
    }

    public Task WriteAdsbRegister(ushort address, ushort value, CancellationToken cancel = default)
    {
        return AtomicEditRegister(edit =>
        {
            WriteAdsbRegister(edit, address, value);
            edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 1, 1, 1);
            edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 1, 1, 0);
        }, cancel);
    }

    private void WriteAdsbRegister(ILmsRegisterEditor edit, ushort address, ushort value)
    {
        edit.WriteFPGAReg(ADDRESS_WR_Address, address);
        edit.WriteFPGAReg(DATA_WR_Address, value);
        edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 0, 1, 1);
        edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 0, 1, 0);
    }
    
    private Task WriteAdsbFrame(ValueTuple<ushort, ushort>[] addressValuePairs, CancellationToken cancel = default)
    {
        return AtomicEditRegister(edit =>
        {
            foreach (var addressValuePair in addressValuePairs)
            {
                WriteAdsbRegister(edit, addressValuePair.Item1, addressValuePair.Item2);
            }
            edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 1, 1, 1);
            edit.InternalWriteFpgaRegisterBits(CONTROL_WR_Address, 1, 1, 0);
        }, cancel);
    }

    private void SetIsHoldingFrame(ILmsRegisterEditor edit, bool enabled)
    {
        edit.InternalWriteFpgaRegisterBits(CONTROL_RD_Address, 0, 1, (ushort)(enabled ? 1 : 0));
    }

    private ushort ReadAdsbRegister(ILmsRegisterEditor edit, ushort address)
    {
        edit.WriteFPGAReg(ADDRESS_RD_Address, address);
        return edit.RaedFPGAReg(DATA_RD_Address);
    }

    private async Task<ushort[]> ReadAdsbFrame(ushort[] addrs, CancellationToken cancel = default)
    {
        var result = new ushort[addrs.Length];

        await AtomicEditRegister(edit =>
        {
            SetIsHoldingFrame(edit, true);
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = ReadAdsbRegister(edit, addrs[i]);
            }
            SetIsHoldingFrame(edit, false);
        }, cancel).ConfigureAwait(false);
        
        return result;
    }
}