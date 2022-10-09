using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Asv.Sdr.LimeSdr;
using Asv.Tools;
using DynamicData;
using JetBrains.Annotations;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ScottPlot.Avalonia;
using ScottPlot.Plottable;

namespace Asv.Sdr.Viewer
{
    [Export(typeof(IMainShellPage))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class VorPageViewModel:MainShellPageBase
    {
        private SignalPlot _signal1;
        private int _readSamples;
        private int _bufferSize;
        private (AvaPlot AvaPlot00, AvaPlot AvaPlot01, AvaPlot AvaPlot10, AvaPlot AvaPlot11) _plot;
        private int _sampleRate;
        private int _index;
        private SignalPlot _signal2;
        private SignalPlot _signal3;
        private SignalPlot _signal4;
        private int _signal1Index;

        public VorPageViewModel() : base("vor")
        {
            Task.Factory.StartNew(() => Init(), TaskCreationOptions.LongRunning);
        }

        [Reactive]
        public string CodeId { get; set; }

        private void Init()
        {
            var dev = LimeSdrDevice.GetAvailableDevices().FirstOrDefault();
            var device = new LimeSdrDevice(dev, true);
            
            _sampleRate = 48_000;
            _readSamples = (int)(_sampleRate / 30 * 1);
            _bufferSize = _readSamples * 2; // I + Q buffer
            var skip = 20;
            var lime = new LimeReaderIq(device, new LimeSourceIqConfig
            {
                Frequency = 110_000_000,
                BandWidth = 32_000,
                Gain = 0.5,
                SampleRate = 48_000,
                GfirEnable = true,
                GfirBandWidth = 32_000,
                LmsLpfEnable = true,
                LmsLpfBandWidth = 32_000,
                LmsSelfCalibrate = true,
                Path = LmsPathRx.LMS_PATH_LNAL,
            });
            _signal1 = _plot.AvaPlot00.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
            _signal2 = _plot.AvaPlot00.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
            _signal3 = _plot.AvaPlot01.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
            _signal4 = _plot.AvaPlot10.Plot.AddSignal(new double[_bufferSize / 2], _sampleRate);
            var source = lime.Sample(_bufferSize, out var start);
            var amCodeId = source.Magnitude().Fft1d().GetAm(_sampleRate, 1020);
            amCodeId.ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
            {
                var index = (_signal1Index++) % _readSamples;
                _signal1.Update(index, data);
                _plot.AvaPlot00.Refresh();
            });
            amCodeId.CodeId(0.05, 0.20, _readSamples, _sampleRate).Subscribe(_ => CodeId = _);
            start();
            // source.Magnitude().SkipEvery(skip).CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
            // {
            //     _signal1.Update(data.Item1);
            // });
            // var filtered = source.AddIqFilter(new LowpassFilter(_sampleRate, 1050), new LowpassFilter(_sampleRate, 1050));
            // filtered.Magnitude().SkipEvery(skip).CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
            // {
            //     _signal2.Update(data.Item1);
            //     _plot.AvaPlot00.Refresh();
            // });
            // var shifted = filtered.FrequencyShift(_sampleRate, 1020);
            // filtered.Magnitude().SkipEvery(skip).CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
            // {
            //     _signal3.Update(data.Item1);
            //     _plot.AvaPlot10.Refresh();
            // });
            // var shiftedFiltered = shifted.AddIqFilter(new LowpassFilter(_sampleRate,150), new LowpassFilter(_sampleRate, 150));
            // shiftedFiltered.Magnitude().SkipEvery(skip).CopyToArray().ObserveOn(RxApp.MainThreadScheduler).Subscribe(data =>
            // {
            //     _signal4.Update(data.Item1);
            //     _plot.AvaPlot01.Refresh();
            // });



        }



        public void InitGraph((AvaPlot AvaPlot00, AvaPlot AvaPlot01, AvaPlot AvaPlot10, AvaPlot AvaPlot11) plot)
        {
            _plot = plot;
        }
    }
    
}
