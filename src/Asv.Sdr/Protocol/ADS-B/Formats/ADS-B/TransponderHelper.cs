using System;
using System.Text;
using Asv.IO;

namespace Asv.Sdr;

public static class TransponderHelper
{
    public static readonly byte[] Preamble = [0xA1, 0x40];
    public const int LongFrameLengthBytes = 14;
    public const int ShortFrameLengthBytes = 7;
    
    
    public static int GetMessageLength(int downlinkFormat)
    {
        return downlinkFormat >= 16 ? LongFrameLengthBytes : ShortFrameLengthBytes;
    }
    public static int GetDownlinkFormat(ReadOnlySpan<byte> frame)
    {
        return (frame[0] >> 3) & 0x1F;
    }

    public static int AdditionalIdentifier(ReadOnlySpan<byte> frame)
    {
        return frame[0] & 0x7;
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
    public static AdsbMessageTypeEnum GetMessageType(ReadOnlySpan<byte> frame)
    {
        var tc = (frame[4] >> 3) & 0x1F;
        return tc switch
        {
            >= 1 and <= 4 => AdsbMessageTypeEnum.AircraftIdentification,
            <= 8 => AdsbMessageTypeEnum.SurfacePosition,
            <= 18 => AdsbMessageTypeEnum.AirborneBarometricPosition,
            19 => AdsbMessageTypeEnum.AirborneVelocities,
            <= 22 => AdsbMessageTypeEnum.AirborneGnssPosition,
            <= 27 => AdsbMessageTypeEnum.EventDriven,
            28 => AdsbMessageTypeEnum.AircraftStatus,
            29 => AdsbMessageTypeEnum.TargetStateAndStatusInformation,
            31 => AdsbMessageTypeEnum.AircraftOperationStatus,
            _ => AdsbMessageTypeEnum.EventDriven
        };
    }

    public static byte GetMessageSybType(ReadOnlySpan<byte> frame)
    {
        return (byte)(frame[4] & 0x07);
    }

    public static ushort GetMessageId(ReadOnlySpan<byte> buffer)
    {
        var df = GetDownlinkFormat(buffer);
        if (GetMessageLength(df) == ShortFrameLengthBytes)
        {
            return (ushort)df;
        }

        if (df is not (17 or 18))
        {
            return (ushort)(df << 8);
        }

        var tc = GetMessageType(buffer);
        switch (tc)
        {
            case AdsbMessageTypeEnum.AircraftIdentification:
            case AdsbMessageTypeEnum.SurfacePosition:
            case AdsbMessageTypeEnum.AirborneBarometricPosition:
            case AdsbMessageTypeEnum.AirborneGnssPosition:
            case AdsbMessageTypeEnum.EventDriven:
                return (ushort)((17 << 8) | ((ushort)tc << 3));
            case AdsbMessageTypeEnum.AircraftStatus:
                var st0 = (ushort)GetMessageSybType(buffer);
                return (ushort)((17 << 8) | ((ushort)tc << 3) | st0);
            case AdsbMessageTypeEnum.TargetStateAndStatusInformation:
                return (ushort)((17 << 8) | ((ushort)tc << 3));
            case AdsbMessageTypeEnum.AirborneVelocities:
                var st1 = (VelocitySubTypeEnum)GetMessageSybType(buffer);
                if (st1 is VelocitySubTypeEnum.SubType1 or VelocitySubTypeEnum.SubType2)
                    return (ushort)((17 << 8) | ((ushort)tc << 3) | ((ushort)VelocitySubTypeEnum.SubType1 & 0x7));
                if (st1 is VelocitySubTypeEnum.SubType3 or VelocitySubTypeEnum.SubType4)
                    return (ushort)((17 << 8) | ((ushort)tc << 3) | ((ushort)VelocitySubTypeEnum.SubType3 & 0x7));
                throw new ArgumentOutOfRangeException();
            case AdsbMessageTypeEnum.AircraftOperationStatus:
                var st2 = (AircraftOperationalStatusEnum)GetMessageSybType(buffer);
                return (ushort)((17 << 8) | ((ushort)tc << 3) | (ushort)st2);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public static AircraftCategoryEnum GetAircraftCategory(int tc, int ca)
    {
        if (ca == 0) return AircraftCategoryEnum.NoCategoryInformation;
        if (tc == 1) return AircraftCategoryEnum.Reserved;
        return tc switch
        {
            2 => ca switch
            {
                1 => AircraftCategoryEnum.SurfaceEmergencyVehicle,
                2 => AircraftCategoryEnum.SurfaceServiceVehicle,
                3 => AircraftCategoryEnum.GroundObstruction
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
                2 => AircraftCategoryEnum.Small,
                3 => AircraftCategoryEnum.Large,
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
                tcValue = 4;
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
            case AircraftCategoryEnum.Small:
                tcValue = 4;
                caValue = 2;
                break;
            case AircraftCategoryEnum.Large:
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

    public static bool CheckCallsign(ReadOnlySpan<byte> data)
    {
        var stringLen = data.Length * 8 / 6;
        var bitIndex = 0;
        for (var i = 0; i < stringLen; i++)
        {
            var ch = (byte)SpanBitHelper.GetBitU(data, ref bitIndex, 6);
            if (ch is 0x20 or >= 0x1 and <= 0x1A or >= 0x30 and <= 0x39) continue;
            return false;
        }
        return true;
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
        if (latOdd >= 270.0) latOdd -= 360.0;

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
    // public static (uint Lat, uint Lon) UnambiguousPositionEncoding(double lat, double lon, CprFormatEnum format,
    //     double zoneSize = 360.0)
    // {
    //     if (lat < 0) lat += zoneSize;
    //     if (lon < 0) lon += 180.0;
    //     lon %= zoneSize;
    //     var nl = Nl(lat) - (format == CprFormatEnum.Even ? 0 : 1);
    //     var dLon = zoneSize / Math.Max(nl, 1); // Prevent division by zero
    //     var nLon = (uint)Math.Floor((1 << 17) * (lon % dLon) / dLon);
    //
    //     var dLat = format == CprFormatEnum.Even ? zoneSize / 60 : zoneSize / 59;
    //     var nLat = (uint)Math.Floor((1 << 17) * (lat % dLat) / dLat);
    //
    //     return (Lat: nLat, Lon: nLon);
    // }

    
    
    private const int CprScale = 1 << 17; // 131072
    private const double Eps = 1e-12;
    
    
    /// <summary>
    /// CPR encoding of latitude/longitude to 17-bit LAT-CPR and LON-CPR fields.
    ///
    /// zoneSize = 360.0: airborne CPR.
    /// zoneSize = 90.0: surface CPR transmitted 17-bit lower-order representation.
    /// </summary>
    public static (uint nCprLat, uint nCprLon) UnambiguousPositionEncoding(
        double lat,
        double lon,
        CprFormatEnum format,
        double zoneSize = 360.0)
    {
        ValidateLatitude(lat, nameof(lat));
        ValidateLongitude(lon, nameof(lon));
        ValidateZoneSize(zoneSize);

        int i = FormatIndex(format);

        lon = NormalizeLongitude180(lon);

        double dLat = DLat(i, zoneSize);

        long yRaw = (long)Math.Floor(
            CprScale * (Mod(lat, dLat) / dLat) + 0.5);

        uint nCprLat = ModCpr17(yRaw);

        // RLat is the latitude that the decoder reconstructs from the transmitted bin.
        // It is used to calculate the longitude zone count NL(RLat).
        double rLat = dLat * (Math.Floor(lat / dLat) + yRaw / (double)CprScale);
        ValidateDecodedLatitude(rLat, nameof(rLat));

        double dLon = DLon(rLat, i, zoneSize);

        long xRaw = (long)Math.Floor(
            CprScale * (Mod(lon, dLon) / dLon) + 0.5);

        uint nCprLon = ModCpr17(xRaw);

        return (nCprLat, nCprLon);
    }

    private static double DLat(CprFormatEnum format, double zoneSize)
    {
        return DLat(FormatIndex(format), zoneSize);
    }

    private static double DLat(int formatIndex, double zoneSize)
    {
        return zoneSize / (4 * Nz - formatIndex);
    }

    private static double DLon(double lat, int formatIndex, double zoneSize)
    {
        int n = Math.Max(NL(lat) - formatIndex, 1);
        return zoneSize / n;
    }

    /// <summary>
    /// CPR NL(lat): number of longitude zones at the given latitude.
    /// </summary>
    private static int NL(double lat)
    {
        double a = Math.Abs(lat);

        if (a < Eps)
            return 59;

        if (a > 87.0)
            return 1;

        if (Math.Abs(a - 87.0) < Eps)
            return 2;

        double latRad = a * Math.PI / 180.0;
        double cosLat = Math.Cos(latRad);

        double x =
            1.0 -
            (1.0 - Math.Cos(Math.PI / (2.0 * Nz))) /
            (cosLat * cosLat);

        // Guard against very small floating-point overshoot.
        x = Math.Max(-1.0, Math.Min(1.0, x));

        return (int)Math.Floor(2.0 * Math.PI / Math.Acos(x));
    }

    /// <summary>
    /// Mathematical modulo:
    /// mod(x, y) = x - y * floor(x / y)
    /// Unlike C# %, this works correctly for negative coordinates.
    /// </summary>
    private static double Mod(double x, double y)
    {
        return x - y * Math.Floor(x / y);
    }

    private static int FloorMod(int x, int y)
    {
        return (int)(x - y * Math.Floor((double)x / y));
    }

    private static uint ModCpr17(long value)
    {
        long r = value % CprScale;

        if (r < 0)
            r += CprScale;

        return (uint)r;
    }

    private static double NormalizeLongitude180(double lon)
    {
        double normalized = Mod(lon + 180.0, 360.0) - 180.0;

        // Avoid returning tiny values like -0.00000000000003.
        if (Math.Abs(normalized) < Eps)
            return 0.0;

        return normalized;
    }

    private static int FormatIndex(CprFormatEnum format)
    {
        return format switch
        {
            CprFormatEnum.Even => 0,
            CprFormatEnum.Odd  => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(format), "Unknown CPR format.")
        };
    }

    private static bool IsAirborneZoneSize(double zoneSize)
    {
        return Math.Abs(zoneSize - 360.0) < Eps;
    }

    private static bool IsSurfaceZoneSize(double zoneSize)
    {
        return Math.Abs(zoneSize - 90.0) < Eps;
    }

    private static void ValidateZoneSize(double zoneSize)
    {
        if (!IsFinite(zoneSize) || (!IsAirborneZoneSize(zoneSize) && !IsSurfaceZoneSize(zoneSize)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(zoneSize),
                "zoneSize must be 360.0 for airborne CPR or 90.0 for surface CPR.");
        }
    }

    private static void ValidateCpr17(uint value, string paramName)
    {
        if (value >= CprScale)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                "CPR latitude/longitude value must be a 17-bit unsigned integer: 0..131071.");
        }
    }

    private static void ValidateLatitude(double lat, string paramName)
    {
        if (!IsFinite(lat) || lat < -90.0 || lat > 90.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                "Latitude must be in range [-90, +90] degrees.");
        }
    }

    private static void ValidateDecodedLatitude(double lat, string paramName)
    {
        if (!IsFinite(lat) || lat < -90.0 - 1e-9 || lat > 90.0 + 1e-9)
        {
            throw new InvalidOperationException(
                $"Decoded latitude is outside valid range: {paramName} = {lat}.");
        }
    }

    private static void ValidateLongitude(double lon, string paramName)
    {
        if (!IsFinite(lon) || lon < -180.0 || lon > 180.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                "Longitude must be in range [-180, +180] degrees.");
        }
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
    
    
    
    
    // public static (byte? NicA, byte? NicB, byte? NicC, byte? Nic, ) GetNic(byte typeCode)
    // {
    //     switch (typeCode)
    //     {
    //         case 5: return (0, null);
    //         case 10: return 0;
    //         case 11: return 1;
    //         
    //     }
    // }
}
