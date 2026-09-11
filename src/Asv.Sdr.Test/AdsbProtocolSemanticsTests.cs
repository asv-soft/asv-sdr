using Xunit;

namespace Asv.Sdr.Test
{
public class AdsbProtocolSemanticsTests
{
    [Theory]
    [InlineData(0, 4, 2, AdsbAircraftCategoryValueEnum.Medium1)]
    [InlineData(1, 4, 2, AdsbAircraftCategoryValueEnum.Small)]
    [InlineData(0, 4, 3, AdsbAircraftCategoryValueEnum.Medium2)]
    [InlineData(2, 4, 3, AdsbAircraftCategoryValueEnum.Large)]
    [InlineData(0, 4, 6, AdsbAircraftCategoryValueEnum.HighPerformanceAircraft)]
    [InlineData(2, 4, 6, AdsbAircraftCategoryValueEnum.HighPerformanceAndHighSpeedAircraft)]
    public void DecodeAircraftCategory_SetA_UsesVersionSpecificMeaning(
        int version,
        byte typeCode,
        byte categoryCode,
        AdsbAircraftCategoryValueEnum expected
    )
    {
        var result = TransponderHelper.DecodeAircraftCategory(
            (AdsbVersionNumberEnum)version,
            typeCode,
            categoryCode
        );

        Assert.Equal(AdsbAircraftCategorySetEnum.SetA, result.CategorySet);
        Assert.Equal(expected, result.Category);
        Assert.Equal(AdsbAircraftCategoryValidityEnum.Valid, result.Validity);
    }

    [Theory]
    [InlineData(0, 2, 3, AdsbAircraftCategoryValueEnum.FixedOrTetheredObstruction)]
    [InlineData(1, 2, 3, AdsbAircraftCategoryValueEnum.FixedOrTetheredObstruction)]
    [InlineData(2, 2, 3, AdsbAircraftCategoryValueEnum.PointOrTetheredObstruction)]
    [InlineData(0, 2, 4, AdsbAircraftCategoryValueEnum.Reserved)]
    [InlineData(1, 2, 4, AdsbAircraftCategoryValueEnum.ClusterObstacle)]
    [InlineData(2, 2, 5, AdsbAircraftCategoryValueEnum.LineObstacle)]
    public void DecodeAircraftCategory_SetC_UsesVersionSpecificMeaning(
        int version,
        byte typeCode,
        byte categoryCode,
        AdsbAircraftCategoryValueEnum expected
    )
    {
        var result = TransponderHelper.DecodeAircraftCategory(
            (AdsbVersionNumberEnum)version,
            typeCode,
            categoryCode
        );

        Assert.Equal(AdsbAircraftCategorySetEnum.SetC, result.CategorySet);
        Assert.Equal(expected, result.Category);
        Assert.Equal(
            expected == AdsbAircraftCategoryValueEnum.Reserved
                ? AdsbAircraftCategoryValidityEnum.Reserved
                : AdsbAircraftCategoryValidityEnum.Valid,
            result.Validity
        );
    }

