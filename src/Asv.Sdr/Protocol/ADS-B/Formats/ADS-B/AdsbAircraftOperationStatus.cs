using System;
using Asv.IO;

namespace Asv.Sdr;

public class AdsbAircraftOperationStatus : AdsbExtendedSquitterBase
{
    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        base.InternalDeserialize(ref buffer);
        var bitIndex = 5;
        OperationStatusType = (OperationStatusTypeEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 3);
        buffer = buffer[(bitIndex / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var bitIndex = 0;
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 5, (uint)MessageType);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 3, (uint)OperationStatusType);
        buffer = buffer[(bitIndex / 8)..];
    }

    public override AdsbMessageTypeEnum MessageType => AdsbMessageTypeEnum.AircraftOperationStatus;

    public OperationStatusTypeEnum OperationStatusType { get; set; }
}

public class AdsbAircraftOperationStatusV0 : AdsbAircraftOperationStatus
{
    public override ushort Id => (ushort)(base.Id | (ushort)AdsbVersionNumberEnum.AppendixA);
    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        var bitIndex = 0;
        base.InternalDeserialize(ref buffer);
        EnrouteOperationalCapabilities = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 4);
        TerminalAreaOperationalCapabilities = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 4);
        ApproachLandingOperationalCapabilities = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 4);
        SurfaceOperationalCapabilities = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 4);
        EnrouteOperationalStatus = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 4);
        TerminalAreaOperationalStatus = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 4);
        ApproachLandingOperationalStatus = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 4);
        SurfaceOperationalStatus = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 4);
        bitIndex += 16; // Reseved
        buffer = buffer[(bitIndex / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var bitIndex = 0;
        base.InternalSerialize(ref buffer);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 4, EnrouteOperationalCapabilities);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 4, TerminalAreaOperationalCapabilities);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 4, ApproachLandingOperationalCapabilities);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 4, SurfaceOperationalCapabilities);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 4, EnrouteOperationalStatus);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 4, TerminalAreaOperationalStatus);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 4, ApproachLandingOperationalStatus);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 4, SurfaceOperationalStatus);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 16, 0); // Reserved
        buffer = buffer[(bitIndex / 8)..];
    }
    
    public byte EnrouteOperationalCapabilities { get; set; }
    public byte TerminalAreaOperationalCapabilities { get; set; }
    public byte ApproachLandingOperationalCapabilities { get; set; }
    public byte SurfaceOperationalCapabilities { get; set; }
    public byte EnrouteOperationalStatus { get; set; }
    public byte TerminalAreaOperationalStatus { get; set; }
    public byte ApproachLandingOperationalStatus { get; set; }
    public byte SurfaceOperationalStatus { get; set; }
}

public class AdsbAircraftOperationStatusV1 : AdsbAircraftOperationStatus
{
    public override ushort Id => (ushort)(base.Id | (ushort)AdsbVersionNumberEnum.AppendixB);
    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        base.InternalDeserialize(ref buffer);
        var bitIndex = 0;
        
        CapacityClass = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 16);
        OperationalMode = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 16);
        AdsbVersionNumber = (AdsbVersionNumberEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 3);
        NICs = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 1;
        NACp = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 4);
        BarometricAltitudeQuality = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        SurveillanceIntegrityLevel = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        BarometricAltitudeIntegrity = TrackAngleOrHeading = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 1;
        HorizontalReferenceDirection = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 1);
        bitIndex += 2;
        buffer = buffer[(bitIndex / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        base.InternalSerialize(ref buffer);
        var bitIndex = 0;
        
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 16, CapacityClass);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 16, OperationalMode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 3, (uint)AdsbVersionNumber);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, NICs ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 4, NACp);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2,
            OperationStatusType == OperationStatusTypeEnum.Airborne ? BarometricAltitudeQuality : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, SurveillanceIntegrityLevel);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1,
            OperationStatusType == OperationStatusTypeEnum.Airborne
                ? BarometricAltitudeIntegrity ? 1 : 0
                : TrackAngleOrHeading ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, HorizontalReferenceDirection);
        bitIndex += 2;
        
        buffer = buffer[(bitIndex / 8)..];
    }
    
    public ushort CapacityClass { get; set; }
    public ushort OperationalMode { get; set; }
    public AdsbVersionNumberEnum AdsbVersionNumber { get; set; }
    public bool NICs { get; set; }
    public byte NACp { get; set; }
    public byte BarometricAltitudeQuality { get; set; }
    public byte SurveillanceIntegrityLevel { get; set; }
    public bool BarometricAltitudeIntegrity { get; set; }
    public bool TrackAngleOrHeading { get; set; }
    public byte HorizontalReferenceDirection { get; set; }
}

public class AdsbAircraftOperationStatusV2 : AdsbAircraftOperationStatus
{
    public override ushort Id => (ushort)(base.Id | (ushort)AdsbVersionNumberEnum.AppendixC);
    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        base.InternalDeserialize(ref buffer);
        var bitIndex = 0;
        CapacityClass = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 16);
        OperationalMode = (ushort)SpanBitHelper.GetBitU(buffer, ref bitIndex, 16);
        AdsbVersionNumber = (AdsbVersionNumberEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 3);
        NICs = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 1;
        NACp = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 4);
        GeometricVerticalAccuracy = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        SourceIntegrityLevel = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 2);
        BarometricAltitudeIntegrity = TrackAngleOrHeading = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 1;
        HorizontalReferenceDirection = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 1);
        SilSupplement = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 1;
        bitIndex += 1;
        buffer = buffer[(bitIndex / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        base.InternalSerialize(ref buffer);
        var bitIndex = 0;
        
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 16, CapacityClass);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 16, OperationalMode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 3, (uint)AdsbVersionNumber);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, NICs ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 4, NACp);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2,
            OperationStatusType == OperationStatusTypeEnum.Airborne ? GeometricVerticalAccuracy : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 2, SourceIntegrityLevel);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1,
            OperationStatusType == OperationStatusTypeEnum.Airborne
                ? BarometricAltitudeIntegrity ? 1 : 0
                : TrackAngleOrHeading ? 1 : 0);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, HorizontalReferenceDirection);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, SilSupplement ? 1 : 0);
        bitIndex += 1;
        
        buffer = buffer[(bitIndex / 8)..];
    }
    
    public ushort CapacityClass { get; set; }
    public ushort OperationalMode { get; set; }
    public AdsbVersionNumberEnum AdsbVersionNumber { get; set; }
    public bool NICs { get; set; }
    public byte NACp { get; set; }
    public byte GeometricVerticalAccuracy  { get; set; }
    public byte SourceIntegrityLevel { get; set; }
    public bool BarometricAltitudeIntegrity { get; set; }
    public bool TrackAngleOrHeading         { get; set; }
    public byte HorizontalReferenceDirection { get; set; }
    public bool SilSupplement { get; set; }
}

