using System;
using System.Collections.Generic;

namespace Asv.Sdr;

internal static class BdsCandidateSelector
{
    private const double Bds60AirspeedToleranceKnots = 20.0;

    public static BdsBase SelectByAltitude(IReadOnlyList<BdsBase> candidates, double altitudeMeters)
    {
        if (candidates.Count != 2 || double.IsFinite(altitudeMeters) == false)
        {
            return candidates[0];
        }

        var bds50 = candidates[0] as Bds50 ?? candidates[1] as Bds50;
        var bds60 = candidates[0] as Bds60 ?? candidates[1] as Bds60;
        if (bds50 == null || bds60 is not { HasIndicatedAirspeed: true, HasMach: true })
        {
            return candidates[0];
        }

        var calibratedAirspeedKnots = CalculateCalibratedAirspeedKnots(bds60.Mach, altitudeMeters);
        if (double.IsFinite(calibratedAirspeedKnots) == false)
        {
            return candidates[0];
        }

        return Math.Abs(bds60.IndicatedAirspeed - calibratedAirspeedKnots) <= Bds60AirspeedToleranceKnots
            ? bds60
            : bds50;
    }

    private static double CalculateCalibratedAirspeedKnots(double mach, double altitudeMeters)
    {
        const double seaLevelPressurePascals = 101325.0;
        const double seaLevelTemperatureKelvin = 288.15;
        const double tropopauseAltitudeMeters = 11000.0;
        const double tropopauseTemperatureKelvin = 216.65;
        const double temperatureLapseRateKelvinPerMeter = 0.0065;
        const double gravitationalAcceleration = 9.80665;
        const double specificGasConstant = 287.05287;
        const double heatCapacityRatio = 1.4;
        const double metersPerSecondPerKnot = 0.514444;

        if (double.IsFinite(mach) == false || mach < 0.0 || altitudeMeters > 20000.0)
        {
            return double.NaN;
        }

        var pressureExponent = gravitationalAcceleration /
                               (specificGasConstant * temperatureLapseRateKelvinPerMeter);
        var tropopausePressurePascals = seaLevelPressurePascals * Math.Pow(
            1.0 - temperatureLapseRateKelvinPerMeter * tropopauseAltitudeMeters /
            seaLevelTemperatureKelvin,
            pressureExponent
        );

        double staticPressurePascals;
        if (altitudeMeters <= tropopauseAltitudeMeters)
        {
            var temperatureRatio = 1.0 - temperatureLapseRateKelvinPerMeter * altitudeMeters /
                seaLevelTemperatureKelvin;
            if (temperatureRatio <= 0.0)
            {
                return double.NaN;
            }

            staticPressurePascals = seaLevelPressurePascals * Math.Pow(temperatureRatio, pressureExponent);
        }
        else
        {
            staticPressurePascals = tropopausePressurePascals * Math.Exp(
                -gravitationalAcceleration * (altitudeMeters - tropopauseAltitudeMeters) /
                (specificGasConstant * tropopauseTemperatureKelvin)
            );
        }

        var impactPressurePascals = staticPressurePascals *
                                    (Math.Pow(
                                         1.0 + (heatCapacityRatio - 1.0) / 2.0 * mach * mach,
                                         heatCapacityRatio / (heatCapacityRatio - 1.0)
                                     ) - 1.0);
        var seaLevelSpeedOfSoundMetersPerSecond = Math.Sqrt(
            heatCapacityRatio * specificGasConstant * seaLevelTemperatureKelvin
        );
        var calibratedAirspeedMetersPerSecond = seaLevelSpeedOfSoundMetersPerSecond * Math.Sqrt(
            2.0 / (heatCapacityRatio - 1.0) *
            (Math.Pow(
                 1.0 + impactPressurePascals / seaLevelPressurePascals,
                 (heatCapacityRatio - 1.0) / heatCapacityRatio
             ) - 1.0)
        );

        return calibratedAirspeedMetersPerSecond / metersPerSecondPerKnot;
    }
}
