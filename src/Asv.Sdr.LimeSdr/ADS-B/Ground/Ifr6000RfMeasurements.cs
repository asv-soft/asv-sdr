using System;
using System.Threading;
using System.Threading.Tasks;

namespace Asv.Sdr.LimeSdr;

internal static class Ifr6000RfMeasurements
{
    internal static double ToFrequencyMHz(ushort offsetKhz) =>
        1090.0 + unchecked((short)offsetKhz) * 0.001;

    // RatioAmpDb_* registers are decoded directly as unsigned Q12.4 dB.
    internal static double ToPulseVariationDb(ushort value) => value / 16.0;

    internal static byte ToPulseVariationTenthsDb(ushort value)
    {
        // Snapshot values are tenths of a dB. Saturate instead of wrapping a large variation.
        return (byte)Math.Min(byte.MaxValue,
            Math.Round(ToPulseVariationDb(value) * 10.0, MidpointRounding.AwayFromZero));
    }

    internal static async Task<double> ReadFrequencyMHzAsync(
        ILimeSdrCustomDevice device, ushort address, CancellationToken cancel)
    {
        var value = await device.ReadCustomRegister(address, cancel).ConfigureAwait(false);
        return ToFrequencyMHz(value);
    }

    internal static async Task<double> ReadPulseVariationDbAsync(
        ILimeSdrCustomDevice device, ushort address, CancellationToken cancel)
    {
        var value = await device.ReadCustomRegister(address, cancel).ConfigureAwait(false);
        return ToPulseVariationDb(value);
    }
}
