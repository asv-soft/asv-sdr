using System;

namespace Asv.Sdr;

public class ModeSUF5 : ModeSUFormatBase
{
    protected override int FormatLength => 7;
    public override byte FormatId => 5;

    public byte PC { get; set; }
    public byte RR { get; set; }
    public byte DI { get; set; }
    public byte SD { get; set; }
    
    public bool IsShortReply { get; set; }
    
    public byte BDS1 { get; set; }

    public byte BDS2 { get; set; } = 0;
    
    /// <summary>
    /// Interrogator Identifier Subfield contains the self-identification
    /// code of the interrogator which is numerically identical to the II code transmitted by the
    /// same interrogator in the Mode S-Only All-Call. IIS codes are assigned to interrogators
    /// and range from 0 through 15; IIS=0 is not a valid interrogator identifier for multisite
    /// purposes.
    /// DI code is 0, 1 or 7
    /// </summary>
    public byte? InterrogatorIdentifier { get; set; }
    
    /// <summary>
    /// Multisite Comm-B Subfield.
    /// DI=1
    /// </summary>
    public MultisiteCommBEnum? MultisiteCommB { get; set; }

    /// <summary>
    /// Multisite ELM Subfield contains reservation and closeout commands for ELM.
    /// DI=1
    /// </summary>
    public MultisiteELMEnum? MultisiteELM { get; set; }
    
    /// <summary>
    /// Reservation Status Subfield can request the transponder to report its reservation status in the UM field.
    /// DI=1
    /// </summary>
    public ReservationStatusEnum? ReservationStatus { get; set; }
    
    /// <summary>
    /// Lockout Subfield, if set to ONE, initiates a multisite All-Call lockout to Mode S-Only All-Calls (UF=11)
    /// from the interrogator indicated in IIS of the same interrogation. If lockout is set to ZERO no change in
    /// lockout state is commanded.
    /// DI code is 1 or 7
    /// </summary>
    public bool? Lockout { get; set; }

    /// <summary>
    /// Tactical message is used for identifying linkage of Comm-A messages, with “0” indicating an unlinked message.
    /// DI code is 1 or 7
    /// </summary>
    public byte? TacticalMessage { get; set; }
    
    /// <summary>
    /// Reply Request (BDS2).
    /// DI code is 1, 3 or 7
    /// </summary>
    public byte? ReplyRequest { get; set; }
    
    /// <summary>
    /// Surveillance identifier subfield in SD contains an assigned SI code of the interrogator.
    /// DI=3
    /// </summary>
    public byte? SurveillanceIdentifier  { get; set; }
    
    /// <summary>
    /// Lockout surveillance subfield if set to TRUE signifies a multisite lockout command from the interrogator
    /// indicated in SIS. LSS set to FALSE is used to signify that no change in lockout state is commanded.
    /// DI=3
    /// </summary>
    public bool? LockoutSurveillance { get; set; }
    
    /// <summary>
    /// If the Overlay Command is equal to “TRUE” then the reply to the interrogation shall contain the
    /// “DP” (Data Parity) field in accordance with §2.2.14.4.12. If the “OVC” is equal to “FALSE”
    /// then the reply to the interrogation shall contain the “AP”.
    /// DI code is 0, 3 or 7
    /// </summary>
    public bool? OverlayCommand { get; set; }

