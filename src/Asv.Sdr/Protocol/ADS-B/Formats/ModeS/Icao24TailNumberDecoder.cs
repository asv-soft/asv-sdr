using System;

namespace Asv.Sdr;

/// <summary>
/// Quality of an ICAO24 -> registration conversion.
/// </summary>
public enum TailNumberDecodeStatus
{
    /// <summary>
    /// A one-to-one national encoding is known for this sub-range.
    /// The current registry should still be used to confirm actual assignment.
    /// </summary>
    DeterministicSubset,

    /// <summary>
    /// A de-facto/reverse-engineered allocation pattern produced a candidate.
    /// Registry validation is mandatory.
    /// </summary>
    PatternCandidate,

    /// <summary>
    /// The address belongs to a known country, but no supported arithmetic
    /// conversion applies to this address.
    /// </summary>
    RegistryLookupRequired,

    /// <summary>
    /// The address is outside the country blocks implemented here.
    /// </summary>
    UnsupportedCountry
}

public sealed record TailNumberDecodeResult(
    uint Icao24,
    string Country,
    string CountryDisplayName,
    string? Registration,
    TailNumberDecodeStatus Status,
    string Scheme)
{
    public bool HasRegistration => Registration is not null;
}

public record Icao24CountryRange(
    uint From,
    uint To,
    string CountryId,      // ISO 3166-1 alpha-3 / UN M49
    string DisplayName);   // Separate display name; do not use it as a key

/// <summary>
/// ICAO 24-bit aircraft address to registration/tail-number decoder.
///
/// Important: a country allocation block is not, by itself, a universal tail-number
/// encoding. The methods below intentionally return RegistryLookupRequired outside
/// the specific, known sub-ranges/patterns.
/// </summary>
public static class Icao24TailNumberDecoder
{
    private const string Alphabet26 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Alphabet36 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string UsAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // I and O omitted.

    // Polish three-letter civil allocation order used by the Mode-S pattern.
    // Q and V are omitted; X and Y were placed after Z in this allocation table.
    private const string PolishAlphabet = "ABCDEFGHIJKLMNOPRSTUWZXY";

    private static readonly StrideMap[] FrenchMaps =
    {
        new(0x380000, 1024, 32, "F-B", "AAA", "ZZZ"),
        new(0x388000, 1024, 32, "F-I", "AAA", "ZZZ"),
        new(0x390000, 1024, 32, "F-G", "AAA", "ZZZ"),
        new(0x398000, 1024, 32, "F-H", "AAA", "ZZZ"),
        new(0x3A0000, 1024, 32, "F-O", "AAA", "ZZZ")
    };

    private static readonly StrideMap[] GermanMaps =
    {
        new(0x3C4421, 1024, 32, "D-A", "AAA", "OZZ"),
        new(0x3C0001,  676, 26, "D-A", "PAA", "ZZZ"),
        new(0x3C8421, 1024, 32, "D-B", "AAA", "OZZ"),
        new(0x3C2001,  676, 26, "D-B", "PAA", "ZZZ"),
        new(0x3CC000,  676, 26, "D-C", "AAA", "ZZZ"),
        new(0x3D04A8,  676, 26, "D-E", "AAA", "ZZZ"),
        new(0x3D4950,  676, 26, "D-F", "AAA", "ZZZ"),
        new(0x3D8DF8,  676, 26, "D-G", "AAA", "ZZZ"),
        new(0x3DD2A0,  676, 26, "D-H", "AAA", "ZZZ"),
        new(0x3E1748,  676, 26, "D-I", "AAA", "ZZZ")
    };
    
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

