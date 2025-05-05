using System;
using Asv.IO;

namespace Asv.Sdr;

public class Bds20 : BdsBase
{
    public override byte Bds1 => 2;
    public override byte Bds2 => 0;
    
    public string AircraftIdentification { get; set; }
    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        AircraftIdentification = TransponderHelper.AircraftIdDecoding(buffer[..6]);
        buffer = buffer[6..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var id = TransponderHelper.AircraftIdEncoding(AircraftIdentification);
        for (var i = 0; i < 6; i++)
        {
            BinSerialize.WriteByte(ref buffer, id[i]);
        }
    }
}