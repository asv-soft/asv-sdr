using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
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
        if (File.Exists($"resources{Path.DirectorySeparatorChar}AddressParityDb.bin"))
        {
            using var fs = new FileStream($"resources{Path.DirectorySeparatorChar}AddressParityDb.bin", FileMode.Open);
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

        if (!Directory.Exists("resources"))
            Directory.CreateDirectory("resources");
        
        using var newFs = new FileStream($"resources{Path.DirectorySeparatorChar}AddressParityDb.bin", FileMode.OpenOrCreate, FileAccess.Write);
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
        if (format != FormatId) throw new InvalidDataException($"Unknown format: {format}");
        pos += 5;
        InternalDeserialize(buffer, ref pos);
        CalculatedCrc = ModeSHelper.CalcCrc24(buffer, FormatLength - 3);
        buffer = buffer[(pos / 8)..];
        ModifiedCrc = (uint)((buffer[0] << 16) | (buffer[1] << 8) | buffer[2]);
        IcaoAddress = GetIcao(CalculatedCrc, ModifiedCrc) ?? 0;
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

    public abstract uint? GetIcao(uint calcCrc, uint originCrc);
    
    protected static uint? FindIcao(uint calcCrc, uint originCrc, bool isDeepSearch = false)
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
    #region CommA

    private static readonly Dictionary<byte, Func<AdsBase>> _factory = new();

    private static IEnumerable<Func<AdsBase>> DefaultCommBBlocks
    {
        get
        {
            yield return () => new Ads05();
        }
    }

    private static void RegisterDefaultCommBBlocks()
    {
        foreach (var func in DefaultCommBBlocks)
        {
            var pkt = func();
            _factory.Add(pkt.DataSelector, func);
        }
    }
    
    #endregion
    
    static ModeSUFormatBase()
    {
        RegisterDefaultCommBBlocks();
    }
    public AdsBase? Ads { get; set; }

    public override uint? GetIcao(uint calcCrc, uint originCrc)
    {
        return FindIcao(calcCrc, originCrc, true);
    }

    protected override uint GetModifiedCrc(uint calcCrc)
    {
        var ap = GetAddressParity(IcaoAddress);
        return ap ^ calcCrc;
    }
    
    protected void DeserializeAds(ReadOnlySpan<byte> buffer, ref int pos)
    {
        var pos1 = pos;
        var buff = buffer[(pos / 8)..];
        var ds = (byte)ModeSHelper.GetBitU(buffer, ref pos1, 8);
        if (_factory.TryGetValue(ds, out var func))
        {
            Ads = func();
            Ads.Deserialize(ref buff);
            pos = (buffer.Length - buff.Length) * 8;
            return;
        }
        pos += 56;
    }

    protected void SerializeAds(Span<byte> buffer, ref int pos)
    {
        var buff = buffer[(pos / 8)..];
        if (Ads == null)
        {
            for (var i = 0; i < 7; i++)
            {
                BinSerialize.WriteByte(ref buff, 0);
            }
            pos = (buffer.Length - buff.Length) * 8;
            return;
        }
        
        Ads.Serialize(ref buff);
        pos = (buffer.Length - buff.Length) * 8;
    }
}

public abstract class ModeSDFormatBase : ModeSFormatBase
{
    #region CommB

    private static readonly Dictionary<byte, Func<BdsBase>> _factory = new();

    private static IEnumerable<Func<BdsBase>> DefaultCommBBlocks
    {
        get
        {
            yield return () => new Bds10();
            yield return () => new Bds20();
            yield return () => new Bds30();
        }
    }

    private static void RegisterDefaultCommBBlocks()
    {
        foreach (var func in DefaultCommBBlocks)
        {
            var pkt = func();
            _factory.Add(pkt.DataSelector, func);
        }
    }

    
    #endregion
    

    static ModeSDFormatBase()
    {
        RegisterDefaultCommBBlocks();
    }

    public override uint? GetIcao(uint calcCrc, uint originCrc)
    {
        return calcCrc ^ originCrc;
    }

    protected override uint GetModifiedCrc(uint calcCrc)
    {
        return IcaoAddress ^ calcCrc;
    }

    public BdsBase? Bds { get; set; }

    protected void DeserializeBds(ReadOnlySpan<byte> buffer, ref int pos)
    {
        var buff = buffer[(pos / 8)..];
        
        if (Bds != null)
        {
            try
            {
                Bds.Deserialize(ref buff);
                pos += (buffer.Length - buff.Length) * 8;
                return;
            }
            catch (Exception)
            {
                // ignored
            }
        }
        
        Bds = BdsFactory.GetBds(ref buff);
        pos = (buffer.Length - buff.Length) * 8;
    }

    protected void SerializeBds(Span<byte> buffer, ref int pos)
    {
        var buff = buffer[(pos / 8)..];
        if (Bds == null)
        {
            for (var i = 0; i < 7; i++)
            {
                BinSerialize.WriteByte(ref buff, 0);
            }
            pos = (buffer.Length - buff.Length) * 8;
            return;
        }
        
        Bds?.Serialize(ref buff);
        pos = (buffer.Length - buff.Length) * 8;
    }
}
