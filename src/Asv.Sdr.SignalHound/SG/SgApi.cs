using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Asv.Sdr.SignalHound
{

    enum SgMode
    {
        SgModeCw = 0,
        SgModeAm = 1,
        SgModeFm = 2,
        SgModePulse = 3,
        SgModePm = 4,
        SgModeStepSweep = 5,
        SgModeListSweep = 6,
        SgModeNoise = 7,
        SgCustomIq = 8
    }

    enum SgMultiTonePhase
    {
        SgParabolic = 0,
        SgRandom = 1,
        SgRandomFixedSeed = 2
    }

    enum SgShape
    {
        SgShapeSine = 0,
        SgShapeTriangle = 1,
        SgShapeSquare = 2,
        SgShapeRamp = 3
    }

    enum SgFilterType
    {
        SgRaisedCosine = 0,
        SgRootRaisedCosine = 1,
        SgGaussian = 2,
        SgNone = 3
    }

    enum SgModulationType
    {
        SgModBpsk = 0,
        SgModDbpsk = 1,
        SgModQpsk = 2,
        SgModDqpsk = 3,
        SgModOqpsk = 4,
        SgModPi4Dqpsk = 5,
        SgMod8Psk = 6,
        SgModD8Psk = 7,
        SgMod16Psk = 8,
        SgMod16Qam = 9,
        SgMod64Qam = 10,
        SgMod256Qam = 11
    }

    enum SgStatus
    {
        SgInvalidParameter = -6,
        SgMaxDevicesOpen = -5,
        SgUnableToFindDevice = -4,
        SgInvalidDeviceHandle = -3,
        SgNullPtrErr = -1,
        SgNoError = 0,
        SgSettingClamped = 1
    }

    static class SgApi
    {
        #region const

        public static int SG_MAX_DEVICES = 16;

        public static double SG_MIN_FREQUENCY = 80.0e6;
        public static double SG_MAX_FREQUENCY = 2.55e9;
        public static double SG_MIN_OUTPUT_POWER = -80.0;
        public static double SG_MAX_OUTPUT_POWER = 13.0;

        public static double SG_MIN_AM_MODULATION_RATE = 30.0;
        public static double SG_MAX_AM_MODULATION_RATE = 40.0e6;

        public static double SG_MIN_FM_MODULATION_RATE = 50.0;
        public static double SG_MAX_FM_MODULATION_RATE = 40.0e6;

        public static double SG_MIN_FM_DEVIATION = 0.0;
        public static double SG_MAX_FM_DEVIATION = 50.0e6;

        public static double SG_MIN_PULSE_WIDTH = 6.0e-9;
        public static double SG_MAX_PULSE_WIDTH = 25.0e-3;

        public static double SG_MIN_PULSE_PERIOD = 1.0e-8;
        public static double SG_MAX_PULSE_PERIOD = 1.0;

        public static double SG_MAX_SWEEP_TIME = 75.0e-3;

        public static long SG_MIN_SPAN = (long)1.0e5;
        public static long SG_MAX_SPAN = (long)120.0e6;

        public static double SG_MIN_SYMBOL_RATE = 53.334e3;
        public static double SG_MAX_SYMBOL_RATE = 180.0e6;

        #endregion

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgGetDeviceList(int[] deviceList, ref int length);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgOpenDevice(ref int device);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgOpenDeviceBySerial(ref int device, int serialNumber);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgCloseDevice(int device);

        public static string SgGetSerialNumber(int device)
        {
            var serialNumber = 0;
            return sgGetSerialNumber(device, ref serialNumber) == SgStatus.SgNoError ? serialNumber.ToString() : "";
        }

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern SgStatus sgGetSerialNumber(int device, ref int serialNumber);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgSetFrequencyAmplitude(int device, double frequency, double amplitude);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgRFOff(int device);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgSetCW(int device);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgSetAM(int device, double frequency, double depth, SgShape shape);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgSetFM(int device, double frequency, double deviation, SgShape shape);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgRFOsgSetPulse(int device, double period, double width);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgSetSweep(int device, double time, double span);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgSetMultitone(int device, int count, double spacing, double notchWidth,
            SgMultiTonePhase phase);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgSetASK(int device, double symbolRate, SgFilterType filterType,
            double filterAlpha, double depth, int[] symbols, int symbolCount);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgSetFSK(int device, double symbolRate, SgFilterType filterType,
            double filterAlpha, double modulationIndex, int[] symbols, int symbolCount);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgSetPSK(int device, double symbolRate, SgModulationType modType,
            SgFilterType filterType, double filterAlpha, int[] symbols, int symbolCount);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgSetCustomIQ(int device, double clockRate, double[] IVals, double[] QVals,
            int length, int period);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgQueryPulse(int device, ref double period, ref double width);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgQuerySymbolClockRate(int device, ref double clock);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgQueryClockError(int device, ref double error);

        public static string SgGetStatusString(SgStatus status)
        {
            var strPtr = sgGetStatusString(status);
            return Marshal.PtrToStringAnsi(strPtr);
        }

        public static string SgGetApiVersion()
        {
            var strPtr = sgGetAPIVersion();
            return Marshal.PtrToStringAnsi(strPtr);
        }

        // Sets an I/Q value to null the LO feed-thru. This may belong in SG_API_internal
        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern SgStatus sgSetIQNullValue(int device, int Icount, int Qcount);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sgGetStatusString(SgStatus status);

        [DllImport("sg_api.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sgGetAPIVersion();
    }


}