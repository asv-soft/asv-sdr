namespace Asv.Sdr.LimeSdr;

internal static class ModeAcReplyDecoder
{
    // The FPGA register includes SPI in bit 0 and the 13-bit interleaved code in bits 1..13.
    // C1 is code bit 12; a 12-bit mask would silently erase it.
    private static ushort GetCode(ushort register) => (ushort)((register >> 1) & 0x1FFF);

    internal static (string Squawk, bool Spi) DecodeSquawk(ushort register) =>
        (ModeSHelper.GetSquawk(GetCode(register)), (register & 1) != 0);

    internal static int? DecodeAltitude(ushort register) =>
        ModeSHelper.GetAltitudeFromModeCAltitudeCode(GetCode(register));
}
