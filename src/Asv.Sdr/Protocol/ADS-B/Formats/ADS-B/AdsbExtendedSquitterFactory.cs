using System;

namespace Asv.Sdr;

/// <summary>
/// Header of a CRC-valid ADS-B extended squitter frame.
/// </summary>
/// <param name="DownlinkFormat">DF17 or DF18.</param>
/// <param name="CapabilityOrControlField">DF17 CA or DF18 CF.</param>
/// <param name="AircraftAddress">24-bit ICAO aircraft address.</param>
public readonly record struct AdsbExtendedSquitterHeader(
    byte DownlinkFormat,
    byte CapabilityOrControlField,
    uint AircraftAddress
);

/// <summary>
/// Creates a typed ADS-B extended squitter from a raw seven-byte ME field.
/// </summary>
public static class AdsbExtendedSquitterFactory
{
    /// <summary>
    /// Reads the header of a complete extended squitter without decoding its ME payload.
    /// The frame must have a valid CRC and must be DF17, or DF18 with CF=0.
    /// </summary>
    public static AdsbExtendedSquitterHeader ReadHeader(ReadOnlySpan<byte> frame)
    {
        if (frame.Length != TransponderHelper.LongFrameLengthBytes)
        {
            throw new ArgumentException(
                $"An ADS-B extended squitter frame must contain exactly {TransponderHelper.LongFrameLengthBytes} bytes.",
                nameof(frame)
            );
        }

        var expectedCrc = ModeSHelper.CalcCrc24(frame, frame.Length - 3);
        var receivedCrc =
            ((uint)frame[^3] << 16) | ((uint)frame[^2] << 8) | frame[^1];
        if (expectedCrc != receivedCrc)
        {
            throw new FormatException(
                $"Invalid ADS-B CRC: expected 0x{expectedCrc:X6}, received 0x{receivedCrc:X6}."
            );
        }

        var downlinkFormat = (byte)TransponderHelper.GetDownlinkFormat(frame);
        if (downlinkFormat is not (17 or 18))
        {
            throw new NotSupportedException(
                $"Downlink format {downlinkFormat} is not an ADS-B extended squitter."
            );
        }

        var capabilityOrControlField = (byte)TransponderHelper.AdditionalIdentifier(frame);
        if (downlinkFormat == 18 && capabilityOrControlField != 0)
        {
            throw new NotSupportedException(
                $"DF18 control field {capabilityOrControlField} is not an ADS-B message from a non-transponder device."
            );
        }

        var aircraftAddress =
            ((uint)frame[1] << 16) | ((uint)frame[2] << 8) | frame[3];
        return new AdsbExtendedSquitterHeader(
            downlinkFormat,
            capabilityOrControlField,
            aircraftAddress
        );
    }

    /// <summary>
    /// Tries to read a validated extended squitter header without decoding its ME payload.
    /// </summary>
    public static bool TryReadHeader(
        ReadOnlySpan<byte> frame,
        out AdsbExtendedSquitterHeader header
    )
    {
        try
        {
            header = ReadHeader(frame);
            return true;
        }
        catch (Exception e) when (
            e is ArgumentException or FormatException or NotSupportedException
        )
        {
            header = default;
            return false;
        }
    }

    /// <summary>
    /// Creates a typed ADS-B extended squitter from a complete 112-bit frame.
    /// </summary>
    public static AdsbExtendedSquitterBase Deserialize(ReadOnlySpan<byte> frame)
    {
        var header = ReadHeader(frame);
        return Deserialize(
            header.DownlinkFormat,
            header.CapabilityOrControlField,
            header.AircraftAddress,
            frame.Slice(4, 7)
        );
    }

    public static AdsbExtendedSquitterBase Deserialize(
        byte downlinkFormat,
        byte capabilityOrControlField,
        uint aircraftAddress,
        ReadOnlySpan<byte> me)
    {
        if (me.Length != 7)
        {
            throw new ArgumentException("An ADS-B ME field must contain exactly seven bytes.", nameof(me));
        }

        if (downlinkFormat is not (17 or 18))
        {
            throw new ArgumentOutOfRangeException(
                nameof(downlinkFormat),
                downlinkFormat,
                "Only DF17 and DF18 extended squitters are supported.");
        }

        if (capabilityOrControlField > 7)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capabilityOrControlField),
                capabilityOrControlField,
                "The CA or CF field must fit in three bits.");
        }

        if (downlinkFormat == 18 && capabilityOrControlField != 0)
        {
            throw new NotSupportedException(
                $"DF18 control field {capabilityOrControlField} is not an ADS-B message from a non-transponder device.");
        }

        if (aircraftAddress > 0xFFFFFF)
        {
            throw new ArgumentOutOfRangeException(
                nameof(aircraftAddress),
                aircraftAddress,
                "The aircraft address must fit in 24 bits.");
        }

        var typeCode = (byte)(me[0] >> 3);
        var subType = typeCode == 29
            ? (byte)((me[0] >> 1) & 0x03)
            : (byte)(me[0] & 0x07);
        var message = Create(typeCode, subType);

        var frame = new byte[TransponderHelper.LongFrameLengthBytes];
        frame[0] = (byte)((downlinkFormat << 3) | capabilityOrControlField);
        frame[1] = (byte)(aircraftAddress >> 16);
        frame[2] = (byte)(aircraftAddress >> 8);
        frame[3] = (byte)aircraftAddress;
        me.CopyTo(frame.AsSpan(4, 7));
        var crc = ModeSHelper.CalcCrc24(frame, 11);
        frame[11] = (byte)(crc >> 16);
        frame[12] = (byte)(crc >> 8);
        frame[13] = (byte)crc;

        ReadOnlySpan<byte> buffer = frame;
        message.Deserialize(ref buffer);
        return message;
    }

    private static AdsbExtendedSquitterBase Create(byte typeCode, byte subType)
    {
        return typeCode switch
        {
            >= 1 and <= 4 => new AdsbAircraftIdentification(),
            >= 5 and <= 8 => new AdsbSurfacePosition(),
            >= 9 and <= 18 => new AdsbAirbornePositionWithBaroAlt(),
            19 when subType is 1 or 2 => new AdsbGroundSpeed(),
            19 when subType is 3 or 4 => new AdsbAirspeed(),
            >= 20 and <= 22 => new AdsbAirbornePositionWithGnssAlt(),
            23 => new AdsbEventDriven(),
            28 when subType == 0 => new AdsbAircraftStatusNoInformation(),
            28 when subType == 1 => new AdsbAircraftEmergencyStatus(),
            28 when subType == 2 => new AdsbAircraftAcasRaBroadcast(),
            29 when subType is 0 or 1 => new AdsbTargetStateAndStatusInformation(),
            31 when subType == 0 => new AdsbAircraftOperationStatusAirborne(),
            31 when subType == 1 => new AdsbAircraftOperationStatusSurface(),
            0 => throw new NotSupportedException("TC 0 contains no ADS-B position or status information."),
            _ => throw new NotSupportedException(
                $"ADS-B type code {typeCode}, subtype {subType} is not supported by a typed decoder.")
        };
    }
}
