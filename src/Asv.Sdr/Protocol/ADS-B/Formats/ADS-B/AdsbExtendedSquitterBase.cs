using System;

namespace Asv.Sdr;

public enum SquitterTypeEnum
{
    Unknown = 0,
    WithTransponder = 17,
    NonTransponder = 18
}

public abstract class AdsbExtendedSquitterBase : AdsbDfMessageBase
{
    public override ushort Id => (ushort)((17 << 8) | ((ushort)MessageType << 3));
    public override int DownlinkFormat => (int)SquitterType;
    
    public SquitterTypeEnum SquitterType { get; set; } = SquitterTypeEnum.WithTransponder;

    public abstract AdsbMessageTypeEnum MessageType { get; }

    protected override void InternalDeserialize(ref ReadOnlySpan<byte> buffer)
    {
        var tc = (buffer[0] >> 3) & 0x1F;
        var msgType = tc switch
        {
            >= 1 and <= 4 => AdsbMessageTypeEnum.AircraftIdentification,
            <= 8 => AdsbMessageTypeEnum.SurfacePosition,
            <= 18 => AdsbMessageTypeEnum.AirborneBarometricPosition,
            19 => AdsbMessageTypeEnum.AirborneVelocities,
            <= 22 => AdsbMessageTypeEnum.AirborneGnssPosition,
            23 => AdsbMessageTypeEnum.EventDriven,
            <= 27 => AdsbMessageTypeEnum.Reserved,
            28 => AdsbMessageTypeEnum.AircraftStatus,
            29 => AdsbMessageTypeEnum.TargetStateAndStatusInformation,
            30 => AdsbMessageTypeEnum.Reserved,
            31 => AdsbMessageTypeEnum.AircraftOperationStatus,
            _ => throw new ArgumentOutOfRangeException()
        };
        
        if (msgType != MessageType)
        {
            throw new Exception($"Deserialization ADS-B message failed: want message type '{MessageType:G}'. Read = '{msgType:G}'");
        }
    }

    public override int GetByteSize()
    {
        return TransponderHelper.LongFrameLengthBytes;
    }

    protected override bool CheckMessageId(int downlinkFormat)
    {
        SquitterType = downlinkFormat switch
        {
            17 => SquitterTypeEnum.WithTransponder,
            18 => SquitterTypeEnum.NonTransponder,
            _ => (SquitterTypeEnum)downlinkFormat
        };
        return downlinkFormat is 17 or 18;
    }
}
