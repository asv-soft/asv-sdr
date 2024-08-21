using System;
using Asv.IO;

namespace Asv.Sdr.Gui;

public class AdsbAircraftIdentification : AdsbExtendedSquitterBase
{
    private int _rawTc = 2;
    private int _rawCa = 0;
    public override TypeCodeEnum TypeCode => TypeCodeEnum.AircraftIdentification;

    public AircraftCategoryEnum AircraftCategory
    {
        get => AdsbHelper.GetAircraftCategory(_rawTc, _rawCa);
        set
        {
            AdsbHelper.SetAircraftCategory(value, out var tc, out var ca);
            _rawTc = tc;
            _rawCa = ca;
        }
    }

    public string AircraftIdentification { get; set; }

    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        _rawTc = (buffer[0] >> 3) & 0x1F;
        _rawCa = buffer[0] & 0x7;
        AircraftIdentification = AdsbHelper.AircraftIdDecoding(buffer.Slice(1, 6));
        buffer = buffer[7..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var tcCa = (byte)(((_rawTc & 0x1F) << 3) | (_rawCa & 0x7));
        BinSerialize.WriteByte(ref buffer, tcCa);
        var id = AdsbHelper.AircraftIdEncoding(AircraftIdentification);
        for (var i = 0; i < 6; i++)
        {
            BinSerialize.WriteByte(ref buffer, id[i]);
        }
    }
}