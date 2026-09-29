using System;
using System.Threading;
using System.Threading.Tasks;

namespace Asv.Sdr.LimeSdr;

public interface ILimeSdrIfr6000Device : ILimeSdrCustomDevice
{
    Ifr6000MeasurementProfile MeasurementProfile { get; }

    Task<bool> IsTurnOn();
    Task TurnOn();
    Task TurnOff();
    Task RfRelaySelectOutput(bool isTx);
    
    
    // Mode A/C
    Task WriteDelayOffsetModeAC(double offset);
    /// <summary>Reads legacy A/C registers without generation or diagnostics registers; hardware atomicity is not guaranteed.</summary>
    Task<Ifr6000ModeAcSnapshot> ReadModeAcSnapshot(CancellationToken cancel = default);
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
    Task SetModeAcWindowOffset(double offsetUs);
    
    
    // Mode S

    Task WriteDelayOffsetModeS(double offset);
    Task WriteModeSControl(bool modeSP5SlsPulseEn, bool modeSP5SlsPulseAtt);
    Task<float> ReadModeSReplyDelay();
    Task<float> ReadModeSReplyJitter();
    Task SetModeSWindowOffset(double offsetUs);
    Task<bool> WriteUfMessage(ModeSUFormatBase msg);
    Task<ModeSDFormatBase?> ReadDfMessage(Func<ModeSDFormatBase> factory, int attempts = 3);
    Task<ModeSDFormatBase?> ReadDfMessage(ModeSUFormatBase reqMsg, Func<ModeSDFormatBase> respFactory, int attempts = 3);
    
    Task<float> ReadReplyRatioModeS();

    /// <summary>
    /// Reads the selective Mode S downlink frame receive counter.
    /// </summary>
    /// <returns>16-bit counter incremented by the device when a selective DF response is received.</returns>
    Task<ushort> ReadSelectiveDfCounter();

    /// <summary>
    /// All-Call request short UF11 with AA=0xFFFFFF
    /// </summary>
    /// <param name="ic">Interrogator code</param>
    /// <param name="cl">Code label</param>
    /// <returns>DF4</returns>
    Task<ModeSDF11?> ReadModeSDf11(byte ic, byte cl);
    
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
    /// Selective air-air request with configurable UF0 RL, AQ and DS fields.
    /// Returns DF0 for RL=0 or a <see cref="ModeSDF16"/> instance for RL=1.
    /// </summary>
    /// <param name="icao">ICAO aircraft address</param>
    /// <param name="replyLength">UF0 RL bit: 0 requests DF0, 1 requests DF16</param>
    /// <param name="acquisition">UF0 AQ bit</param>
    /// <param name="dataSelector">UF0 DS field</param>
    /// <returns>DF0 or DF16, depending on <paramref name="replyLength"/></returns>
    Task<ModeSDF0?> ReadModeSAirAirReply(
        uint icao,
        byte replyLength,
        byte acquisition,
        byte dataSelector
    );
    
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

    /// <summary>
    /// Waits for a validated DF11 snapshot whose counter differs from the supplied baseline.
    /// The FPGA slot is shared by unsolicited and intermode all-call DF11 messages.
    /// Acquisition is bounded to 2.5 seconds, excluding lock acquisition and pending device I/O.
    /// Freshness does not identify the interrogation that caused the received message.
    /// </summary>
    Task<ModeSDF11?> ReadDf11Squitter(byte counterBefore, CancellationToken cancel = default);

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
    Task<AdsbAircraftEmergencyStatus?> ReadExBds61EmergencyPriorityStatus();
    Task<AdsbAircraftAcasRaBroadcast?> ReadExBds61TcasRaBroadcast();
    Task<AdsbTargetStateAndStatusInformation?> ReadExBds62Old();
    Task<AdsbTargetStateAndStatusInformation?> ReadExBds62New();
    Task<AdsbAircraftOperationStatus?> ReadExBds65Airborne();
    Task<AdsbAircraftOperationStatus?> ReadExBds65Surface();

}
