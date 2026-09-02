using System;
using System.Collections.Generic;

namespace Asv.Sdr;

public static class BdsFactory
{
    public static List<BdsBase> GetBds(ref ReadOnlySpan<byte> buffer)
    {
        return GetBds(ref buffer, BdsInferenceLimits.CivilSubsonic);
    }

    public static List<BdsBase> GetBds(ref ReadOnlySpan<byte> buffer, BdsInferenceLimits inferenceLimits)
    {
        ArgumentNullException.ThrowIfNull(inferenceLimits);

        var pos = 0;
        var bds1 = ModeSHelper.GetBitU(buffer, ref pos, 4);
        var bds2 = ModeSHelper.GetBitU(buffer, ref pos, 4);
        
        // BDS 1,0
        pos += 1;
        var reserved = ModeSHelper.GetBitU(buffer, ref pos, 5);
        if (bds1 == 1 && bds2 == 0 && reserved == 0)
        {
            var bds10 = new Bds10();
            bds10.Deserialize(ref buffer);
            return new List<BdsBase> { bds10 };
        }
        
        // BDS 2,0
        var isCallSign = TransponderHelper.CheckCallsign(buffer[1..]);
        if (bds1 == 2 && bds2 == 0 && isCallSign)
        {
            var bds20 = new Bds20();
            bds20.Deserialize(ref buffer);
            return new List<BdsBase> { bds20 };
        }
        
        // BDS 3,0
        pos = 15;
        var acas = ModeSHelper.GetBitU(buffer, ref pos, 7);
        pos = 28;
        var threatType = ModeSHelper.GetBitU(buffer, ref pos, 2);
        if (bds1 == 3 && bds2 == 0 && acas < 48 && threatType != 3)
        {
            var bds30 = new Bds30();
            bds30.Deserialize(ref buffer);
            return new List<BdsBase> { bds30 };
        }
        
        // BDS 1,7
        pos = 6;
        var bds20Enable = ModeSHelper.GetBitU(buffer, ref pos, 1) == 1;
        pos = 28;
        reserved = ModeSHelper.GetBitU(buffer, ref pos, 28);
        if (bds20Enable && reserved == 0)
        {
            var bds17 = new Bds17();
            bds17.Deserialize(ref buffer);
            return new List<BdsBase> { bds17 };
        }

        var candidates = new List<BdsBase>();
        var remainingBuffer = buffer;
        
        // BDS 4,0
        var bds40 = new Bds40();
        try
        {
            var candidateBuffer = buffer;
            bds40.Deserialize(ref candidateBuffer);
            candidates.Add(bds40);
            remainingBuffer = candidateBuffer;
        }
        catch (Exception)
        {
            // ignored
        }

        // BDS 5,0
        var bds50 = new Bds50();
        try
        {
            var candidateBuffer = buffer;
            bds50.Deserialize(ref candidateBuffer);
            if (BdsInference.Is50(bds50, inferenceLimits))
            {
                candidates.Add(bds50);
                remainingBuffer = candidateBuffer;
            }
        }
        catch (Exception)
        {
            // ignored
        }
        
        // BDS 6,0
        var bds60 = new Bds60();
        try
        {
            var candidateBuffer = buffer;
            bds60.Deserialize(ref candidateBuffer);
            if (BdsInference.Is60(bds60, inferenceLimits))
            {
                candidates.Add(bds60);
                remainingBuffer = candidateBuffer;
            }
        }
        catch (Exception)
        {
            // ignored
        }

        if (candidates.Count == 0)
        {
            var bdsAny = new BdsAny(0, 0);
            bdsAny.Deserialize(ref buffer);
            candidates.Add(bdsAny);
            return candidates;
        }

        buffer = remainingBuffer;
        return candidates;
    }
}
