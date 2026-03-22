namespace Asv.Sdr;

public enum CapabilityEnum
{
    Reserved,
    Level1,
    Level2OnGround,
    Level2Airborne,
    Level2OnGroundOrAirborne,
    DLRequest0OrFlightStatus2345OnGroundOrAirborne
}

public enum AdsbMessageTypeEnum
{
    /// <summary>
    /// TC = 1–4 для BDS 0,8 — Extended Squitter Identification and Category
    /// </summary>
    AircraftIdentification = 1,
    
    /// <summary>
    /// TC = 5–8 для BDS 0,6 — Extended Squitter Surface Position
    /// </summary>
    SurfacePosition = 5,
    
    /// <summary>
    /// TC = 9–18 для airborne position с баро-высотой
    /// </summary>
    AirborneBarometricPosition = 9,
    
    /// <summary>
    /// TC = 19 для BDS 0,9 — Extended Squitter Airborne Velocity.
    /// SubType = 1 или 2 для Velocity over ground,
    /// SubType = 3 или 4 для Airspeed / Heading
    /// </summary>
    AirborneVelocities = 19,
    
    /// <summary>
    /// TC = 20–22 для airborne position с GNSS-высотой
    /// </summary>
    AirborneGnssPosition = 20,
    
    /// <summary>
    /// TC = 23 для BDS 0,A — Extended Squitter Event-Driven Register
    /// </summary>
    EventDriven = 23,
    
    /// <summary>
    /// TC = 28 для BDS 6,1 — Extended Squitter Aircraft Status.
    /// SubType = 1 для Emergency/Priority Status,
    /// SubType = 2 для TCAS RA Broadcast
    /// </summary>
    AircraftStatus = 28,
    
    /// <summary>
    /// TC = 29 для BDS 6,2 — Target State and Status Information
    /// SubType = 0 для старого формата TSS,
    /// SubType = 1  для нового формата TSS
    /// </summary>
    TargetStateAndStatusInformation = 29,
    
    /// <summary>
    /// TC = 31 для BDS 6,5 — Aircraft Operational Status
    /// SubType = 0 для Airborne,
    /// SubType = 1  для Surface
    /// </summary>
    AircraftOperationStatus = 31
}

public enum SurfacePositionTypeCodes
{
    /// <summary>
    /// Воздушное судно на земле, передает скорость и направление.
    /// </summary>
    GroundVehicle = 5,

    /// <summary>
    /// Воздушное судно на земле с GNSS данными для точного положения.
    /// </summary>
    GroundVehicleWithGNSS = 6,

    /// <summary>
    /// Воздушное судно на земле, передает скорость, направление и изменение высоты.
    /// </summary>
    GroundVehicleWithVerticalRate = 7,

    /// <summary>
    /// Статус и индикаторы режима для воздушных судов на земле.
    /// </summary>
    GroundStatusAndMode = 8
}

public enum AirbornePositionBaroAltTypeCode
{
    /// <summary>
    /// Базовое позиционирование с низкой точностью
    /// </summary>
    BasicPositionLowPrecision = 9,
    /// <summary>
    /// Базовое позиционирование с низкой частотой обновления
    /// </summary>
    BasicPositionLowUpdateRate = 10,
    /// <summary>
    /// Базовое позиционирование с высокой частотой обновления
    /// </summary>
    BasicPositionHighUpdateRate = 11,
    /// <summary>
    /// Базовое позиционирование с высокой точностью
    /// </summary>
    BasicPositionHighPrecision = 12,
    /// <summary>
    /// Улучшенное позиционирование с индикацией NIC
    /// </summary>
    EnhancedPositionWithNic = 13,
    /// <summary>
    /// Улучшенное позиционирование с индикацией NACp
    /// </summary>
    EnhancedPositionWithNacP = 14,
    /// <summary>
    /// Улучшенное позиционирование с данными вертикальной скорости
    /// </summary>
    EnhancedPositionWithVerticalRate = 15,
    /// <summary>
    /// Высокоточное позиционирование с индикацией NIC
    /// </summary>
    HighPrecisionPositionWithNic = 16,
    /// <summary>
    /// Высокоточное позиционирование с индикацией NACp
    /// </summary>
    HighPrecisionPositionWithNacP = 17,
    /// <summary>
    /// Высокоточное позиционирование с вертикальной скоростью и данными целостности
    /// </summary>
    HighPrecisionPositionWithVerticalRateAndIntegrity = 18,
}

public enum AirbornePositionGnssAltTypeCode
{
    /// <summary>
    /// Базовая информация о GNSS местоположении
    /// </summary>
    BasicGnssPosition = 20,
    /// <summary>
    /// Улучшенная информация о GNSS местоположении с дополнительными данными о целостности
    /// </summary>
    EnhancedGnssPositionWithIntegrity = 21,
    /// <summary>
    /// Высокоточная информация о GNSS местоположении с расширенными функциями навигации
    /// </summary>
    HighPrecisionGnssPositionWithAdvNavFeatures = 22
}

public enum AircraftCategoryEnum
{
    Reserved,
    NoCategoryInformation,
    SurfaceEmergencyVehicle,
    SurfaceServiceVehicle,
    GroundObstruction,
    GliderOrSailplane,
    LighterThanAir,
    ParachutistOrSkydiver,
    UltralightOrHangGliderOrParaGlider,
    UnmannedAerialVehicle,
    SpaceOrTransAtmosphericVehicle,
    Light,
    Small,
    Large,
    HighVortexAircraft,
    Heavy,
    HighPerformanceAndHighSpeed,
    Rotorcraft
}

public enum VelocitySubTypeEnum
{
    SubType1 = 1,
    SubType2 = 2,
    SubType3 = 3,
    SubType4 = 4
}

public enum AircraftOperationalStatusEnum
{
    SubType1 = 0,
    SubType2 = 1,
}

public enum NavigationUncertaintyCategoryEnum
{
    AdsbVersion0,
    AdsbVersion1,
    AdsbVersion2
}

public enum VerticalRateSourceEnum
{
    Barometric,
    Gnss
}

public enum CprFormatEnum
{
    Even,
    Odd
}

public enum GroundTrackStatusEnum
{
    Invalid = 0,
    Valid = 1
}
public enum SurveillanceStatusEnum
{
    NoCondition = 0,
    PermanentAlert = 1,
    TemporaryAlert = 2,
    SpecialPositionIdentification = 3
}

public enum EastWestVelocityDirectionEnum
{
    FromWestToEast = 0,
    FromEastToWest = 1
}

public enum NorthSouthVelocityDirectionEnum
{
    FromSouthToNorth = 0,
    FromNorthToSouth = 1
}

public enum MagneticHeadingStatusEnum
{
    NotAvailable = 0,
    Available = 1
}

public enum AirspeedTypeEnum
{
    /// <summary>
    ///  Indicated airspeed
    /// </summary>
    IAS = 0,
    /// <summary>
    /// True airspeed
    /// </summary>
    TAS = 1
}

public enum OperationStatusTypeEnum
{
    Airborne = 0,
    Surface = 1
}

public enum AdsbVersionNumberEnum
{
    AppendixA = 0,
    AppendixB = 1,
    AppendixC = 2
}