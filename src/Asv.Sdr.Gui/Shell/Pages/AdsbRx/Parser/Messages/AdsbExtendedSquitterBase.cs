namespace Asv.Sdr.Gui;

public enum SquitterTypeEnum
{
    NonTransponder,
    WithTransponder
}

public abstract class AdsbExtendedSquitterBase : AdsbDfMessageBase
{
    public override int DownlinkFormat => SquitterType == SquitterTypeEnum.WithTransponder ? 17 : 18;
    
    public SquitterTypeEnum SquitterType { get; set; }

    public abstract TypeCodeEnum TypeCode { get; }
    
    public override int GetByteSize()
    {
        return AdsbHelper.LongFrameLengthBytes;
    }

    protected override bool CheckMessageId(int downlinkFormat)
    {
        SquitterType = downlinkFormat switch
        {
            17 => SquitterTypeEnum.WithTransponder,
            18 => SquitterTypeEnum.NonTransponder,
            _ => SquitterType
        };
        return downlinkFormat is 17 or 18;
    }
}