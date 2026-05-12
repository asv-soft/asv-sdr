using System;
using System.Threading.Tasks;

namespace Asv.Sdr.LimeSdr;

public interface ILimeSdrIfr6000Device : ILimeSdrCustomDevice
{

    Task<bool> IsTurnOn();
    Task TurnOn();
    Task TurnOff();
    Task RfRelaySelectOutput(bool isTx);
    
    
    // Mode A/C
    Task WriteDelayOffsetModeAC(double offset);
    Task<(float ModeA, float ModeC)> ReadReplyRatioModeAC();

    Task WriteP1P3SpacingOffset(float modeAOffset, float modeCOffset);
    Task WriteModeACControl(bool modeAP2SlsPulseEn, bool modeCP2SlsPulseEn, bool modeAP2SlsPulseAtt, bool modeCP2SlsPulseAtt, bool allCallModeAC_A, bool allCallModeAC_C, bool allCallModeS_A, bool allCallModeS_C);

    Task<(float F1, float F2)> ReadModeAPulseWidth();
    Task<(float F1, float F2)> ReadModeCPulseWidth();
    Task<(float ModeA, float ModeC)> ReadModeACPulseSpacing();
    Task<float> ReadModeAReplyDelay();
    Task<float> ReadModeCReplyDelay();
    Task<float> ReadModeAReplyJitter();
    Task<float> ReadModeCReplyJitter();
    
    Task<(string Squawk, bool Spi)> ReadModeASquawkCode();
    Task<int> ReadModeCAltitude();
    
    
    // Mode S

    Task<bool> WriteUfMessage(ModeSUFormatBase msg);
    Task<ModeSDFormatBase?> ReadDfMessage(Func<ModeSDFormatBase> factory, int attempts = 3);
    Task<ModeSDFormatBase?> ReadDfMessage(ModeSUFormatBase reqMsg, Func<ModeSDFormatBase> respFactory, int attempts = 3);
    
    Task<float> ReadReplyRatioModeS();
    
    Task<(ModeSDF11 Msg, byte Req, byte Resp)> ReadModeSDf11IcaoAddress();
    Task<ModeSDF4> ReadModeSDf4Altitude();
    Task<ModeSDF20> ReadModeSDf20Altitude();
    Task<ModeSDF5> ReadModeSDf5IdentityCode();
    Task<ModeSDF21> ReadModeSDf21IdentityCode();
    
    Task<(ModeSDF0 Msg, byte Req, byte Resp)> ReadModeSDf0AirAir();
    Task<(ModeSDF16 Msg, byte Req, byte Resp)> ReadModeSDf16AirAir();
    
    Task<Bds10> ReadBds10();
    
    Task<Bds17> ReadBds17();
    Task<Bds20> ReadBds20();
    Task<Bds30> ReadBds30();
    
    Task<Bds40> ReadBds40();
    Task<Bds50> ReadBds50();
    Task<Bds60> ReadBds60();
    
    
    // Mode S Extended
    Task<AdsbAirbornePosition> ReadAdsbAirbornePosition();
    Task<AdsbSurfacePosition> ReadAdsbSurfacePosition();
    Task<AdsbAircraftIdentification> ReadAdsbAircraftIdentification();
    Task<AdsbGroundSpeed> ReadAdsbGroundSpeed();
    Task<AdsbAirspeed> ReadAdsbAirspeed();
    Task<AdsbAircraftOperationStatus> ReadAdsbAircraftOperationStatus();
    
}