namespace Asv.Sdr;

/// <summary>
/// Identification-category set encoded by the BDS 0,8 type code.
/// </summary>
public enum AdsbAircraftCategorySetEnum
{
    SetD = 1,
    SetC = 2,
    SetB = 3,
    SetA = 4
}

/// <summary>
/// Version-aware semantic value of the BDS 0,8 aircraft/emitter category.
/// </summary>
public enum AdsbAircraftCategoryValueEnum
{
    Unknown = 0,
    Reserved,
    NoCategoryInformation,
    SurfaceEmergencyVehicle,
    SurfaceServiceVehicle,
    FixedOrTetheredObstruction,
    PointOrTetheredObstruction,
    ClusterObstacle,
    LineObstacle,
    GliderOrSailplane,
    LighterThanAir,
    ParachutistOrSkydiver,
    UltralightHangGliderOrParaglider,
    UnmannedAerialVehicle,
    SpaceOrTransAtmosphericVehicle,
    Light,
    Medium1,
    Medium2,
    Small,
    Large,
    HighVortexAircraft,
    HighVortexLargeAircraft,
    Heavy,
    HighPerformanceAircraft,
    HighPerformanceAndHighSpeedAircraft,
    Rotorcraft
}

public enum AdsbAircraftCategoryValidityEnum
{
    Invalid = 0,
    Valid,
    NoInformation,
    Reserved,
    TypeCodeNotApplicable,
    UnsupportedVersion
}

/// <summary>
/// Typed interpretation of the raw BDS 0,8 type-code/category-code pair.
/// It deliberately contains no presentation text.
/// </summary>
public sealed class AdsbAircraftCategoryInfo
{
    public AdsbVersionNumberEnum Version { get; set; }
    public byte TypeCode { get; set; }
    public byte CategoryCode { get; set; }
    public AdsbAircraftCategorySetEnum? CategorySet { get; set; }
    public AdsbAircraftCategoryValueEnum Category { get; set; }
    public AdsbAircraftCategoryValidityEnum Validity { get; set; }
    public bool IsValid => Validity is AdsbAircraftCategoryValidityEnum.Valid
        or AdsbAircraftCategoryValidityEnum.NoInformation;
}

public enum AdsbPositionQualityMetricEnum
{
    Unknown = 0,
    Nucp,
    Nic
}

public enum AdsbPositionQualityValidityEnum
{
    NotAvailable = 0,
    Valid,
    Unknown,
    NotApplicable,
    Invalid
}

public enum AdsbPositionQualityReasonEnum
{
    None = 0,
    ReportedUnknown,
    UnsupportedVersion,
    TypeCodeNotApplicable,
    NicSupplementAUnavailable,
    NicSupplementBUnavailable,
    NicSupplementCUnavailable,
    InvalidNicSupplementCombination
}

public enum AdsbContainmentBoundKindEnum
{
    None = 0,
    HorizontalProtectionLimit,
    RadiusOfContainment
}

public enum AdsbContainmentBoundComparisonEnum
{
    None = 0,
    LessThan,
    GreaterThanOrEqual
}

public enum AdsbDistanceUnitEnum
{
    None = 0,
    Meters,
    NauticalMiles
}

/// <summary>
/// Version-aware interpretation of a BDS 0,5 or BDS 0,6 position-quality code.
/// </summary>
public sealed class AdsbPositionQualityInfo
{
    public AdsbVersionNumberEnum Version { get; set; }
    public byte TypeCode { get; set; }
    public AdsbPositionQualityMetricEnum Metric { get; set; }
    public int? Code { get; set; }
    public AdsbPositionQualityValidityEnum Validity { get; set; }
    public AdsbPositionQualityReasonEnum Reason { get; set; }
    public AdsbContainmentBoundKindEnum BoundKind { get; set; }
    public AdsbContainmentBoundComparisonEnum BoundComparison { get; set; }
    public double? Bound { get; set; }
    public AdsbDistanceUnitEnum BoundUnit { get; set; }
}

