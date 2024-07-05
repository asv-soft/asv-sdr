using System;
using System.Threading;
using System.Threading.Tasks;

namespace Asv.Sdr.LimeSdr
{
    public enum LmsChannel
    {
        Rx,
        Tx
    }

    /// <summary>
    /// 
    /// </summary>
    public enum LmsPathRx
    {
        /// <summary>
        /// No active path (RX or TX)
        /// </summary>
        LMS_PATH_NONE = 0,
        /// <summary>
        /// RX LNA_H port
        /// </summary>
        LMS_PATH_LNAH = 1,
        /// <summary>
        /// RX LNA_L port
        /// </summary>
        LMS_PATH_LNAL = 2,
        /// <summary>
        /// RX LNA_W port
        /// </summary>
        LMS_PATH_LNAW = 3,
        /// <summary>
        /// Automatically select port (if supported)
        /// </summary>
        LMS_PATH_AUTO = 255,

    }

    public enum LmsPathTx
    {

        /// <summary>
        /// TX port 1
        /// </summary>
        LMS_PATH_TX1 = 1,
        /// <summary>
        /// TX port 2
        /// </summary>
        LMS_PATH_TX2 = 2,
    }

    public interface ILmsStream:IDisposable
    {
        LmsChannel Type { get; }
        ulong Channel { get; }
        bool IsStarted { get; }
        Task Start(CancellationToken cancel);
        Task Stop(CancellationToken cancel);
        Task<LmsStreamStatus> GetStatus(CancellationToken cancel);
        lms_stream_meta_t Metadata { get; }
        void EditMetadata(Func<lms_stream_meta_t, lms_stream_meta_t> edit);
        Task<int> Read(Memory<float> iqBuffer, uint timeoutMs = 1000, CancellationToken cancel = default);
        Task<int> Write(ReadOnlyMemory<float> iqBuffer, uint timeoutMs = 1000, CancellationToken cancel = default);

    }

    public interface ILimeSdrDevice:IDisposable
    {
        string DeviceId { get; }
        Task<int> GetChannelNumbers(LmsChannel channel, CancellationToken cancel);
        Task<double> GetChipTemperature(CancellationToken cancel);
        Task SetGFIRLPF(LmsChannel type, uint channel, bool enable, double bandwidth, CancellationToken cancel);
        Task Calibrate(LmsChannel type, uint channel, double bandwidth, uint flags, CancellationToken cancel);
        Task SaveConfig(string path, CancellationToken cancel);
        Task LoadConfig(string path, CancellationToken cancel);
        Task Reset(CancellationToken cancel);
        Task SetSampleRate(double rate, uint oversample, CancellationToken cancel);
        Task SetSampleRateDir(LmsChannel type, double rate, uint oversample, CancellationToken cancel);
        Task SetFrequency(LmsChannel type, uint channel, double freq, CancellationToken cancel);
        Task SetAntenna(LmsChannel type, uint channel, uint index, CancellationToken cancel);

        Task SetNormalizedGain(LmsChannel type, uint channel, double normalizedGain, CancellationToken cancel);
        Task<double> GetNormalizedGain(LmsChannel type, uint channel, CancellationToken cancel);
        Task SetNormalizedGainDbm(LmsChannel type, uint channel, uint gainDbm, CancellationToken cancel);
        Task<uint> GetNormalizedGainDbm(LmsChannel type, uint channel, CancellationToken cancel);

        Task<ILmsStream> CreateStream(LmsChannel type, uint channel, uint bufferLength, float throughputVsLatency = 0.5F, bool flushPartialPacket = true, bool waitForTimestamp = false, CancellationToken cancel = default);

        Task EnableChannel(LmsChannel type, uint channel, bool isEnable, CancellationToken cancel);
        Task SetBandWidth(LmsChannel type, uint channel, double bandwidth, CancellationToken cancel);


        Task SetLPF(LmsChannel type, uint channel, bool enable, CancellationToken cancel);
        Task SetLPFBw(LmsChannel type, uint channel, double bandwidth, CancellationToken cancel);

        Task<ushort> ReadFpgaRegister(ushort address, CancellationToken cancel);
        Task WriteFpgaRegister(ushort address, ushort value, CancellationToken cancel);
        Task<ushort> ReadLMSReg(uint address, CancellationToken cancel);
        Task WriteLMSReg(uint address, ushort value, CancellationToken cancel);