    /// <summary>
    /// Decode an ICAO 24-bit address. The returned status must be inspected;
    /// PatternCandidate is not authoritative registry data.
    /// </summary>
    public static TailNumberDecodeResult Decode(uint icao24)
    {
        if (icao24 > 0xFFFFFF)
            throw new ArgumentOutOfRangeException(nameof(icao24), "ICAO address must contain no more than 24 bits.");

        foreach (var range in Icao24CountryRanges)
        {
            if (icao24 >= range.From && icao24 <= range.To)
            {
                return range.CountryId switch
                {
                    "RUS" => DecodeRussia(icao24),
                    "USA" => DecodeUnitedStates(icao24),
                    "BEL" => DecodeBelgium(icao24),
                    "CAN" => DecodeCanada(icao24),
                    "DNK" => DecodeDenmark(icao24),
                    "FRA" => DecodeFrance(icao24),
                    "DEU" => DecodeGermany(icao24),
                    "POL" => DecodePoland(icao24),
                    "CHE" => DecodeSwitzerland(icao24),
                    "SWE" => DecodeSweden(icao24),
                    "KOR" => DecodeSouthKorea(icao24),
                    "AUS" => DecodeAustralia(icao24),
                    _ => new TailNumberDecodeResult(
                        icao24,
                        range.CountryId,
                        range.DisplayName,
                        null,
                        TailNumberDecodeStatus.UnsupportedCountry,
                        "No decoder for this country block.")
                };
            }
        }

        return new TailNumberDecodeResult(
            icao24,
            string.Empty,
            string.Empty,
            null,
            TailNumberDecodeStatus.UnsupportedCountry,
            "No decoder for this country block.");
    }

    /// <summary>
    /// Convenience wrapper. It returns both deterministic results and pattern
    /// candidates; use Decode() when the confidence/status matters.
    /// </summary>
    public static bool TryDecode(uint icao24, out string registration)
    {
        var result = Decode(icao24);
        registration = result.Registration ?? string.Empty;
        return result.HasRegistration;
    }

    private static TailNumberDecodeResult DecodeRussia(uint address)
    {
        // Numeric Russian civil series: RA-00000 ... RA-99999.
        const uint start = 0x140000;
        const uint count = 100_000;

        if (address >= start && address < start + count)
        {
            var number = address - start;
            return Hit(
                address,
                "RUS",
                "Russian Federation",
                $"RA-{number:D5}",
                TailNumberDecodeStatus.DeterministicSubset,
                "RA-xxxxx numeric subset: registration number = ICAO24 - 0x140000.");
        }

        return Lookup(address, "RUS", "Russian Federation",
            "No universal inverse for the rest of 0x100000-0x1FFFFF (letter-suffix, state, temporary and special allocations).");
    }

    private static TailNumberDecodeResult DecodeUnitedStates(uint address)
    {
        var registration = DecodeUsNNumber(address);
        return registration is not null
            ? Hit(address, "USA", "United States", registration, TailNumberDecodeStatus.DeterministicSubset,
                "FAA standard N-number enumeration, 0xA00001-0xADF7C7.")
            : Lookup(address, "USA", "United States",
                "Outside the standard N-number arithmetic range; use the FAA registry (including special/alternative allocations).");
    }

    private static TailNumberDecodeResult DecodeBelgium(uint address)
    {
        return TryDecodePacked5OneBased(address, 0x448000, "OO-", out var registration)
            ? Hit(address, "BEL", "Belgium", registration, TailNumberDecodeStatus.PatternCandidate,
                "Three-letter OO-xxx series, 5-bit fields with A=1 ... Z=26.")
            : Lookup(address, "BEL", "Belgium", "No supported three-letter OO-xxx mapping for this address.");
    }

    private static TailNumberDecodeResult DecodeCanada(uint address)
    {
        const uint start = 0xC00001;
        const uint blockSize = 26 * 26 * 26; // 17,576
        const uint total = 3 * blockSize;

        if (address >= start && address < start + total)
        {
            var value = address - start;
            var prefixIndex = (int)(value / blockSize);
            var suffixValue = value % blockSize;
            var secondPrefixCharacter = "FGI"[prefixIndex];
            var registration = $"C-{secondPrefixCharacter}{DecodeFixedBase(suffixValue, Alphabet26, 3)}";

            return Hit(address, "CAN", "Canada", registration, TailNumberDecodeStatus.DeterministicSubset,
                "Canadian C-Fxxx, C-Gxxx and C-Ixxx base-26 blocks.");
        }

        return Lookup(address, "CAN", "Canada", "Outside the arithmetic C-F/C-G/C-I civil registration blocks.");
    }

