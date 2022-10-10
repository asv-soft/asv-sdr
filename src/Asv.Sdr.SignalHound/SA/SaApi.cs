using System;
using System.Runtime.InteropServices;

namespace Asv.Sdr.SignalHound
{
    /*
     * The C# API class for the SA series devices is a class of static members and methods which
     * are simply a 1-to-1 mapping of the C API. This makes is easy to modify and look up
     * functions in the API manual.
     */

    enum saStatus
    {
        saUnknownErr = -666,

        // Setting specific error codes
        saFrequencyRangeErr = -99,
        saInvalidDetectorErr = -95,
        saInvalidScaleErr = -94,
        saBandwidthErr = -91,
        saExternalReferenceNotFound = -89,

        // Device-specific errors
        saLNAErr = -21,
        saOvenColdErr = -20,

        // Data errors
        saInternetErr = -12,
        saUSBCommErr = -11,

        // General configuration errors
        saTrackingGeneratorNotFound = -10,
        saDeviceNotIdleErr = -9,
        saDeviceNotFoundErr = -8,
        saInvalidModeErr = -7,
        saNotConfiguredErr = -6,
        saTooManyDevicesErr = -5,
        saInvalidParameterErr = -4,
        saDeviceNotOpenErr = -3,
        saInvalidDeviceErr = -2,
        saNullPtrErr = -1,

        // No Error
        saNoError = 0,

        // Warnings
        saNoCorrections = 1,
        saCompressionWarning = 2,
        saParameterClamped = 3,
        saBandwidthClamped = 4,
    };

    enum saDeviceType
    {
        saDeviceTypeNone = 0,
        saDeviceTypeSA44 = 1,
        saDeviceTypeSA44B = 2,
        saDeviceTypeSA124A = 3,
        saDeviceTypeSA124B = 4
    };

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
        public static extern saStatus saGetSerialNumberList(int[] serialNumbers, ref int deviceCount);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saOpenDeviceBySerialNumber(ref int device, int serialNumber);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saOpenDevice(ref int device);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saCloseDevice(int device);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saPreset(int device);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saGetSerialNumber(int device, ref int serial);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saGetDeviceType(int device, ref saDeviceType device_type);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saConfigAcquisition(int device, int detector, int scale);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saConfigCenterSpan(int device, double center, double span);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saConfigLevel(int device, double reflevel);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saConfigGainAtten(int device, int atten, int gain, bool preAmp);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saConfigSweepCoupling(int device, double rbw, double vbw, bool reject);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saConfigRBWShape(int device, int rbwShape);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saConfigProcUnits(int device, int units);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saConfigIQ(int device, int decimation, double bandwidth);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saConfigAudio(int device, int audioType, double centerFreq, double bandwidth, double audioLowPassFreq, double audioHighPassFreq, double fmDeemphasis);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saConfigRealTime(int device, double frameScale, int frameRate);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saConfigRealTimeOverlap(int device, double advanceRate);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saSetTimebase(int device, int timebase);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saInitiate(int device, int mode, int flag);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saAbort(int device);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saQuerySweepInfo(int device, ref int sweepLength, ref double startFreq, ref double binSize);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saQueryStreamInfo(int device, ref int returnLen, ref double bandwidth, ref double samplesPerSecond);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saQueryRealTimeFrameInfo(int device, ref int frameWidth, ref int frameHeight);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saQueryRealTimePoi(int device, ref double poi);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saGetSweep_32f(int device, float[] min, float[] max);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saGetSweep_64f(int device, double[] min, double[] max);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saGetPartialSweep_32f(int device, float[] min, float[] max, ref int start, ref int stop);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saGetPartialSweep_64f(int device, double[] min, double[] max, ref int start, ref int stop);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saGetRealTimeFrame(int device, float[] sweep_min, float[] sweep_max, float[] colorFrame, float[] alphaFrame);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saGetIQ_32f(int device, float[] iq);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saGetIQ_64f(int device, double[] iq);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saGetIQDataUnpacked(int device, float[] iqData, int iqCount, int purge, ref int dataRemaining, ref int sampleLoss, ref int sec, ref int milli);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saGetAudio(int device, float[] audio);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saQueryTemperature(int device, ref float temp);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saQueryDiagnostics(int device, ref float voltage);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saAttachTg(int device);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saIsTgAttached(int device, ref bool attached);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saConfigTgSweep(int device, int sweepSize, bool highDynamicRange, bool passiveDevice);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saStoreTgThru(int device, int flag);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saSetTg(int device, double frequency, double amplitude);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saSetTgReference(int device, int reference);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saGetTgFreqAmpl(int device, ref double frequency, ref double amplitude);

        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern saStatus saConfigIFOutput(int device, double inputFreq, double outputFreq, int inputAtten, int outputGain);

        public static string saGetAPIString()
        {
            IntPtr strPtr = saGetAPIVersion();
            return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(strPtr);
        }

        public static string saGetProductString()
        {
            IntPtr strPtr = saGetProductID();
            return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(strPtr);
        }

        public static string saGetStatusString(saStatus status)
        {
            IntPtr strPtr = saGetErrorString(status);
            return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(strPtr);
        }

        // Call string variants above instead
        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr saGetAPIVersion();
        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr saGetProductID();
        [DllImport("sa_api.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr saGetErrorString(saStatus status);
    }

}