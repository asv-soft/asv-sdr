using System;

namespace Asv.Sdr.Gui;

public enum TypeCodeEnum
{
    AircraftIdentification,
    SurfacePosition,
    AirborneBaroAltitude,
    AirborneVelocities,
    AirborneGnssHeight,
    Reserved
}

public static class AdsbHelper
{
    public static readonly byte[] Preamble = [0xA1, 0x40];
    public const int LongFrameLengthBytes = 2 + 14;
    public const int ShortFrameLengthBytes = 2 + 7;
    
    
    private const uint Polynomial = 0xfffa0480;
    
    public static uint CalcCrc(ReadOnlySpan<byte> frame)
    {
        var df = GetDownlinkFormat(frame);
        var messageLength = GetMessageLength(df);
        var crcBuffer = frame.Slice(2, messageLength - 2);
        return messageLength switch
        {
            LongFrameLengthBytes => GetLongFrameParity(crcBuffer),
            ShortFrameLengthBytes => GetShortFrameParity(crcBuffer),
            _ => throw new Exception($"Unable to calculate checksum for frame. Frame length={messageLength} unknown")
        };
    }

    private static uint GetLongFrameParity(ReadOnlySpan<byte> frame)
    {
        var data = (uint)(frame[0] << 24) | (uint)(frame[1] << 16) | (uint)(frame[2] << 8) | frame[3];
        var data1 = (uint)(frame[4] << 24) | (uint)(frame[5] << 16) | (uint)(frame[6] << 8) | frame[7];
        var data2 = (uint)(frame[8] << 24) | (uint)(frame[9] << 16) | (uint)(frame[10] << 8);

        for (var i = 0; i < 88; i++)
        {
            if ((data & 0x80000000) != 0)
            {
                data ^= Polynomial;
            }

            data <<= 1;
            if ((data1 & 0x80000000) != 0)
            {
                data |= 1;
            }

            data1 <<= 1;
            if ((data2 & 0x80000000) != 0)
            {
                data1 |= 1;
            }

            data2 <<= 1;
        }

        var result0 = (byte)(data >> 24);
        var result1 = (byte)((data >> 16) & 0xff);
        var result2 = (byte)((data >> 8) & 0xff);

        // var sum = (uint)((result0 ^ frame[11]) << 16) | (uint)((result1 ^ frame[12]) << 8) |
        //           (uint)(result2 ^ frame[13]);
        
        var sum = (uint)(result0 << 16) | (uint)(result1 << 8) | result2;


        return sum;
    }
    private static uint GetShortFrameParity(ReadOnlySpan<byte> frame)
    {
        var data = (uint)(frame[0] << 24) | (uint)(frame[1] << 16) | (uint)(frame[2] << 8) | frame[3];
        for (var i = 0; i < 32; i++)
        {
            if ((data & 0x80000000) != 0)
            {
                data ^= Polynomial;
            }

            data <<= 1;
        }

        var result0 = (byte)(data >> 24);
        var result1 = (byte)((data >> 16) & 0xff);
        var result2 = (byte)((data >> 8) & 0xff);

        // var sum = (uint)((result0 ^ frame[4]) << 16) | (uint)((result1 ^ frame[5]) << 8) | (uint)(result2 ^ frame[6]);

        var sum = (uint)(result0 << 16) | (uint)(result1 << 8) | result2;
        
        return sum;
    }

    public static int GetMessageLength(int downlinkFormat)
    {
        return downlinkFormat >= 16 ? LongFrameLengthBytes : ShortFrameLengthBytes;
    }
    public static int GetDownlinkFormat(ReadOnlySpan<byte> frame)
    {
        return (frame[2] >> 3) & 0x1F;
    }

    public static int AdditionalIdentifier(ReadOnlySpan<byte> frame)
    {
        return frame[2] & 0x7;
    }
    public static TypeCodeEnum GetTypeCode(ReadOnlySpan<byte> frame)
    {
        var tc = (frame[7] >> 3) & 0x1F;
        return tc switch
        {
            >= 1 and <= 4 => TypeCodeEnum.AircraftIdentification,
            <= 8 => TypeCodeEnum.SurfacePosition,
            <= 18 => TypeCodeEnum.AirborneBaroAltitude,
            19 => TypeCodeEnum.AirborneVelocities,
            <= 22 => TypeCodeEnum.AirborneGnssHeight,
            _ => TypeCodeEnum.Reserved
        };
    }
}