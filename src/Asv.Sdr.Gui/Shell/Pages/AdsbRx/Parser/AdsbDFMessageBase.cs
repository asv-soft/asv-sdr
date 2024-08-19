using System;
using Asv.IO;

namespace Asv.Sdr.Gui;

public abstract class AdsbDFMessageBase : ISizedSpanSerializable
{
    public abstract int DownlinkFormat { get; }
    public int AddId { get; set; }
    public int AircraftAddress { get; set; }
    
    protected abstract int MessageLength { get; }

    public void Deserialize(ref ReadOnlySpan<byte> buffer)
    {
        var originBuffer = buffer;

        var preamble1 = BinSerialize.ReadByte(ref buffer);
        var preamble2 = BinSerialize.ReadByte(ref buffer);
        if (preamble1 != AdsbHelper.Preamble[0] || preamble2 != AdsbHelper.Preamble[1])
        {
            throw new Exception(
                $"Deserialization ADS-B message failed: want 0x{AdsbHelper.Preamble[0]:X} 0x{AdsbHelper.Preamble[1]}. Read 0x{preamble1:X} 0x{preamble2:X}");
        }

        var crc = AdsbHelper.CalcCrc(originBuffer);
        var dfAndCa = BinSerialize.ReadByte(ref buffer);
        var downLinkFormat = (dfAndCa >> 3) & 0x1F;
        var addId = dfAndCa & 0x7;
        if (CheckMessageId(downLinkFormat, addId))
        {
            throw new Exception($"Deserialization ADS-B message failed: want message number '{DownlinkFormat}'. Read = '{downLinkFormat}'");
        }
        AircraftAddress = GetAircraftAddress(DownlinkFormat, crc, ref buffer);
        AddId = addId;
        InternalDeserialize(ref buffer);
    }

    protected virtual void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        
    }

    protected virtual bool CheckMessageId(int downlinkFormat, int addId)
    {
        return downlinkFormat == DownlinkFormat;
    }

    protected virtual int GetAircraftAddress(int df, uint crc, ref ReadOnlySpan<byte> aaFrame)
    {
        if (crc == 0)
        {
            return (BinSerialize.ReadByte(ref aaFrame) << 16) |
                   (BinSerialize.ReadByte(ref aaFrame) << 8) |
                   BinSerialize.ReadByte(ref aaFrame);
        }
        for (var i = 0; i < 3; i++)
        {
            BinSerialize.ReadByte(ref aaFrame);
        }
        return 0;
    }
    
    

    public void Serialize(ref Span<byte> buffer)
    {
        throw new NotImplementedException();
    }
    public abstract int GetByteSize();
}