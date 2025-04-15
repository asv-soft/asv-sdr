using System;
using System.IO;
using Asv.IO;

namespace Asv.Sdr;

public abstract class ModeSFormatBase : ISizedSpanSerializable
{
    private static readonly uint[] AllAddressParity;
    private static readonly uint BroadcastAddress;

    private const uint Generator = 0x1FFF409;
    
    static ModeSFormatBase()
    {
        BroadcastAddress = GetAddressParity(0xFFFFFF);
        AllAddressParity = new uint[0xFFFFFF];
        
        var readSuccess = false;
        if (File.Exists($"Resources{Path.DirectorySeparatorChar}AddressParityDb.bin"))
        {
            using var fs = new FileStream($"Resources{Path.DirectorySeparatorChar}AddressParityDb.bin", FileMode.Open);
            if (fs.Length == 0xFFFFFF * 3)
            {
                var buffer = new byte[0xFFFFFF * 3];
                var span = new Span<byte>(buffer);
                fs.ReadExactly(span);

                for (var i = 0; i < 0xFFFFFF; i++)
                {
                    AllAddressParity[i] =
                        (uint)((buffer[3 * i] << 16) | (buffer[3 * i + 1] << 8) | buffer[3 * i + 2]);
                }
                readSuccess = true;
            }
            else
            {
                readSuccess = false;
            }
        }
        
        if (readSuccess) return;

        if (!Directory.Exists("Resources"))
            Directory.CreateDirectory("Resources");
        
        using var newFs = new FileStream($"Resources{Path.DirectorySeparatorChar}AddressParityDb.bin", FileMode.OpenOrCreate, FileAccess.Write);
        var newBuffer = new byte[0xFFFFFF * 3];
        var newSpan = new ReadOnlySpan<byte>(newBuffer);
        for (uint i = 0x0; i < 0xFFFFFF; i++)
        {
            AllAddressParity[i] = GetAddressParity(i);
            newBuffer[3 * i] = (byte)((AllAddressParity[i] >> 16) & 0xFF);
            newBuffer[3 * i + 1] = (byte)((AllAddressParity[i] >> 8) & 0xFF);
            newBuffer[3 * i + 2] = (byte)(AllAddressParity[i] & 0xFF);
        }
        newFs.Write(newSpan);
    }
    
    protected abstract int FormatLength { get; }

    public abstract byte FormatId { get; }

    public uint IcaoAddress { get; set; }
    public uint CalculatedCrc { get; set; }

    public uint ModifiedCrc { get; set; }

    
    public void Deserialize(ref ReadOnlySpan<byte> buffer)
    {
        var pos = 0;
        var format = ModeSHelper.GetModeSFormat(buffer);
        pos += 5;
        InternalDeserialize(buffer, ref pos);
        CalculatedCrc = ModeSHelper.CalcCrc24(buffer, FormatLength - 3);
        buffer = buffer[(pos / 8)..];
        ModifiedCrc = (uint)((buffer[0] << 16) | (buffer[1] << 8) | buffer[2]);
        IcaoAddress = FindIcao(CalculatedCrc, ModifiedCrc, true) ?? 0;
        buffer = buffer[3..];
    }

    public void Serialize(ref Span<byte> buffer)
    {
        var pos = 0;
        ModeSHelper.SetModeSFormat(buffer, FormatId);
        pos += 5;
        InternalSerialize(buffer, ref pos);
        CalculatedCrc = ModeSHelper.CalcCrc24(buffer, FormatLength - 3);
        ModifiedCrc = GetModifiedCrc(CalculatedCrc);
        buffer = buffer[(pos / 8)..];
        buffer[0] = (byte)((ModifiedCrc >> 16) & 0xFF);
        buffer[1] = (byte)((ModifiedCrc >> 8) & 0xFF);
        buffer[2] = (byte)(ModifiedCrc & 0xFF);
        buffer = buffer[3..];
    }

    protected abstract void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos);
    protected abstract void InternalSerialize(Span<byte> buffer, ref int pos);
    protected abstract uint GetModifiedCrc(uint calcCrc);
    
    public int GetByteSize()
    {
        return FormatLength;
    }

    public static uint? FindIcao(uint calcXorOriginCrc, bool isDeepSearch = false)
    {
        uint? resul = null;

        if (BroadcastAddress == calcXorOriginCrc)
        {
            return 0xFFFFFF;
        }
        
        if (!isDeepSearch)
        {
            for (uint i = 0x140000; i < 0x160000; i++)
            {
                if (AllAddressParity[i] == calcXorOriginCrc)
                {
                    return i;
                }
            }
        }
        else
        {
            for (uint i = 0x140000; i < 0x160000; i++)
            {
                if (AllAddressParity[i] == calcXorOriginCrc)
                {
                    return i;
                }
            }
            for (uint i = 0; i < 0x140000; i++)
            {
                if (AllAddressParity[i] == calcXorOriginCrc)
                {
                    return i;
                }
            }
            for (uint i = 0x160000; i < AllAddressParity.Length; i++)
            {
                if (AllAddressParity[i] == calcXorOriginCrc)
                {
                    return i;
                }
            }
        }
        
        return resul;
    }
    
    public static uint? FindIcao(uint calcCrc, uint originCrc, bool isDeepSearch = false)
    {
        uint? resul = null;

        if ((BroadcastAddress ^ calcCrc) == originCrc)
        {
            return 0xFFFFFF;
        }
        
        if (!isDeepSearch)
        {
            for (uint i = 0x140000; i < 0x160000; i++)
            {
                if ((AllAddressParity[i] ^ calcCrc) == originCrc)
                {
                    return i;
                }
            }
        }
        else
        {
            for (uint i = 0x140000; i < 0x160000; i++)
            {
                if ((AllAddressParity[i] ^ calcCrc) == originCrc)
                {
                    return i;
                }
            }
            for (uint i = 0; i < 0x140000; i++)
            {
                if ((AllAddressParity[i] ^ calcCrc) == originCrc)
                {
                    return i;
                }
            }
            for (uint i = 0x160000; i < AllAddressParity.Length; i++)
            {
                if ((AllAddressParity[i] ^ calcCrc) == originCrc)
                {
                    return i;
                }
            }
        }
        
        return resul;
    }
    
    protected static uint GetAddressParity(uint icaoAddr)
    {
        uint crc = 0;
        for (var i = 23; i >= 0; i--)
        {
            for (var j = 24; j >= 0; j--)
            {
                if (i + j < 24) continue;
                crc ^= (((icaoAddr >> i) & 0x1) & ((Generator >> j) & 0x1)) << (i + j - 24);
                if (crc != 0) {}
            }
        }
        return crc & 0xFFFFFF;
    }
}

public abstract class ModeSUFormatBase : ModeSFormatBase
{
    protected override uint GetModifiedCrc(uint calcCrc)
    {
        var ap = GetAddressParity(IcaoAddress);
        return ap ^ calcCrc;
    }
}

public abstract class ModeSDFormatBase : ModeSFormatBase
{
    protected override uint GetModifiedCrc(uint calcCrc)
    {
        return IcaoAddress ^ calcCrc;
    }
}
