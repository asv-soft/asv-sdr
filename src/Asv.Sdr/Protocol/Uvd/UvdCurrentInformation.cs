namespace Asv.Sdr;

/// <summary>TI fields from P1..P20, independent of the coordinate distress indication.</summary>
public sealed class UvdCurrentInformation
{
    internal UvdCurrentInformation(uint informationWord, int? altitudeMeters, byte? fuelRemainingPercent)
    {
        RawAltitudeCode = (ushort)(informationWord & 0x3FFF);
        AltitudeMeters = altitudeMeters;
        IsAbsoluteAltitude = (informationWord & 0x4000) != 0;
        Emergency = (informationWord & 0x8000) != 0;
        FuelCode = (byte)(informationWord >> 16);
        FuelRemainingPercent = fuelRemainingPercent;
    }

    public ushort RawAltitudeCode { get; }
    /// <summary>Decoded BCD height in metres; null when the altitude digits are invalid.</summary>
    public int? AltitudeMeters { get; }
    public bool IsAbsoluteAltitude { get; }
    public bool Emergency { get; }
    public byte FuelCode { get; }
    /// <summary>SO-2010 Table 4 fuel gradation; code zero means not transmitted, not zero percent.</summary>
    public byte? FuelRemainingPercent { get; }
}