    private static TailNumberDecodeResult DecodeDenmark(uint address)
    {
        return TryDecodePacked5OneBased(address, 0x458000, "OY-", out var registration)
            ? Hit(address, "DNK", "Denmark", registration, TailNumberDecodeStatus.PatternCandidate,
                "Three-letter OY-xxx series, 5-bit fields with A=1 ... Z=26.")
            : Lookup(address, "DNK", "Denmark", "No supported three-letter OY-xxx mapping for this address.");
    }

    private static TailNumberDecodeResult DecodeFrance(uint address)
    {
        if (TryDecodeStrideMaps(address, FrenchMaps, out var registration))
        {
            return Hit(address, "FRA", "France", registration, TailNumberDecodeStatus.PatternCandidate,
                "French F-B/F-I/F-G/F-H/F-O four-letter series, three 5-bit suffix fields.");
        }

        return Lookup(address, "FRA", "France", "No supported French arithmetic sub-range for this address.");
    }

    private static TailNumberDecodeResult DecodeGermany(uint address)
    {
        if (TryDecodeStrideMaps(address, GermanMaps, out var registration))
        {
            return Hit(address, "DEU", "Germany", registration, TailNumberDecodeStatus.PatternCandidate,
                "German D-A/D-B/D-C/D-E/D-F/D-G/D-H/D-I allocation tables.");
        }

        return Lookup(address, "DEU", "Germany", "No supported German arithmetic sub-range for this address.");
    }

    private static TailNumberDecodeResult DecodePoland(uint address)
    {
        if (TryDecodePacked5ZeroBased(address, 0x488000, "SP-", PolishAlphabet, out var registration))
        {
            return Hit(address, "POL", "Poland", registration, TailNumberDecodeStatus.PatternCandidate,
                "Three-letter SP-xxx pattern with custom 24-character allocation alphabet; registry validation is mandatory.");
        }

        return Lookup(address, "POL", "Poland", "No supported three-letter SP-xxx pattern. Four-letter, military and individually assigned addresses require registry data.");
    }

    private static TailNumberDecodeResult DecodeSwitzerland(uint address)
    {
        const uint start = 0x4B0000;
        const uint count = 26 * 26 * 26;

        if (address >= start && address < start + count)
        {
            var registration = "HB-" + DecodeFixedBase(address - start, Alphabet26, 3);
            return Hit(address, "CHE", "Switzerland", registration, TailNumberDecodeStatus.PatternCandidate,
                "Three-letter HB-xxx base-26 block.");
        }

        return Lookup(address, "CHE", "Switzerland", "Outside the arithmetic three-letter HB-xxx block.");
    }

    private static TailNumberDecodeResult DecodeSweden(uint address)
    {
        return TryDecodePacked5OneBased(address, 0x4A8000, "SE-", out var registration)
            ? Hit(address, "SWE", "Sweden", registration, TailNumberDecodeStatus.PatternCandidate,
                "Three-letter SE-xxx series, 5-bit fields with A=1 ... Z=26.")
            : Lookup(address, "SWE", "Sweden", "No supported three-letter SE-xxx mapping for this address.");
    }

