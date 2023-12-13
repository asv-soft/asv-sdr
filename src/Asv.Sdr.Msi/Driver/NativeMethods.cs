using System.Runtime.InteropServices;

namespace Asv.Sdr.Msi
{
    public enum mir_sdr_ErrT : int
    {
        mir_sdr_Success = 0,
        mir_sdr_Fail = 1,
        mir_sdr_InvalidParam = 2,
        mir_sdr_OutOfRange = 3,
        mir_sdr_GainUpdateError = 4,
        mir_sdr_RfUpdateError = 5,
        mir_sdr_FsUpdateError = 6,
        mir_sdr_HwError = 7,
        mir_sdr_AliasingError = 8,
        mir_sdr_NotInitialised
    }

    public enum mir_sdr_Bw_MHzT : int
    {
        mir_sdr_BW_0_200 = 200,
        mir_sdr_BW_0_300 = 300,
        mir_sdr_BW_0_600 = 600,
        mir_sdr_BW_1_536 = 1536,
        mir_sdr_BW_5_000 = 5000,
        mir_sdr_BW_6_000 = 6000,
        mir_sdr_BW_7_000 = 7000,
        mir_sdr_BW_8_000 = 8000
    }

    public enum mir_sdr_If_kHzT : int
    {
        mir_sdr_IF_Zero = 0,
        mir_sdr_IF_0_450 = 450,
        mir_sdr_IF_1_620 = 1620,
        mir_sdr_IF_2_048 = 2048
    }

    public enum mir_sdr_LoModeT : int
    {
        mir_sdr_LO_Undefined = 0,
        /// <summary>
        /// 1st LO is automatically selected to provide appropriate coverage across all tuner frequency ranges
        /// </summary>
        mir_sdr_LO_Auto = 1,
        /// <summary>
        /// 1st LO is set to 120MHz (coverage gap between 370MHZ and 420MHz)
        /// </summary>
        mir_sdr_LO_120MHz = 2,
        /// <summary>
        /// 1st LO is set to 144MHz (coverage gap between 250MHZ and 255MHz and between 400MHz and 420MHz)
        /// </summary>
        mir_sdr_LO_144MHz = 3,
        /// <summary>
        /// 1st LO is set to 168MHz (coverage gap between 250MHZ and 265MHz)
        /// </summary>
        mir_sdr_LO_168MHz = 4
    }

    public enum mir_sdr_AgcControlT
    {
        mir_sdr_AGC_DISABLE = 0,
        mir_sdr_AGC_100HZ = 1,
        mir_sdr_AGC_50HZ = 2,
        mir_sdr_AGC_5HZ = 3
    }

    public class NativeMethods
    {
        // const string APIDLL = "mir_sdr_api.dll";
        const string APIDLL = "mirsdrapi-rsp";

        /// <summary>
        /// Инициирует API для указанной частоты тюнера
        /// </summary>
        /// <param name="gRdB">Начальное уменьшение усиления</param>
        /// <param name="fsMHz">Частота дискретизации в МГц, допустимы значения от 2 МГц до 10 МГц. Прореживание можно использовать для получения более низких частот дискретизации.</param>
        /// <param name="rfMHz">Частота тюнера в МГц</param>
        /// <param name="bwType">Полоса пропускания</param>
        /// <param name="ifType">Полоса пропускания IF</param>
        /// <param name="samplesPerPacket">Количество выборок, которые будут возвращены при каждом вызове mir_sdr_ReadPacket()</param>
        /// <returns></returns>
        [DllImport(APIDLL)]
        public static extern mir_sdr_ErrT mir_sdr_Init(int gRdB, double fsMHz, double rfMHz, mir_sdr_Bw_MHzT bwType, mir_sdr_If_kHzT ifType, out int samplesPerPacket);

        [DllImport(APIDLL)]
        public static extern mir_sdr_ErrT mir_sdr_Uninit();

        [DllImport(APIDLL)]
        public unsafe static extern mir_sdr_ErrT mir_sdr_ReadPacket(short* xi, short* xq, out uint firstSampleNum, out int grChanged, out int rfChanged, out int fsChanged);

