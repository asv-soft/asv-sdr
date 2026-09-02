namespace Asv.Sdr;

/// <summary>
/// Plausibility limits used to infer a BDS register when its selector is unknown.
/// A null limit disables the corresponding check.
/// </summary>
public sealed record BdsInferenceLimits
{
    /// <summary>
    /// Limits suitable for typical civil subsonic traffic.
    /// </summary>
    public static BdsInferenceLimits CivilSubsonic { get; } = new()
    {
        MaximumAbsoluteRollAngleDegrees = 49.921875,
        MaximumGroundSpeedKnots = 600.0,
        MaximumTrueAirspeedKnots = 500.0,
        MaximumGroundSpeedTrueAirspeedDifferenceKnots = 200.0,
        MaximumIndicatedAirspeedKnots = 500.0,
        MaximumMach = 1.0,
        MaximumAbsoluteVerticalRateFeetPerMinute = 6016.0,
    };

    /// <summary>
    /// Disables value-based inference while retaining structural validation.
    /// </summary>
    public static BdsInferenceLimits Unrestricted { get; } = new();

    public double? MaximumAbsoluteRollAngleDegrees { get; init; }
    public double? MaximumGroundSpeedKnots { get; init; }
    public double? MaximumTrueAirspeedKnots { get; init; }
    public double? MaximumGroundSpeedTrueAirspeedDifferenceKnots { get; init; }
    public double? MaximumIndicatedAirspeedKnots { get; init; }
    public double? MaximumMach { get; init; }
    public double? MaximumAbsoluteVerticalRateFeetPerMinute { get; init; }
}