    private static TailNumberDecodeResult DecodeSouthKorea(uint address)
    {
        // Highest commonly encoded civil registration in the known BCD layout is HL9699.
        if (address <= 0x71CE99)
        {
            var digit0 = (int)((address & 0x007800) >> 11);
            var digit1 = (int)((address & 0x000700) >> 8);
            var digit2 = (int)((address & 0x0000F0) >> 4);
            var digit3 = (int)(address & 0x00000F);

            if (digit0 <= 9 && digit2 <= 9 && digit3 <= 9)
            {
                var registration = $"HL{digit0}{digit1}{digit2}{digit3}";
                return Hit(address, "KOR", "South Korea", registration, TailNumberDecodeStatus.PatternCandidate,
                    "Modified-BCD Korean HLdddd pattern; confirm that the decoded number is an assigned registration series.");
            }
        }

        return Lookup(address, "KOR", "South Korea", "No valid modified-BCD HLdddd candidate for this address.");
    }

    private static TailNumberDecodeResult DecodeAustralia(uint address)
    {
        const uint start = 0x7C0000;
        const uint count = 36 * 36 * 36; // 46,656

        if (address >= start && address < start + count)
        {
            var registration = "VH-" + DecodeFixedBase(address - start, Alphabet36, 3);
            return Hit(address, "AUS", "Australia", registration, TailNumberDecodeStatus.PatternCandidate,
                "Three-character VH-xxx base-36 sequence (A-Z, then 0-9).");
        }

        return Lookup(address, "AUS", "Australia", "Outside the arithmetic VH-xxx base-36 block.");
    }

    private static string? DecodeUsNNumber(uint address)
    {
        const uint start = 0xA00001;
        const uint count = 915_399;

        const int digit1Block = 101_711;
        const int digit2Block = 10_111;
        const int digit3Block = 951;
        const int digit4Block = 35;
        const int letterSuffixBlock = 601;

        if (address < start || address >= start + count)
            return null;

        var value = checked((int)(address - start));
        var registration = "N";

        registration += (value / digit1Block + 1).ToString();
        value %= digit1Block;

        if (value <= 600)
            return registration + DecodeUsLetters(value);

        value -= letterSuffixBlock;
        registration += (value / digit2Block).ToString();
        value %= digit2Block;

        if (value <= 600)
            return registration + DecodeUsLetters(value);

        value -= letterSuffixBlock;
        registration += (value / digit3Block).ToString();
        value %= digit3Block;

        if (value <= 600)
            return registration + DecodeUsLetters(value);

        value -= letterSuffixBlock;
        registration += (value / digit4Block).ToString();
        value %= digit4Block;

        if (value <= 24)
            return registration + DecodeUsLetter(value);

        value -= 25;
        return registration + value.ToString();
    }

    private static string DecodeUsLetter(int value)
    {
        if (value == 0)
            return string.Empty;

        if (value < 0 || value > UsAlphabet.Length)
            throw new ArgumentOutOfRangeException(nameof(value));

        return UsAlphabet[value - 1].ToString();
    }

    private static string DecodeUsLetters(int value)
    {
        if (value == 0)
            return string.Empty;

        if (value < 0 || value > 600)
            throw new ArgumentOutOfRangeException(nameof(value));

        value--;
        var first = UsAlphabet[value / 25];
        var second = DecodeUsLetter(value % 25);
        return first + second;
    }

    private static bool TryDecodePacked5OneBased(
        uint address,
        uint countryBase,
        string prefix,
        out string registration)
    {
        registration = string.Empty;
        var value = address - countryBase;

        var c0 = (int)((value >> 10) & 0x1F);
        var c1 = (int)((value >> 5) & 0x1F);
        var c2 = (int)(value & 0x1F);

        if (!InRange(c0, 1, 26) || !InRange(c1, 1, 26) || !InRange(c2, 1, 26))
            return false;

        registration = prefix +
                       (char)('A' + c0 - 1) +
                       (char)('A' + c1 - 1) +
                       (char)('A' + c2 - 1);
        return true;
    }