        /// <summary>
        /// Регулирует номинальную центральную частоту тюнера, поддерживаемую во внутреннем состоянии API.
        /// В зависимости от состояния параметра abs параметр drfHz либо применяется как смещение от внутренне сохраненного состояния API,
        /// либо используется абсолютным образом для изменения внутренне сохраненного состояния.
        /// Эта команда разрешает только изменения частоты, которые подпадают под ограничения таблиц распределения частот
        /// </summary>
        /// <param name="drfHz">Частота в Гц</param>
        /// <param name="abs">Indicates if drfHz is an absolute value or offset from previously set value:
        /// 0 - Смещение
        /// 1 - Абсолютное</param>
        /// <param name="syncUpdate">Указывает, должно ли обновление радиочастоты применяться немедленно или с задержкой до следующей точки синхронного обновления,
        /// как настроено в вызовах mir_sdr_SetSyncUpdateSampleNum() и mir_sdr_SetSyncUpdatePeriod().
        /// 0 - Немедленно
        /// 1 - Синхронно</param>
        /// <returns></returns>
        [DllImport(APIDLL)]
        public static extern mir_sdr_ErrT mir_sdr_SetRf(double drfHz, int abs, int syncUpdate);

        [DllImport(APIDLL)]
        public static extern mir_sdr_ErrT mir_sdr_SetFs(double dfsHz, int abs, int syncUpdate, int reCal);

        /// <summary>
        /// Программирует требуемое снижение усиления в тюнере. Внутреннее состояние обновляется независимо от того, какой параметр abs установлен.
        /// </summary>
        /// <param name="gRdB">Требуемое снижение усиления в дБ. (табличное значение)</param>
        /// <param name="abs">Является ли gRdB абсолютным значением или смещением от ранее установленного значения. 0 - Смещение, 1 - Абсолютное</param>
        /// <param name="syncUpdate">Указывает, должно ли уменьшение усиления применяться немедленно или с задержкой до следующей точки синхронного обновления, как настроено в вызовах
        /// mir_sdr_SetSyncUpdateSampleNum() и mir_sdr_SetSyncUpdatePeriod(). 0 - Немедленно, 1 - Синхронно</param>
        /// <returns></returns>
        [DllImport(APIDLL)]
        public static extern mir_sdr_ErrT mir_sdr_SetGr(int gRdB, int abs, int syncUpdate);

        /// <summary>
        /// Изменяет параметры снижения усиления по умолчанию, требуемые в тюнере.
        /// </summary>
        /// <param name="minimumGr">Минимальное снижение усиления в дБ, которое можно запрограммировать (табличное значение)</param>
        /// <param name="lnaGrThreshold">Порог, при котором LNA будет включен</param>
        /// <returns></returns>
        [DllImport(APIDLL)]
        public static extern mir_sdr_ErrT mir_sdr_SetGrParams(int minimumGr, int lnaGrThreshold);

        /// <summary>
        /// Устанавливает режим коррекции смещения постоянного тока для тюнера.
        /// </summary>
        /// <param name="dcCal">Режим коррекции смещения:
        /// 0 - Статический.
        /// 1 - Каждые 6 мс.
        /// 2 - Каждые 12 мс.
        /// 3 - Каждые 24 мс.
        /// 4 - Однократно (коррекция применяется каждый раз, когда выполняется обновление усиления).
        /// 5 - Непрерывный</param>
        /// <param name="speedUp">Режим ускорения. 0 - Отключен, 1 - Включен</param>
        /// <returns></returns>
        [DllImport(APIDLL)]
        public static extern mir_sdr_ErrT mir_sdr_SetDcMode(int dcCal, int speedUp);


        /// <summary>
        /// Для указания поправочного коэффициента, используемого для учета смещения от номинала в кварцевом генераторе.
        /// </summary>
        /// <param name="ppm">Смещение частей на миллион (например, +/- 1 ppm указывает погрешность +/- 24 Гц для кристалла 24 МГц).</param>
        /// <returns></returns>
        [DllImport(APIDLL)]
        public static extern mir_sdr_ErrT mir_sdr_SetPpm(double ppm);


        /// <summary>
        /// Позволяет указать конкретную частоту 1-го гетеродина или выбирает автоматический режим,
        /// который позволяет API определять наиболее подходящую частоту 1-го гетеродина во всех диапазонах частот тюнера.
        /// Эта функция должна быть вызвана до инициализации API — в противном случае используйте mir_sdr_ReInit().
        /// </summary>
        /// <param name="loMode"></param>
        /// <returns></returns>
        [DllImport(APIDLL)]
        public static extern mir_sdr_ErrT mir_sdr_SetLoMode(mir_sdr_LoModeT loMode);