public enum AdsbEmergencyStateEnum : byte
{
    NoEmergency = 0,
    GeneralEmergency = 1,
    LifeguardOrMedicalEmergency = 2,
    MinimumFuel = 3,
    NoCommunications = 4,
    UnlawfulInterference = 5,
    DownedAircraft = 6,
    Reserved = 7
}

public enum AdsbTargetVerticalDataSourceEnum : byte
{
    NoValidData = 0,
    McpOrFcu = 1,
    HoldingAltitude = 2,
    FmsOrNavigation = 3
}

public enum AdsbTargetHorizontalDataSourceEnum : byte
{
    NoValidData = 0,
    McpOrFcu = 1,
    HoldingHeadingOrTrack = 2,
    FmsOrRnav = 3
}

public enum AdsbTargetAltitudeTypeEnum : byte
{
    FlightLevel = 0,
    MeanSeaLevel = 1
}

public enum AdsbTargetAltitudeCapabilityEnum : byte
{
    HoldingAltitude = 0,
    HoldingAltitudeAndAutopilotControlPanel = 1,
    HoldingAltitudeAutopilotControlPanelAndFms = 2,
    Reserved = 3
}

public enum AdsbTargetModeIndicatorEnum : byte
{
    Unknown = 0,
    Acquiring = 1,
    Maintaining = 2,
    Reserved = 3
}

public enum AdsbSelectedAltitudeSourceEnum : byte
{
    McpOrFcu = 0,
    Fms = 1
}

public static partial class TransponderHelper
{
    /// <summary>
    /// Decodes the BDS 0,8 category using the semantics of the indicated ADS-B version.
    /// </summary>
    /// <remarks>
    /// Category sets A through D follow ICAO Doc 9871 / the corresponding DO-260 edition.
    /// </remarks>
    public static AdsbAircraftCategoryInfo DecodeAircraftCategory(
        AdsbVersionNumberEnum version,
        byte typeCode,
        byte categoryCode
    )
    {
        var result = new AdsbAircraftCategoryInfo
        {
            Version = version,
            TypeCode = typeCode,
            CategoryCode = categoryCode,
            Category = AdsbAircraftCategoryValueEnum.Unknown,
            Validity = AdsbAircraftCategoryValidityEnum.Invalid,
            CategorySet = typeCode is >= 1 and <= 4
                ? (AdsbAircraftCategorySetEnum)typeCode
                : null
        };

        if (categoryCode > 7)
        {
            return result;
        }

        if (typeCode is < 1 or > 4)
        {
            result.Validity = AdsbAircraftCategoryValidityEnum.TypeCodeNotApplicable;
            return result;
        }

        var versionNumber = (int)version;
        if (versionNumber is < 0 or > 2)
        {
            result.Validity = AdsbAircraftCategoryValidityEnum.UnsupportedVersion;
            return result;
        }

        result.Category = DecodeAircraftCategoryValue(versionNumber, typeCode, categoryCode);
        result.Validity = result.Category switch
        {
            AdsbAircraftCategoryValueEnum.NoCategoryInformation =>
                AdsbAircraftCategoryValidityEnum.NoInformation,
            AdsbAircraftCategoryValueEnum.Reserved =>
                AdsbAircraftCategoryValidityEnum.Reserved,
            AdsbAircraftCategoryValueEnum.Unknown =>
                AdsbAircraftCategoryValidityEnum.Invalid,
            _ => AdsbAircraftCategoryValidityEnum.Valid
        };
        return result;
    }

