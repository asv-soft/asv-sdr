using System;
using System.Runtime.InteropServices;

namespace Asv.Sdr.SignalHound
{
    /*
     * The C# API class for the SA series devices is a class of static members and methods which
     * are simply a 1-to-1 mapping of the C API. This makes is easy to modify and look up
     * functions in the API manual.
     */

    enum SaStatus
    {
        SaUnknownErr = -666,

        // Setting specific error codes
        SaFrequencyRangeErr = -99,
        SaInvalidDetectorErr = -95,
        SaInvalidScaleErr = -94,
        SaBandwidthErr = -91,
        SaExternalReferenceNotFound = -89,

        // Device-specific errors
        SaLnaErr = -21,
        SaOvenColdErr = -20,

        // Data errors
        SaInternetErr = -12,
        SaUsbCommErr = -11,

        // General configuration errors
        SaTrackingGeneratorNotFound = -10,
        SaDeviceNotIdleErr = -9,
        SaDeviceNotFoundErr = -8,
        SaInvalidModeErr = -7,
        SaNotConfiguredErr = -6,
        SaTooManyDevicesErr = -5,
        SaInvalidParameterErr = -4,
        SaDeviceNotOpenErr = -3,
        SaInvalidDeviceErr = -2,
        SaNullPtrErr = -1,

        // No Error
        SaNoError = 0,

        // Warnings
        SaNoCorrections = 1,
        SaCompressionWarning = 2,
        SaParameterClamped = 3,
        SaBandwidthClamped = 4,
    }

    enum SaDeviceType
    {
        SaDeviceTypeNone = 0,
        SaDeviceTypeSa44 = 1,
        SaDeviceTypeSa44B = 2,
        SaDeviceTypeSa124A = 3,
        SaDeviceTypeSa124B = 4,
    }

    class SaApi
    {
        public static int SA_FALSE = 0;
        public static int SA_TRUE = 1;

        public static int SA_MAX_DEVICES = 8;

        public static int SA_FIRMWARE_STR_LEN = 16;
        public static int SA_NUM_AUDIO_SAMPLES = 4096;

        // Modes
        public static int SA_IDLE = -1;
        public static int SA_SWEEPING = 0;
        public static int SA_REAL_TIME = 1;
        public static int SA_IQ = 2;
        public static int SA_AUDIO = 3;
        public static int SA_TG_SWEEP = 4;

        // RBW shapes
        public static int SA_RBW_SHAPE_FLATTOP = 1;
        public static int SA_RBW_SHAPE_CISPR = 2;

        // Detectors
        public static int SA_MIN_MAX = 0;
        public static int SA_AVERAGE = 1;

        // Scales
        public static int SA_LOG_SCALE = 0;
        public static int SA_LIN_SCALE = 1;
        public static int SA_LOG_FULL_SCALE = 2;
        public static int SA_LIN_FULL_SCALE = 3;

        // Levels
        public static int SA_AUTO_ATTEN = -1;
        public static int SA_AUTO_GAIN = -1;

        // Video Processing Units
        public static int SA_LOG_UNITS = 0;
        public static int SA_VOLT_UNITS = 1;
        public static int SA_POWER_UNITS = 2;
        public static int SA_BYPASS = 3;

        // Audio
        public static int SA_AUDIO_AM = 0;
        public static int SA_AUDIO_FM = 1;
        public static int SA_AUDIO_USB = 2;
        public static int SA_AUDIO_LSB = 3;
        public static int SA_AUDIO_CW = 4;

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaGetSerialNumberList(
            int[] serialNumbers,
            ref int deviceCount
        );

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaOpenDeviceBySerialNumber(ref int device, int serialNumber);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaOpenDevice(ref int device);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaCloseDevice(int device);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaPreset(int device);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaGetSerialNumber(int device, ref int serial);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaGetDeviceType(int device, ref SaDeviceType deviceType);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaConfigAcquisition(int device, int detector, int scale);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaConfigCenterSpan(int device, double center, double span);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaConfigLevel(int device, double reflevel);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaConfigGainAtten(
            int device,
            int atten,
            int gain,
            bool preAmp
        );

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaConfigSweepCoupling(
            int device,
            double rbw,
            double vbw,
            bool reject
        );

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaConfigRBWShape(int device, int rbwShape);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaConfigProcUnits(int device, int units);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaConfigIQ(int device, int decimation, double bandwidth);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaConfigAudio(
            int device,
            int audioType,
            double centerFreq,
            double bandwidth,
            double audioLowPassFreq,
            double audioHighPassFreq,
            double fmDeemphasis
        );

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaConfigRealTime(
            int device,
            double frameScale,
            int frameRate
        );

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaConfigRealTimeOverlap(int device, double advanceRate);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaSetTimebase(int device, int timebase);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaInitiate(int device, int mode, int flag);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaAbort(int device);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaQuerySweepInfo(
            int device,
            ref int sweepLength,
            ref double startFreq,
            ref double binSize
        );

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaQueryStreamInfo(
            int device,
            ref int returnLen,
            ref double bandwidth,
            ref double samplesPerSecond
        );

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaQueryRealTimeFrameInfo(
            int device,
            ref int frameWidth,
            ref int frameHeight
        );

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaQueryRealTimePoi(int device, ref double poi);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaGetSweep32f(int device, float[] min, float[] max);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaGetSweep64f(int device, double[] min, double[] max);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaGetPartialSweep32f(
            int device,
            float[] min,
            float[] max,
            ref int start,
            ref int stop
        );

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaGetPartialSweep64f(
            int device,
            double[] min,
            double[] max,
            ref int start,
            ref int stop
        );

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaGetRealTimeFrame(
            int device,
            float[] sweep_min,
            float[] sweep_max,
            float[] colorFrame,
            float[] alphaFrame
        );

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaGetIQ32f(int device, float[] iq);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaGetIQ64f(int device, double[] iq);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaGetIQDataUnpacked(
            int device,
            float[] iqData,
            int iqCount,
            int purge,
            ref int dataRemaining,
            ref int sampleLoss,
            ref int sec,
            ref int milli
        );

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaGetAudio(int device, float[] audio);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaQueryTemperature(int device, ref float temp);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaQueryDiagnostics(int device, ref float voltage);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaAttachTg(int device);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaIsTgAttached(int device, ref bool attached);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaConfigTgSweep(
            int device,
            int sweepSize,
            bool highDynamicRange,
            bool passiveDevice
        );

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaStoreTgThru(int device, int flag);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaSetTg(int device, double frequency, double amplitude);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaSetTgReference(int device, int reference);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaGetTgFreqAmpl(
            int device,
            ref double frequency,
            ref double amplitude
        );

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SaStatus SaConfigIFOutput(
            int device,
            double inputFreq,
            double outputFreq,
            int inputAtten,
            int outputGain
        );

        public static string SaGetAPIString()
        {
            IntPtr strPtr = SaGetAPIVersion();
            return Marshal.PtrToStringAnsi(strPtr) ?? string.Empty;
        }

        public static string SaGetProductString()
        {
            IntPtr strPtr = SaGetProductID();
            return Marshal.PtrToStringAnsi(strPtr) ?? string.Empty;
        }

        public static string SaGetStatusString(SaStatus status)
        {
            IntPtr strPtr = SaGetErrorString(status);
            return Marshal.PtrToStringAnsi(strPtr) ?? string.Empty;
        }

        // Call string variants above instead
        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SaGetAPIVersion();

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SaGetProductID();

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SaGetErrorString(SaStatus status);
    }
}
