namespace Asv.Sdr.Gui;

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
    AircraftIdentification = 1,
    SurfacePosition = 5,
    AirborneBarometricPosition = 9,
    AirborneVelocities = 19,
    AirborneGnssPosition = 20,
    Reserved = 23,
    AircraftStatus = 28,
    TargetStateAndStatusInformation = 29,
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

public enum AirbornePositionTypeCode
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
    Medium1,
    Medium2,
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

public enum NavigationUncertaintyCategoryEnum
{
    CategoryA,
    CategoryB,
    CategoryC,
    CategoryD,
    CategoryE,
    CategoryF,
    CategoryG,
    NotAvailable
}

public enum AltitudeTypeEnum
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

public enum GnssBaroAltDiffEnum
{
    GnssAboveBaro = 0,
    GnssBelowBaro = 1,
}

public enum VerticalDirectionEnum
{
    Up = 0,
    Down = 1
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