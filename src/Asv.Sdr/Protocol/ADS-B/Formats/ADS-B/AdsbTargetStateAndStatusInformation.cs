using System;
using Asv.IO;

namespace Asv.Sdr;

/// <summary>
/// BDS 6,2 target state and status information (TC 29).
/// </summary>
public class AdsbTargetStateAndStatusInformation : AdsbExtendedSquitterBase
{
    public override AdsbMessageTypeEnum MessageType => AdsbMessageTypeEnum.TargetStateAndStatusInformation;

    public TargetStateStatusSubTypeEnum TargetStateStatusSubType { get; set; }

    // Legacy subtype 0 fields.
    public byte VerticalDataSource { get; set; }
    public AdsbTargetVerticalDataSourceEnum VerticalDataSourceValue
    {
        get => (AdsbTargetVerticalDataSourceEnum)VerticalDataSource;
        set => VerticalDataSource = (byte)value;
    }
    public byte TargetAltitudeType { get; set; }
    public AdsbTargetAltitudeTypeEnum TargetAltitudeTypeValue
    {
        get => (AdsbTargetAltitudeTypeEnum)TargetAltitudeType;
        set => TargetAltitudeType = (byte)value;
    }
    public byte ReservedBit10 { get; set; }
    public byte TargetAltitudeCapability { get; set; }
    public AdsbTargetAltitudeCapabilityEnum TargetAltitudeCapabilityValue
    {
        get => (AdsbTargetAltitudeCapabilityEnum)TargetAltitudeCapability;
        set => TargetAltitudeCapability = (byte)value;
    }
    public byte VerticalModeIndicator { get; set; }
    public AdsbTargetModeIndicatorEnum VerticalMode
    {
        get => (AdsbTargetModeIndicatorEnum)VerticalModeIndicator;
        set => VerticalModeIndicator = (byte)value;
    }
    public ushort TargetAltitudeRaw { get; set; }
    public int? TargetAltitudeFt { get; set; }
    public byte HorizontalDataSource { get; set; }
    public AdsbTargetHorizontalDataSourceEnum HorizontalDataSourceValue
    {
        get => (AdsbTargetHorizontalDataSourceEnum)HorizontalDataSource;
        set => HorizontalDataSource = (byte)value;
    }
    public ushort TargetHeadingRaw { get; set; }
    public double? TargetHeadingDeg { get; set; }
    public byte ReservedBit36 { get; set; }
    public byte HorizontalModeIndicator { get; set; }
    public AdsbTargetModeIndicatorEnum HorizontalMode
    {
        get => (AdsbTargetModeIndicatorEnum)HorizontalModeIndicator;
        set => HorizontalModeIndicator = (byte)value;
    }
    public byte LegacyNacPCode { get; set; }
    public TransponderHelper.NacPInfo LegacyNacP { get; set; } = new();
    public bool LegacyNicBaro { get; set; }
    public byte LegacySilCode { get; set; }
    public TransponderHelper.SilInfo LegacySil { get; set; } = new();
    public byte LegacyReservedBits46To50 { get; set; }
    public byte CapabilityModeCode { get; set; }
    public bool LegacyTcasOperational => (CapabilityModeCode & 0x02) != 0;
    public bool LegacyResolutionAdvisoryActive => (CapabilityModeCode & 0x01) != 0;
    public byte EmergencyPriorityCode { get; set; }
    public AdsbEmergencyStateEnum EmergencyPriority
    {
        get => (AdsbEmergencyStateEnum)EmergencyPriorityCode;
        set => EmergencyPriorityCode = (byte)value;
    }