    /// <summary>
    /// Decodes airborne-position NUCp/NIC and its HPL/RC containment bound.
    /// </summary>
    /// <remarks>
    /// The version-dependent mapping follows ICAO Mode S DAPs IGD 7.4.2,
    /// Table 7-2, and the associated NIC supplement tables.
    /// </remarks>
    public static AdsbPositionQualityInfo DecodeAirbornePositionQuality(
        AdsbVersionNumberEnum version,
        byte typeCode,
        bool? nicSupplementA,
        bool? nicSupplementB
    )
    {
        var versionNumber = (int)version;
        if (versionNumber is < 0 or > 2)
        {
            return PositionQualityUnavailable(
                version,
                typeCode,
                AdsbPositionQualityReasonEnum.UnsupportedVersion
            );
        }

        if (typeCode is not (>= 9 and <= 18) and not (>= 20 and <= 22))
        {
            return PositionQualityNotApplicable(version, typeCode);
        }

        return version switch
        {
            AdsbVersionNumberEnum.AppendixA => DecodeVersion0AirborneNucp(typeCode),
            AdsbVersionNumberEnum.AppendixB => DecodeVersion1AirborneNic(
                typeCode,
                nicSupplementA
            ),
            AdsbVersionNumberEnum.AppendixC => DecodeVersion2AirborneNic(
                typeCode,
                nicSupplementA,
                nicSupplementB
            ),
            _ => PositionQualityUnavailable(
                version,
                typeCode,
                AdsbPositionQualityReasonEnum.UnsupportedVersion
            )
        };
    }

    /// <summary>
    /// Decodes surface-position NUCp/NIC and its HPL/RC containment bound.
    /// </summary>
    /// <remarks>
    /// The version-dependent mapping follows ICAO Mode S DAPs IGD 7.4.2,
    /// Table 7-2, and the associated NIC supplement tables.
    /// </remarks>
    public static AdsbPositionQualityInfo DecodeSurfacePositionQuality(
        AdsbVersionNumberEnum version,
        byte typeCode,
        bool? nicSupplementA,
        bool? nicSupplementC
    )
    {
        var versionNumber = (int)version;
        if (versionNumber is < 0 or > 2)
        {
            return PositionQualityUnavailable(
                version,
                typeCode,
                AdsbPositionQualityReasonEnum.UnsupportedVersion
            );
        }

        if (typeCode is < 5 or > 8)
        {
            return PositionQualityNotApplicable(version, typeCode);
        }

        return version switch
        {
            AdsbVersionNumberEnum.AppendixA => DecodeVersion0SurfaceNucp(typeCode),
            AdsbVersionNumberEnum.AppendixB => DecodeVersion1SurfaceNic(
                typeCode,
                nicSupplementA
            ),
            AdsbVersionNumberEnum.AppendixC => DecodeVersion2SurfaceNic(
                typeCode,
                nicSupplementA,
                nicSupplementC
            ),
            _ => PositionQualityUnavailable(
                version,
                typeCode,
                AdsbPositionQualityReasonEnum.UnsupportedVersion
            )
        };
    }

