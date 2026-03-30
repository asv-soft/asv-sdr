using System;

namespace Asv.Sdr;

public class ModeSUF5 : ModeSUFormatBase
{
    protected override int FormatLength => 7;
    public override byte FormatId => 5;

    public byte PC { get; set; }
    public byte RR { get; set; }
    public byte DI { get; set; }
    public ushort SD { get; set; }
    
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
    /// TCS, the 3-bit (21-23) type control subfield in SD shall control the extended squitter airborne and surface format
    /// types reported by the transponder and its response to Mode A/C, Mode A/C/S all-call and Mode S-only all-call
    /// interrogations. The following codes have been assigned:
    /// 0 signifies no surface format types or reply inhibit command
    /// 1 signifies surface format types for the next 15 seconds (see 3.1.2.6.1.4.2)
    /// 2 signifies surface format types for the next 60 seconds (see 3.1.2.6.1.4.3)
    /// 3 signifies cancel surface format types and reply inhibit commands
    /// 4-7 reserved
    /// </summary>
    public byte? TypeControl { get; set; }

    /// <summary>
    /// RCS, the 3-bit (24-26) rate control subfield in SD shall control the squitter rate of the transponder when it is
    /// reporting the extended squitter surface type formats. This subfield shall have no effect on the transponder squitter
    /// rate when it is reporting the extended squitter airborne type formats. The following codes have been assigned: 
    /// 0 signifies no surface extended squitter rate command;
    /// 1 signifies report high surface extended squitter rate for 60 seconds;
    /// 2 signifies report low surface extended squitter rate for 60 seconds;
    /// 3-7 reserved. 
    /// </summary>
    public byte? RateControl { get; set; }

    /// <summary>
    /// SAS, the 2-bit (27-28) surface antenna subfield in SD shall control the selection of the transponder diversity antenna
    /// that is used for (1) the extended squitter when the transponder is reporting the surface type formats, and (2) the
    /// acquisition squitter when the transponder is reporting the on-the-ground status. This subfield shall have no effect on
    /// the transponder diversity antenna selection when it is reporting the airborne status. The following codes have been
    /// assigned: 
    /// 0 signifies no antenna command
    /// 1 signifies alternate top and bottom antennas for 120 seconds
    /// 2 signifies use bottom antenna for 120 seconds
    /// 3 signifies return to the defaul
    /// </summary>
    public byte? SurfaceAntenna { get; set; }
    
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
        SD = (ushort)ModeSHelper.GetBitU(buffer, ref pos, 16);

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
            case 2:
                TypeControl = (byte?)((SD & 0xE00) >> 9);
                RateControl = (byte?)((SD & 0x1C0) >> 6);
                SurfaceAntenna = (byte?)((SD & 0x30) >> 4);
                break;
            case 3:
                SurveillanceIdentifier = (byte?)((SD & 0xFC00) >> 10);
                LockoutSurveillance = (SD & 0x200) != 0;
                ReplyRequest = (byte)((SD & 0x1E0) >> 5);
                OverlayCommand = (SD & 0x10) != 0;
                break;
            case 7:
                InterrogatorIdentifier = (byte?)((SD & 0xF000) >> 12);
                ReplyRequest = (byte)((SD & 0xF00) >> 8);
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
        var interrogatorIdentifier = InterrogatorIdentifier ?? 0;
        var overlayCommand = OverlayCommand != null ? (OverlayCommand.Value ? 1 : 0) : 0;
        var multisiteCommB = MultisiteCommB != null ? (byte)MultisiteCommB.Value : 0;
        var multisiteELM = MultisiteELM != null ? (byte)MultisiteELM.Value : 0;
        var lockout = Lockout != null ? (Lockout.Value ? 1 : 0) : 0;
        var reservationStatus = ReservationStatus != null ? (byte)ReservationStatus.Value : 0;
        var tacticalMessage = TacticalMessage ?? 0;
        SD = 0;
        switch (DI)
        {
            case 0:
                SD = (ushort)(SD | 
                              ((interrogatorIdentifier & 0xF) << 12) | 
                              (overlayCommand << 4));
                break;
            case 1:
                SD = (ushort)(SD | 
                              ((interrogatorIdentifier & 0xF) << 12) | 
                              ((multisiteCommB & 0x3) << 10) |
                              ((multisiteELM & 0x7) << 7) | 
                              (lockout << 6) | 
                              ((reservationStatus & 0x3) << 4) |
                              (tacticalMessage & 0xF));
                break;
            case 2:
                var typeControl = TypeControl ?? 0;
                var rateControl = RateControl ?? 0;
                var surfaceAntenna = SurfaceAntenna ?? 0;
                SD = (ushort)(SD | 
                              ((typeControl & 0x7) << 9) | 
                              ((rateControl & 0x7) << 6) | 
                              ((surfaceAntenna & 0x3) << 4));
                break;
            case 3:
                var surveillanceIdentifier = SurveillanceIdentifier ?? 0;
                var lockoutSurveillance = LockoutSurveillance != null ? LockoutSurveillance.Value ? 1 : 0 : 0;
                SD = (ushort)(SD | 
                              ((surveillanceIdentifier & 0x3F) << 10) | 
                              (lockoutSurveillance << 9) | 
                              ((BDS2 & 0xF) << 5) | 
                              (overlayCommand << 4));
                break;
            case 7:
                SD = (ushort)(SD | 
                              ((interrogatorIdentifier & 0xF) << 12) | 
                              ((BDS2 & 0xF) << 8) | 
                              (lockout << 6) | 
                              (overlayCommand << 4) | 
                              (tacticalMessage & 0xF));
                break;
        }
        ModeSHelper.SetBitU(buffer, ref pos, 16, SD);
    }
}

public class ModeSDF5 : ModeSDFormatBase
{
    protected override int FormatLength => 7;
    public override byte FormatId => 5;

    public byte FS { get; set; } = 0x0;
    public byte DR { get; set; } = 0x0;
    public byte UM { get; set; } = 0x0;
    private ushort ID { get; set; } = ModeSHelper.SetSquawk("0777");

    
    public string Squawk
    {
        get => ModeSHelper.GetSquawk(ID);
        set => ID = ModeSHelper.SetSquawk(value);
    }

    
    protected override void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos)
    {
        FS = (byte)ModeSHelper.GetBitU(buffer, ref pos, 3);
        DR = (byte)ModeSHelper.GetBitU(buffer, ref pos, 5);
        UM = (byte)ModeSHelper.GetBitU(buffer, ref pos, 6);
        ID = (ushort)ModeSHelper.GetBitU(buffer, ref pos, 13);
    }

    protected override void InternalSerialize(Span<byte> buffer, ref int pos)
    {
        ModeSHelper.SetBitU(buffer, ref pos, 3, FS);
        ModeSHelper.SetBitU(buffer, ref pos, 5, DR);
        ModeSHelper.SetBitU(buffer, ref pos, 6, UM);
        ModeSHelper.SetBitU(buffer, ref pos, 13, ID);
    }
    
    
}