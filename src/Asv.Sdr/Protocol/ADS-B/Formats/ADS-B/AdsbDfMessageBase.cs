using System;
using Asv.IO;

namespace Asv.Sdr;

public abstract class AdsbDfMessageBase : ISizedSpanSerializable
{
    /// <summary>
    /// Raw Capability
    /// Default Level 2+ transponder, with ability to set CA to 7, airborne
    /// </summary>
    private int _rawCa = 5;

    
    /// <summary>
    /// Id: [...DF (5 bit)... | ...TC (5 bit)... | SubType (3 bit) ]
    /// </summary>
    public abstract ushort Id { get; }
    
    public abstract int DownlinkFormat { get; }

    public CapabilityEnum Capability
    {
        get => TransponderHelper.GetCapability(_rawCa);
        set => _rawCa = TransponderHelper.SetCapability(value);
    }

    public int AircraftAddress { get; set; }
    
    public void Deserialize(ref ReadOnlySpan<byte> buffer)
    {
        // var preamble1 = BinSerialize.ReadByte(ref buffer);
        // var preamble2 = BinSerialize.ReadByte(ref buffer);
        // if (preamble1 != TransponderHelper.Preamble[0] || preamble2 != TransponderHelper.Preamble[1])
        // {
        //     throw new Exception(
        //         $"Deserialization ADS-B message failed: want 0x{TransponderHelper.Preamble[0]:X} 0x{TransponderHelper.Preamble[1]}. Read 0x{preamble1:X} 0x{preamble2:X}");
        // }

        var dfAndCa = BinSerialize.ReadByte(ref buffer);
        var downLinkFormat = (dfAndCa >> 3) & 0x1F;
        _rawCa = dfAndCa & 0x7;
        
        if (!CheckMessageId(downLinkFormat))
        {
            throw new Exception($"Deserialization ADS-B message failed: want message number '{DownlinkFormat}'. Read = '{downLinkFormat}'");
        }
        AircraftAddress = ReadAircraftAddress(ref buffer);
        InternalDeserialize(ref buffer);
        
        // Read CRC
        BinSerialize.ReadByte(ref buffer); // << 16;
        BinSerialize.ReadByte(ref buffer); // << 8;
        BinSerialize.ReadByte(ref buffer);
    }

    protected abstract void InternalDeserialize(ref ReadOnlySpan<byte> buffer);

    protected abstract bool CheckMessageId(int downlinkFormat);

    private static int ReadAircraftAddress(ref ReadOnlySpan<byte> aaFrame)
    {
        return (BinSerialize.ReadByte(ref aaFrame) << 16) |
               (BinSerialize.ReadByte(ref aaFrame) << 8) |
               BinSerialize.ReadByte(ref aaFrame);
    }

    private static void WriteAircraftAddress(ref Span<byte> aaFrame, int address)
    {
        BinSerialize.WriteByte(ref aaFrame, (byte)((address >> 16) & 0xFF));
        BinSerialize.WriteByte(ref aaFrame, (byte)((address >> 8) & 0xFF));
        BinSerialize.WriteByte(ref aaFrame, (byte)(address & 0xFF));
    }
    
    public void Serialize(ref Span<byte> buffer)
    {
        var originSpan = buffer;
        BinSerialize.WriteByte(ref buffer, (byte)(((DownlinkFormat & 0x1F) << 3) | (_rawCa & 0x7)));
        WriteAircraftAddress(ref buffer, AircraftAddress);
        InternalSerialize(ref buffer);
        var crc = ModeSHelper.CalcCrc24(originSpan, originSpan.Length - buffer.Length);
        BinSerialize.WriteByte(ref buffer, (byte)((crc >> 16) & 0xFF));
        BinSerialize.WriteByte(ref buffer, (byte)((crc >> 8) & 0xFF));
        BinSerialize.WriteByte(ref buffer, (byte)(crc & 0xFF));
    }
    
    protected abstract void InternalSerialize(ref Span<byte> buffer);
    public abstract int GetByteSize();
}