        /// <summary>
        /// Используется для контроля того, включена ли AGC или нет, и параметров, позволяющих настраивать AGC.
        /// Примечание. Для включения AGC требуется, чтобы поток внутреннего потока был создан с помощью mir_sdr_StreamInit().
        /// </summary>
        /// <param name="enable">Определяет требуемый режим AGC. Режим по умолчанию - 100 Гц (mir_sdr_100HZ).</param>
        /// <param name="setPoint_dBfs">Задает требуемую уставку в dBfs.</param>
        /// <param name="knee_dBfs">В настоящее время не используется, установлено значение 0.</param>
        /// <param name="decay_ms">В настоящее время не используется, установлено значение 0.</param>
        /// <param name="hang_ms">В настоящее время не используется, установлено значение 0.</param>
        /// <param name="syncAgcUpdate">0 - немедленное обновление; 1 - синхронное обновление</param>
        /// <param name="lagcLNAstate">Указывает состояние LNA для использования в обновлениях усиления, когда AGC использует mir_sdr_SetGrAltMode() или mir_sdr_RSP_SetGr(), как указано при вызове mir_sdr_StreamInit().</param>
        /// <returns></returns>
        [DllImport(APIDLL)]
        public static extern mir_sdr_ErrT mir_sdr_AgcControl(mir_sdr_AgcControlT enable, int setPoint_dBfs, int knee_dBfs, uint decay_ms, uint hang_ms, int syncAgcUpdate, int lagcLNAstate);



        [DllImport(APIDLL)]
        public static extern mir_sdr_ErrT mir_sdr_SetDcTrackTime(int trackTime);

        [DllImport(APIDLL)]
        public static extern mir_sdr_ErrT mir_sdr_SetSyncUpdateSampleNum(uint sampleNum);

        [DllImport(APIDLL)]
        public static extern mir_sdr_ErrT mir_sdr_SetSyncUpdatePeriod(uint period);

        /// <summary>
        /// Команда, которая преобразует выборочные данные IF, полученные из потоковых данных, в данные I и Q в формате нулевой IF.
        /// Функции преобразуют низкую IF в нулевую IF путем смешивания, фильтрации и прореживания выборочных данных IF.
        /// Функция будет работать правильно только для параметров, указанных в таблице
        /// </summary>
        /// <param name="xin">Указатель на массив размера (samplesPerPacket * sizeof(short)), в котором содержатся выборки I,
        /// возвращенные из потоковых данных. Если ненулевой режим IF, этот массив будет содержать выборочные данные IF.</param>
        /// <param name="xi">Указатель на массив (минимального размера ((samplesPerPacket/M) * sizeof(short)) в
        /// котором будут возвращены преобразованные с понижением частоты выборки I.</param>
        /// <param name="xq">Указатель на массив (минимального размера ((samplesPerPacket/M) * sizeof(short)) в
        /// котором будут возвращены преобразованные с понижением частоты выборки Q.</param>
        /// <param name="SamplesPerPacket">Целое число без знака, содержащее количество выборок, содержащихся во входном массиве выборочных данных IF (in)</param>
        /// <param name="ifType">Указывает полосу пропускания IF, которая была настроена, см. список в нумерованном типе для поддерживаемых режимов.</param>
        /// <param name="Decimation">Желаемый коэффициент прореживания, список применимых значений см. в таблице.</param>
        /// <param name="Preset">Если Preset равно 1, то состояние фильтрации будет сброшено перед любой операцией фильтрации.</param>
        /// <returns></returns>
        [DllImport(APIDLL)]
        public unsafe static extern mir_sdr_ErrT mir_sdr_DownConvert(short* xin, short* xi, short* xq,  uint SamplesPerPacket, mir_sdr_If_kHzT ifType, uint Decimation, uint Preset);


        /// <summary>
        /// Команда, предназначенная для установки различных параметров управления оборудованием.
        /// Эта команда должна быть выполнена перед mir_sdr_Init, чтобы настроить оборудование перед инициализацией.
        /// </summary>
        /// <param name="ParameterId">101 - настроить частоту 1-го гетеродина</param>
        /// <param name="Value">
        /// 19200000 → 1st LO Frequency = 168MHz;
        /// 22000000 → 1st LO Frequency = 144MHz;
        /// 24576000 → 1st LO Frequency = 120MHz
        /// </param>
        /// <returns></returns>
        [DllImport(APIDLL)]
        public static extern mir_sdr_ErrT mir_sdr_SetParam(int ParameterId, int Value);

        [DllImport(APIDLL)]
        public static extern mir_sdr_ErrT mir_sdr_ResetUpdateFlags(int ResetGainUpdate, int ResetRFUpdate, int ResetFsUpdate);

        [DllImport(APIDLL)]
        public static extern mir_sdr_ErrT mir_sdr_ApiVersion(out float version);    // Called by application to retrieve version of API used to create Dll


    }
}