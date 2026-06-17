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

    Task WriteModeSControl(bool modeSP5SlsPulseEn, bool modeSP5SlsPulseAtt);
    Task<bool> WriteUfMessage(ModeSUFormatBase msg);
    Task<ModeSDFormatBase?> ReadDfMessage(Func<ModeSDFormatBase> factory, int attempts = 3);
    Task<ModeSDFormatBase?> ReadDfMessage(ModeSUFormatBase reqMsg, Func<ModeSDFormatBase> respFactory, int attempts = 3);
    
    Task<float> ReadReplyRatioModeS();
    
    /// <summary>
    /// Selective request short UF4 short DF4
    /// </summary>
    /// <param name="icao">ICAO aircraft address</param>
    /// <returns>DF4</returns>
    Task<ModeSDF4?> ReadModeSDf4(uint icao);
    
    /// <summary>
    /// Selective request short UF4 long DF20
    /// </summary>
    /// <param name="icao">ICAO aircraft address</param>
    /// <param name="bds">Requested register number</param>
    /// <returns>DF20</returns>
    Task<ModeSDF20?> ReadModeSDf20(uint icao, byte bds);
    
    /// <summary>
    /// Selective request long UF20 long DF20
    /// </summary>
    /// <param name="icao">ICAO aircraft address</param>
    /// <param name="ads">Transferred register number</param>
    /// <param name="bds">Requested register number</param>
    /// <returns>DF20</returns>
    Task<ModeSDF20?> ReadModeSDf20(uint icao, byte ads, byte bds);
    
    /// <summary>
    /// Selective request long UF20 short DF4
    /// </summary>
    /// <param name="icao">ICAO aircraft address</param>
    /// <param name="ads">Transferred register number</param>
    /// <returns>DF4</returns>
    Task<ModeSDF4?> ReadModeSDf4(uint icao, byte ads);
    
    /// <summary>
    /// Selective request short UF5 short DF5
    /// </summary>
    /// <param name="icao">ICAO aircraft address</param>
    /// <returns>DF5</returns>
    Task<ModeSDF5?> ReadModeSDf5(uint icao);
    
    /// <summary>
    /// Selective request short UF5 long DF21
    /// </summary>
    /// <param name="icao">ICAO aircraft address</param>
    /// <param name="bds">Requested register number</param>
    /// <returns>DF21</returns>
    Task<ModeSDF21?> ReadModeSDf21(uint icao, byte bds);
    
    /// <summary>
    /// Selective request long UF21 long DF21
    /// </summary>
    /// <param name="icao">ICAO aircraft address</param>
    /// <param name="ads">Transferred register number</param>
    /// <param name="bds">Requested register number</param>
    /// <returns>DF21</returns>
    Task<ModeSDF21?> ReadModeSDf21(uint icao, byte ads, byte bds);
    
    /// <summary>
    /// Selective request long UF21 short DF5
    /// </summary>
    /// <param name="icao">ICAO aircraft address</param>
    /// <param name="ads">Transferred register number</param>
    /// <returns>DF5</returns>
    Task<ModeSDF5?> ReadModeSDf5(uint icao, byte ads);
    
    /// <summary>
    /// Selective air-air request short UF0 short DF0
    /// </summary>
    /// <param name="icao">ICAO aircraft address</param>
    /// <returns>DF0</returns>
    Task<ModeSDF0?> ReadModeSDf0(uint icao);
    
    /// <summary>
    /// Selective air-air request short UF0 long DF16
    /// </summary>
    /// <param name="icao">ICAO aircraft address</param>
    /// <param name="bds">Requested register number</param>
    /// <returns>DF16</returns>
    Task<ModeSDF16?> ReadModeSDf16(uint icao, byte bds);
    
    /// <summary>
    /// Selective air-air request long UF16 long DF16
    /// </summary>
    /// <param name="icao">ICAO aircraft address</param>
    /// <param name="ads">Transferred register number</param>
    /// <param name="bds">Requested register number</param>
    /// <returns>DF16</returns>
    Task<ModeSDF16?> ReadModeSDf16(uint icao, byte ads, byte bds);
    
    /// <summary>
    /// Selective request long UF16 short DF0
    /// </summary>
    /// <param name="icao">ICAO aircraft address</param>
    /// <param name="ads">Transferred register number</param>
    /// <returns>DF0</returns>
    Task<ModeSDF0?> ReadModeSDf0(uint icao, byte ads);

    /// <summary>
    /// Self-generating squitter DF11
    /// </summary>
    /// <returns>DF11</returns>
    Task<ModeSDF11?> ReadDf11Squitter();

    Task<(byte Counter, float Period)> ReadDf11SquitterStatistics();
    
    // ADS-B Extended
    Task<ExSquitterStatistics> ReadExSquitterStatistics();
    Task<AdsbAirbornePosition?> ReadExBds05Even();
    Task<AdsbAirbornePosition?> ReadExBds05Odd();
    Task<AdsbSurfacePosition?> ReadExBds06Even();
    Task<AdsbSurfacePosition?> ReadExBds06Odd();
    Task<AdsbAircraftIdentification?> ReadExBds08Id();
    Task<AdsbGroundSpeed?> ReadExBds09GroundSpeed();
    Task<AdsbAirspeed?> ReadExBds09Airspeed();
    
}