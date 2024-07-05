using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using NLog;

namespace Asv.Sdr.LimeSdr
{
    public static class LmsNativeDllUsage
    {
#if X64
        public static bool Is64BitOperatingSystem = true;
#else
        public static bool Is64BitOperatingSystem = false;
#endif
    }
    
    
    //Enumeration of LMS7 TEST signal types
    public enum lms_testsig_t
    {
        LMS_TESTSIG_NONE = 0,     ///<Disable test signals. Return to normal operation
        LMS_TESTSIG_NCODIV8,    ///<Test signal from NCO half scale
        LMS_TESTSIG_NCODIV4,    ///<Test signal from NCO half scale
        LMS_TESTSIG_NCODIV8F,   ///<Test signal from NCO full scale
        LMS_TESTSIG_NCODIV4F,   ///<Test signal from NCO full scale
        LMS_TESTSIG_DC          ///<DC test signal
    }

    public enum LogLevel
    {
        LOG_LEVEL_CRITICAL = 0, //!< A critical error. The application might not be able to continue running successfully.
        LOG_LEVEL_ERROR = 1, //!< An error. An operation did not complete successfully, but the application as a whole is not affected.
        LOG_LEVEL_WARNING = 2, //!< A warning. An operation completed with an unexpected result.
        LOG_LEVEL_INFO = 3, //!< An informational message, usually denoting the successful completion of an operation.
        LOG_LEVEL_DEBUG = 4, //!< A debugging message, only shown in Debug configuration.
    };

