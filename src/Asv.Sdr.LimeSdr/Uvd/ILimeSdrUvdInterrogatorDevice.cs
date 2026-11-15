using System.Threading;
using System.Threading.Tasks;

namespace Asv.Sdr.LimeSdr;

/// <summary>UVD-only FPGA interrogation module, CUSTOM_MODE_ID=6.</summary>
public interface ILimeSdrUvdInterrogatorDevice : ILimeSdrCustomDevice
{
    /// <summary>Resets and enables the module. FPGA register defaults are restored by the reset.</summary>
    Task TurnOnAsync(CancellationToken cancel = default);
    Task TurnOffAsync(CancellationToken cancel = default);
    Task SetCodeEnabledAsync(UvdRequestCode code, bool enabled, CancellationToken cancel = default);
    Task SetAllCodesEnabledAsync(bool enabled, CancellationToken cancel = default);
    Task SetSlsPulseEnabledAsync(UvdRequestCode code, bool enabled, CancellationToken cancel = default);
    /// <summary>Selects the P2 attenuation: false = 0 dB, true = 9 dB.</summary>
    Task SetSlsPulseAttenuatedAsync(UvdRequestCode code, bool attenuated, CancellationToken cancel = default);
    /// <summary>Pauses an enabled code and restores its state. An already disabled code remains disabled.</summary>
    Task ResetStatisticsAsync(UvdRequestCode code, CancellationToken cancel = default);
    Task ResetAllStatisticsAsync(CancellationToken cancel = default);
    /// <summary>
    /// Reads both stored reply words in one held register frame. There is no information-word
    /// freshness indicator in this map; zero data is not evidence of a newly received reply.
    /// ZK4 has no information-word registers and is not supported by this method.
    /// </summary>
    Task<UvdReply> ReadStoredReplyAsync(UvdRequestCode code, CancellationToken cancel = default);
    Task<UvdCodeSnapshot> ReadCodeSnapshotAsync(UvdRequestCode code, CancellationToken cancel = default);
    /// <summary>Reads 0x050D as signed kHz offset from 740 MHz, without rounding or a freshness claim.</summary>
    Task<double> ReadFrequencyMHzAsync(CancellationToken cancel = default);
    Task<UvdInterrogatorSnapshot> ReadSnapshotAsync(CancellationToken cancel = default);
}
