using System;

namespace Asv.Sdr;

/// <summary>Decodes extracted UVD information words without hardware or freshness assumptions.</summary>
public static class UvdReplyDecoder
{
    public const uint InformationWordMask = 0xFFFFF;

    public static UvdReply Decode(UvdRequestCode requestCode, uint informationWord,
        bool coordinateDistress = false)
    {
        if (requestCode == UvdRequestCode.Zk4)
            throw new NotSupportedException("ZK4 does not define an information-word reply.");
        if (requestCode is not (UvdRequestCode.Zk1 or UvdRequestCode.Zk2 or UvdRequestCode.Zk3))
            throw new ArgumentOutOfRangeException(nameof(requestCode));
        if (informationWord > InformationWordMask)
            throw new ArgumentOutOfRangeException(nameof(informationWord), "A UVD information word contains twenty bits.");

        string? boardNumber = null;
        if (requestCode == UvdRequestCode.Zk1)
            TryDecodeBoardNumber(informationWord, out boardNumber);
        UvdCurrentInformation? currentInformation = null;
        if (requestCode == UvdRequestCode.Zk2)
        {
            var altitude = TryDecodeAltitude(informationWord, out var meters) ? (int?)meters : null;
            currentInformation = new UvdCurrentInformation(informationWord, altitude,
                DecodeSo2010FuelPercent((byte)(informationWord >> 16)));
        }
        return new UvdReply(requestCode, informationWord, coordinateDistress, boardNumber, currentInformation);
    }

    /// <summary>SO-2010 RE 113.32.11, printed p. 6, Table 3; SO-69, printed pp. 19-20.</summary>
    public static bool TryDecodeBoardNumber(uint informationWord, out string? boardNumber)
    {
        boardNumber = null;
        if (informationWord > InformationWordMask)
            return false;

        Span<char> digits = stackalloc char[5];
        for (var index = 0; index < digits.Length; index++)
        {
            var digit = (informationWord >> (index * 4)) & 0xF;
            if (digit > 9)
                return false;
            digits[digits.Length - index - 1] = (char)('0' + digit);
        }

        boardNumber = new string(digits);
        return true;
    }

    /// <summary>
    /// SO-2010 RE 113.32.11, printed p. 6, Table 3; negative absolute height marker:
    /// Kolomiets, section 2.3.3.1v, printed p. 66 (P8/P13/P14=0, P9..P12=1).
    /// Equipment operating limits are not inferred from the representable field range.
    /// </summary>
    public static bool TryDecodeAltitude(uint informationWord, out int altitudeMeters)
    {
        altitudeMeters = 0;
        if (informationWord > InformationWordMask)
            return false;

        var raw = informationWord & 0x3FFF;
        var negativeMarker = (raw & 0x3F80) == 0x0F00;
        if (negativeMarker)
        {
            if ((informationWord & 0x4000) == 0 || !TryDecodeBcd(raw & 0x7F, 2, out var magnitude))
                return false;
            altitudeMeters = -magnitude * 10;
            return true;
        }

        if (!TryDecodeBcd(raw & 0xFFF, 3, out var lowerDigits))
            return false;
        altitudeMeters = (lowerDigits + (int)(raw >> 12) * 1000) * 10;
        return true;
    }

    /// <summary>SO-2010 RE 113.32.11, printed p. 7, Table 4. Other equipment may expose a fuel interval.</summary>
    public static byte? DecodeSo2010FuelPercent(byte code)
    {
        if (code > 15)
            throw new ArgumentOutOfRangeException(nameof(code));
        return code switch
        {
            0 => null,
            <= 10 => (byte)(code * 5),
            _ => (byte)(50 + (code - 10) * 10)
        };
    }

    private static bool TryDecodeBcd(uint data, int digits, out int value)
    {
        value = 0;
        var multiplier = 1;
        for (var index = 0; index < digits; index++)
        {
            var digit = data & 0xF;
            if (digit > 9)
                return false;
            value += (int)digit * multiplier;
            multiplier *= 10;
            data >>= 4;
        }
        return data == 0;
    }
}