    private static AdsbAircraftCategoryValueEnum DecodeAircraftCategoryValue(
        int version,
        byte typeCode,
        byte categoryCode
    ) =>
        (version, typeCode, categoryCode) switch
        {
            (0 or 1, 1, _) => AdsbAircraftCategoryValueEnum.Reserved,
            (2, 1, 0) => AdsbAircraftCategoryValueEnum.NoCategoryInformation,
            (2, 1, _) => AdsbAircraftCategoryValueEnum.Reserved,

            (_, 2, 0) => AdsbAircraftCategoryValueEnum.NoCategoryInformation,
            (_, 2, 1) => AdsbAircraftCategoryValueEnum.SurfaceEmergencyVehicle,
            (_, 2, 2) => AdsbAircraftCategoryValueEnum.SurfaceServiceVehicle,
            (0 or 1, 2, 3) => AdsbAircraftCategoryValueEnum.FixedOrTetheredObstruction,
            (2, 2, 3) => AdsbAircraftCategoryValueEnum.PointOrTetheredObstruction,
            (1 or 2, 2, 4) => AdsbAircraftCategoryValueEnum.ClusterObstacle,
            (1 or 2, 2, 5) => AdsbAircraftCategoryValueEnum.LineObstacle,
            (_, 2, _) => AdsbAircraftCategoryValueEnum.Reserved,

            (_, 3, 0) => AdsbAircraftCategoryValueEnum.NoCategoryInformation,
            (_, 3, 1) => AdsbAircraftCategoryValueEnum.GliderOrSailplane,
            (_, 3, 2) => AdsbAircraftCategoryValueEnum.LighterThanAir,
            (_, 3, 3) => AdsbAircraftCategoryValueEnum.ParachutistOrSkydiver,
            (_, 3, 4) => AdsbAircraftCategoryValueEnum.UltralightHangGliderOrParaglider,
            (_, 3, 5) => AdsbAircraftCategoryValueEnum.Reserved,
            (_, 3, 6) => AdsbAircraftCategoryValueEnum.UnmannedAerialVehicle,
            (_, 3, 7) => AdsbAircraftCategoryValueEnum.SpaceOrTransAtmosphericVehicle,

            (_, 4, 0) => AdsbAircraftCategoryValueEnum.NoCategoryInformation,
            (_, 4, 1) => AdsbAircraftCategoryValueEnum.Light,
            (0, 4, 2) => AdsbAircraftCategoryValueEnum.Medium1,
            (0, 4, 3) => AdsbAircraftCategoryValueEnum.Medium2,
            (1 or 2, 4, 2) => AdsbAircraftCategoryValueEnum.Small,
            (1 or 2, 4, 3) => AdsbAircraftCategoryValueEnum.Large,
            (0 or 1, 4, 4) => AdsbAircraftCategoryValueEnum.HighVortexAircraft,
            (2, 4, 4) => AdsbAircraftCategoryValueEnum.HighVortexLargeAircraft,
            (_, 4, 5) => AdsbAircraftCategoryValueEnum.Heavy,
            (0, 4, 6) => AdsbAircraftCategoryValueEnum.HighPerformanceAircraft,
            (1 or 2, 4, 6) =>
                AdsbAircraftCategoryValueEnum.HighPerformanceAndHighSpeedAircraft,
            (_, 4, 7) => AdsbAircraftCategoryValueEnum.Rotorcraft,
            _ => AdsbAircraftCategoryValueEnum.Unknown
        };

    private static AdsbPositionQualityInfo DecodeVersion0AirborneNucp(byte typeCode) =>
        typeCode switch
        {
            9 or 20 => PositionQualityValid(0, typeCode, 9, 7.5, AdsbDistanceUnitEnum.Meters),
            10 or 21 => PositionQualityValid(0, typeCode, 8, 25, AdsbDistanceUnitEnum.Meters),
            11 => PositionQualityValid(0, typeCode, 7, 0.1, AdsbDistanceUnitEnum.NauticalMiles),
            12 => PositionQualityValid(0, typeCode, 6, 0.2, AdsbDistanceUnitEnum.NauticalMiles),
            13 => PositionQualityValid(0, typeCode, 5, 0.5, AdsbDistanceUnitEnum.NauticalMiles),
            14 => PositionQualityValid(0, typeCode, 4, 1, AdsbDistanceUnitEnum.NauticalMiles),
            15 => PositionQualityValid(0, typeCode, 3, 2, AdsbDistanceUnitEnum.NauticalMiles),
            16 => PositionQualityValid(0, typeCode, 2, 10, AdsbDistanceUnitEnum.NauticalMiles),
            17 => PositionQualityValid(0, typeCode, 1, 20, AdsbDistanceUnitEnum.NauticalMiles),
            18 or 22 => PositionQualityUnknown(0, typeCode, 0),
            _ => PositionQualityNotApplicable(AdsbVersionNumberEnum.AppendixA, typeCode)
        };

    private static AdsbPositionQualityInfo DecodeVersion0SurfaceNucp(byte typeCode) =>
        typeCode switch
        {
            5 => PositionQualityValid(0, typeCode, 9, 7.5, AdsbDistanceUnitEnum.Meters),
            6 => PositionQualityValid(0, typeCode, 8, 25, AdsbDistanceUnitEnum.Meters),
            7 => PositionQualityValid(0, typeCode, 7, 0.1, AdsbDistanceUnitEnum.NauticalMiles),
            8 => PositionQualityValid(
                0,
                typeCode,
                6,
                0.1,
                AdsbDistanceUnitEnum.NauticalMiles,
                AdsbContainmentBoundComparisonEnum.GreaterThanOrEqual
            ),
            _ => PositionQualityNotApplicable(AdsbVersionNumberEnum.AppendixA, typeCode)
        };

