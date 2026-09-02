using System;

namespace Asv.Sdr;

/// <summary>
/// Applies value-based plausibility rules to structurally valid BDS candidates.
/// </summary>
public static class BdsInference
{
    public static bool Is50(Bds50 candidate, BdsInferenceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(limits);

        if (candidate.HasRollAngle && ExceedsAbsolute(candidate.RollAngle, limits.MaximumAbsoluteRollAngleDegrees))
        {
            return false;
        }

        if (candidate.HasGroundSpeed && Exceeds(candidate.GroundSpeed, limits.MaximumGroundSpeedKnots))
        {
            return false;
        }

        if (candidate.HasTrueAirspeed && Exceeds(candidate.TrueAirspeed, limits.MaximumTrueAirspeedKnots))
        {
            return false;
        }

        return candidate.HasGroundSpeed == false ||
               candidate.HasTrueAirspeed == false ||
               ExceedsAbsolute(
                   candidate.GroundSpeed - candidate.TrueAirspeed,
                   limits.MaximumGroundSpeedTrueAirspeedDifferenceKnots
               ) == false;
    }

    public static bool Is60(Bds60 candidate, BdsInferenceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(limits);

        if (candidate.HasIndicatedAirspeed && Exceeds(candidate.IndicatedAirspeed, limits.MaximumIndicatedAirspeedKnots))
        {
            return false;
        }

        if (candidate.HasMach && Exceeds(candidate.Mach, limits.MaximumMach))
        {
            return false;
        }

        if (candidate.HasBarometricAltitudeRate &&
            ExceedsAbsolute(candidate.BarometricAltitudeRate, limits.MaximumAbsoluteVerticalRateFeetPerMinute))
        {
            return false;
        }

        return candidate.HasInertialVerticalVelocity == false ||
               ExceedsAbsolute(
                   candidate.InertialVerticalVelocity,
                   limits.MaximumAbsoluteVerticalRateFeetPerMinute
               ) == false;
    }

    private static bool Exceeds(double value, double? maximum)
    {
        return maximum.HasValue && value > maximum.Value;
    }

    private static bool ExceedsAbsolute(double value, double? maximum)
    {
        return maximum.HasValue && Math.Abs(value) > maximum.Value;
    }
}