    #region structures

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct lms_range_t
    {
        public double min;
        public double max;
        public double step;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LMS7Parameter
    {
        public UInt16 address;
        public byte msb;
        public byte lsb;
        public UInt16 defaultValue;
        public string name;
        public string tooltip;
    };

    public enum DataFormat
    {
        LMS_FMT_F32 = 0,    /**<32-bit floating point*/
        LMS_FMT_I16 = 1,      /**<16-bit integers*/
        LMS_FMT_I12 = 2       /**<12-bit integers stored in 16-bit variables*/
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct lms_stream_t_X32
    {
        public uint handle;
        public bool isTx;
        public uint channel;
        public uint fifoSize;
        public float throughputVsLatency;
        internal DataFormat dataFmt;
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct lms_stream_t_X64
    {

        public UInt64 handle;
        public bool isTx;
        public uint channel;
        public uint fifoSize;
        public float throughputVsLatency;
        internal DataFormat dataFmt;
    }

    /**Streaming status structure*/
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct LmsStreamStatus
    {
        ///Indicates whether the stream is currently active
        bool active;
        ///Number of samples in FIFO buffer
        public uint fifoFilledCount;
        ///Size of FIFO buffer
        public uint fifoSize;
        ///FIFO underrun count
        public uint underrun;
        ///FIFO overrun count
        public uint overrun;
        ///Number of dropped packets by HW
        public uint droppedPackets;
        ///Sampling rate of the stream
        public double sampleRate;
        ///Combined buffer rate of all stream of the same direction (TX or RX)
        public double linkRate;
        ///Current HW timestamp
        public ulong timestamp;
    }

    [StructLayout(LayoutKind.Sequential)]

    /**Metadata structure used in sample transfers*/
    public struct lms_stream_meta_t
    {
        /**
         * Timestamp is a value of HW counter with a tick based on sample rate.
         * In RX: time when the first sample in the returned buffer was received
         * In TX: time when the first sample in the submitted buffer should be send
         */
        public ulong timestamp;

        /**In TX: wait for the specified HW timestamp before broadcasting buffer over
         * the air
         * In RX: wait for the specified HW timestamp before starting to receive
         * samples
         */
        public bool waitForTimestamp;

        /**Indicates the end of send/receive transaction. Currently has no effect
         * @todo force send samples to HW (ignore transfer size) when selected
         */
        public bool flushPartialPacket;
    }

    [StructLayout(LayoutKind.Sequential)]
    /**Device information structure*/
    public struct lms_dev_info_t
    {
        public char[] deviceName;            ///<The display name of the device
        public char[] expansionName;         ///<The display name of the expansion card
        public char[] firmwareVersion;       ///<The firmware version as a string
        public char[] hardwareVersion;       ///<The hardware version as a string
        public char[] protocolVersion;       ///<The protocol version as a string
        public ulong boardSerialNumber;     ///<A unique board serial number
        public char[] gatewareVersion;       ///<Gateware version as a string
        public char[] gatewareTargetBoard;   ///<Which board should use this gateware
    }

    public enum lms_loopback_t
    {
        LMS_LOOPBACK_NONE   /**<Return to normal operation (disable loopback)*/
    }

    #endregion

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LogCallBack(LogLevel level, string msg);

    /// <summary>
    /// DLl from here https://downloads.myriadrf.org/project/limesuite/20.10/
    /// </summary>
    public class NativeMethods
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();
        
        static NativeMethods()
        {
            var os = DetectPlatform();
            switch (os)
            {
                case OperatingSystem.Undefined:
                    break;
                case OperatingSystem.Windows:
                    if (LmsNativeDllUsage.Is64BitOperatingSystem == true)
                    {
                        var dllDir64 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib","x64");
                        if (!Directory.Exists(dllDir64))
                        {
                            _logger.Info("Create native library directory {0}", dllDir64);
                            Directory.CreateDirectory(dllDir64);
                        }
                        CheckFile(Path.Combine(dllDir64, "LimeSuite.dll"), Libs.LimeSuiteX64);
                        if (!SetDllDirectory(dllDir64))
                            throw new Win32Exception($"Error to execute kernel32.dll:SetDllDirectory({dllDir64})");
                        _logger.Info("Set native library directory {0}", dllDir64);
                        Is64BitOperatingSystem = true;
                    }
                    else
                    {
                        var dllDir32 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib","x86");
                        if (!Directory.Exists(dllDir32))
                        {
                            _logger.Info("Create native library directory {0}", dllDir32);
                            Directory.CreateDirectory(dllDir32);
                        }
                        CheckFile(Path.Combine(dllDir32, "LimeSuite.dll"), Libs.LimeSuiteX32);
                        if (!SetDllDirectory(dllDir32))
                            throw new Win32Exception($"Error to execute kernel32.dll:SetDllDirectory({dllDir32})");
                        _logger.Info("Set native library directory {0}", dllDir32);
                        Is64BitOperatingSystem = false;
                    }
                    break;
                case OperatingSystem.Linux:
                    break;
                case OperatingSystem.MacOsX:
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Can't detect OS");
            }
        }

        public static bool Is64BitOperatingSystem = false;


        public enum OperatingSystem
        {
            Undefined,
            Windows,
            Linux,
            MacOsX
        }

        

        private static void CheckFile(string path, byte[] data)
        {
            if (!File.Exists(path)) File.WriteAllBytes(path, data);
        }


        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);

        private static OperatingSystem DetectPlatform()
        {
            var windir = Environment.GetEnvironmentVariable("windir");
            if (!string.IsNullOrEmpty(windir) && windir.Contains(@"\") && Directory.Exists(windir)) return OperatingSystem.Windows;

            if (File.Exists(@"/proc/sys/kernel/ostype"))
            {
                var osType = File.ReadAllText(@"/proc/sys/kernel/ostype");
                return osType.StartsWith("Linux", StringComparison.OrdinalIgnoreCase)
                    ? OperatingSystem.Linux
                    : OperatingSystem.Undefined;
            }

            return File.Exists(@"/System/Library/CoreServices/SystemVersion.plist")
                ? OperatingSystem.MacOsX
                : OperatingSystem.Undefined;
        }
        
        
        
        [DllImport("LimeSuite", EntryPoint = "LMS_ReadLMSReg", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_ReadLMSReg(IntPtr device, UInt32 address, UInt16* val);

        [DllImport("LimeSuite", EntryPoint = "LMS_WriteLMSReg", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_WriteLMSReg(IntPtr device, UInt32 address, UInt16 val);

        #region Dll import

        [DllImport("LimeSuite", EntryPoint = "LMS_GetDeviceList", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetDeviceList(byte* dev_list);

        [DllImport("LimeSuite", EntryPoint = "LMS_Open", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_Open(out IntPtr device, string info, string args);

        [DllImport("LimeSuite", EntryPoint = "LMS_Close", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_Close(IntPtr device);

        [DllImport("LimeSuite", EntryPoint = "LMS_IsOpen", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool LMS_IsOpen(IntPtr device, int port);

        [DllImport("LimeSuite", EntryPoint = "LMS_Init", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_Init(IntPtr device);

        [DllImport("LimeSuite", EntryPoint = "LMS_GetNumChannels", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_GetNumChannels(IntPtr device, bool dir_tx);


        [DllImport("LimeSuite", EntryPoint = "LMS_RegisterLogHandler", CallingConvention = CallingConvention.Cdecl)]
        public static extern void LMS_RegisterLogHandler(LogCallBack callback);


        // --------------------------------------- BEGIN X86 / X64 platform ------------------------------------------------

        public static int LMS_EnableChannel(IntPtr device, bool dir_tx, uint chan, bool enabled) =>
            Is64BitOperatingSystem
                ? LMS_EnableChannel_X64(device, dir_tx, chan, enabled)
                : LMS_EnableChannel_X32(device, dir_tx, chan, enabled);

        public static int LMS_SetLOFrequency(IntPtr device, bool dir_tx, uint chan, double frequency)
        {
            return Is64BitOperatingSystem
                ? LMS_SetLOFrequency_X64(device, dir_tx, chan, frequency)
                : LMS_SetLOFrequency_X32(device, dir_tx, chan, frequency);
        }

        public static int LMS_GetLOFrequency(IntPtr device, bool dir_tx, uint chan, ref double frequency)
        {
            return Is64BitOperatingSystem
                ? LMS_GetLOFrequency_X64(device, dir_tx, chan, ref frequency)
                : LMS_GetLOFrequency_X32(device, dir_tx, chan, ref frequency);
        }

        public static unsafe int LMS_SetNCOFrequency(IntPtr device, bool dir_tx, uint chan, double* frequency,
            double pho)
        {
            return Is64BitOperatingSystem
                ? LMS_SetNCOFrequency_X64(device, dir_tx, chan, frequency, pho)
                : LMS_SetNCOFrequency_X32(device, dir_tx, chan, frequency, pho);
        }

        public static unsafe int LMS_GetNCOFrequency(IntPtr device, bool dir_tx, uint chan, double* frequency,
            double* pho)
        {
            return Is64BitOperatingSystem
                ? LMS_GetNCOFrequency_X64(device, dir_tx, chan, frequency, pho)
                : LMS_GetNCOFrequency_X32(device, dir_tx, chan, frequency, pho);
        }

        public static int LMS_SetNCOIndex(IntPtr device, bool dir_tx, uint chan, int index, bool downconv)
        {
            return Is64BitOperatingSystem
                ? LMS_SetNCOIndex_X64(device, dir_tx, chan, index, downconv)
                : LMS_SetNCOIndex_X32(device, dir_tx, chan, index, downconv);
        }

        public static int LMS_GetNCOIndex(IntPtr device, bool dir_tx, uint chan)
        {
            return Is64BitOperatingSystem
                ? LMS_GetNCOIndex_X64(device, dir_tx, chan)
                : LMS_GetNCOIndex_X32(device, dir_tx, chan);
        }

        public static int LMS_SetNCOPhase(IntPtr device, bool dir_tx, uint chan, double phase, double fcw)
        {
            return Is64BitOperatingSystem
                ? LMS_SetNCOPhase_X64(device, dir_tx, chan, phase, fcw)
                : LMS_SetNCOPhase_X32(device, dir_tx, chan, phase, fcw);
        }

        public static int LMS_GetNCOPhase(IntPtr device, bool dir_tx, uint chan, ref double phase, ref double fcw)
        {
            return Is64BitOperatingSystem
                ? LMS_GetNCOPhase_X64(device, dir_tx, chan, ref phase, ref fcw)
                : LMS_GetNCOPhase_X32(device, dir_tx, chan, ref phase, ref fcw);
        }

        public static int LMS_SetSampleRateDir(IntPtr device, bool dir_tx, double rate, uint oversample)
        {
            return Is64BitOperatingSystem
                ? LMS_SetSampleRateDir_X64(device, dir_tx, rate, oversample)
                : LMS_SetSampleRateDir_X32(device, dir_tx, rate, oversample);
        }

        public static int LMS_SetSampleRate(IntPtr device, double rate, uint oversample)
        {
            return Is64BitOperatingSystem
                ? LMS_SetSampleRate_X64(device, rate, oversample)
                : LMS_SetSampleRate_X32(device, rate, oversample);
        }

        public static int LMS_GetSampleRate(IntPtr device, bool dir_tx, uint chan, ref double host_Hz,
            ref double rf_Hz)
        {
            return Is64BitOperatingSystem
                ? LMS_GetSampleRate_X64(device, dir_tx, chan, ref host_Hz, ref rf_Hz)
                : LMS_GetSampleRate_X32(device, dir_tx, chan, ref host_Hz, ref rf_Hz);
        }

        public static unsafe int LMS_RecvStream(IntPtr stream, void* samples, uint sample_count,
            ref lms_stream_meta_t meta, uint timeout_ms)
        {
            return Is64BitOperatingSystem
                ? LMS_RecvStream_X64(stream, samples, sample_count, ref meta, timeout_ms)
                : LMS_RecvStream_X32(stream, samples, sample_count, ref meta, timeout_ms);
        }

        public static unsafe int LMS_SendStream(IntPtr stream, void* samples, uint sample_count,
            ref lms_stream_meta_t meta, uint timeout_ms)
        {
            return Is64BitOperatingSystem
                ? LMS_SendStream_X64(stream, samples, sample_count, ref meta, timeout_ms)
                : LMS_SendStream_X32(stream, samples, sample_count, ref meta, timeout_ms);
        }

        public static unsafe int LMS_SetAntenna(IntPtr device, bool dir_tx, uint chan, uint index)
        {
            return Is64BitOperatingSystem
                ? LMS_SetAntenna_X64(device, dir_tx, chan, index)
                : LMS_SetAntenna_X32(device, dir_tx, chan, index);
        }

        public static unsafe int LMS_SetTestSignal(IntPtr device, bool dir_tx, uint chan, lms_testsig_t sig,
            Int16 dc_i, Int16 dc_q)
        {
            return Is64BitOperatingSystem
                ? LMS_SetTestSignal_X64(device, dir_tx, chan, sig, dc_i, dc_q)
                : LMS_SetTestSignal_X32(device, dir_tx, chan, sig, dc_i, dc_q);
        }

        public static unsafe int LMS_GPIOWrite(IntPtr device, void* buffer, uint length)
        {
            return Is64BitOperatingSystem
                ? LMS_GPIOWrite_X64(device, buffer, length)
                : LMS_GPIOWrite_X32(device, buffer, length);
        }

        public static unsafe int LMS_GPIODirWrite(IntPtr device, void* buffer, uint length)
        {
            return Is64BitOperatingSystem
                ? LMS_GPIODirWrite_X64(device, buffer, length)
                : LMS_GPIODirWrite_X32(device, buffer, length);
        }

        public static unsafe int LMS_GPIORead(IntPtr device, void* buffer, uint length)
        {
            return Is64BitOperatingSystem
                ? LMS_GPIORead_X64(device, buffer, length)
                : LMS_GPIORead_X32(device, buffer, length);
        }

        public static unsafe int LMS_GPIODirRead(IntPtr device, void* buffer, uint length)
        {
            return Is64BitOperatingSystem
                ? LMS_GPIODirRead_X64(device, buffer, length)
                : LMS_GPIODirRead_X32(device, buffer, length);
        }

        public static int LMS_SetGaindB(IntPtr device, bool dir_tx, uint chan, uint gain)
        {
            return Is64BitOperatingSystem
                ? LMS_SetGaindB_X64(device, dir_tx, chan, gain)
                : LMS_SetGaindB_X32(device, dir_tx, chan, gain);
        }

        public static unsafe int LMS_GetGaindB(IntPtr device, bool dir_tx, uint chan, ref uint gain)
        {
            int res;
            if (Is64BitOperatingSystem)
            {
                var buffer = new ulong[1];

                fixed (ulong* buf = &buffer[0])
                {
                    res = LMS_GetGaindB_X64(device, dir_tx, chan, buf);
                }
                gain = (uint)buffer[0];

            }
            else
            {
                var buffer = new uint[1];

                fixed (uint* buf = &buffer[0])
                {
                    res = LMS_GetGaindB_X32(device, dir_tx, chan, buf);
                }
                gain = buffer[0];
            }
            return res;

        }

        public static int LMS_SetNormalizedGain(IntPtr device, bool dir_tx, uint chan, double gain)
        {
            return Is64BitOperatingSystem
                ? LMS_SetNormalizedGain_X64(device, dir_tx, chan, gain)
                : LMS_SetNormalizedGain_X32(device, dir_tx, chan, gain);
        }

        public static unsafe int LMS_GetNormalizedGain(IntPtr device, bool dir_tx, uint chan, double* gain)
        {
            return Is64BitOperatingSystem
                ? LMS_GetNormalizedGain_X64(device, dir_tx, chan, gain)
                : LMS_GetNormalizedGain_X32(device, dir_tx, chan, gain);
        }

        public static unsafe int LMS_GetChipTemperature(IntPtr dev, uint ind, double* temp)
        {
            return Is64BitOperatingSystem
                ? LMS_GetChipTemperature_X64(dev, ind, temp)
                : LMS_GetChipTemperature_X32(dev, ind, temp);
        }

        public static unsafe int LMS_SetLPFBW(IntPtr device, bool dir_tx, uint chan, double bandwidth)
        {
            return Is64BitOperatingSystem
                ? LMS_SetLPFBW_X64(device, dir_tx, chan, bandwidth)
                : LMS_SetLPFBW_X32(device, dir_tx, chan, bandwidth);
        }

        public static unsafe int LMS_GetLPFBW(IntPtr device, bool dir_tx, uint chan, double* bandwidth)
        {
            return Is64BitOperatingSystem
                ? LMS_GetLPFBW_X64(device, dir_tx, chan, bandwidth)
                : LMS_GetLPFBW_X32(device, dir_tx, chan, bandwidth);
        }

        public static unsafe int LMS_GetLPFBWRange(IntPtr device, bool dir_tx, uint chan, lms_range_t* range)
        {
            return Is64BitOperatingSystem
                ? LMS_GetLPFBWRange_X64(device, dir_tx, chan, range)
                : LMS_GetLPFBWRange_X32(device, dir_tx, chan, range);
        }

        public static unsafe int LMS_SetLPF(IntPtr device, bool dir_tx, uint chan, bool enable)
        {
            return Is64BitOperatingSystem
                ? LMS_SetLPF_X64(device, dir_tx, chan, enable)
                : LMS_SetLPF_X32(device, dir_tx, chan, enable);
        }

        public static unsafe int LMS_SetGFIRLPF(IntPtr device, bool dir_tx, uint chan, bool enable, double bandwidth)
        {
            return Is64BitOperatingSystem
                ? LMS_SetGFIRLPF_X64(device, dir_tx, chan, enable, bandwidth)
                : LMS_SetGFIRLPF_X32(device, dir_tx, chan, enable, bandwidth);
        }

        public static unsafe int LMS_SetGFIRCoeff(IntPtr device, bool dir_tx, uint chan, IntPtr filt, double* coef, uint count)
        {
            return Is64BitOperatingSystem
                ? LMS_SetGFIRCoeff_X64(device, dir_tx, chan, filt, coef, count)
                : LMS_SetGFIRCoeff_X32(device, dir_tx, chan, filt, coef, count);
        }

        public static unsafe int LMS_GetGFIRCoeff(IntPtr device, bool dir_tx, uint chan, IntPtr filt, double* coef)
        {
            return Is64BitOperatingSystem
                ? LMS_GetGFIRCoeff_X64(device, dir_tx, chan, filt, coef)
                : LMS_GetGFIRCoeff_X32(device, dir_tx, chan, filt, coef);
        }

        public static unsafe int LMS_GetAntennaBW(IntPtr device, bool dir_tx, uint chan, uint path, lms_range_t* range)
        {
            return Is64BitOperatingSystem
                ? LMS_GetAntennaBW_X64(device, dir_tx, chan, path, range)
                : LMS_GetAntennaBW_X32(device, dir_tx, chan, path, range);
        }

        public static unsafe int LMS_GetClockFreq(IntPtr device, uint clk_id, double* freq)
        {
            return Is64BitOperatingSystem
                ? LMS_GetClockFreq_X64(device, clk_id, freq)
                : LMS_GetClockFreq_X32(device, clk_id, freq);
        }

        public static unsafe int LMS_SetClockFreq(IntPtr device, uint clk_id, double freq)
        {
            return Is64BitOperatingSystem
                ? LMS_SetClockFreq_X64(device, clk_id, freq)
                : LMS_SetClockFreq_X32(device, clk_id, freq);
        }

        public static unsafe int LMS_Calibrate(IntPtr device, bool dir_tx, uint chan, double bw, uint flags)
        {
            return Is64BitOperatingSystem
                ? LMS_Calibrate_X64(device, dir_tx, chan, bw, flags)
                : LMS_Calibrate_X32(device, dir_tx, chan, bw, flags);
        }


        [DllImport("LimeSuite", EntryPoint = "LMS_EnableChannel", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_EnableChannel_X64(IntPtr device, bool dir_tx, UInt64 chan, bool enabled);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetLOFrequency", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_SetLOFrequency_X64(IntPtr device, bool dir_tx, uint chan, double frequency);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetLOFrequency", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_GetLOFrequency_X64(IntPtr device, bool dir_tx, UInt64 chan, ref double frequency);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetNCOFrequency", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SetNCOFrequency_X64(IntPtr device, bool dir_tx, UInt64 chan, double* frequency, double pho);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetNCOFrequency", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetNCOFrequency_X64(IntPtr device, bool dir_tx, UInt64 chan, double* frequency, double* pho);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetNCOIndex", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_SetNCOIndex_X64(IntPtr device, bool dir_tx, UInt64 chan, int index, bool downconv);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetNCOIndex", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_GetNCOIndex_X64(IntPtr device, bool dir_tx, UInt64 chan);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetNCOPhase", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_SetNCOPhase_X64(IntPtr device, bool dir_tx, UInt64 chan, double phase, double fcw);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetNCOPhase", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_GetNCOPhase_X64(IntPtr device, bool dir_tx, UInt64 chan, ref double phase, ref double fcw);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetSampleRateDir", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_SetSampleRateDir_X64(IntPtr device, bool dir_tx, double rate, UInt64 oversample);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetSampleRate", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_SetSampleRate_X64(IntPtr device, double rate, UInt64 oversample);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetSampleRate", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_GetSampleRate_X64(IntPtr device, bool dir_tx, UInt64 chan, ref double host_Hz, ref double rf_Hz);
        [DllImport("LimeSuite", EntryPoint = "LMS_RecvStream", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_RecvStream_X64(IntPtr stream, void* samples, UInt64 sample_count, ref lms_stream_meta_t meta, UInt64 timeout_ms);
        [DllImport("LimeSuite", EntryPoint = "LMS_SendStream", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SendStream_X64(IntPtr stream, void* samples, UInt64 sample_count, ref lms_stream_meta_t meta, UInt64 timeout_ms);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetAntenna", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SetAntenna_X64(IntPtr device, bool dir_tx, UInt64 chan, UInt64 index);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetTestSignal", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SetTestSignal_X64(IntPtr device, bool dir_tx, UInt64 chan, lms_testsig_t sig, Int16 dc_i, Int16 dc_q);
        [DllImport("LimeSuite", EntryPoint = "LMS_GPIOWrite", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GPIOWrite_X64(IntPtr device, void* buffer, UInt64 length);
        [DllImport("LimeSuite", EntryPoint = "LMS_GPIODirWrite", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GPIODirWrite_X64(IntPtr device, void* buffer, UInt64 length);
        [DllImport("LimeSuite", EntryPoint = "LMS_GPIORead", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GPIORead_X64(IntPtr device, void* buffer, UInt64 length);
        [DllImport("LimeSuite", EntryPoint = "LMS_GPIODirRead", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GPIODirRead_X64(IntPtr device, void* buffer, UInt64 length);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetGaindB", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_SetGaindB_X64(IntPtr device, bool dir_tx, UInt64 chan, UInt64 gain);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetGaindB", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetGaindB_X64(IntPtr device, bool dir_tx, UInt64 chan, UInt64* gain);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetNormalizedGain", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_SetNormalizedGain_X64(IntPtr device, bool dir_tx, UInt64 chan, double gain);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetNormalizedGain", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetNormalizedGain_X64(IntPtr device, bool dir_tx, UInt64 chan, double* gain);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetChipTemperature", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetChipTemperature_X64(IntPtr dev, UInt64 ind, double* temp);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetLPFBW", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SetLPFBW_X64(IntPtr device, bool dir_tx, UInt64 chan, double bandwidth);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetLPFBW", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetLPFBW_X64(IntPtr device, bool dir_tx, UInt64 chan, double* bandwidth);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetLPFBWRange", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetLPFBWRange_X64(IntPtr device, bool dir_tx, UInt64 chan, lms_range_t* range);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetLPF", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SetLPF_X64(IntPtr device, bool dir_tx, UInt64 chan, bool enable);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetGFIRLPF", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SetGFIRLPF_X64(IntPtr device, bool dir_tx, UInt64 chan, bool enable, double bandwidth);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetGFIRCoeff", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SetGFIRCoeff_X64(IntPtr device, bool dir_tx, UInt64 chan, IntPtr filt, double* coef, UInt64 count);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetGFIRCoeff", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetGFIRCoeff_X64(IntPtr device, bool dir_tx, UInt64 chan, IntPtr filt, double* coef);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetAntennaBW", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetAntennaBW_X64(IntPtr device, bool dir_tx, UInt64 chan, UInt64 path, lms_range_t* range);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetClockFreq", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetClockFreq_X64(IntPtr device, UInt64 clk_id, double* freq);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetClockFreq", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SetClockFreq_X64(IntPtr device, UInt64 clk_id, double freq);
        [DllImport("LimeSuite", EntryPoint = "LMS_Calibrate", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_Calibrate_X64(IntPtr device, bool dir_tx, UInt64 chan, double bw, UInt64 flags);


        [DllImport("LimeSuite", EntryPoint = "LMS_SetLOFrequency", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_SetLOFrequency_X32(IntPtr device, bool dir_tx, UInt32 chan, double frequency);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetLOFrequency", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_GetLOFrequency_X32(IntPtr device, bool dir_tx, UInt32 chan, ref double frequency);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetNCOFrequency", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SetNCOFrequency_X32(IntPtr device, bool dir_tx, UInt32 chan, double* frequency, double pho);
        [DllImport("LimeSuite", EntryPoint = "LMS_EnableChannel", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_EnableChannel_X32(IntPtr device, bool dir_tx, UInt32 chan, bool enabled);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetNCOFrequency", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetNCOFrequency_X32(IntPtr device, bool dir_tx, UInt32 chan, double* frequency, double* pho);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetNCOIndex", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_SetNCOIndex_X32(IntPtr device, bool dir_tx, UInt32 chan, int index, bool downconv);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetNCOIndex", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_GetNCOIndex_X32(IntPtr device, bool dir_tx, UInt32 chan);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetNCOPhase", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_SetNCOPhase_X32(IntPtr device, bool dir_tx, UInt32 chan, double phase, double fcw);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetNCOPhase", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_GetNCOPhase_X32(IntPtr device, bool dir_tx, UInt32 chan, ref double phase, ref double fcw);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetSampleRateDir", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_SetSampleRateDir_X32(IntPtr device, bool dir_tx, double rate, UInt32 oversample);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetSampleRate", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_SetSampleRate_X32(IntPtr device, double rate, UInt32 oversample);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetSampleRate", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_GetSampleRate_X32(IntPtr device, bool dir_tx, UInt32 chan, ref double host_Hz, ref double rf_Hz);
        [DllImport("LimeSuite", EntryPoint = "LMS_RecvStream", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_RecvStream_X32(IntPtr stream, void* samples, UInt32 sample_count, ref lms_stream_meta_t meta, UInt32 timeout_ms);
        [DllImport("LimeSuite", EntryPoint = "LMS_SendStream", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SendStream_X32(IntPtr stream, void* samples, uint sample_count, ref lms_stream_meta_t meta, uint timeout_ms);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetAntenna", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SetAntenna_X32(IntPtr device, bool dir_tx, UInt32 chan, UInt32 index);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetTestSignal", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SetTestSignal_X32(IntPtr device, bool dir_tx, UInt32 chan, lms_testsig_t sig, Int16 dc_i, Int16 dc_q);
        [DllImport("LimeSuite", EntryPoint = "LMS_GPIOWrite", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GPIOWrite_X32(IntPtr device, void* buffer, UInt32 length);
        [DllImport("LimeSuite", EntryPoint = "LMS_GPIODirWrite", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GPIODirWrite_X32(IntPtr device, void* buffer, UInt32 length);
        [DllImport("LimeSuite", EntryPoint = "LMS_GPIORead", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GPIORead_X32(IntPtr device, void* buffer, UInt32 length);
        [DllImport("LimeSuite", EntryPoint = "LMS_GPIODirRead", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GPIODirRead_X32(IntPtr device, void* buffer, UInt32 length);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetGaindB", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_SetGaindB_X32(IntPtr device, bool dir_tx, UInt32 chan, UInt32 gain);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetGaindB", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetGaindB_X32(IntPtr device, bool dir_tx, UInt32 chan, UInt32* gain);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetNormalizedGain", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_SetNormalizedGain_X32(IntPtr device, bool dir_tx, UInt32 chan, double gain);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetNormalizedGain", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetNormalizedGain_X32(IntPtr device, bool dir_tx, UInt32 chan, double* gain);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetChipTemperature", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetChipTemperature_X32(IntPtr dev, UInt32 ind, double* temp);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetLPFBW", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SetLPFBW_X32(IntPtr device, bool dir_tx, UInt32 chan, double bandwidth);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetLPFBW", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetLPFBW_X32(IntPtr device, bool dir_tx, UInt32 chan, double* bandwidth);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetLPFBWRange", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetLPFBWRange_X32(IntPtr device, bool dir_tx, UInt32 chan, lms_range_t* range);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetLPF", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SetLPF_X32(IntPtr device, bool dir_tx, UInt32 chan, bool enable);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetGFIRLPF", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SetGFIRLPF_X32(IntPtr device, bool dir_tx, UInt32 chan, bool enable, double bandwidth);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetGFIRCoeff", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SetGFIRCoeff_X32(IntPtr device, bool dir_tx, UInt32 chan, IntPtr filt, double* coef, UInt32 count);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetGFIRCoeff", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetGFIRCoeff_X32(IntPtr device, bool dir_tx, UInt32 chan, IntPtr filt, double* coef);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetAntennaBW", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetAntennaBW_X32(IntPtr device, bool dir_tx, UInt32 chan, UInt32 path, lms_range_t* range);
        [DllImport("LimeSuite", EntryPoint = "LMS_GetClockFreq", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetClockFreq_X32(IntPtr device, UInt32 clk_id, double* freq);
        [DllImport("LimeSuite", EntryPoint = "LMS_SetClockFreq", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SetClockFreq_X32(IntPtr device, UInt32 clk_id, double freq);
        [DllImport("LimeSuite", EntryPoint = "LMS_Calibrate", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_Calibrate_X32(IntPtr device, bool dir_tx, UInt32 chan, double bw, UInt32 flags);

        public static int LMS_SetupStream(IntPtr device, bool dir_tx, DataFormat dataFmt, ref IntPtr stream, uint chan, uint fifoSize, float throughputVsLatency)
        {
            if (Is64BitOperatingSystem)
            {
                var streamId = new lms_stream_t_X64
                {
                    handle = 0,
                    channel = chan,
                    fifoSize = fifoSize,
                    throughputVsLatency = throughputVsLatency,
                    isTx = dir_tx,
                    dataFmt = dataFmt
                };
                stream = Marshal.AllocHGlobal(Marshal.SizeOf(streamId));
                Marshal.StructureToPtr(streamId, stream, false);
                return NativeMethods.LMS_SetupStream(device, stream);
            }
            else
            {
                var streamId = new lms_stream_t_X32
                {
                    handle = 0,
                    channel = chan,
                    fifoSize = fifoSize,
                    throughputVsLatency = throughputVsLatency,
                    isTx = dir_tx,
                    dataFmt = dataFmt
                };
                stream = Marshal.AllocHGlobal(Marshal.SizeOf(streamId));
                Marshal.StructureToPtr(streamId, stream, false);
                return NativeMethods.LMS_SetupStream(device, stream);
            }
        }



        // --------------------------------------- END X86 / X64 platform ------------------------------------------------

        [DllImport("LimeSuite", EntryPoint = "LMS_SetupStream", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int LMS_SetupStream(IntPtr dev, IntPtr stream);

        [DllImport("LimeSuite", EntryPoint = "LMS_StartStream", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_StartStream(IntPtr stream);

        [DllImport("LimeSuite", EntryPoint = "LMS_StopStream", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_StopStream(IntPtr stream);

        [DllImport("LimeSuite", EntryPoint = "LMS_DestroyStream", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_DestroyStream(IntPtr dev, IntPtr stream);

        [DllImport("LimeSuite", EntryPoint = "LMS_GetLastErrorMessage", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr LMS_GetLastErrorMessage();

        public static string limesdr_strerror()
        {
            IntPtr ret = LMS_GetLastErrorMessage();
            if (ret != IntPtr.Zero)
                return Marshal.PtrToStringAnsi(ret);
            return String.Empty;
        }

        [DllImport("LimeSuite", EntryPoint = "LMS_WriteParam", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LMS_WriteParam(IntPtr device, LMS7Parameter param, UInt16 val);

        [DllImport("LimeSuite", EntryPoint = "LMS_ReadParam", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_ReadParam(IntPtr device, LMS7Parameter param, ushort* val);

        [DllImport("LimeSuite", EntryPoint = "LMS_Reset", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_Reset(IntPtr device);

        [DllImport("LimeSuite", EntryPoint = "LMS_GetStreamStatus", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_GetStreamStatus(IntPtr stream, ref LmsStreamStatus status);

        [DllImport("LimeSuite", EntryPoint = "LMS_GetLibraryVersion", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe char* LMS_GetLibraryVersion();

        [DllImport("LimeSuite", EntryPoint = "LMS_GetDeviceInfo", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void* LMS_GetDeviceInfo(IntPtr device);

        [DllImport("LimeSuite", EntryPoint = "LMS_SaveConfig", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_SaveConfig(IntPtr device, string fileName);

        [DllImport("LimeSuite", EntryPoint = "LMS_LoadConfig", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_LoadConfig(IntPtr device, string fileName);

        [DllImport("LimeSuite", EntryPoint = "LMS_VCTCXOWrite", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_VCTCXOWrite(IntPtr device, ushort val);

        [DllImport("LimeSuite", EntryPoint = "LMS_VCTCXORead", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_VCTCXORead(IntPtr device, ushort* val);

        [DllImport("LimeSuite", EntryPoint = "LMS_WriteFPGAReg", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_WriteFPGAReg(IntPtr device, UInt32 address, UInt16 val);

        [DllImport("LimeSuite", EntryPoint = "LMS_ReadFPGAReg", CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int LMS_ReadFPGAReg(IntPtr device, UInt32 address, UInt16* val);

        #endregion
    }
}