    private static AdsbPositionQualityInfo DecodeVersion1AirborneNic(
        byte typeCode,
        bool? nicSupplementA
    )
    {
        if (!nicSupplementA.HasValue)
        {
            return PositionQualityUnavailable(
                AdsbVersionNumberEnum.AppendixB,
                typeCode,
                AdsbPositionQualityReasonEnum.NicSupplementAUnavailable
            );
        }

        return (typeCode, nicSupplementA.Value) switch
        {
            (9 or 20, false) => PositionQualityValid(1, typeCode, 11, 7.5, AdsbDistanceUnitEnum.Meters),
            (10 or 21, false) => PositionQualityValid(1, typeCode, 10, 25, AdsbDistanceUnitEnum.Meters),
            (11, true) => PositionQualityValid(1, typeCode, 9, 75, AdsbDistanceUnitEnum.Meters),
            (11, false) => PositionQualityValid(1, typeCode, 8, 0.1, AdsbDistanceUnitEnum.NauticalMiles),
            (12, false) => PositionQualityValid(1, typeCode, 7, 0.2, AdsbDistanceUnitEnum.NauticalMiles),
            (13, true) => PositionQualityValid(1, typeCode, 6, 0.6, AdsbDistanceUnitEnum.NauticalMiles),
            (13, false) => PositionQualityValid(1, typeCode, 6, 0.5, AdsbDistanceUnitEnum.NauticalMiles),
            (14, false) => PositionQualityValid(1, typeCode, 5, 1, AdsbDistanceUnitEnum.NauticalMiles),
            (15, false) => PositionQualityValid(1, typeCode, 4, 2, AdsbDistanceUnitEnum.NauticalMiles),
            (16, true) => PositionQualityValid(1, typeCode, 3, 4, AdsbDistanceUnitEnum.NauticalMiles),
            (16, false) => PositionQualityValid(1, typeCode, 2, 8, AdsbDistanceUnitEnum.NauticalMiles),
            (17, false) => PositionQualityValid(1, typeCode, 1, 20, AdsbDistanceUnitEnum.NauticalMiles),
            (18 or 22, false) => PositionQualityUnknown(1, typeCode, 0),
            _ => PositionQualityInvalid(
                AdsbVersionNumberEnum.AppendixB,
                typeCode,
                AdsbPositionQualityReasonEnum.InvalidNicSupplementCombination
            )
        };
    }

    private static AdsbPositionQualityInfo DecodeVersion1SurfaceNic(
        byte typeCode,
        bool? nicSupplementA
    )
    {
        if (!nicSupplementA.HasValue)
        {
            return PositionQualityUnavailable(
                AdsbVersionNumberEnum.AppendixB,
                typeCode,
                AdsbPositionQualityReasonEnum.NicSupplementAUnavailable
            );
        }

        return (typeCode, nicSupplementA.Value) switch
        {
            (5, false) => PositionQualityValid(1, typeCode, 11, 7.5, AdsbDistanceUnitEnum.Meters),
            (6, false) => PositionQualityValid(1, typeCode, 10, 25, AdsbDistanceUnitEnum.Meters),
            (7, true) => PositionQualityValid(1, typeCode, 9, 75, AdsbDistanceUnitEnum.Meters),
            (7, false) => PositionQualityValid(1, typeCode, 8, 0.1, AdsbDistanceUnitEnum.NauticalMiles),
            (8, false) => PositionQualityUnknown(1, typeCode, 0),
            _ => PositionQualityInvalid(
                AdsbVersionNumberEnum.AppendixB,
                typeCode,
                AdsbPositionQualityReasonEnum.InvalidNicSupplementCombination
            )
        };
    }