    // Target state and mode status subtype 1 fields.
    public bool SilSupplement { get; set; }
    public bool SelectedAltitudeSourceIsFms { get; set; }
    public AdsbSelectedAltitudeSourceEnum SelectedAltitudeSource
    {
        get => SelectedAltitudeSourceIsFms
            ? AdsbSelectedAltitudeSourceEnum.Fms
            : AdsbSelectedAltitudeSourceEnum.McpOrFcu;
        set => SelectedAltitudeSourceIsFms = value == AdsbSelectedAltitudeSourceEnum.Fms;
    }
    public ushort SelectedAltitudeRaw { get; set; }
    public int? SelectedAltitudeFt { get; set; }
    public ushort BarometricPressureSettingRaw { get; set; }
    public double? BarometricPressureSettingMbar { get; set; }
    public bool SelectedHeadingStatus { get; set; }
    public bool SelectedHeadingIsNegative { get; set; }
    public byte SelectedHeadingMagnitudeRaw { get; set; }
    public ushort SelectedHeadingRaw
    {
        get => (ushort)((SelectedHeadingIsNegative ? 0x100 : 0) | SelectedHeadingMagnitudeRaw);
        set
        {
            if (value > 0x1FF)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "The selected heading field must fit in nine bits.");
            }

            SelectedHeadingIsNegative = (value & 0x100) != 0;
            SelectedHeadingMagnitudeRaw = (byte)value;
        }
    }
    public double? SelectedHeadingDeg { get; set; }
    public byte NacPCode { get; set; }
    public TransponderHelper.NacPInfo NacP { get; set; } = new();
    public bool NicBaro { get; set; }
    public byte SilCode { get; set; }
    public TransponderHelper.SilInfo Sil { get; set; } = new();
    public bool ModeStatus { get; set; }
    public bool? AutopilotEngaged { get; set; }
    public bool? VnavMode { get; set; }
    public bool? AltitudeHoldMode { get; set; }
    public bool ImfOrAdsrReservedFlag { get; set; }
    public bool? ApproachMode { get; set; }
    public bool TcasOperational { get; set; }
    public bool? LnavMode { get; set; }
    public byte Reserved54_55 { get; set; }

    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        base.InternalDeserialize(ref buffer);
        var bitIndex = 5;
        TargetStateStatusSubType = (TargetStateStatusSubTypeEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);

        switch (TargetStateStatusSubType)
        {
            case TargetStateStatusSubTypeEnum.Legacy:
                ReadLegacy(buffer, ref bitIndex);
                break;
            case TargetStateStatusSubTypeEnum.TargetStateAndModeStatus:
                ReadTargetStateAndModeStatus(buffer, ref bitIndex);
                break;
            default:
                throw new NotSupportedException(
                    $"ADS-B target state subtype {(byte)TargetStateStatusSubType} is reserved.");
        }

        buffer = buffer[(bitIndex / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var bitIndex = 0;
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 5, (uint)MessageType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, (uint)TargetStateStatusSubType);

        switch (TargetStateStatusSubType)
        {
            case TargetStateStatusSubTypeEnum.Legacy:
                WriteLegacy(buffer, ref bitIndex);
                break;
            case TargetStateStatusSubTypeEnum.TargetStateAndModeStatus:
                WriteTargetStateAndModeStatus(buffer, ref bitIndex);
                break;
            default:
                throw new NotSupportedException(
                    $"ADS-B target state subtype {(byte)TargetStateStatusSubType} is reserved.");
        }

        buffer = buffer[(bitIndex / 8)..];
    }

    private void ReadLegacy(ReadOnlySpan<byte> buffer, ref int bitIndex)
    {
        VerticalDataSource = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        TargetAltitudeType = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 1);
        ReservedBit10 = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 1);
        TargetAltitudeCapability = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        VerticalModeIndicator = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        TargetAltitudeRaw = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 10);
        HorizontalDataSource = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        TargetHeadingRaw = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 9);
        ReservedBit36 = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 1);
        HorizontalModeIndicator = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        LegacyNacPCode = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 4);
        LegacyNicBaro = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        LegacySilCode = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        LegacyReservedBits46To50 = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 5);
        CapabilityModeCode = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        EmergencyPriorityCode = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 3);

        TargetAltitudeFt = TargetAltitudeRaw <= 1010
            ? TargetAltitudeRaw * 100 - 1000
            : null;
        TargetHeadingDeg = TargetHeadingRaw <= 359 ? TargetHeadingRaw : null;
        LegacyNacP = TransponderHelper.DecodeNacP(LegacyNacPCode);
        LegacySil = TransponderHelper.DecodeSil(LegacySilCode, null);
    }

    private void WriteLegacy(Span<byte> buffer, ref int bitIndex)
    {
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, VerticalDataSource);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, TargetAltitudeType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, ReservedBit10);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, TargetAltitudeCapability);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, VerticalModeIndicator);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 10, TargetAltitudeRaw);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, HorizontalDataSource);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 9, TargetHeadingRaw);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, ReservedBit36);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, HorizontalModeIndicator);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 4, LegacyNacPCode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, LegacyNicBaro ? 1U : 0U);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, LegacySilCode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 5, LegacyReservedBits46To50);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, CapabilityModeCode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 3, EmergencyPriorityCode);
    }

    private void ReadTargetStateAndModeStatus(ReadOnlySpan<byte> buffer, ref int bitIndex)
    {
        SilSupplement = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        SelectedAltitudeSourceIsFms = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        SelectedAltitudeRaw = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 11);
        BarometricPressureSettingRaw = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 9);
        SelectedHeadingStatus = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        SelectedHeadingIsNegative = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        SelectedHeadingMagnitudeRaw = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 8);
        NacPCode = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 4);
        NicBaro = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        SilCode = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        ModeStatus = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        AutopilotEngaged = ReadModeFlag(buffer, ref bitIndex);
        VnavMode = ReadModeFlag(buffer, ref bitIndex);
        AltitudeHoldMode = ReadModeFlag(buffer, ref bitIndex);
        ImfOrAdsrReservedFlag = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        ApproachMode = ReadModeFlag(buffer, ref bitIndex);
        TcasOperational = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        LnavMode = ReadModeFlag(buffer, ref bitIndex);
        Reserved54_55 = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);

        SelectedAltitudeFt = SelectedAltitudeRaw == 0 ? null : (SelectedAltitudeRaw - 1) * 32;
        BarometricPressureSettingMbar = BarometricPressureSettingRaw == 0
            ? null
            : 800.0 + (BarometricPressureSettingRaw - 1) * 0.8;
        var heading = SelectedHeadingMagnitudeRaw * 180.0 / 256.0;
        SelectedHeadingDeg = SelectedHeadingStatus
            ? SelectedHeadingIsNegative ? -heading : heading
            : null;
        NacP = TransponderHelper.DecodeNacP(NacPCode);
        Sil = TransponderHelper.DecodeSil(SilCode, SilSupplement);
    }

    private void WriteTargetStateAndModeStatus(Span<byte> buffer, ref int bitIndex)
    {
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, SilSupplement ? 1U : 0U);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, SelectedAltitudeSourceIsFms ? 1U : 0U);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 11, SelectedAltitudeRaw);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 9, BarometricPressureSettingRaw);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, SelectedHeadingStatus ? 1U : 0U);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, SelectedHeadingIsNegative ? 1U : 0U);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 8, SelectedHeadingMagnitudeRaw);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 4, NacPCode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, NicBaro ? 1U : 0U);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, SilCode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, ModeStatus ? 1U : 0U);
        WriteModeFlag(buffer, ref bitIndex, AutopilotEngaged);
        WriteModeFlag(buffer, ref bitIndex, VnavMode);
        WriteModeFlag(buffer, ref bitIndex, AltitudeHoldMode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, ImfOrAdsrReservedFlag ? 1U : 0U);
        WriteModeFlag(buffer, ref bitIndex, ApproachMode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, TcasOperational ? 1U : 0U);
        WriteModeFlag(buffer, ref bitIndex, LnavMode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, Reserved54_55);
    }

    private bool? ReadModeFlag(ReadOnlySpan<byte> buffer, ref int bitIndex)
    {
        var value = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        return ModeStatus ? value : null;
    }

    private static void WriteModeFlag(Span<byte> buffer, ref int bitIndex, bool? value)
    {
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, value == true ? 1U : 0U);
    }
}