    private static bool TryDecodePacked5ZeroBased(
        uint address,
        uint countryBase,
        string prefix,
        string alphabet,
        out string registration)
    {
        registration = string.Empty;
        var value = address - countryBase;

        var c0 = (int)((value >> 10) & 0x1F);
        var c1 = (int)((value >> 5) & 0x1F);
        var c2 = (int)(value & 0x1F);

        if (c0 >= alphabet.Length || c1 >= alphabet.Length || c2 >= alphabet.Length)
            return false;

        registration = prefix + alphabet[c0] + alphabet[c1] + alphabet[c2];
        return true;
    }

    private static bool TryDecodeStrideMaps(
        uint address,
        StrideMap[] maps,
        out string registration)
    {
        foreach (var map in maps)
        {
            if (TryDecodeStrideMap(address, map, out registration))
                return true;
        }

        registration = string.Empty;
        return false;
    }

    private static bool TryDecodeStrideMap(
        uint address,
        StrideMap map,
        out string registration)
    {
        registration = string.Empty;

        var firstValue = EncodeSuffix(map.First, map.Stride1, map.Stride2);
        var lastValue = EncodeSuffix(map.Last, map.Stride1, map.Stride2);
        var end = (long)map.Start - firstValue + lastValue;

        if (address < map.Start || address > end)
            return false;

        var value = (long)address - map.Start + firstValue;
        var c0 = (int)(value / map.Stride1);
        value %= map.Stride1;
        var c1 = (int)(value / map.Stride2);
        var c2 = (int)(value % map.Stride2);

        if (!InRange(c0, 0, 25) || !InRange(c1, 0, 25) || !InRange(c2, 0, 25))
            return false;

        registration = map.Prefix +
                       (char)('A' + c0) +
                       (char)('A' + c1) +
                       (char)('A' + c2);
        return true;
    }

    private static int EncodeSuffix(string suffix, int stride1, int stride2)
    {
        if (suffix.Length != 3)
            throw new ArgumentException("Suffix must contain exactly three letters.", nameof(suffix));

        return LetterIndex(suffix[0]) * stride1 +
               LetterIndex(suffix[1]) * stride2 +
               LetterIndex(suffix[2]);
    }

    private static int LetterIndex(char value)
    {
        var index = value - 'A';
        if (!InRange(index, 0, 25))
            throw new ArgumentOutOfRangeException(nameof(value), "Expected A-Z.");
        return index;
    }

    private static string DecodeFixedBase(uint value, string alphabet, int width)
    {
        var numberBase = (uint)alphabet.Length;
        var result = new char[width];

        for (var index = width - 1; index >= 0; index--)
        {
            result[index] = alphabet[(int)(value % numberBase)];
            value /= numberBase;
        }

        if (value != 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Value does not fit into the requested width.");

        return new string(result);
    }

    private static TailNumberDecodeResult Hit(
        uint address,
        string country,
        string countryDisplayName,
        string registration,
        TailNumberDecodeStatus status,
        string scheme)
    {
        return new TailNumberDecodeResult(address, country, countryDisplayName, registration, status, scheme);
    }

    private static TailNumberDecodeResult Lookup(uint address, string country, string countryDisplayName, string reason)
    {
        return new TailNumberDecodeResult(
            address,
            country,
            countryDisplayName,
            null,
            TailNumberDecodeStatus.RegistryLookupRequired,
            reason);
    }

    private static bool InRange(uint value, uint minimum, uint maximum) =>
        value >= minimum && value <= maximum;

    private static bool InRange(int value, int minimum, int maximum) =>
        value >= minimum && value <= maximum;

    private readonly struct StrideMap
    {
        public StrideMap(
            uint start,
            int stride1,
            int stride2,
            string prefix,
            string first,
            string last)
        {
            Start = start;
            Stride1 = stride1;
            Stride2 = stride2;
            Prefix = prefix;
            First = first;
            Last = last;
        }

        public uint Start { get; }
        public int Stride1 { get; }
        public int Stride2 { get; }
        public string Prefix { get; }
        public string First { get; }
        public string Last { get; }
    }
}