using System;
using System.Collections.Generic;
using System.Linq;
using Asv.IO;

namespace Asv.Sdr;

public class Gicb
{
    public Gicb(byte bds1, byte bds2)
    {
        Bds1 = bds1;
        Bds2 = bds2;
    }
    public byte Bds1 { get; set; }
    public byte Bds2 { get; set; }
    
    public byte DataSelector => (byte)((Bds1 << 0x4) | (Bds2 & 0xF));
    
    public override string ToString()
    {
        return $"{Bds1:X1},{Bds2:X1}";
    }
}

public class Bds17 : BdsBase
{
    public override byte Bds1 => 1;
    public override byte Bds2 => 7;

    public List<Gicb> Gicbs { get; } = [];
    
    public override void Deserialize(ref ReadOnlySpan<byte> buffer)
    {
        InternalDeserialize(ref buffer);
    }

    public override void Serialize(ref Span<byte> buffer)
    {
        InternalSerialize(ref buffer);
    }

    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        var pos = 0;
        
        // ADS-B registers
        var adsb = (byte)ModeSHelper.GetBitU(buffer, ref pos, 6);
        for (byte i = 0; i < 6; i++)
        {
            if ((adsb & (1 << 5 - i)) != 0) Gicbs.Add(new Gicb(0, (byte)(i + 5)));
        }
        
        
        // Elementary surveillance
        var els = (byte)ModeSHelper.GetBitU(buffer, ref pos, 2);
        for (byte i = 0; i < 2; i++)
        {
            if ((els & (1 << 1 - i)) != 0) Gicbs.Add(new Gicb(2, i));
        }
        
        // Enhanced surveillance
        // BDS 4,X
        var ehs4Mask = (byte)(1 << 6);
        var ehs4 = (byte)ModeSHelper.GetBitU(buffer, ref pos, 7);
        for (byte i = 0; i < 6; i++)
        {
            if ((ehs4 & ehs4Mask) != 0) Gicbs.Add(new Gicb(4, i));
            ehs4Mask = (byte)(ehs4Mask >> 1);
        }
        if ((ehs4 & ehs4Mask) != 0) Gicbs.Add(new Gicb(4, 8));
        
        // BDS 5,X
        var ehs5Mask = (byte)(1 << 7);
        var ehs5 = (byte)ModeSHelper.GetBitU(buffer, ref pos, 8);
        for (byte i = 0; i < 7; i++)
        {
            if ((ehs5 & ehs5Mask) != 0) Gicbs.Add(new Gicb(5, i));
            ehs5Mask = (byte)(ehs5Mask >> 1);
        }
        if ((ehs5 & ehs5Mask) != 0) Gicbs.Add(new Gicb(5, 0xF));
        
        // BDS 6,0
        var ehs6 = (byte)ModeSHelper.GetBitU(buffer, ref pos, 1);
        if (ehs6 != 0) Gicbs.Add(new Gicb(6, 0));
        
        pos += 32;
        
        buffer = buffer[(pos / 8)..];
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        var pos = 0;

        byte adsbMask = 1 << 5;
        byte adsb = 0;
        for (byte i = 0; i < 6; i++)
        {
            var gicb = Gicbs.FirstOrDefault(x => x.Bds1 == 0 && x.Bds2 == i + 5);
            if (gicb != null) adsb = (byte)(adsb | adsbMask);
            adsb = (byte)(adsb >> 1);
        }
        ModeSHelper.SetBitU(buffer, ref pos, 6, adsb);

        // Elementary surveillance
        byte els = 0;
        for (byte i = 0; i < 2; i++)
        {
            var gicb = Gicbs.FirstOrDefault(x => x.Bds1 == 2 && x.Bds2 == i);
            if (gicb != null) els = (byte)(els | (1 << 1 - i));
        }
        ModeSHelper.SetBitU(buffer, ref pos, 2, els);
        
        // Enhanced surveillance
        // BDS 4,X
        var ehs4Mask = (byte)(1 << 6);
        byte ehs4 = 0;
        for (byte i = 0; i < 6; i++)
        {
            var gicb = Gicbs.FirstOrDefault(x => x.Bds1 == 4 && x.Bds2 == i);
            if (gicb != null) ehs4 = (byte)(ehs4 | ehs4Mask);
            ehs4Mask = (byte)(ehs4Mask >> 1);
        }
        if (Gicbs.FirstOrDefault(x => x is { Bds1: 4, Bds2: 8 }) != null)
            ehs4 = (byte)(ehs4 | ehs4Mask);
        ModeSHelper.SetBitU(buffer, ref pos, 7, ehs4);
        
        // BDS 5,X
        var ehs5Mask = (byte)(1 << 7);
        byte ehs5 = 0;
        for (byte i = 0; i < 7; i++)
        {
            var gicb = Gicbs.FirstOrDefault(x => x.Bds1 == 5 && x.Bds2 == i);
            if (gicb != null) ehs5 = (byte)(ehs5 | ehs5Mask);
            ehs5Mask = (byte)(ehs5Mask >> 1);
        }
        if (Gicbs.FirstOrDefault(x => x is { Bds1: 5, Bds2: 0xF }) != null)
            ehs5 = (byte)(ehs5 | ehs5Mask);
        ModeSHelper.SetBitU(buffer, ref pos, 8, ehs5);
        
        // BDS 6,0
        if (Gicbs.FirstOrDefault(x => x is { Bds1: 6, Bds2: 0x0 }) != null)
            ModeSHelper.SetBitU(buffer, ref pos, 1, 1);
        else
            ModeSHelper.SetBitU(buffer, ref pos, 1, 0);
        
        pos += 32;
        buffer = buffer[(pos / 8)..];
    }
}