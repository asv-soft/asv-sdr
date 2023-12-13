using System;
using System.Threading;
using System.Threading.Tasks;
using Asv.Common;

namespace Asv.Sdr.LimeSdr
{
    public class LimeTxSourceIqConfig
    {
        public double Frequency { get; set; }
        public LmsPathTx Path { get; set; } = LmsPathTx.LMS_PATH_TX1;
        public double BandWidth { get; set; } = 96_000;
        public double Gain { get; set; } = 1;
        public double SampleRate { get; set; } = 96_000;
        public bool LmsSelfCalibrate { get; set; } = true;
        public uint Channel { get; set; } = 0;
    }

    public class LimeWriterIq : DisposableOnceWithCancel, IWriterIq<float>
    {
        private readonly ILimeSdrDevice _device;
        private readonly LimeTxSourceIqConfig _config;
        private ILmsStream _txStream;
        const uint MaxRssi = 0x15FF4;

        public LimeWriterIq(ILimeSdrDevice device, LimeTxSourceIqConfig config)
        {
            _device = device;
            _config = config;
            Init(config).Wait(DisposeCancel);
        }

        private async Task Init(LimeTxSourceIqConfig config)
        {

            await _device.EnableChannel(LmsChannel.Tx, config.Channel, true, DisposeCancel);
            await _device.SetSampleRate(config.SampleRate, 0, DisposeCancel);
            await _device.SetFrequency(LmsChannel.Tx, config.Channel, config.Frequency, DisposeCancel);
            await _device.SetAntenna(LmsChannel.Tx, config.Channel, (uint)config.Path, DisposeCancel);
            await _device.SetBandWidth(LmsChannel.Tx, config.Channel, config.BandWidth, DisposeCancel);
            await _device.SetNormalizedGain(LmsChannel.Tx, config.Channel, config.Gain, DisposeCancel);
            await _device.SetLPFBw(LmsChannel.Tx, config.Channel, config.BandWidth, DisposeCancel);
            await _device.SetLPF(LmsChannel.Tx, config.Channel, true, DisposeCancel);
            await _device.SetGFIRLPF(LmsChannel.Tx, config.Channel, true, config.BandWidth, DisposeCancel);
            
            if (config.LmsSelfCalibrate)
            {
                await _device.Calibrate(LmsChannel.Tx, config.Channel, config.BandWidth, 0, DisposeCancel);
            }
            _txStream = await _device.CreateStream(LmsChannel.Tx, config.Channel, (uint)config.SampleRate);
            await _txStream.Start(DisposeCancel);
        }

        public async Task<double> GetLevel(CancellationToken cancel)
        {
            var gain = await _device.GetNormalizedGainDbm(LmsChannel.Tx, _config.Channel, DisposeCancel);
            var rssi = await _device.ReadRSSI(DisposeCancel);
            var result = 20 * Math.Log10((float)rssi / MaxRssi) - gain;
            return result;
        }

        public Task<int> Write(ReadOnlyMemory<float> iqBuffer, CancellationToken cancel = default)
        {
            return _txStream.Write(iqBuffer, 1000, DisposeCancel);
        }

        public async Task<double> GetLinkDataRate(CancellationToken cancel = default)
        {
            var streamStatus = await _txStream.GetStatus(cancel);
            return streamStatus.linkRate;
        }
    }
}
