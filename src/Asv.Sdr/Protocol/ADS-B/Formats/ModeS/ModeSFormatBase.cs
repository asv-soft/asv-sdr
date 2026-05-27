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

    private static readonly Icao24CountryRange[] Icao24CountryRanges =
    {
        new(0x700000, 0x700FFF, "AFG", "Afghanistan"),
        new(0x501000, 0x5017FF, "ALB", "Albania"),
        new(0x0A0000, 0x0A7FFF, "DZA", "Algeria"),
        new(0xC91000, 0xC917FF, "AND", "Andorra"),
        new(0x090000, 0x090FFF, "AGO", "Angola"),
        new(0x0CA000, 0x0CA7FF, "ATG", "Antigua and Barbuda"),
        new(0xE00000, 0xE3FFFF, "ARG", "Argentina"),
        new(0x600000, 0x6007FF, "ARM", "Armenia"),
        new(0x7C0000, 0x7FFFFF, "AUS", "Australia"),
        new(0x440000, 0x447FFF, "AUT", "Austria"),
        new(0x600800, 0x600FFF, "AZE", "Azerbaijan"),
        new(0x0A8000, 0x0A8FFF, "BHS", "Bahamas"),
        new(0x894000, 0x894FFF, "BHR", "Bahrain"),
        new(0x702000, 0x702FFF, "BGD", "Bangladesh"),
        new(0x0AA000, 0x0AA7FF, "BRB", "Barbados"),
        new(0x510000, 0x5107FF, "BLR", "Belarus"),
        new(0x448000, 0x44FFFF, "BEL", "Belgium"),
        new(0x0AB000, 0x0AB7FF, "BLZ", "Belize"),
        new(0x094000, 0x0947FF, "BEN", "Benin"),
        new(0x680000, 0x6807FF, "BTN", "Bhutan"),
        new(0xE94000, 0xE94FFF, "BOL", "Bolivia (Plurinational State of)"),
        new(0x513000, 0x5137FF, "BIH", "Bosnia and Herzegovina"),
        new(0x030000, 0x0307FF, "BWA", "Botswana"),
        new(0xE40000, 0xE7FFFF, "BRA", "Brazil"),
        new(0x895000, 0x8957FF, "BRN", "Brunei Darussalam"),
        new(0x450000, 0x457FFF, "BGR", "Bulgaria"),
        new(0x09C000, 0x09CFFF, "BFA", "Burkina Faso"),
        new(0x032000, 0x032FFF, "BDI", "Burundi"),
        new(0x096000, 0x0967FF, "CPV", "Cabo Verde"),
        new(0x70E000, 0x70EFFF, "KHM", "Cambodia"),
        new(0x034000, 0x034FFF, "CMR", "Cameroon"),
        new(0xC00000, 0xC3FFFF, "CAN", "Canada"),
        new(0x06C000, 0x06CFFF, "CAF", "Central African Republic"),
        new(0x084000, 0x084FFF, "TCD", "Chad"),
        new(0xE80000, 0xE80FFF, "CHL", "Chile"),
        new(0x780000, 0x7BFFFF, "CHN", "China"),
        new(0x0AC000, 0x0ADFFF, "COL", "Colombia"),
        new(0x035000, 0x0357FF, "COM", "Comoros"),
        new(0x036000, 0x036FFF, "COG", "Congo"),
        new(0x901000, 0x9017FF, "COK", "Cook Islands"),
        new(0x0AE000, 0x0AEFFF, "CRI", "Costa Rica"),
        new(0x038000, 0x038FFF, "CIV", "Côte d'Ivoire"),
        new(0x501800, 0x501FFF, "HRV", "Croatia"),
        new(0x0B0000, 0x0B0FFF, "CUB", "Cuba"),
        new(0x4C8000, 0x4C87FF, "CYP", "Cyprus"),
        new(0x498000, 0x49FFFF, "CZE", "Czechia"),
        new(0x720000, 0x727FFF, "PRK", "Democratic People's Republic of Korea"),
        new(0x08C000, 0x08CFFF, "COD", "Democratic Republic of the Congo"),
        new(0x458000, 0x45FFFF, "DNK", "Denmark"),
        new(0x098000, 0x0987FF, "DJI", "Djibouti"),
        new(0xC92000, 0xC927FF, "DMA", "Dominica"),
        new(0x0C4000, 0x0C4FFF, "DOM", "Dominican Republic"),
        new(0xE84000, 0xE84FFF, "ECU", "Ecuador"),
        new(0x010000, 0x017FFF, "EGY", "Egypt"),
        new(0x0B2000, 0x0B2FFF, "SLV", "El Salvador"),
        new(0x042000, 0x042FFF, "GNQ", "Equatorial Guinea"),
        new(0x202000, 0x2027FF, "ERI", "Eritrea"),
        new(0x511000, 0x5117FF, "EST", "Estonia"),
        new(0x07A000, 0x07A7FF, "SWZ", "Eswatini"),
        new(0x040000, 0x040FFF, "ETH", "Ethiopia"),
        new(0xC88000, 0xC88FFF, "FJI", "Fiji"),
        new(0x460000, 0x467FFF, "FIN", "Finland"),
        new(0x380000, 0x3BFFFF, "FRA", "France"),
        new(0x03E000, 0x03EFFF, "GAB", "Gabon"),
        new(0x09A000, 0x09AFFF, "GMB", "Gambia"),
        new(0x514000, 0x5147FF, "GEO", "Georgia"),
        new(0x3C0000, 0x3FFFFF, "DEU", "Germany"),
        new(0x044000, 0x044FFF, "GHA", "Ghana"),
        new(0x468000, 0x46FFFF, "GRC", "Greece"),
        new(0x0CC000, 0x0CC7FF, "GRD", "Grenada"),
        new(0x0B4000, 0x0B4FFF, "GTM", "Guatemala"),
        new(0x046000, 0x046FFF, "GIN", "Guinea"),
        new(0x048000, 0x0487FF, "GNB", "Guinea-Bissau"),
        new(0x0B6000, 0x0B6FFF, "GUY", "Guyana"),
        new(0x0B8000, 0x0B8FFF, "HTI", "Haiti"),
        new(0x0BA000, 0x0BAFFF, "HND", "Honduras"),
        new(0x470000, 0x477FFF, "HUN", "Hungary"),
        new(0x4CC000, 0x4CCFFF, "ISL", "Iceland"),
        new(0x800000, 0x83FFFF, "IND", "India"),
        new(0x8A0000, 0x8A7FFF, "IDN", "Indonesia"),
        new(0x730000, 0x737FFF, "IRN", "Iran (Islamic Republic of)"),
        new(0x728000, 0x72FFFF, "IRQ", "Iraq"),
        new(0x4CA000, 0x4CAFFF, "IRL", "Ireland"),
        new(0x738000, 0x73FFFF, "ISR", "Israel"),
        new(0x300000, 0x33FFFF, "ITA", "Italy"),
        new(0x0BE000, 0x0BEFFF, "JAM", "Jamaica"),
        new(0x840000, 0x87FFFF, "JPN", "Japan"),
        new(0x740000, 0x747FFF, "JOR", "Jordan"),
        new(0x683000, 0x6837FF, "KAZ", "Kazakhstan"),
        new(0x04C000, 0x04CFFF, "KEN", "Kenya"),
        new(0xC8E000, 0xC8E7FF, "KIR", "Kiribati"),
        new(0x706000, 0x706FFF, "KWT", "Kuwait"),
        new(0x601000, 0x6017FF, "KGZ", "Kyrgyzstan"),
        new(0x708000, 0x708FFF, "LAO", "Lao People's Democratic Republic"),
        new(0x502800, 0x502FFF, "LVA", "Latvia"),
        new(0x748000, 0x74FFFF, "LBN", "Lebanon"),
        new(0x04A000, 0x04A7FF, "LSO", "Lesotho"),
        new(0x050000, 0x050FFF, "LBR", "Liberia"),
        new(0x018000, 0x01FFFF, "LBY", "Libya"),
        new(0x503800, 0x503FFF, "LTU", "Lithuania"),
        new(0x4D0000, 0x4D07FF, "LUX", "Luxembourg"),
        new(0x054000, 0x054FFF, "MDG", "Madagascar"),
        new(0x058000, 0x058FFF, "MWI", "Malawi"),
        new(0x750000, 0x757FFF, "MYS", "Malaysia"),
        new(0x05A000, 0x05A7FF, "MDV", "Maldives"),
        new(0x05C000, 0x05CFFF, "MLI", "Mali"),
        new(0x4D2000, 0x4D27FF, "MLT", "Malta"),
        new(0x900000, 0x9007FF, "MHL", "Marshall Islands"),
        new(0x05E000, 0x05E7FF, "MRT", "Mauritania"),
        new(0x060000, 0x0607FF, "MUS", "Mauritius"),
        new(0x0D0000, 0x0D7FFF, "MEX", "Mexico"),
        new(0x681000, 0x6817FF, "FSM", "Micronesia (Federated States of)"),
        new(0x4D4000, 0x4D47FF, "MCO", "Monaco"),
        new(0x682000, 0x6827FF, "MNG", "Mongolia"),
        new(0x516000, 0x5167FF, "MNE", "Montenegro"),
        new(0x020000, 0x027FFF, "MAR", "Morocco"),
        new(0x006000, 0x006FFF, "MOZ", "Mozambique"),
        new(0x704000, 0x704FFF, "MMR", "Myanmar"),
        new(0x201000, 0x2017FF, "NAM", "Namibia"),
        new(0xC8A000, 0xC8A7FF, "NRU", "Nauru"),
        new(0x70A000, 0x70AFFF, "NPL", "Nepal"),
        new(0x480000, 0x487FFF, "NLD", "Netherlands"),
        new(0xC80000, 0xC87FFF, "NZL", "New Zealand"),
        new(0x0C0000, 0x0C0FFF, "NIC", "Nicaragua"),
        new(0x062000, 0x062FFF, "NER", "Niger"),
        new(0x064000, 0x064FFF, "NGA", "Nigeria"),
        new(0x512000, 0x5127FF, "MKD", "North Macedonia"),
        new(0x478000, 0x47FFFF, "NOR", "Norway"),
        new(0x70C000, 0x70C7FF, "OMN", "Oman"),
        new(0x760000, 0x767FFF, "PAK", "Pakistan"),
        new(0x684000, 0x6847FF, "PLW", "Palau"),
        new(0x0C2000, 0x0C2FFF, "PAN", "Panama"),
        new(0x898000, 0x898FFF, "PNG", "Papua New Guinea"),
        new(0xE88000, 0xE88FFF, "PRY", "Paraguay"),
        new(0xE8C000, 0xE8CFFF, "PER", "Peru"),
        new(0x758000, 0x75FFFF, "PHL", "Philippines"),
        new(0x488000, 0x48FFFF, "POL", "Poland"),
        new(0x490000, 0x497FFF, "PRT", "Portugal"),
        new(0x06A000, 0x06AFFF, "QAT", "Qatar"),
        new(0x718000, 0x71FFFF, "KOR", "Republic of Korea"),
        new(0x504800, 0x504FFF, "MDA", "Republic of Moldova"),
        new(0x4A0000, 0x4A7FFF, "ROU", "Romania"),
        new(0x100000, 0x1FFFFF, "RUS", "Russian Federation"),
        new(0x06E000, 0x06EFFF, "RWA", "Rwanda"),
        new(0xC93000, 0xC937FF, "KNA", "Saint Kitts and Nevis"),
        new(0xC8C000, 0xC8C7FF, "LCA", "Saint Lucia"),
        new(0x0BC000, 0x0BC7FF, "VCT", "Saint Vincent and the Grenadines"),
        new(0x902000, 0x9027FF, "WSM", "Samoa"),
        new(0x500000, 0x5007FF, "SMR", "San Marino"),
        new(0x09E000, 0x09E7FF, "STP", "Sao Tome and Principe"),
        new(0x710000, 0x717FFF, "SAU", "Saudi Arabia"),
        new(0x070000, 0x070FFF, "SEN", "Senegal"),
        new(0x4C0000, 0x4C7FFF, "SRB", "Serbia"),
        new(0x074000, 0x0747FF, "SYC", "Seychelles"),
        new(0x076000, 0x0767FF, "SLE", "Sierra Leone"),
        new(0x768000, 0x76FFFF, "SGP", "Singapore"),
        new(0x505800, 0x505FFF, "SVK", "Slovakia"),
        new(0x506800, 0x506FFF, "SVN", "Slovenia"),
        new(0x897000, 0x8977FF, "SLB", "Solomon Islands"),
        new(0x078000, 0x078FFF, "SOM", "Somalia"),
        new(0x008000, 0x00FFFF, "ZAF", "South Africa"),
        new(0xC94000, 0xC947FF, "SSD", "South Sudan"),
        new(0x340000, 0x37FFFF, "ESP", "Spain"),
        new(0x770000, 0x777FFF, "LKA", "Sri Lanka"),
        new(0x07C000, 0x07CFFF, "SDN", "Sudan"),
        new(0x0C8000, 0x0C8FFF, "SUR", "Suriname"),
        new(0x4A8000, 0x4AFFFF, "SWE", "Sweden"),
        new(0x4B0000, 0x4B7FFF, "CHE", "Switzerland"),
        new(0x778000, 0x77FFFF, "SYR", "Syrian Arab Republic"),
        new(0x515000, 0x5157FF, "TJK", "Tajikistan"),
        new(0x880000, 0x887FFF, "THA", "Thailand"),
        new(0xC95000, 0xC957FF, "TLS", "Timor-Leste"),
        new(0x088000, 0x088FFF, "TGO", "Togo"),
        new(0xC8D000, 0xC8D7FF, "TON", "Tonga"),
        new(0x0C6000, 0x0C6FFF, "TTO", "Trinidad and Tobago"),
        new(0x028000, 0x02FFFF, "TUN", "Tunisia"),
        new(0x4B8000, 0x4BFFFF, "TUR", "Türkiye"),
        new(0x601800, 0x601FFF, "TKM", "Turkmenistan"),
        new(0xC97000, 0xC977FF, "TUV", "Tuvalu"),
        new(0x068000, 0x068FFF, "UGA", "Uganda"),
        new(0x508000, 0x50FFFF, "UKR", "Ukraine"),
        new(0x896000, 0x896FFF, "ARE", "United Arab Emirates"),
        new(0x400000, 0x43FFFF, "GBR", "United Kingdom"),
        new(0x080000, 0x080FFF, "TZA", "United Republic of Tanzania"),
        new(0xA00000, 0xAFFFFF, "USA", "United States"),
        new(0xE90000, 0xE90FFF, "URY", "Uruguay"),
        new(0x507800, 0x507FFF, "UZB", "Uzbekistan"),
        new(0xC90000, 0xC907FF, "VUT", "Vanuatu"),
        new(0x0D8000, 0x0DFFFF, "VEN", "Venezuela (Bolivarian Republic of)"),
        new(0x888000, 0x88FFFF, "VNM", "Viet Nam"),
        new(0x890000, 0x890FFF, "YEM", "Yemen"),
        new(0x08A000, 0x08AFFF, "ZMB", "Zambia"),
        new(0x004000, 0x0047FF, "ZWE", "Zimbabwe"),
        new(0xF00000, 0xF07FFF, "", "Temporary ICAO"),
        new(0x899000, 0x8997FF, "", "Special ICAO1"),
        new(0xF09000, 0xF097FF, "", "Special ICAO2"),
    };

    private Icao24CountryRange _icao24Country = new(0, 0, "", "Unknown");

    public static Icao24CountryRange ResolveIcao24Country(uint cap)
    {
        if (cap > 0xFFFFFFu)
            throw new ArgumentOutOfRangeException(nameof(cap), cap, "ICAO address must be a 24-bit value.");

        foreach (var range in Icao24CountryRanges)
        {
            if (cap >= range.From && cap <= range.To)
                return range;
        }

        return new Icao24CountryRange(cap, cap, "", "Unknown");
    }
    
    protected abstract int FormatLength { get; }

    public abstract byte FormatId { get; }

    public uint IcaoAddress
    {
        get;
        set
        {
            field = value;
            _icao24Country = ResolveIcao24Country(value);
        }
    }

    public string CountryId => _icao24Country.CountryId;
    public string CountryName => _icao24Country.DisplayName;
    
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


public record Icao24CountryRange(
    uint From,
    uint To,
    string CountryId,      // ISO 3166-1 alpha-3 / UN M49
    string DisplayName);   // Separate display name; do not use it as a key