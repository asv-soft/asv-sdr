using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asv.Cfg;
using Asv.Common;
using Asv.Sdr.SignalHound;
using Material.Icons;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ScottPlot.Avalonia;

namespace Asv.Sdr.Gui;

[Export(typeof(IShellPage))]
public class SignalHoundViewModel:ShellPage
{
    private ShBbDevice _device;
    private CancellationTokenSource _cancelStream;
    private AvaPlot? _plot;

    public SignalHoundViewModel(): base(WellKnownUri.Shell + ".bb60c")
    {
        Title = "SH BB60C";
        Icon = MaterialIconKind.ChartFinance;
    }
    
    [ImportingConstructor]
    public SignalHoundViewModel(IConfiguration cfg):this()
    {
        LibHelper.CheckLibraryFiles();
        Devices = ShBbDevice.GetAvailableDevices().ToArray();
        SelectedDevice = Devices.FirstOrDefault();
        Connect = ReactiveCommand
            .CreateRunInBackground(ConnectImpl)
            .DisposeItWith(Disposable);
    }

    public void InitCharts(AvaPlot plot)
    {
        _plot = plot;
    }
    private async void ConnectImpl()
    {
        try
        {
            _cancelStream = new CancellationTokenSource();
            _device = new ShBbDevice(SelectedDevice);
            await _device.ConfigureLevel(-20);
            
            await _device.ConfigureCenterSpan( 915_000_000, 20.0e6);
            await _device.ConfigureSweepCoupling( 10e3, 10.0e3, 0.001, BBRbwShape.Nuttall, BbRejection.NoSpurReject);
            await _device.ConfigureAcquisition(BbDetector.MinAndMax, BbScale.LogScale);
            await _device.ConfigureRealTime(200.0, 4);
            await _device.Init(BBMode.RealTime, 0, CancellationToken.None);
            var traceInfo = await _device.QueryTraceInfo();
            var realTimeInfo = await _device.QueryRealTimeInfo();

            var sweepMin = new float[traceInfo.TraceLen];
            var sweepMax = new float[traceInfo.TraceLen];
            var frame = new float[realTimeInfo.FrameHeight * realTimeInfo.FrameWidth];
            var alphaFrame = new float[realTimeInfo.FrameHeight * realTimeInfo.FrameWidth];
            
            var readThread = new Thread(() =>
            {
                while (_cancelStream.IsCancellationRequested == false)
                {
                    _device.FetchRealTimeFrame(sweepMin, sweepMax, frame, alphaFrame).Wait();
                    var tcs = new TaskCompletionSource();
                    RxApp.MainThreadScheduler.Schedule(() =>
                    {
                        _plot?.Plot.Clear();
                        _plot?.Plot.Add.Signal(sweepMin);
                        _plot?.Plot.Add.Signal(sweepMax);
                        /*_plot?.Plot.Add.Heatmap(ConvertTo2DArray(alphaFrame, realTimeInfo.FrameHeight,
                            realTimeInfo.FrameWidth));*/
                        
                        _plot?.Plot.Axes.AutoScale();
                        _plot?.Refresh();
                        tcs.TrySetResult();
                    });
                    tcs.Task.Wait();
                    Thread.Sleep(100);
                }    
            });
            readThread.Start();
        }
        catch (Exception e)
        {
            
        }
    }

    
    public static double[,] ConvertTo2DArray(float[] sourceArray, int rows, int cols)
    {
        if (sourceArray.Length != rows * cols)
        {
            throw new ArgumentException("The length of the source array does not match the specified dimensions.");
        }

        var resultArray = new double[rows, cols];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                resultArray[rows - i - 1, j] = sourceArray[i * cols + j];
            }
        }

        return resultArray;
    }

    public IEnumerable<int> Devices { get; set; }
    [Reactive]
    public int SelectedDevice { get; set; }
    public ReactiveCommand<Unit,Unit> Connect { get; }
}