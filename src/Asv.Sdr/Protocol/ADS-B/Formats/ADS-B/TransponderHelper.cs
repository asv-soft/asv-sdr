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
            23 => AdsbMessageTypeEnum.EventDriven,
            <= 27 => AdsbMessageTypeEnum.Reserved,
            28 => AdsbMessageTypeEnum.AircraftStatus,
            29 => AdsbMessageTypeEnum.TargetStateAndStatusInformation,
            30 => AdsbMessageTypeEnum.Reserved,
            31 => AdsbMessageTypeEnum.AircraftOperationStatus,
            _ => AdsbMessageTypeEnum.Reserved
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

    public static string DecodeEmergencyState(int state)
    {
        return state switch
        {
            0 => "No emergency",
            1 => "General emergency",
            2 => "Lifeguard or medical emergency",
            3 => "Minimum fuel",
            4 => "No communications",
            5 => "Unlawful interference",
            6 => "Downed aircraft",
            7 => "Reserved",
            _ => "Invalid"
        };
    }

    public static bool GetModeAIdentityXBit(ushort identityCode)
    {
        return (identityCode & 0x0040) != 0;
    }

    public static ushort RemoveModeAIdentityXBit(ushort identityCode)
    {
        return (ushort)(((identityCode & 0x1F80) << 0) | (identityCode & 0x003F));
    }

    public static string DecodeModeAIdentityToSquawk(ushort identityCode)
    {
        return ModeSHelper.GetSquawk(RemoveModeAIdentityXBit(identityCode));
    }

    public static int? DecodeModeCAltitudeCode13(ushort altitudeCode)
    {
        altitudeCode &= 0x1FFF;
        if (altitudeCode == 0)
        {
            return null;
        }

        var mBit = (altitudeCode >> 6) & 0x1;
        var qBit = (altitudeCode >> 4) & 0x1;
        if (mBit != 0)
        {
            return null;
        }

        if (qBit != 0)
        {
            var n = ((altitudeCode >> 2) & 0x07E0) |
                    ((altitudeCode >> 1) & 0x0010) |
                    (altitudeCode & 0x000F);
            return n * 25 - 1000;
        }

        return ModeSHelper.GetAltitudeFromModeCAltitudeCode(altitudeCode);
    }

    public static string DecodeAdsbVersion(int version)
    {
        return version switch
        {
            0 => "ADS-B Version 0 / DO-260",
            1 => "ADS-B Version 1 / DO-260A",
            2 => "ADS-B Version 2 / DO-260B",
            _ => "Reserved"
        };
    }

    public static NacPInfo DecodeNacP(int code)
    {
        double?[] epu = [null, 18520, 7408, 3704, 1852, 926, 556, 185, 93, 30, 10, 3];
        double?[] vepu = [null, null, null, null, null, null, null, null, null, 45, 15, 4];
        var valid = code >= 0 && code < epu.Length;

        return new NacPInfo
        {
            Code = code,
            EpuMeters = valid ? epu[code] : null,
            VepuMeters = valid ? vepu[code] : null,
            Text = valid && epu[code].HasValue
                ? $"95% horizontal accuracy bound EPU <= {epu[code]} m"
                : code == 0 ? "Unknown" : "Reserved"
        };
    }

    public static NacVInfo DecodeNacV(int code)
    {
        double?[] hfomr = [null, 10, 3, 1, 0.3];
        double?[] vfomr = [null, 15.2, 4.5, 1.5, 0.46];
        var valid = code >= 0 && code < hfomr.Length;

        return new NacVInfo
        {
            Code = code,
            HorizontalVelocityErrorMps = valid ? hfomr[code] : null,
            VerticalVelocityErrorMps = valid ? vfomr[code] : null,
            Text = valid && hfomr[code].HasValue
                ? $"HFOMr <= {hfomr[code]} m/s, VFOMr <= {vfomr[code]} m/s"
                : code == 0 ? "Unknown" : "Reserved"
        };
    }

    public static SilInfo DecodeSil(int code, bool? supplement)
    {
        double? peRcu = code switch
        {
            1 => 1e-3,
            2 => 1e-5,
            3 => 1e-7,
            _ => null
        };

        double? peVpl = code switch
        {
            1 => 1e-3,
            2 => 1e-5,
            3 => 2e-7,
            _ => null
        };

        return new SilInfo
        {
            Code = code,
            Supplement = supplement,
            ProbabilityBasis = supplement.HasValue
                ? supplement.Value ? "per sample" : "per hour"
                : "not present",
            ProbabilityOfExceedingRc = peRcu,
            ProbabilityOfExceedingVpl = peVpl,
            Text = code == 0
                ? "Unknown or no integrity"
                : $"P(exceeding Rc) <= {peRcu:E0}, P(exceeding VPL) <= {peVpl:E0}"
        };
    }

    public static SdaInfo DecodeSda(int code)
    {
        return new SdaInfo
        {
            Code = code,
            Text = code switch
            {
                0 => "SDA=0: no or unknown system design assurance",
                1 => "SDA=1",
                2 => "SDA=2",
                3 => "SDA=3",
                _ => "Invalid"
            }
        };
    }

    public static GvaInfo DecodeGva(int code)
    {
        return new GvaInfo
        {
            Code = code,
            VerticalAccuracyMeters = code switch
            {
                1 => 150,
                2 => 45,
                _ => null
            },
            Text = code switch
            {
                0 => "Unknown or > 150 m",
                1 => "GVA <= 150 m",
                2 => "GVA <= 45 m",
                3 => "Reserved",
                _ => "Invalid"
            }
        };
    }

    public static AircraftLengthWidthInfo DecodeAircraftLengthWidth(int code)
    {
        int? length = code switch
        {
            1 => 15,
            2 or 3 => 25,
            4 or 5 => 35,
            6 or 7 => 45,
            8 or 9 => 55,
            10 or 11 => 65,
            12 or 13 => 75,
            14 or 15 => 85,
            _ => null
        };

        double? width = code switch
        {
            1 => 23,
            2 => 28.5,
            3 => 34,
            4 => 33,
            5 => 38,
            6 => 39.5,
            7 or 8 => 45,
            9 => 52,
            10 => 59.5,
            11 => 67,
            12 => 72.5,
            13 or 14 => 80,
            15 => 90,
            _ => null
        };

        return new AircraftLengthWidthInfo
        {
            Code = code,
            LengthMeters = length,
            WidthMeters = width
        };
    }

    public static AirborneCapabilityStatus DecodeAirborneCapability(ushort capabilityClass, int adsbVersion)
    {
        var targetChange = (capabilityClass & 0x00C0) >> 6;

        return new AirborneCapabilityStatus
        {
            Raw16 = capabilityClass,
            ReservedTop2 = (capabilityClass & 0xC000) >> 14,
            TcasOperational = adsbVersion >= 2
                ? (capabilityClass & 0x2000) != 0
                : (capabilityClass & 0x2000) == 0,
            Has1090EsIn = (capabilityClass & 0x1000) != 0,
            ReservedBits13_14 = (capabilityClass & 0x0C00) >> 10,
            AirReferencedVelocityReportCapability = (capabilityClass & 0x0200) != 0,
            TargetStateReportCapability = (capabilityClass & 0x0100) != 0,
            TargetChangeReportCapability = targetChange,
            TargetChangeReportCapabilityText = targetChange switch
            {
                0 => "Not supported",
                1 => "Supports TC+0 reports only",
                2 => "Supports multiple trajectory-change reports",
                3 => "Reserved",
                _ => "Invalid"
            },
            HasUatIn = adsbVersion >= 2 ? (capabilityClass & 0x0020) != 0 : null,
            ReservedLowBits = capabilityClass & 0x003F
        };
    }

    public static SurfaceCapabilityStatus DecodeSurfaceCapability(ushort capabilityClass)
    {
        var nacvCode = (capabilityClass & 0x00E0) >> 5;

        return new SurfaceCapabilityStatus
        {
            Raw16 = capabilityClass,
            ReservedTop2 = (capabilityClass & 0xC000) >> 14,
            PositionOffsetApplied = (capabilityClass & 0x2000) != 0,
            Has1090EsIn = (capabilityClass & 0x1000) != 0,
            ReservedBits = (capabilityClass & 0x0C00) >> 10,
            LowTxPowerClassB2GroundVehicle = (capabilityClass & 0x0200) != 0,
            HasUatIn = (capabilityClass & 0x0100) != 0,
            NacV = DecodeNacV(nacvCode),
            NicSupplementC = (capabilityClass & 0x0010) != 0,
            LengthWidthCode = capabilityClass & 0x000F
        };
    }

    public static OperationalModeStatus DecodeOperationalMode(ushort operationalMode, int adsbVersion)
    {
        var result = new OperationalModeStatus
        {
            Raw16 = operationalMode,
            ReservedTop2 = (operationalMode & 0xC000) >> 14,
            TcasRaActive = (operationalMode & 0x2000) != 0,
            IdentSwitchActive = (operationalMode & 0x1000) != 0,
            ReceivingAtcServices = (operationalMode & 0x0800) != 0
        };

        if (adsbVersion >= 2)
        {
            result.SingleAntenna = (operationalMode & 0x0400) != 0;
            result.Sda = DecodeSda((operationalMode & 0x0300) >> 8);
        }

        return result;
    }

    public static AcasRaInfo DecodeAcasRaBds30(ReadOnlySpan<byte> bds30Payload)
    {
        var tti = GetBitU(bds30Payload, 28, 2);

        var info = new AcasRaInfo
        {
            ThreatTypeIndicator = tti,
            ThreatTypeIndicatorText = tti switch
            {
                0 => "No threat identity data",
                1 => "Threat identity is 24-bit ICAO address",
                2 => "Threat identity is altitude, range and bearing",
                3 => "Reserved",
                _ => "Invalid"
            },
            IssuedRa = GetBitU(bds30Payload, 8, 1) != 0,
            Corrective = GetBitU(bds30Payload, 9, 1) != 0,
            DownwardSense = GetBitU(bds30Payload, 10, 1) != 0,
            IncreasedRate = GetBitU(bds30Payload, 11, 1) != 0,
            SenseReversal = GetBitU(bds30Payload, 12, 1) != 0,
            AltitudeCrossing = GetBitU(bds30Payload, 13, 1) != 0,
            Positive = GetBitU(bds30Payload, 14, 1) != 0,
            AraReserved15_21 = GetBitU(bds30Payload, 15, 7),
            NoBelow = GetBitU(bds30Payload, 22, 1) != 0,
            NoAbove = GetBitU(bds30Payload, 23, 1) != 0,
            NoLeft = GetBitU(bds30Payload, 24, 1) != 0,
            NoRight = GetBitU(bds30Payload, 25, 1) != 0,
            RaTerminated = GetBitU(bds30Payload, 26, 1) != 0,
            MultipleThreat = GetBitU(bds30Payload, 27, 1) != 0
        };

        if (tti == 1)
        {
            info.ThreatIcao = GetBitU(bds30Payload, 30, 24).ToString("X6");
            info.ThreatIdentityReserved54_55 = GetBitU(bds30Payload, 54, 2);
        }
        else if (tti == 2)
        {
            var ac13 = GetBitU(bds30Payload, 30, 13);
            var rangeRaw = GetBitU(bds30Payload, 43, 7);
            var bearingRaw = GetBitU(bds30Payload, 50, 6);

            info.ThreatAc13Raw = ac13;
            info.ThreatAltitudeFt = DecodeModeCAltitudeCode13((ushort)ac13);
            info.ThreatRangeRaw = rangeRaw;
            info.ThreatRangeNm = rangeRaw > 0 ? (rangeRaw - 1) / 10.0 : null;
            info.ThreatBearingRaw = bearingRaw;
            info.ThreatBearingDeg = bearingRaw > 0 ? 6 * (bearingRaw - 1) + 3 : null;
        }

        return info;
    }

    private static int GetBitU(ReadOnlySpan<byte> buffer, int bitOffset, int bitCount)
    {
        var bitIndex = bitOffset;
        return (int)SpanBitHelper.GetBitU(buffer, ref bitIndex, bitCount);
    }

    public sealed class NacPInfo
    {
        public int Code { get; set; }
        public double? EpuMeters { get; set; }
        public double? VepuMeters { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public sealed class NacVInfo
    {
        public int Code { get; set; }
        public double? HorizontalVelocityErrorMps { get; set; }
        public double? VerticalVelocityErrorMps { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public sealed class SilInfo
    {
        public int Code { get; set; }
        public bool? Supplement { get; set; }
        public string ProbabilityBasis { get; set; } = string.Empty;
        public double? ProbabilityOfExceedingRc { get; set; }
        public double? ProbabilityOfExceedingVpl { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public sealed class SdaInfo
    {
        public int Code { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public sealed class GvaInfo
    {
        public int Code { get; set; }
        public int? VerticalAccuracyMeters { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public sealed class AircraftLengthWidthInfo
    {
        public int Code { get; set; }
        public int? LengthMeters { get; set; }
        public double? WidthMeters { get; set; }
    }

    public sealed class AirborneCapabilityStatus
    {
        public ushort Raw16 { get; set; }
        public int ReservedTop2 { get; set; }
        public bool TcasOperational { get; set; }
        public bool Has1090EsIn { get; set; }
        public int ReservedBits13_14 { get; set; }
        public bool AirReferencedVelocityReportCapability { get; set; }
        public bool TargetStateReportCapability { get; set; }
        public int TargetChangeReportCapability { get; set; }
        public string TargetChangeReportCapabilityText { get; set; } = string.Empty;
        public bool? HasUatIn { get; set; }
        public int ReservedLowBits { get; set; }
    }

    public sealed class SurfaceCapabilityStatus
    {
        public ushort Raw16 { get; set; }
        public int ReservedTop2 { get; set; }
        public bool PositionOffsetApplied { get; set; }
        public bool Has1090EsIn { get; set; }
        public int ReservedBits { get; set; }
        public bool LowTxPowerClassB2GroundVehicle { get; set; }
        public bool HasUatIn { get; set; }
        public NacVInfo NacV { get; set; } = new();
        public bool NicSupplementC { get; set; }
        public int LengthWidthCode { get; set; }
    }

    public sealed class OperationalModeStatus
    {
        public ushort Raw16 { get; set; }
        public int ReservedTop2 { get; set; }
        public bool TcasRaActive { get; set; }
        public bool IdentSwitchActive { get; set; }
        public bool ReceivingAtcServices { get; set; }
        public bool? SingleAntenna { get; set; }
        public SdaInfo? Sda { get; set; }
    }

    public sealed class AcasRaInfo
    {
        public int ThreatTypeIndicator { get; set; }
        public string ThreatTypeIndicatorText { get; set; } = string.Empty;
        public bool IssuedRa { get; set; }
        public bool Corrective { get; set; }
        public bool DownwardSense { get; set; }
        public bool IncreasedRate { get; set; }
        public bool SenseReversal { get; set; }
        public bool AltitudeCrossing { get; set; }
        public bool Positive { get; set; }
        public int AraReserved15_21 { get; set; }
        public bool NoBelow { get; set; }
        public bool NoAbove { get; set; }
        public bool NoLeft { get; set; }
        public bool NoRight { get; set; }
        public bool RaTerminated { get; set; }
        public bool MultipleThreat { get; set; }
        public string? ThreatIcao { get; set; }
        public int? ThreatIdentityReserved54_55 { get; set; }
        public int? ThreatAc13Raw { get; set; }
        public int? ThreatAltitudeFt { get; set; }
        public int? ThreatRangeRaw { get; set; }
        public double? ThreatRangeNm { get; set; }
        public int? ThreatBearingRaw { get; set; }
        public int? ThreatBearingDeg { get; set; }
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