    private static AdsbPositionQualityInfo DecodeVersion2AirborneNic(
        byte typeCode,
        bool? nicSupplementA,
        bool? nicSupplementB
    )
    {
        if (!nicSupplementA.HasValue)
        {
            return PositionQualityUnavailable(
                AdsbVersionNumberEnum.AppendixC,
                typeCode,
                AdsbPositionQualityReasonEnum.NicSupplementAUnavailable
            );
        }

        if (!nicSupplementB.HasValue)
        {
            return PositionQualityUnavailable(
                AdsbVersionNumberEnum.AppendixC,
                typeCode,
                AdsbPositionQualityReasonEnum.NicSupplementBUnavailable
            );
        }

        return (typeCode, nicSupplementA.Value, nicSupplementB.Value) switch
        {
            (9 or 20, false, false) => PositionQualityValid(2, typeCode, 11, 7.5, AdsbDistanceUnitEnum.Meters),
            (10 or 21, false, false) => PositionQualityValid(2, typeCode, 10, 25, AdsbDistanceUnitEnum.Meters),
            (11, true, true) => PositionQualityValid(2, typeCode, 9, 75, AdsbDistanceUnitEnum.Meters),
            (11, false, false) => PositionQualityValid(2, typeCode, 8, 0.1, AdsbDistanceUnitEnum.NauticalMiles),
            (12, false, false) => PositionQualityValid(2, typeCode, 7, 0.2, AdsbDistanceUnitEnum.NauticalMiles),
            (13, false, true) => PositionQualityValid(2, typeCode, 6, 0.3, AdsbDistanceUnitEnum.NauticalMiles),
            (13, false, false) => PositionQualityValid(2, typeCode, 6, 0.5, AdsbDistanceUnitEnum.NauticalMiles),
            (13, true, true) => PositionQualityValid(2, typeCode, 6, 0.6, AdsbDistanceUnitEnum.NauticalMiles),
            (14, false, false) => PositionQualityValid(2, typeCode, 5, 1, AdsbDistanceUnitEnum.NauticalMiles),
            (15, false, false) => PositionQualityValid(2, typeCode, 4, 2, AdsbDistanceUnitEnum.NauticalMiles),
            (16, true, true) => PositionQualityValid(2, typeCode, 3, 4, AdsbDistanceUnitEnum.NauticalMiles),
            (16, false, false) => PositionQualityValid(2, typeCode, 2, 8, AdsbDistanceUnitEnum.NauticalMiles),
            (17, false, false) => PositionQualityValid(2, typeCode, 1, 20, AdsbDistanceUnitEnum.NauticalMiles),
            (18 or 22, false, false) => PositionQualityUnknown(2, typeCode, 0),
            _ => PositionQualityInvalid(
                AdsbVersionNumberEnum.AppendixC,
                typeCode,
                AdsbPositionQualityReasonEnum.InvalidNicSupplementCombination
            )
        };
    }

    private static AdsbPositionQualityInfo DecodeVersion2SurfaceNic(
        byte typeCode,
        bool? nicSupplementA,
        bool? nicSupplementC
    )
    {
        if (!nicSupplementA.HasValue)
        {
            return PositionQualityUnavailable(
                AdsbVersionNumberEnum.AppendixC,
                typeCode,
                AdsbPositionQualityReasonEnum.NicSupplementAUnavailable
            );
        }

        if (!nicSupplementC.HasValue)
        {
            return PositionQualityUnavailable(
                AdsbVersionNumberEnum.AppendixC,
                typeCode,
                AdsbPositionQualityReasonEnum.NicSupplementCUnavailable
            );
        }

        return (typeCode, nicSupplementA.Value, nicSupplementC.Value) switch
        {
            (5, false, false) => PositionQualityValid(2, typeCode, 11, 7.5, AdsbDistanceUnitEnum.Meters),
            (6, false, false) => PositionQualityValid(2, typeCode, 10, 25, AdsbDistanceUnitEnum.Meters),
            (7, true, false) => PositionQualityValid(2, typeCode, 9, 75, AdsbDistanceUnitEnum.Meters),
            (7, false, false) => PositionQualityValid(2, typeCode, 8, 0.1, AdsbDistanceUnitEnum.NauticalMiles),
            (8, true, true) => PositionQualityValid(2, typeCode, 7, 0.2, AdsbDistanceUnitEnum.NauticalMiles),
            (8, true, false) => PositionQualityValid(2, typeCode, 6, 0.3, AdsbDistanceUnitEnum.NauticalMiles),
            (8, false, true) => PositionQualityValid(2, typeCode, 6, 0.6, AdsbDistanceUnitEnum.NauticalMiles),
            (8, false, false) => PositionQualityUnknown(2, typeCode, 0),
            _ => PositionQualityInvalid(
                AdsbVersionNumberEnum.AppendixC,
                typeCode,
                AdsbPositionQualityReasonEnum.InvalidNicSupplementCombination
            )
        };
    }

