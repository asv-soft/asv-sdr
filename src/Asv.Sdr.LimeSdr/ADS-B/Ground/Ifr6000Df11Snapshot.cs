using System;
using System.IO;

namespace Asv.Sdr.LimeSdr;

internal static class Ifr6000Df11Snapshot
{
    internal const int SampleLength = 6;

    internal static bool TryDecode(ReadOnlySpan<ushort> first, ReadOnlySpan<ushort> second,
        byte? counterBefore, out ModeSDF11? message, out string reason)
    {
        message = null;
        if (first.Length != SampleLength || second.Length != SampleLength)
        {
            reason = "sample-length";
            return false;
        }

        var counter = (byte)(first[0] >> 8);
        if (counter != (byte)(first[5] >> 8) || counter != (byte)(second[0] >> 8) ||
            counter != (byte)(second[5] >> 8))
        {
            reason = "counter-changed";
            return false;
        }

        if (counterBefore.HasValue && counter == counterBefore.Value)
        {
            reason = "no-fresh-frame";
            return false;
        }

        // The low byte of the fourth word is outside the 56-bit DF11 message.
        for (var word = 1; word <= 4; word++)
        {
            var mask = word == 4 ? 0xFF00 : 0xFFFF;
            if ((first[word] & mask) != (second[word] & mask))
            {
                reason = "raw-changed";
                return false;
            }
        }

        Span<byte> bytes = stackalloc byte[7];
        for (var index = 0; index < bytes.Length; index++)
        {
            var word = first[index / 2 + 1];
            bytes[index] = (byte)(index % 2 == 0 ? word >> 8 : word);
        }
        ReadOnlySpan<byte> buffer = bytes;
        try
        {
            var decoded = new ModeSDF11();
            decoded.Deserialize(ref buffer);
            // DF11 overlays only the seven IC/CL bits on the 24-bit parity.
            if ((decoded.CalculatedCrc ^ decoded.ModifiedCrc) > 0x7F)
            {
                reason = "crc-invalid";
                return false;
            }

            if (decoded.IcaoAddress is 0 or 0xFFFFFF)
            {
                reason = "icao-invalid";
                return false;
            }

            message = decoded;
            reason = "validated";
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException or ArgumentException or IndexOutOfRangeException)
        {
            reason = "parse-failed:" + exception.GetType().Name;
            return false;
        }
    }
}
