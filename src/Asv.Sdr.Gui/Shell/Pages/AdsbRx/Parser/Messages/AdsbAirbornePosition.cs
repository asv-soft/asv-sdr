using System;

namespace Asv.Sdr.Gui;

public enum AltitudeType
{
    Barometric,
    Gnss
}

public class AdsbAirbornePosition : AdsbExtendedSquitterBase
{
    /// <summary>
    /// When Type Code is from 9 to 18, the encoded altitude represents the barometric altitude of the aircraft.
    /// When the Type Code is from 20 to 22, the encoded altitude contains the GNSS altitude of the aircraft.
    /// </summary>
    private int _rawTc = 20;
    
    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        throw new NotImplementedException();
    }

    protected override void InternalSerialize(ref Span<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public override TypeCodeEnum TypeCode => _rawTc switch
    {
        >= 9 and <= 18 => TypeCodeEnum.AirborneBarometricPosition,
        >= 20 and <= 22 => TypeCodeEnum.AirborneGnssPosition,
        _ => TypeCodeEnum.Reserved
    };
}