    private static AdsbPositionQualityInfo PositionQualityValid(
        int version,
        byte typeCode,
        int code,
        double bound,
        AdsbDistanceUnitEnum unit,
        AdsbContainmentBoundComparisonEnum comparison = AdsbContainmentBoundComparisonEnum.LessThan
    ) =>
        new()
        {
            Version = (AdsbVersionNumberEnum)version,
            TypeCode = typeCode,
            Metric = version == 0
                ? AdsbPositionQualityMetricEnum.Nucp
                : AdsbPositionQualityMetricEnum.Nic,
            Code = code,
            Validity = AdsbPositionQualityValidityEnum.Valid,
            Reason = AdsbPositionQualityReasonEnum.None,
            BoundKind = version == 0
                ? AdsbContainmentBoundKindEnum.HorizontalProtectionLimit
                : AdsbContainmentBoundKindEnum.RadiusOfContainment,
            BoundComparison = comparison,
            Bound = bound,
            BoundUnit = unit
        };

    private static AdsbPositionQualityInfo PositionQualityUnknown(
        int version,
        byte typeCode,
        int code
    ) =>
        new()
        {
            Version = (AdsbVersionNumberEnum)version,
            TypeCode = typeCode,
            Metric = version == 0
                ? AdsbPositionQualityMetricEnum.Nucp
                : AdsbPositionQualityMetricEnum.Nic,
            Code = code,
            Validity = AdsbPositionQualityValidityEnum.Unknown,
            Reason = AdsbPositionQualityReasonEnum.ReportedUnknown,
            BoundKind = version == 0
                ? AdsbContainmentBoundKindEnum.HorizontalProtectionLimit
                : AdsbContainmentBoundKindEnum.RadiusOfContainment
        };

    private static AdsbPositionQualityInfo PositionQualityUnavailable(
        AdsbVersionNumberEnum version,
        byte typeCode,
        AdsbPositionQualityReasonEnum reason
    ) =>
        new()
        {
            Version = version,
            TypeCode = typeCode,
            Metric = version == AdsbVersionNumberEnum.AppendixA
                ? AdsbPositionQualityMetricEnum.Nucp
                : (int)version is 1 or 2
                    ? AdsbPositionQualityMetricEnum.Nic
                    : AdsbPositionQualityMetricEnum.Unknown,
            Validity = AdsbPositionQualityValidityEnum.NotAvailable,
            Reason = reason,
            BoundKind = version == AdsbVersionNumberEnum.AppendixA
                ? AdsbContainmentBoundKindEnum.HorizontalProtectionLimit
                : (int)version is 1 or 2
                    ? AdsbContainmentBoundKindEnum.RadiusOfContainment
                    : AdsbContainmentBoundKindEnum.None
        };

    private static AdsbPositionQualityInfo PositionQualityNotApplicable(
        AdsbVersionNumberEnum version,
        byte typeCode
    )
    {
        var result = PositionQualityUnavailable(
            version,
            typeCode,
            AdsbPositionQualityReasonEnum.TypeCodeNotApplicable
        );
        result.Validity = AdsbPositionQualityValidityEnum.NotApplicable;
        return result;
    }

    private static AdsbPositionQualityInfo PositionQualityInvalid(
        AdsbVersionNumberEnum version,
        byte typeCode,
        AdsbPositionQualityReasonEnum reason
    )
    {
        var result = PositionQualityUnavailable(version, typeCode, reason);
        result.Validity = AdsbPositionQualityValidityEnum.Invalid;
        return result;
    }
}