        Task WriteLMSParam(LMS7Parameter param, ushort value, CancellationToken cancel);
        Task<ushort> ReadLMSParam(LMS7Parameter param, CancellationToken cancel);

        Task WriteCustomRegister(ushort addr, ushort val, CancellationToken cancel);
        Task<ushort> ReadCustomRegister(ushort addr, CancellationToken cancel);

        
        Task WriteGpioDirection(ReadOnlyMemory<byte> val, CancellationToken cancel);
        public Task WriteGpio8Direction(byte val, CancellationToken cancel)
        {
            return WriteGpioDirection(new ReadOnlyMemory<byte>(new[] { val }), cancel);
        }
        Task WriteGpio(ReadOnlyMemory<byte> val, CancellationToken cancel);
        public Task WriteGpio8(byte val, CancellationToken cancel)
        {
            return WriteGpio(new ReadOnlyMemory<byte>(new[] { val }), cancel);
        }
        Task ReadGpioDirection(Memory<byte> val, CancellationToken cancel);
        public async Task<byte> ReadGpio8Direction(CancellationToken cancel)
        {
            var arr = new byte[1] ;
            await ReadGpioDirection(arr, cancel);
            return arr[0];
        }
        Task ReadGpio(Memory<byte> val, CancellationToken cancel);
        public async Task<byte> ReadGpio8( CancellationToken cancel)
        {
            var arr = new byte[1];
            await ReadGpio(arr, cancel);
            return arr[0];
        }

    }

    public static class LimeSdrDeviceHelper
    {

        /// <summary>
        /// Setup requirements for digital RSSI
        /// </summary>
        /// <param name="src"></param>
        /// <param name="samples">any value from 0 to 7 (from 2^7 to 2^14 samples), set how many samples used to calculate RSSI</param>
        /// <param name="cancel"></param>
        /// <returns></returns>
        public static async Task EnableRSSIMeasure(this ILimeSdrDevice src, byte samples = 1, CancellationToken cancel = default)
        {
            // https://discourse.myriadrf.org/t/read-rx-digital-rssi-value/1320
            await src.WriteLMSParam(LimeSdrParams.LMS7_AGC_BYP_RXTSP, 0, cancel);
            await src.WriteLMSParam(LimeSdrParams.LMS7_AGC_MODE_RXTSP, 1, cancel);
            await src.WriteLMSParam(LimeSdrParams.LMS7_AGC_AVG_RXTSP, samples, cancel);
        }

        public static async Task<uint> ReadRSSI(this ILimeSdrDevice src, CancellationToken cancel)
        {
            //https://github.com/myriadrf/LimeSuite/blob/3e8503f8eeacd07d072d5eecc02682a91a98867b/src/lms7002m/LMS7002M_RxTxCalibrations.cpp#L177
            await src.WriteLMSParam(LimeSdrParams.LMS7_CAPSEL, 0, cancel);
            await src.WriteLMSParam(LimeSdrParams.LMS7_CAPTURE, 0, cancel);
            await src.WriteLMSParam(LimeSdrParams.LMS7_CAPTURE, 1, cancel);
            await src.WriteLMSParam(LimeSdrParams.LMS7_CAPTURE, 0, cancel);
            var val1 = (uint)await src.ReadLMSReg(0x040E, cancel);
            var val2 = (uint)await src.ReadLMSReg(0x040F, cancel);
            return ((val1 & 0x3) | (val2 << 2)) & 0x3FFFF;
        }

        public static async Task WriteFpgaRegisterBits(this ILimeSdrDevice src, ushort address, ushort index, ushort length, ushort value, CancellationToken cancel)
        {
            if (length is <= 0 or > 16) throw new Exception("Error length");
            var mask = 1;
            var reg = await src.ReadFpgaRegister(address, cancel);
            for (int i = index; i < index + length; i++, mask <<= 1)
            {
                if ((value & mask) > 0)
                {
                    reg |= (ushort)(1u << i);
                }
                else
                {
                    reg &= (ushort)(~(1u << i));
                }
            }
            await src.WriteFpgaRegister(address, reg, cancel);
        }

        public static async Task<ushort> ReadFpgaRegisterBits(this ILimeSdrDevice src, ushort address, ushort index, ushort length, CancellationToken cancel)
        {
            if (length is <= 0 or > 16) throw new Exception("Error length");
            var reg = await src.ReadFpgaRegister(address, cancel);
            reg <<= sizeof(ushort) - length;
            reg >>= index;
            return reg;
        }
    }
}
