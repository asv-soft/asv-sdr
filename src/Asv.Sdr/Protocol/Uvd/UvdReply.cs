namespace Asv.Sdr;

/// <summary>
/// Decoded contents of a stored UVD information word. This model does not establish
/// whether a complete information reply was received in the current interrogation batch.
/// </summary>
public sealed class UvdReply
{
    internal UvdReply(UvdRequestCode requestCode, uint informationWord,
        bool coordinateDistress, string? boardNumber, UvdCurrentInformation? currentInformation)
    {
        RequestCode = requestCode;
        InformationWord = informationWord;
        CoordinateDistress = coordinateDistress;
        BoardNumber = boardNumber;
        CurrentInformation = currentInformation;
    }

    public UvdRequestCode RequestCode { get; }

    /// <summary>The original twenty P1..P20 bits; P1 is the least significant bit.</summary>
    public uint InformationWord { get; }

    /// <summary>The distress indication in the coordinate reply, independent of TI bit P16.</summary>
    public bool CoordinateDistress { get; }

    /// <summary>Five BCD digits for ZK1, including leading zeroes; null for other codes or invalid BCD.</summary>
    public string? BoardNumber { get; }
    /// <summary>Decoded TI for ZK2; null for other interrogation codes.</summary>
    public UvdCurrentInformation? CurrentInformation { get; }
}
