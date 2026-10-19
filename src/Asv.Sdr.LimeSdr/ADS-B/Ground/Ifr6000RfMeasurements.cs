using System;
using System.Threading;
using System.Threading.Tasks;

namespace Asv.Sdr.LimeSdr;

internal static class Ifr6000RfMeasurements
{
    internal static double ToFrequencyMHz(ushort offsetKhz) =>
        1090.0 + unchecked((short)offsetKhz) * 0.001;

    internal static double ToPulseVariationDb(ushort min, ushort max) =>
        min > max ? double.NaN : 20.0 * Math.Log10((max + 1.0) / (min + 1.0));

    internal static byte? ToPulseVariationTenthsDb(ushort min, ushort max)
    {
        var variationDb = ToPulseVariationDb(min, max);
        // Snapshot values are tenths of a dB. Saturate instead of wrapping a large variation.
        return double.IsNaN(variationDb) ? null
            : (byte?)Math.Min(byte.MaxValue, Math.Round(variationDb * 10.0, MidpointRounding.AwayFromZero));
    }

    internal static async Task<double> ReadFrequencyMHzAsync(
        ILimeSdrCustomDevice device, ushort address, CancellationToken cancel)
    {
        var value = await device.ReadCustomRegister(address, cancel).ConfigureAwait(false);
        return ToFrequencyMHz(value);
    }

    internal static async Task<double> ReadPulseVariationDbAsync(
        ILimeSdrCustomDevice device, ushort minAddress, ushort maxAddress, CancellationToken cancel)
    {
        var values = await device.ReadCustomRegistersFrame([minAddress, maxAddress], cancel).ConfigureAwait(false);
        return ToPulseVariationDb(values[0], values[1]);
    }
}
