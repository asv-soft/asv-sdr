using System;
using System.Text;
using Asv.IO;

namespace Asv.Sdr.Gui;

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

    public static CapabilityEnum GetCapability(int ca)
    {
        return ca switch
        {
            0 => CapabilityEnum.Level1,
            >= 1 and <= 3 => CapabilityEnum.Reserved,
            4 => CapabilityEnum.Level2OnGround,
            5 => CapabilityEnum.Level2Airborne,
            6 => CapabilityEnum.Level2OnGroundOrAirborne,
            7 => CapabilityEnum.DLRequest0OrFlightStatus2345OnGroundOrAirborne,
            _ => CapabilityEnum.Reserved
        };
    }

    public static int SetCapability(CapabilityEnum ca)
    {
        return ca switch
        {
            CapabilityEnum.Reserved => 1,
            CapabilityEnum.Level1 => 0,
            CapabilityEnum.Level2OnGround => 4,
            CapabilityEnum.Level2Airborne => 5,
            CapabilityEnum.Level2OnGroundOrAirborne => 6,
            CapabilityEnum.DLRequest0OrFlightStatus2345OnGroundOrAirborne => 7,
            _ => throw new ArgumentOutOfRangeException(nameof(ca), ca, null)
        };
    }
    public static TypeCodeEnum GetTypeCode(ReadOnlySpan<byte> frame)
    {
        var tc = (frame[7] >> 3) & 0x1F;
        return tc switch
        {
            >= 1 and <= 4 => TypeCodeEnum.AircraftIdentification,
            <= 8 => TypeCodeEnum.SurfacePosition,
            <= 18 => TypeCodeEnum.AirborneBarometricPosition,
            19 => TypeCodeEnum.AirborneVelocities,
            <= 22 => TypeCodeEnum.AirborneGnssPosition,
            <= 27 => TypeCodeEnum.Reserved,
            28 => TypeCodeEnum.AircraftStatus,
            29 => TypeCodeEnum.TargetStateAndStatusInformation,
            31 => TypeCodeEnum.AircraftOperationStatus,
            _ => TypeCodeEnum.Reserved
        };
    }

    public static AircraftCategoryEnum GetAircraftCategory(int tc, int ca)
    {
        if (tc == 1) return AircraftCategoryEnum.Reserved;
        if (ca == 0) return AircraftCategoryEnum.NoCategoryInformation;
        return tc switch
        {
            2 => ca switch
            {
                1 => AircraftCategoryEnum.SurfaceEmergencyVehicle,
                3 => AircraftCategoryEnum.SurfaceServiceVehicle,
                >= 4 and <= 7 => AircraftCategoryEnum.GroundObstruction
            },
            3 => ca switch
            {
                1 => AircraftCategoryEnum.GliderOrSailplane,
                2 => AircraftCategoryEnum.LighterThanAir,
                3 => AircraftCategoryEnum.ParachutistOrSkydiver,
                4 => AircraftCategoryEnum.UltralightOrHangGliderOrParaGlider,
                5 => AircraftCategoryEnum.Reserved,
                6 => AircraftCategoryEnum.UnmannedAerialVehicle,
                7 => AircraftCategoryEnum.SpaceOrTransAtmosphericVehicle,
            },
            4 => ca switch
            {
                1 => AircraftCategoryEnum.Light,
                2 => AircraftCategoryEnum.Medium1,
                3 => AircraftCategoryEnum.Medium2,
                4 => AircraftCategoryEnum.HighVortexAircraft,
                5 => AircraftCategoryEnum.Heavy,
                6 => AircraftCategoryEnum.HighPerformanceAndHighSpeed,
                7 => AircraftCategoryEnum.Rotorcraft,
            },
            _ => AircraftCategoryEnum.Reserved
        };
    }

    public static void SetAircraftCategory(AircraftCategoryEnum ca, out int tcValue, out int caValue)
    {
        switch (ca)
        {
            case AircraftCategoryEnum.Reserved:
                tcValue = 1;
                caValue = 0;
                break;
            case AircraftCategoryEnum.NoCategoryInformation:
                tcValue = 2;
                caValue = 0;
                break;
            case AircraftCategoryEnum.SurfaceEmergencyVehicle:
                tcValue = 2;
                caValue = 1;
                break;
            case AircraftCategoryEnum.SurfaceServiceVehicle:
                tcValue = 2;
                caValue = 3;
                break;
            case AircraftCategoryEnum.GroundObstruction:
                tcValue = 2;
                caValue = 7;
                break;
            case AircraftCategoryEnum.GliderOrSailplane:
                tcValue = 3;
                caValue = 1;
                break;
            case AircraftCategoryEnum.LighterThanAir:
                tcValue = 3;
                caValue = 2;
                break;
            case AircraftCategoryEnum.ParachutistOrSkydiver:
                tcValue = 3;
                caValue = 3;
                break;
            case AircraftCategoryEnum.UltralightOrHangGliderOrParaGlider:
                tcValue = 3;
                caValue = 4;
                break;
            case AircraftCategoryEnum.UnmannedAerialVehicle:
                tcValue = 3;
                caValue = 6;
                break;
            case AircraftCategoryEnum.SpaceOrTransAtmosphericVehicle:
                tcValue = 3;
                caValue = 7;
                break;
            case AircraftCategoryEnum.Light:
                tcValue = 4;
                caValue = 1;
                break;
            case AircraftCategoryEnum.Medium1:
                tcValue = 4;
                caValue = 2;
                break;
            case AircraftCategoryEnum.Medium2:
                tcValue = 4;
                caValue = 3;
                break;
            case AircraftCategoryEnum.HighVortexAircraft:
                tcValue = 4;
                caValue = 4;
                break;
            case AircraftCategoryEnum.Heavy:
                tcValue = 4;
                caValue = 5;
                break;
            case AircraftCategoryEnum.HighPerformanceAndHighSpeed:
                tcValue = 4;
                caValue = 6;
                break;
            case AircraftCategoryEnum.Rotorcraft:
                tcValue = 4;
                caValue = 7;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ca), ca, null);
        }
    }

    public static byte[] AircraftIdEncoding(string aircraftId)
    {
        byte[]? enc = null;
        if (!string.IsNullOrWhiteSpace(aircraftId))
        {
            aircraftId = aircraftId.ToUpper().TrimStart();
            if (aircraftId.Length < 8)
            {
                var space = string.Create(8 - aircraftId.Length, ' ', (span, c) =>
                {
                    for (var i = 0; i < span.Length; i++)
                    {
                        span[i] = c;
                    }
                });
                aircraftId += space;
            }
            
            enc = Encoding.ASCII.GetBytes(aircraftId);
            for (var i = 0; i < enc.Length; i++)
            {
                if (enc[i] != 0x20 && (enc[i] < 0x30 || enc[i] > 0x39) && (enc[i] < 0x41 || enc[i] > 0x5A))
                {
                    enc[i] = 0x20;
                }
            }
        }
        else
        {
            enc = [0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20];
        }
        
        var result = new byte[6];
        var span = new Span<byte>(result);
        var bitIndex = 0;
        for (var i = 0; i < Math.Min(enc.Length, 8); i++)
        {
            SpanBitHelper.SetBitU(span, ref bitIndex, 6, (uint)(enc[i] & 0x3F));
        }
        return result;
    }

    public static string AircraftIdDecoding(ReadOnlySpan<byte> data)
    {
        var stringLen = (data.Length * 8) / 6;
        var src = new byte[stringLen];
        var bitIndex = 0;
        for (var i = 0; i < stringLen; i++)
        {
            src[i] = (byte)SpanBitHelper.GetBitU(data, ref bitIndex, 6);
            if (src[i] <= 0x1A) src[i] |= 0x40;
        }

        return Encoding.ASCII.GetString(src);
    }

    private static double Mod(double x, double y)
    {
        return x - y * Math.Floor(x / y);
    }

    /// <summary>
    /// Number of latitude zones between the equator and a pole
    /// </summary>
    private const int Nz = 15;
    
    /// <summary>
    /// Longitude zone number (1 .. 59)
    /// </summary>
    /// <param name="lat">Latitude [°]</param>
    /// <returns></returns>
    private static int Nl(double lat)
    {
        if (lat is < -87.0 or > 87.0) return 1;
        var latRad = Math.PI * lat / 180.0;
        var x = 1.0 - Math.Cos(Math.PI / (2.0 * Nz));
        var y = Math.Pow(Math.Cos(latRad), 2);
        var arcArg = 1.0 - x / y;
        if (arcArg > 1.0) arcArg = 1.0;
        if (arcArg < -1.0) arcArg = -1.0;
        var z = Math.Acos(arcArg);
        return (int)Math.Floor(2.0 * Math.PI / z);
    }

    /// <summary>
    /// Decoding aircraft position (globally method)
    /// </summary>
    /// <param name="nCprLatEven">17 bits of latitude from even message</param>
    /// <param name="nCprLonEven">17 bits of longitude from even message</param>
    /// <param name="nCprLatOdd">17 bits of latitude from odd message</param>
    /// <param name="nCprLonOdd">17 bits of longitude from odd message</param>
    /// <param name="tEven">Even message reception timestamp</param>
    /// <param name="tOdd">Odd message reception timestamp</param>
    /// <param name="zoneSize">
    /// For Airborne Position = 360.0.
    /// For Surface position = 90.0.</param>
    /// <returns>Latitude/Longitude</returns>
    public static (double Lat, double Lon) GloballyUnambiguousPositionDecoding(uint nCprLatEven, uint nCprLonEven,
        uint nCprLatOdd, uint nCprLonOdd, DateTime tEven, DateTime tOdd, double zoneSize = 360.0)
    {
        var dLatEven = zoneSize / (4.0 * Nz);
        var dLatOdd = zoneSize / (4.0 * Nz - 1.0);
        const double part = 1 << 17;
        
        var latCprEven = nCprLatEven / part;
        var lonCprEven = nCprLonEven / part;
        var latCprOdd = nCprLatOdd / part;
        var lonCprOdd = nCprLonOdd / part;
       
        var j = (int)Math.Floor(59.0 * latCprEven - 60.0 * latCprOdd + 0.5);

        var latEven = dLatEven * (Mod(j, 60) + latCprEven);
        var latOdd = dLatOdd * (Mod(j, 59) + latCprOdd);

        if (latEven >= 270.0) latEven -= 360.0;
        if (latOdd >= 270.0) latOdd -= 360;

        var nlEven = Nl(latEven);
        var nlOdd = Nl(latOdd);

        if (nlEven != nlOdd) return (Lat: double.NaN, Lon: double.NaN);

        var lat = tEven >= tOdd ? latEven : latOdd;

        var m = Math.Floor(lonCprEven * (nlEven - 1) - lonCprOdd * nlEven + 0.5);

        var nEven = Math.Max(nlEven, 1);
        var nOdd = Math.Max(nlEven - 1, 1);

        var dLonEven = zoneSize / nEven;
        var dLonOdd = zoneSize / nOdd;

        var lonEven = dLonEven * (Mod(m, nEven) + lonCprEven);
        var lonOdd = dLonOdd * (Mod(m, nOdd) + lonCprOdd);
        
        var lon = tEven >= tOdd ? lonEven : lonOdd;
        if (lon >= 180.0) lon -= 360.0;

        return (Lat: lat, Lon: lon);
    }

    /// <summary>
    /// Decoding aircraft position (locally method)
    /// </summary>
    /// <param name="nCprLat">17 bits of latitude from message</param>
    /// <param name="nCprLon">17 bits of longitude from message</param>
    /// <param name="latRef">Latitude of the nearest reference position [-90.0; 90.0]</param>
    /// <param name="lonRef">Longitude of the nearest reference position [-180.0; 180.0]</param>
    /// <param name="format">Even/Odd</param>
    /// <param name="zoneSize">
    /// For Airborne Position = 360.0.
    /// For Surface position = 90.0.</param>
    /// <returns>Latitude/Longitude</returns>
    public static (double Lat, double Lon) LocallyUnambiguousPositionDecoding(uint nCprLat, uint nCprLon, double latRef,
        double lonRef, CprFormatEnum format, double zoneSize = 360.0)
    {
        const double part = 1 << 17;
        var latCpr = nCprLat / part;
        var lonCpr = nCprLon / part;
        
        var i = format == CprFormatEnum.Even ? 0 : 1;
        var dLat = zoneSize / (4.0 * Nz - i);
        var j = (int)Math.Floor(latRef / dLat) + (int)Math.Floor(Mod(latRef, dLat) / dLat - latCpr + 0.5);
        var lat = dLat * (j + latCpr);
        if (lat >= 270.0) lat -= 360.0;
        
        var nl = Nl(lat);
        var dLon = zoneSize / Math.Max(nl - i, 1);
        var m = (int)Math.Floor(lonRef / dLon) + (int)Math.Floor(Mod(lonRef, dLon) / dLon - lonCpr + 0.5);
        var lon = dLon * (m + lonCpr);
        if (lon >= 180.0) lon -= 360.0;
        
        return (Lat: lat, Lon: lon);
    }

    /// <summary>
    /// Encoding aircraft position
    /// </summary>
    /// <param name="lat">Latitude [-90.0; 90.0]</param>
    /// <param name="lon">Longitude [-180.0; 180.0]</param>
    /// <param name="format">Even/Odd</param>
    /// <param name="zoneSize">
    /// For Airborne Position = 360.0.
    /// For Surface position = 90.0.</param>
    /// <returns></returns>
    public static (uint Lat, uint Lon) UnambiguousPositionEncoding(double lat, double lon, CprFormatEnum format,
        double zoneSize = 360.0)
    {
        if (lat < 0) lat += zoneSize;
        if (lon < 0) lon += 180.0;
        lon %= zoneSize;
        var nl = Nl(lat) - (format == CprFormatEnum.Even ? 0 : 1);
        var dLon = zoneSize / Math.Max(nl, 1); // Prevent division by zero
        var nLon = (uint)Math.Floor((1 << 17) * (lon % dLon) / dLon);

        var dLat = format == CprFormatEnum.Even ? zoneSize / 60 : zoneSize / 59;
        var nLat = (uint)Math.Floor((1 << 17) * (lat % dLat) / dLat);

        return (Lat: nLat, Lon: nLon);
    }
}
