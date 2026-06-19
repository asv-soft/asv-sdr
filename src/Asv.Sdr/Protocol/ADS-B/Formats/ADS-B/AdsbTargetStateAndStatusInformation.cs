using System;
using Asv.IO;

namespace Asv.Sdr;

public class AdsbTargetStateAndStatusInformation : AdsbExtendedSquitterBase
{
    public override AdsbMessageTypeEnum MessageType => AdsbMessageTypeEnum.TargetStateAndStatusInformation;

    public TargetStateStatusSubTypeEnum TargetStateStatusSubType { get; set; }
    public bool SilSupplement { get; set; }
    public bool SelectedAltitudeSourceIsFms { get; set; }
    public ushort SelectedAltitudeRaw { get; set; }
    public int? SelectedAltitudeFt { get; set; }
    public ushort BarometricPressureSettingRaw { get; set; }
    public double? BarometricPressureSettingMbar { get; set; }
    public bool SelectedHeadingStatus { get; set; }
    public ushort SelectedHeadingRaw { get; set; }
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
        SilSupplement = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        SelectedAltitudeSourceIsFms = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        SelectedAltitudeRaw = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 11);
        BarometricPressureSettingRaw = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 9);
        SelectedHeadingStatus = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        SelectedHeadingRaw = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 9);
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

        UpdateCalculatedProperties();
        buffer = buffer[(bitIndex / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var bitIndex = 0;
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 5, (uint)MessageType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, (uint)TargetStateStatusSubType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, SilSupplement ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, SelectedAltitudeSourceIsFms ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 11, SelectedAltitudeRaw);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 9, BarometricPressureSettingRaw);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, SelectedHeadingStatus ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 9, SelectedHeadingRaw);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 4, NacPCode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, NicBaro ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, SilCode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, ModeStatus ? 1 : 0);
        WriteModeFlag(buffer, ref bitIndex, AutopilotEngaged);
        WriteModeFlag(buffer, ref bitIndex, VnavMode);
        WriteModeFlag(buffer, ref bitIndex, AltitudeHoldMode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, ImfOrAdsrReservedFlag ? 1 : 0);
        WriteModeFlag(buffer, ref bitIndex, ApproachMode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, TcasOperational ? 1 : 0);
        WriteModeFlag(buffer, ref bitIndex, LnavMode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, Reserved54_55);
        buffer = buffer[(bitIndex / 8)..];
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

    private void UpdateCalculatedProperties()
    {
        SelectedAltitudeFt = SelectedAltitudeRaw == 0 ? null : (SelectedAltitudeRaw - 1) * 32;
        BarometricPressureSettingMbar = BarometricPressureSettingRaw == 0
            ? null
            : 800.0 + (BarometricPressureSettingRaw - 1) * 0.8;
        SelectedHeadingDeg = SelectedHeadingStatus ? SelectedHeadingRaw * 360.0 / 512.0 : null;
        NacP = TransponderHelper.DecodeNacP(NacPCode);
        Sil = TransponderHelper.DecodeSil(SilCode, SilSupplement);
    }
}
