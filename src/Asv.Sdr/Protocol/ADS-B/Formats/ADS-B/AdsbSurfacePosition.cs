using System;
using Asv.IO;

namespace Asv.Sdr;

public class AdsbSurfacePosition : AdsbExtendedSquitterBase
{
    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        base.InternalDeserialize(ref buffer);
        var bitIndex = 0;
        SurfacePositionType = (SurfacePositionTypeCodes)SpanBitHelper.GetBitU(buffer, ref bitIndex, 5);
        MovementCode = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 7);
        Movement = GetMovement(MovementCode);
        GroundTrackStatus = (GroundTrackStatusEnum)SpanBitHelper.GetBitU(buffer, ref bitIndex, 1);
        GroundTrackRaw = (byte)SpanBitHelper.GetBitU(buffer, ref bitIndex, 7);
        GroundTrack = GetGroundTrack(GroundTrackRaw);
        TimeSynchronizedWithUtc = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) != 0;
        CprFormat = SpanBitHelper.GetBitU(buffer, ref bitIndex, 1) == 0 ? CprFormatEnum.Even : CprFormatEnum.Odd;
        NCprLat = SpanBitHelper.GetBitU(buffer, ref bitIndex, 17);
        NCprLon = SpanBitHelper.GetBitU(buffer, ref bitIndex, 17);
        buffer = buffer[(bitIndex / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var bitIndex = 0;
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 5, (uint)SurfacePositionType);
        var movementCode = MovementCode is >= 125 and <= 127 ? MovementCode : SetMovement(Movement);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 7, movementCode);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, (uint)GroundTrackStatus);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 7, SetGroundTrack(GroundTrack));
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, TimeSynchronizedWithUtc ? 1U : 0U);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 1, (uint)CprFormat);
        if (!double.IsNaN(Latitude) && !double.IsNaN(Longitude))
        {
            var position = TransponderHelper.UnambiguousPositionEncoding(Latitude, Longitude, CprFormat, 90.0);
            NCprLat = position.nCprLat;
            NCprLon = position.nCprLon;
        }
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 17, NCprLat);
        SpanBitHelper.SetBitU(buffer, ref bitIndex, 17, NCprLon);
        buffer = buffer[(bitIndex / 8)..];
    }

    public void CalculatePosition(AdsbSurfacePosition prevPosition)
    {
        if (CprFormat == prevPosition.CprFormat) return;

        if (CprFormat == CprFormatEnum.Even)
        {
            var pos = TransponderHelper.GloballyUnambiguousPositionDecoding(NCprLat, NCprLon, prevPosition.NCprLat,
                prevPosition.NCprLon, DateTime.Now, DateTime.Now.AddSeconds(-1), 90.0);
            Latitude = pos.Lat;
            Longitude = pos.Lon;
        }
        else
        {
            var pos = TransponderHelper.GloballyUnambiguousPositionDecoding(prevPosition.NCprLat, prevPosition.NCprLon,
                NCprLat, NCprLon, DateTime.Now.AddSeconds(-1), DateTime.Now, 90.0);
            Latitude = pos.Lat;
            Longitude = pos.Lon;
        }
    }
    
    public uint NCprLat { get; set; }
    public uint NCprLon { get; set; }
    public override AdsbMessageTypeEnum MessageType => AdsbMessageTypeEnum.SurfacePosition;

    public SurfacePositionTypeCodes SurfacePositionType { get; set; } =
        SurfacePositionTypeCodes.GroundVehicleWithVerticalRate;

    public double Movement { get; set; }
    public byte MovementCode { get; set; }
    public bool IsMovementReserved => MovementCode is >= 125 and <= 127;
    public bool IsMovementAvailable => MovementCode is >= 1 and <= 124;
    public GroundTrackStatusEnum GroundTrackStatus { get; set; }

    public double GroundTrack { get; set; }
    public byte GroundTrackRaw { get; set; }
    public bool TimeSynchronizedWithUtc { get; set; }
    public CprFormatEnum CprFormat { get; set; }

    /// <summary>
    /// Interprets the position type code as NUCp or NIC for the supplied ADS-B version.
    /// </summary>
    public AdsbPositionQualityInfo GetPositionQuality(
        AdsbVersionNumberEnum version,
        bool? nicSupplementA,
        bool? nicSupplementC
    ) =>
        TransponderHelper.DecodeSurfacePositionQuality(
            version,
            (byte)SurfacePositionType,
            nicSupplementA,
            nicSupplementC
        );

    public double Latitude { get; set; } = double.NaN;

    public double Longitude { get; set; } = double.NaN;

    #region Common

    private double GetMovement(uint mov)
    {
        var m = mov switch
        {
            0 => double.NaN,
            1 => 0.0,
            <= 8 => 0.125 + (mov - 2) * 0.125,
            <= 12 => 1.0 + (mov - 9) * 0.25,
            <= 38 => 2.0 + (mov - 13) * 0.5,
            <= 93 => 15 + (mov - 39) * 1.0,
            <= 108 => 70 + (mov - 94) * 2.0,
            <= 123 => 100 + (mov - 109) * 5.0,
            124 => 175.0,
            _ => double.MaxValue
        };
        return m * 0.51444;  // knots => m/s
    }
    
    private uint SetMovement(double mov)
    {
        if (double.IsNaN(mov)) return 0;
        mov /= 0.51444; // m/s => knots
        if (Math.Abs(double.MaxValue - mov) < 1) return 125;
        return mov switch
        {
            < 0.125 => 1,
            < 1.0 => (uint)Math.Round((mov - 0.125) / 0.125) + 2,
            < 2.0 => (uint)Math.Round((mov - 1.0) / 0.25) + 9,
            < 15.0 => (uint)Math.Round((mov - 2.0) / 0.5) + 13,
            < 70.0 => (uint)Math.Round((mov - 15.0) / 1.0) + 39,
            < 100.0 => (uint)Math.Round((mov - 70.0) / 2.0) + 94,
            < 175.0 => (uint)Math.Round((mov - 100.0) / 5.0) + 109,
            >= 175.0 => 124
        };
    }

    private double GetGroundTrack(uint gt)
    {
        return 360.0 * gt / 128.0;
    }
    
    private uint SetGroundTrack(double gt)
    {
        gt %= 360.0;
        if (gt < 0) gt += 360.0;
        return (uint)Math.Round(128.0 * gt / 360.0) & 0x7F;
    }

    #endregion
}
