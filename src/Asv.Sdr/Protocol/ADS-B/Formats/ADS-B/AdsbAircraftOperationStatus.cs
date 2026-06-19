using System;
using Asv.IO;

namespace Asv.Sdr;

/// <summary>
/// BDS 6,5 Aircraft Operational Status.
/// </summary>
public class AdsbAircraftOperationStatus : AdsbExtendedSquitterBase
{
    public override ushort Id => (ushort)(base.Id | (ushort)OperationStatusType);
    public override AdsbMessageTypeEnum MessageType => AdsbMessageTypeEnum.AircraftOperationStatus;

    public OperationStatusTypeEnum OperationStatusType { get; set; }
    public ushort CapabilityClass { get; set; }
    public ushort OperationalModeRaw { get; set; }
    public AdsbVersionNumberEnum AdsbVersionNumber { get; set; }
    public string AdsbVersionText { get; set; } = string.Empty;
    public bool NicSupplementA { get; set; }
    public byte NacPCode { get; set; }
    public TransponderHelper.NacPInfo NacP { get; set; } = new();
    public byte BarometricAltitudeQuality { get; set; }
    public TransponderHelper.GvaInfo? GeometricVerticalAccuracy { get; set; }
    public byte SourceIntegrityLevelCode { get; set; }
    public TransponderHelper.SilInfo SourceIntegrityLevel { get; set; } = new();
    public bool BarometricAltitudeIntegrity { get; set; }
    public bool TrackAngleOrHeading { get; set; }
    public bool HorizontalReferenceDirection { get; set; }
    public string HorizontalReferenceDirectionText { get; set; } = string.Empty;
    public bool? SilSupplement { get; set; }
    public bool ReservedBit55 { get; set; }
    public TransponderHelper.OperationalModeStatus OperationalMode { get; set; } = new();
    public TransponderHelper.AirborneCapabilityStatus? AirborneCapability { get; set; }
    public TransponderHelper.SurfaceCapabilityStatus? SurfaceCapability { get; set; }
    public TransponderHelper.AircraftLengthWidthInfo? LengthWidth { get; set; }
    public byte? GpsAntennaOffsetRaw { get; set; }
    public byte ReservedBits48_49 { get; set; }
    public bool Bit52Raw { get; set; }

    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        base.InternalDeserialize(ref buffer);
        var bitIndex = 5;
        OperationStatusType = (OperationStatusTypeEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 3);
        CapabilityClass = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 16);
        OperationalModeRaw = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 16);
        AdsbVersionNumber = (AdsbVersionNumberEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 3);
        NicSupplementA = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        NacPCode = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 4);
        ReservedBits48_49 = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        SourceIntegrityLevelCode = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        Bit52Raw = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        HorizontalReferenceDirection = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        var silSupplement = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        ReservedBit55 = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;

        UpdateCalculatedProperties(silSupplement);
        buffer = buffer[(bitIndex / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var bitIndex = 0;
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 5, (uint)MessageType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 3, (uint)OperationStatusType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 16, CapabilityClass);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 16, OperationalModeRaw);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 3, (uint)AdsbVersionNumber);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, NicSupplementA ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 4, NacPCode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, ReservedBits48_49);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, SourceIntegrityLevelCode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, Bit52Raw ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, HorizontalReferenceDirection ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, SilSupplement == true ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, ReservedBit55 ? 1 : 0);
        buffer = buffer[(bitIndex / 8)..];
    }

    private void UpdateCalculatedProperties(bool silSupplement)
    {
        var version = (int)AdsbVersionNumber;
        AdsbVersionText = TransponderHelper.DecodeAdsbVersion(version);
        NacP = TransponderHelper.DecodeNacP(NacPCode);
        SilSupplement = version == 2 ? silSupplement : null;
        SourceIntegrityLevel = TransponderHelper.DecodeSil(SourceIntegrityLevelCode, SilSupplement);
        HorizontalReferenceDirectionText = HorizontalReferenceDirection ? "Magnetic north" : "True north";
        OperationalMode = TransponderHelper.DecodeOperationalMode(OperationalModeRaw, version);

        AirborneCapability = null;
        SurfaceCapability = null;
        LengthWidth = null;
        GpsAntennaOffsetRaw = null;
        BarometricAltitudeQuality = 0;
        GeometricVerticalAccuracy = null;
        BarometricAltitudeIntegrity = false;
        TrackAngleOrHeading = false;

        if (OperationStatusType == OperationStatusTypeEnum.Airborne)
        {
            AirborneCapability = TransponderHelper.DecodeAirborneCapability(CapabilityClass, version);
            BarometricAltitudeIntegrity = Bit52Raw;
            if (version == 2)
            {
                GeometricVerticalAccuracy = TransponderHelper.DecodeGva(ReservedBits48_49);
            }
            else
            {
                BarometricAltitudeQuality = ReservedBits48_49;
            }
        }
        else if (OperationStatusType == OperationStatusTypeEnum.Surface)
        {
            SurfaceCapability = TransponderHelper.DecodeSurfaceCapability(CapabilityClass);
            LengthWidth = TransponderHelper.DecodeAircraftLengthWidth(CapabilityClass & 0x000F);
            TrackAngleOrHeading = Bit52Raw;
            if (version == 2)
            {
                GpsAntennaOffsetRaw = (byte)(OperationalModeRaw & 0x00FF);
            }
        }
    }
}

public class AdsbAircraftOperationStatusV0 : AdsbAircraftOperationStatus
{
    public AdsbAircraftOperationStatusV0()
    {
        OperationStatusType = OperationStatusTypeEnum.Airborne;
    }
}

public class AdsbAircraftOperationStatusV1 : AdsbAircraftOperationStatus
{
    public AdsbAircraftOperationStatusV1()
    {
        OperationStatusType = OperationStatusTypeEnum.Surface;
    }
}

public class AdsbAircraftOperationStatusV2 : AdsbAircraftOperationStatus
{
    public AdsbAircraftOperationStatusV2()
    {
        OperationStatusType = (OperationStatusTypeEnum)2;
    }
}
