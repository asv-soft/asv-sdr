using System;
using System.Linq;

namespace Asv.Sdr.LimeSdr;


public class SignalThreshold
{
    public double Gain { get; set; }
    public double[] Limits { get; set; }

    public int ValidateLevel(double levelDBm)
    {
        if (levelDBm < Limits[0]) return -1;
        if (levelDBm > Limits[1]) return 1;
        return 0;
    }
}

public class DmeChannelInfo
{
    public DmeChannel Channel { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public double RequesterFreq { get; set; }
    public double ResponderFreq { get; set; }
}


public static class DmeHelper
{
    private static readonly (int? Number, double ReqFr, double ResFr)[] ChannelXTable;
    private static readonly (int? Number, double ReqFr, double ResFr)[] ChannelYTable;
    
    static DmeHelper()
    {
        ChannelXTable = new (int? Number, double ReqFr, double ResFr)[252];
        ChannelYTable = new (int? Number, double ReqFr, double ResFr)[252];

        for (var i = 0; i < 63; i++)
        {
            ChannelXTable[i] = (Number: null, ReqFr: 962_000_000 + 1_000_000 * i, ResFr: 1_025_000_000 + 1_000_000 * i);
            ChannelYTable[i] = (Number: null, ReqFr: 962_000_000 + 1_000_000 * i, ResFr: 1_151_000_000 + 1_000_000 * i);
        }
        
        for (var i = 0; i < 63; i++)
        {
            ChannelXTable[i + 63] = (Number: i + 1, ReqFr: 1_025_000_000 + 1_000_000 * i, ResFr: 962_000_000 + 1_000_000 * i);
            ChannelYTable[i + 63] = (Number: i + 1, ReqFr: 1_025_000_000 + 1_000_000 * i, ResFr: 1_088_000_000 + 1_000_000 * i);
        }
        
        for (var i = 0; i < 63; i++)
        {
            ChannelXTable[i + 126] = (Number: i + 64, ReqFr: 1_088_000_000 + 1_000_000 * i, ResFr: 1_151_000_000 + 1_000_000 * i);
            ChannelYTable[i + 126] = (Number: i + 64, ReqFr: 1_088_000_000 + 1_000_000 * i, ResFr: 1_025_000_000 + 1_000_000 * i);
        }
        
        for (var i = 0; i < 63; i++)
        {
            ChannelXTable[i + 189] = (Number: null, ReqFr: 1_151_000_000 + 1_000_000 * i, ResFr: 1_088_000_000 + 1_000_000 * i);
            ChannelYTable[i + 189] = (Number: null, ReqFr: 1_151_000_000 + 1_000_000 * i, ResFr: 962_000_000 + 1_000_000 * i);
        }

    }
    
    public static DmeChannelInfo? GetInfoFromRequestFreq(this DmeChannel channel, double freq)
    {
        var freqSm = Math.Round(freq / 1_000_000) * 1_000_000;

        if (freqSm < 962_000_000)
            return channel == DmeChannel.XChannel
                ? new DmeChannelInfo
                {
                    ChannelName = "   X",
                    RequesterFreq = ChannelXTable[0].ReqFr,
                    ResponderFreq = ChannelXTable[0].ResFr
                }
                : new DmeChannelInfo
                {
                    ChannelName = "   Y",
                    RequesterFreq = ChannelYTable[0].ReqFr,
                    ResponderFreq = ChannelYTable[0].ResFr
                };
        
        if (freqSm > 1_213_000_000)
            return channel == DmeChannel.XChannel
                ? new DmeChannelInfo
                {
                    ChannelName = "   X",
                    RequesterFreq = ChannelXTable[251].ReqFr,
                    ResponderFreq = ChannelXTable[251].ResFr
                }
                : new DmeChannelInfo
                {
                    ChannelName = "   Y",
                    RequesterFreq = ChannelYTable[251].ReqFr,
                    ResponderFreq = ChannelYTable[251].ResFr
                };

        switch (channel)
        {
            case DmeChannel.XChannel:
                var itemX = ChannelXTable.First(it => Math.Abs(it.ReqFr - freqSm) < 1);
                var chXName = itemX.Number != null ? $"{itemX.Number:000}X" : "   X";
                return new DmeChannelInfo 
                { 
                    Channel = channel, 
                    ChannelName = chXName, 
                    RequesterFreq = itemX.ReqFr, 
                    ResponderFreq = itemX.ResFr 
                };
            case DmeChannel.YChannel:
                var itemY = ChannelXTable.First(it => Math.Abs(it.ReqFr - freqSm) < 1);
                var chYName = itemY.Number != null ? $"{itemY.Number:000}Y" : "   Y";
                return new DmeChannelInfo 
                { 
                    Channel = channel, 
                    ChannelName = chYName, 
                    RequesterFreq = itemY.ReqFr, 
                    ResponderFreq = itemY.ResFr 
                };
            default:
                return null;
        }
    }
    
    public static DmeChannelInfo? GetInfoFromResponderFreq(this DmeChannel channel, double freq)
    {
        var freqSm = Math.Round(freq / 1_000_000) * 1_000_000;

        if (freqSm < 962_000_000)
            return channel == DmeChannel.XChannel
                ? new DmeChannelInfo
                {
                    ChannelName = $"{ChannelXTable[63].Number:000}X",
                    RequesterFreq = ChannelXTable[63].ReqFr,
                    ResponderFreq = ChannelXTable[63].ResFr
                }
                : new DmeChannelInfo
                {
                    ChannelName = "   Y",
                    RequesterFreq = ChannelYTable[189].ReqFr,
                    ResponderFreq = ChannelYTable[189].ResFr
                };
        
        if (freqSm > 1_213_000_000)
            return channel == DmeChannel.XChannel
                ? new DmeChannelInfo
                {
                    ChannelName = $"{ChannelXTable[188].Number:000}X",
                    RequesterFreq = ChannelXTable[188].ReqFr,
                    ResponderFreq = ChannelXTable[188].ResFr
                }
                : new DmeChannelInfo
                {
                    ChannelName = "   Y",
                    RequesterFreq = ChannelYTable[62].ReqFr,
                    ResponderFreq = ChannelYTable[62].ResFr
                };

        switch (channel)
        {
            case DmeChannel.XChannel:
                var itemX = ChannelXTable.First(it => Math.Abs(it.ResFr - freqSm) < 1);
                var chXName = itemX.Number != null ? $"{itemX.Number:000}X" : "   X";
                return new DmeChannelInfo 
                { 
                    Channel = channel, 
                    ChannelName = chXName, 
                    RequesterFreq = itemX.ReqFr, 
                    ResponderFreq = itemX.ResFr 
                };
            case DmeChannel.YChannel:
                var itemY = ChannelYTable.First(it => Math.Abs(it.ResFr - freqSm) < 1);
                var chYName = itemY.Number != null ? $"{itemY.Number:000}Y" : "   Y";
                return new DmeChannelInfo 
                { 
                    Channel = channel, 
                    ChannelName = chYName, 
                    RequesterFreq = itemY.ReqFr, 
                    ResponderFreq = itemY.ResFr 
                };
            default:
                return null;
        }
    }
}