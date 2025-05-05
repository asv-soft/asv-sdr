using System;

namespace Asv.Sdr;

public class ModeSUF4 : ModeSUFormatBase
{
    protected override int FormatLength => 7;
    public override byte FormatId => 4;

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

public enum ReservationStatusEnum
{
    NoRequest,
    ReportCommBReservationStatusInUM,
    ReportCommCReservationStatusInUM,
    ReportCommDReservationStatusInUM
}

public enum MultisiteELMEnum
{
    NoELMAction,
    CommCReservation,
    CommCCloseout,
    CommDReservation,
    CommDCloseout,
    CommCReservationAndCommDCloseout,
    CommCCloseoutAndCommDReservation,
    CommCAndCommDCloseouts
}

public enum MultisiteCommBEnum
{
    NoCommBAction,
    CommBReservation,
    CommBCloseout
}

public class ModeSDF4 : ModeSDFormatBase
{
    protected override int FormatLength => 7;
    public override byte FormatId => 4;

    public byte FS { get; set; } = 0x0;
    public byte DR { get; set; } = 0x0;
    public byte UM { get; set; } = 0x0;
    private ushort AC { get; set; } = (ushort)SetAltitude(0.0);

    public double Altitude
    {
        get => GetAltitude(AC);
        set => AC = (ushort)SetAltitude(value);
    }


    protected override void InternalDeserialize(ReadOnlySpan<byte> buffer, ref int pos)
    {
        FS = (byte)ModeSHelper.GetBitU(buffer, ref pos, 3);
        DR = (byte)ModeSHelper.GetBitU(buffer, ref pos, 5);
        UM = (byte)ModeSHelper.GetBitU(buffer, ref pos, 6);
        AC = (ushort)ModeSHelper.GetBitU(buffer, ref pos, 13);
    }

    protected override void InternalSerialize(Span<byte> buffer, ref int pos)
    {
        ModeSHelper.SetBitU(buffer, ref pos, 3, FS);
        ModeSHelper.SetBitU(buffer, ref pos, 5, DR);
        ModeSHelper.SetBitU(buffer, ref pos, 6, UM);
        ModeSHelper.SetBitU(buffer, ref pos, 13, AC);
    }
    
    private static double GetAltitude(uint nAlt)
    {
        if (nAlt == 0) return double.NaN;
        
        var m = (nAlt & 0x40) != 0 ? 1 : 0;
        
        if (m == 1) // meter
        {
            return GetAltitudeInMeters(((nAlt & 0x1F80) >> 1) | (nAlt & 0x3F));
        }
        
        var q = (nAlt & 0x10) != 0 ? 1 : 0;
        nAlt = ((nAlt & 0x1F80) >> 2) | ((nAlt & 0x20) >> 1) | (nAlt & 0xF);
        if (q == 1)
        {
            return (nAlt * 25.0 - 1000.0) / 3.28084;
        }
        return GetAltitudeFromModeC((int)nAlt) / 3.28084;
    }

    private static double GetAltitudeFromModeC(int nAlt)
    {
        return 0;
    }
    
    private static int SetAltitudeToModeC(double altitude)
    {
        return 0;
    }

    /// <summary>
    /// Сам придумал этот метод, в Icao пока кодирование в метрах не описано
    /// </summary>
    /// <param name="nAlt"></param>
    /// <returns></returns>
    private static double GetAltitudeInMeters(uint nAlt)
    {
        return nAlt * 4.0 - 304.0;
    }

    private static uint SetAltitude(double alt)
    {
        if (double.IsNaN(alt)) return 0;
    
        alt *= 3.28084;
        var altNorm = alt switch
        {
            > 50187.5 => (int)Math.Round(alt / 100.0, 0) * 100,
            < -1000.0 => -1000,
            _ => (int)Math.Round(alt / 25.0) * 25
        };
    
        if (altNorm > 50175)
        {
            var nAlt = SetAltitudeToModeC(altNorm);
            return (uint)(((nAlt & 0x7E0) << 2) | ((nAlt & 0x10) << 1) | (nAlt & 0xF));
        }
    
        altNorm = (altNorm + 1000) / 25;
        return (uint)(((altNorm & 0x7E0) << 2) | ((altNorm & 0x10) << 1) | 0x10 | (altNorm & 0xF));
    }
}