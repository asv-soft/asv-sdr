using System;
using Asv.IO;

namespace Asv.Sdr;

public class AdsbAircraftIdentification : AdsbExtendedSquitterBase
{
    private int _rawMt = 2;
    private int _rawCa = 0;
    public override AdsbMessageTypeEnum MessageType => AdsbMessageTypeEnum.AircraftIdentification;

    public AircraftCategoryEnum AircraftCategory
    {
        get => TransponderHelper.GetAircraftCategory(_rawMt, _rawCa);
        set
        {
            TransponderHelper.SetAircraftCategory(value, out var tc, out var ca);
            _rawMt = tc;
            _rawCa = ca;
        }
    }

    public string AircraftIdentification { get; set; }

    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        base.InternalDeserialize(ref buffer);
        _rawMt = (buffer[0] >> 3) & 0x1F;
        _rawCa = buffer[0] & 0x7;
        AircraftIdentification = TransponderHelper.AircraftIdDecoding(buffer.Slice(1, 6));
        buffer = buffer[7..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var tcCa = (byte)(((_rawMt & 0x1F) << 3) | (_rawCa & 0x7));
        BinSerialize.WriteByte(ref buffer, tcCa);
        var id = TransponderHelper.AircraftIdEncoding(AircraftIdentification);
        for (var i = 0; i < 6; i++)
        {
            BinSerialize.WriteByte(ref buffer, id[i]);
        }
    }
}