    [Fact]
    public void DecodeAircraftCategory_SetD_DistinguishesVersion2NoInformation()
    {
        var version1 = TransponderHelper.DecodeAircraftCategory(
            AdsbVersionNumberEnum.AppendixB,
            1,
            0
        );
        var version2 = TransponderHelper.DecodeAircraftCategory(
            AdsbVersionNumberEnum.AppendixC,
            1,
            0
        );

        Assert.Equal(AdsbAircraftCategoryValueEnum.Reserved, version1.Category);
        Assert.Equal(AdsbAircraftCategoryValidityEnum.Reserved, version1.Validity);
        Assert.Equal(AdsbAircraftCategoryValueEnum.NoCategoryInformation, version2.Category);
        Assert.Equal(AdsbAircraftCategoryValidityEnum.NoInformation, version2.Validity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void DecodeAircraftCategory_SetB_HasSameMeaningInAllSupportedVersions(
        int version
    )
    {
        var result = TransponderHelper.DecodeAircraftCategory(
            (AdsbVersionNumberEnum)version,
            3,
            6
        );

        Assert.Equal(AdsbAircraftCategorySetEnum.SetB, result.CategorySet);
        Assert.Equal(AdsbAircraftCategoryValueEnum.UnmannedAerialVehicle, result.Category);
        Assert.Equal(AdsbAircraftCategoryValidityEnum.Valid, result.Validity);
    }

    [Fact]
    public void DecodeAircraftCategory_UnsupportedVersion_ReturnsExplicitValidity()
    {
        var result = TransponderHelper.DecodeAircraftCategory(
            (AdsbVersionNumberEnum)3,
            4,
            1
        );

        Assert.Equal(AdsbAircraftCategorySetEnum.SetA, result.CategorySet);
        Assert.Equal(AdsbAircraftCategoryValidityEnum.UnsupportedVersion, result.Validity);
        Assert.Equal(AdsbAircraftCategoryValueEnum.Unknown, result.Category);
    }

    [Fact]
    public void AircraftIdentification_GetCategory_UsesDecodedRawFields()
    {
        var message = new AdsbAircraftIdentification
        {
            AircraftCategory = AircraftCategoryEnum.Light
        };

        var result = message.GetCategory(AdsbVersionNumberEnum.AppendixC);

        Assert.Equal(AdsbAircraftCategorySetEnum.SetA, result.CategorySet);
        Assert.Equal(AdsbAircraftCategoryValueEnum.Light, result.Category);
    }

    [Fact]
    public void DecodeAirbornePositionQuality_Version0_ReturnsTypedNucpAndHpl()
    {
        var result = TransponderHelper.DecodeAirbornePositionQuality(
            AdsbVersionNumberEnum.AppendixA,
            11,
            null,
            null
        );

        Assert.Equal(AdsbPositionQualityMetricEnum.Nucp, result.Metric);
        Assert.Equal(7, result.Code);
        Assert.Equal(AdsbPositionQualityValidityEnum.Valid, result.Validity);
        Assert.Equal(AdsbContainmentBoundKindEnum.HorizontalProtectionLimit, result.BoundKind);
        Assert.Equal(AdsbContainmentBoundComparisonEnum.LessThan, result.BoundComparison);
        Assert.Equal(0.1, result.Bound);
        Assert.Equal(AdsbDistanceUnitEnum.NauticalMiles, result.BoundUnit);
    }

    [Theory]
    [InlineData(11, true, true, 9, 75, AdsbDistanceUnitEnum.Meters)]
    [InlineData(13, false, true, 6, 0.3, AdsbDistanceUnitEnum.NauticalMiles)]
    [InlineData(16, false, false, 2, 8, AdsbDistanceUnitEnum.NauticalMiles)]
    public void DecodeAirbornePositionQuality_Version2_ReturnsTypedNicAndRc(
        byte typeCode,
        bool nicA,
        bool nicB,
        int code,
        double bound,
        AdsbDistanceUnitEnum unit
    )
    {
        var result = TransponderHelper.DecodeAirbornePositionQuality(
            AdsbVersionNumberEnum.AppendixC,
            typeCode,
            nicA,
            nicB
        );

        Assert.Equal(AdsbPositionQualityMetricEnum.Nic, result.Metric);
        Assert.Equal(code, result.Code);
        Assert.Equal(AdsbPositionQualityValidityEnum.Valid, result.Validity);
        Assert.Equal(AdsbContainmentBoundKindEnum.RadiusOfContainment, result.BoundKind);
        Assert.Equal(bound, result.Bound);
        Assert.Equal(unit, result.BoundUnit);
    }

    [Fact]
    public void DecodeSurfacePositionQuality_Version0Type8_ReturnsTypedLowerBound()
    {
        var result = TransponderHelper.DecodeSurfacePositionQuality(
            AdsbVersionNumberEnum.AppendixA,
            8,
            null,
            null
        );

        Assert.Equal(6, result.Code);
        Assert.Equal(AdsbContainmentBoundComparisonEnum.GreaterThanOrEqual, result.BoundComparison);
        Assert.Equal(0.1, result.Bound);
        Assert.Equal(AdsbDistanceUnitEnum.NauticalMiles, result.BoundUnit);
    }

    [Fact]
    public void DecodePositionQuality_MissingSupplement_ReturnsExplicitReason()
    {
        var result = TransponderHelper.DecodeAirbornePositionQuality(
            AdsbVersionNumberEnum.AppendixC,
            11,
            true,
            null
        );

        Assert.Null(result.Code);
        Assert.Equal(AdsbPositionQualityValidityEnum.NotAvailable, result.Validity);
        Assert.Equal(AdsbPositionQualityReasonEnum.NicSupplementBUnavailable, result.Reason);
    }

    [Fact]
    public void DecodePositionQuality_InvalidSupplementCombination_ReturnsExplicitReason()
    {
        var result = TransponderHelper.DecodeSurfacePositionQuality(
            AdsbVersionNumberEnum.AppendixC,
            5,
            true,
            false
        );

        Assert.Null(result.Code);
        Assert.Equal(AdsbPositionQualityValidityEnum.Invalid, result.Validity);
        Assert.Equal(
            AdsbPositionQualityReasonEnum.InvalidNicSupplementCombination,
            result.Reason
        );
    }

    [Fact]
    public void DecodePositionQuality_CodeZero_ReturnsReportedUnknown()
    {
        var result = TransponderHelper.DecodeSurfacePositionQuality(
            AdsbVersionNumberEnum.AppendixC,
            8,
            false,
            false
        );

        Assert.Equal(0, result.Code);
        Assert.Equal(AdsbPositionQualityValidityEnum.Unknown, result.Validity);
        Assert.Equal(AdsbPositionQualityReasonEnum.ReportedUnknown, result.Reason);
        Assert.Null(result.Bound);
    }

    [Fact]
    public void DecodePositionQuality_NonPositionTypeCode_ReturnsNotApplicable()
    {
        var result = TransponderHelper.DecodeAirbornePositionQuality(
            AdsbVersionNumberEnum.AppendixC,
            19,
            false,
            false
        );

        Assert.Null(result.Code);
        Assert.Equal(AdsbPositionQualityValidityEnum.NotApplicable, result.Validity);
        Assert.Equal(AdsbPositionQualityReasonEnum.TypeCodeNotApplicable, result.Reason);
    }

    [Fact]
    public void PositionMessages_GetPositionQuality_UseMessageTypeAndSupplementBits()
    {
        var airborne = new AdsbAirbornePositionWithBaroAlt
        {
            AirbornePositionType = AirbornePositionBaroAltTypeCode.BasicPositionHighUpdateRate,
            NicSupplementB = true
        };
        var surface = new AdsbSurfacePosition
        {
            SurfacePositionType = SurfacePositionTypeCodes.GroundStatusAndMode
        };

        var airborneQuality = airborne.GetPositionQuality(
            AdsbVersionNumberEnum.AppendixC,
            nicSupplementA: true
        );
        var surfaceQuality = surface.GetPositionQuality(
            AdsbVersionNumberEnum.AppendixC,
            nicSupplementA: true,
            nicSupplementC: true
        );

        Assert.Equal(9, airborneQuality.Code);
        Assert.Equal(7, surfaceQuality.Code);
    }

    [Fact]
    public void TypedStatusProperties_PreserveRawFields()
    {
        var emergency = new AdsbAircraftEmergencyStatus
        {
            EmergencyStateValue = AdsbEmergencyStateEnum.MinimumFuel
        };
        var target = new AdsbTargetStateAndStatusInformation
        {
            VerticalDataSourceValue = AdsbTargetVerticalDataSourceEnum.FmsOrNavigation,
            TargetAltitudeTypeValue = AdsbTargetAltitudeTypeEnum.MeanSeaLevel,
            TargetAltitudeCapabilityValue =
                AdsbTargetAltitudeCapabilityEnum.HoldingAltitudeAutopilotControlPanelAndFms,
            VerticalMode = AdsbTargetModeIndicatorEnum.Acquiring,
            HorizontalDataSourceValue = AdsbTargetHorizontalDataSourceEnum.HoldingHeadingOrTrack,
            HorizontalMode = AdsbTargetModeIndicatorEnum.Maintaining,
            EmergencyPriority = AdsbEmergencyStateEnum.NoCommunications,
            SelectedAltitudeSource = AdsbSelectedAltitudeSourceEnum.Fms
        };
        var acas = new TransponderHelper.AcasRaInfo
        {
            ThreatType = ThreatTypeIndicatorEnum.ModeSTransponderAddress
        };

        Assert.Equal(3, emergency.EmergencyState);
        Assert.Equal(3, target.VerticalDataSource);
        Assert.Equal(1, target.TargetAltitudeType);
        Assert.Equal(2, target.TargetAltitudeCapability);
        Assert.Equal(1, target.VerticalModeIndicator);
        Assert.Equal(2, target.HorizontalDataSource);
        Assert.Equal(2, target.HorizontalModeIndicator);
        Assert.Equal(4, target.EmergencyPriorityCode);
        Assert.True(target.SelectedAltitudeSourceIsFms);
        Assert.Equal(1, acas.ThreatTypeIndicator);
    }

    [Fact]
    public void LegacyAircraftCategorySerialization_UsesCorrectSetCCodes()
    {
        TransponderHelper.SetAircraftCategory(
            AircraftCategoryEnum.SurfaceServiceVehicle,
            out var serviceTypeCode,
            out var serviceCategoryCode
        );
        TransponderHelper.SetAircraftCategory(
            AircraftCategoryEnum.GroundObstruction,
            out var obstructionTypeCode,
            out var obstructionCategoryCode
        );

        Assert.Equal(2, serviceTypeCode);
        Assert.Equal(2, serviceCategoryCode);
        Assert.Equal(2, obstructionTypeCode);
        Assert.Equal(3, obstructionCategoryCode);
    }
}
}
