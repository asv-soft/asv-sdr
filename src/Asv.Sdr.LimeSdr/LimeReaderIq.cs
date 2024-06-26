using System;
using System.Threading;
using System.Threading.Tasks;
using Asv.Common;

namespace Asv.Sdr.LimeSdr
{
    public class LimeSourceIqConfig
    {
        public double Frequency { get; set; }
        public LmsPathRx Path { get; set; } = LmsPathRx.LMS_PATH_LNAL;
        public double BandWidth { get; set; } = 96_000;
        public double Gain { get; set; } = 1;
        public bool LmsLpfEnable { get; set; } = true;
        public double SampleRate { get; set; } = 96_000;
        public double LmsLpfBandWidth { get; set; } = 66_000;
        public bool GfirEnable { get; set; } = true;
        public double GfirBandWidth { get; set; } = 66_000;
        public bool LmsSelfCalibrate { get; set; } = true;
        public uint Channel { get; set; } = 0;
        public byte AmountDataRssi { get; set; } = 7;
        public uint? RxBufferSize { get; set; }
        public float ThroughputVsLatency { get; set; } = 0.5f;
        public bool FlushPartialPacket { get; set; } = true;
    }

    public class LimeReaderIq: DisposableOnceWithCancel, IReaderIq<float>
    {
        private readonly ILimeSdrDevice _device;
        private readonly LimeSourceIqConfig _config;
        private ILmsStream _rxStream;
        private double _rssi;
        const uint MaxRssi = 0x15FF4;

        public LimeReaderIq(ILimeSdrDevice device, LimeSourceIqConfig config)
        {
            _device = device;
            _config = config;
            Init(config).Wait(DisposeCancel);
        }

        private async Task Init(LimeSourceIqConfig config)
        {
            
            
            await _device.EnableChannel(LmsChannel.Rx, config.Channel, true, DisposeCancel);
            await _device.EnableChannel(LmsChannel.Tx, config.Channel, true, DisposeCancel);
            await _device.SetSampleRate(config.SampleRate, 0, DisposeCancel);
            await _device.SetFrequency(LmsChannel.Rx, config.Channel, config.Frequency, DisposeCancel);
            await _device.SetAntenna(LmsChannel.Rx, config.Channel, (uint)config.Path, DisposeCancel);
            await _device.SetBandWidth(LmsChannel.Rx, config.Channel, config.BandWidth, DisposeCancel);
            await _device.SetNormalizedGain(LmsChannel.Rx, config.Channel, config.Gain, DisposeCancel);
            await _device.EnableRSSIMeasure(config.AmountDataRssi);
            
            // await _device.WriteLMSParam(LimeSdrParams.LMS7_G_PGA_RBB_R3, 24, DisposeCancel);
            if (config.LmsLpfEnable == true)
            {
                await _device.SetLPFBw(LmsChannel.Rx, config.Channel, config.LmsLpfBandWidth, DisposeCancel);
                await _device.SetLPF(LmsChannel.Rx, config.Channel, true, DisposeCancel);
            }
            else
            {
                await _device.SetLPF(LmsChannel.Rx, config.Channel, false, DisposeCancel);
            }
            
            if (config.GfirEnable)
            {
                await _device.SetGFIRLPF(LmsChannel.Rx, config.Channel, true, config.GfirBandWidth, DisposeCancel);
            }
            else
            {
                await _device.SetGFIRLPF(LmsChannel.Rx, config.Channel, false, 96_000, DisposeCancel);
            }
            
            if (config.LmsSelfCalibrate)
            {
                await _device.Calibrate(LmsChannel.Rx, config.Channel, config.BandWidth, 0, DisposeCancel);
            }
        
            
            _rxStream = await _device.CreateStream(LmsChannel.Rx, config.Channel, (uint)( config.RxBufferSize ?? config.SampleRate), throughputVsLatency: config.ThroughputVsLatency, flushPartialPacket:config.FlushPartialPacket);
            await _rxStream.Start(DisposeCancel);
            
        }

        public async Task<double> GetLevel(CancellationToken cancel)
        {
            var gain = await _device.GetNormalizedGainDbm(LmsChannel.Rx, _config.Channel, DisposeCancel);
            var rssi = await _device.ReadRSSI(DisposeCancel);
            var result = 20 * Math.Log10((float)rssi / MaxRssi) - gain;
            return result;
        }

        public Task<int> Read(Memory<float> iqBuffer, CancellationToken cancel = default)
        {
            return _rxStream.Read(iqBuffer, 5000, DisposeCancel);
        }
    }
}