    protected override void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos)
    {
        PC = (byte)ModeSHelper.GetBitU(buffer, ref pos, 3);
        RR = (byte)ModeSHelper.GetBitU(buffer, ref pos, 5);
        if (RR < 16)
        {
            IsShortReply = true;
        }
        else
        {
            IsShortReply = false;
            BDS1 = (byte)(RR & 0xF);
        }
        DI = (byte)ModeSHelper.GetBitU(buffer, ref pos, 3);
        SD = (byte)ModeSHelper.GetBitU(buffer, ref pos, 16);

        switch (DI)
        {
            case 0:
                InterrogatorIdentifier = (byte?)((SD & 0xF000) >> 12);
                OverlayCommand = (SD & 0x10) != 0;
                break;
            case 1:
                InterrogatorIdentifier = (byte?)((SD & 0xF000) >> 12);
                MultisiteCommB = (MultisiteCommBEnum)((SD & 0xC00) >> 10);
                MultisiteELM = (MultisiteELMEnum)((SD & 0x380) >> 7);
                Lockout = (SD & 0x40) != 0;
                ReservationStatus = (ReservationStatusEnum)((SD & 0x30) >> 4);
                TacticalMessage = (byte?)(SD & 0xF);
                break;
            case 3:
                SurveillanceIdentifier = (byte?)((SD & 0xFC00) >> 10);
                LockoutSurveillance = (SD & 0x200) != 0;
                ReplyRequest = BDS2 = (byte)((SD & 0x1E0) >> 5);
                OverlayCommand = (SD & 0x10) != 0;
                break;
            case 7:
                InterrogatorIdentifier = (byte?)((SD & 0xF000) >> 12);
                ReplyRequest = BDS2 = (byte)((SD & 0xF00) >> 8);
                Lockout = (SD & 0x40) != 0;
                OverlayCommand = (SD & 0x10) != 0;
                TacticalMessage = (byte?)(SD & 0xF);
                break;
        }
    }

    protected override void InternalSerialize(Span<byte> buffer, ref int pos)
    {
        ModeSHelper.SetBitU(buffer, ref pos, 3, PC);
        ModeSHelper.SetBitU(buffer, ref pos, 5, RR);
        ModeSHelper.SetBitU(buffer, ref pos, 3, DI);
        ModeSHelper.SetBitU(buffer, ref pos, 16, SD);
    }
}

public class ModeSDF5 : ModeSDFormatBase
{
    protected override int FormatLength => 7;
    public override byte FormatId => 5;

    public byte FS { get; set; } = 0x1;
    public byte DR { get; set; } = 0x0;
    public byte UM { get; set; } = 0x0;
    private ushort ID { get; set; } = SetSquawk(5124); // Squawk 5124

    
    public ushort Squawk
    {
        get => GetSquawk(ID);
        set => ID = SetSquawk(value);
    }

    private static void SetBits(out byte x1, out byte x2, out byte x4, byte value)
    {
        if ((value & 0xF8) != 0) throw new ArgumentOutOfRangeException(nameof(value));
        x1 = (byte)(value & 0x1);
        x2 = (byte)((value & 0x2) >> 1);
        x4 = (byte)((value & 0x4) >> 2);
    }
    private static ushort GetSquawk(ushort id)
    {
        var a = (byte)(((id & (1 << 11)) >> 11) | ((id & (1 << 9)) >> 8) | ((id & (1 << 7)) >> 5));
        var b = (byte)(((id & (1 << 5)) >> 5) | ((id & (1 << 3)) >> 2) | ((id & (1 << 1)) << 1));
        var c = (byte)(((id & (1 << 12)) >> 12) | ((id & (1 << 10)) >> 9) | ((id & (1 << 8)) >> 6));
        var d = (byte)(((id & (1 << 4)) >> 4) | ((id & (1 << 2)) >> 1) | ((id & 1) << 2));

        return (ushort)(a * 1000 + b * 100 + c * 10 + d);
    }
    
    private static ushort SetSquawk(uint squawk)
    {
        var a = (byte)((squawk / 1000) % 10);
        var b = (byte)((squawk / 100) % 10);
        var c = (byte)((squawk / 10) % 10);
        var d = (byte)(squawk % 10);

        SetBits(out var a1, out var a2, out var a4, a);
        SetBits(out var b1, out var b2, out var b4, b);
        SetBits(out var c1, out var c2, out var c4, c);
        SetBits(out var d1, out var d2, out var d4, d);

        return (ushort)((c1 << 12) | (a1 << 11) | (c2 << 10) | (a2 << 9) | (c4 << 8) | (a4 << 7) | (b1 << 5) | (d1 << 4) |
                        (b2 << 3) | (d2 << 2) | (b4 << 1) | d4);
    }

    protected override void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos)
    {
        
    }

    protected override void InternalSerialize(Span<byte> buffer, ref int pos)
    {
        ModeSHelper.SetBitU(buffer, ref pos, 3, FS);
        ModeSHelper.SetBitU(buffer, ref pos, 5, DR);
        ModeSHelper.SetBitU(buffer, ref pos, 6, UM);
        ModeSHelper.SetBitU(buffer, ref pos, 13, ID);
    }
    
    
}