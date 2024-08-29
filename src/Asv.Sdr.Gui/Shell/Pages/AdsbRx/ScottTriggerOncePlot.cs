using System;
using System.Collections.Concurrent;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Threading;
using Asv.Sdr.DebugPlot;
using ReactiveUI;
using ScottPlot;
using ScottPlot.Avalonia;

namespace Asv.Sdr.Gui;

public static class DebugPlotHelper
{
    public static Alignment Convert(AnnotationPosition position)
    {
        return position switch
        {
            AnnotationPosition.UpperLeft => Alignment.UpperLeft,
            AnnotationPosition.UpperCenter => Alignment.UpperCenter,
            AnnotationPosition.UpperRight => Alignment.UpperRight,
            AnnotationPosition.MiddleLeft => Alignment.MiddleLeft,
            AnnotationPosition.MiddleCenter => Alignment.MiddleCenter,
            AnnotationPosition.MiddleRight => Alignment.MiddleRight,
            AnnotationPosition.LowerLeft => Alignment.LowerLeft,
            AnnotationPosition.LowerCenter => Alignment.LowerCenter,
            AnnotationPosition.LowerRight => Alignment.LowerRight,
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, null)
        };
    }
}

public class ScottTriggerOncePlot(AvaPlot plot) : IDebugPlot
{
    private readonly Subject<Unit> _onTrigger = new();
    private bool _plotOnce;
    public bool IsEnabled => true;
    public bool IsPlotEnabled { get; set; } = true;

    public void PlotOnce()
    {
        _plotOnce = true;
    }
    
    public void Begin()
    {
        if (!_plotOnce || !IsPlotEnabled) return;
        RxApp.MainThreadScheduler.Schedule(()=>plot.Plot.Clear());
    }

    public void AddSignal(string name, double[] values)
    {
        if (!_plotOnce || !IsPlotEnabled) return;
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            var plot1 = plot.Plot.Add.Signal(values);
            plot1.LegendText = name;
        });
    }

    public void AddHorizontalLine(string name, double value)
    {
        if (!_plotOnce || !IsPlotEnabled) return;
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            var plot1 = plot.Plot.Add.HorizontalLine(value);
            plot1.LegendText = name;
        });
    }

    public void AddVerticalLine(string name, int xValue)
    {
        if (!_plotOnce || !IsPlotEnabled) return;
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            var plot1 = plot.Plot.Add.VerticalLine(xValue);
            plot1.LegendText = name;
        });
    }

    public void AddMarker(string name, string text, int xValue, double yValue)
    {
        if (!_plotOnce || !IsPlotEnabled) return;
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            var plot1 = plot.Plot.Add.Marker(xValue, yValue);
            plot1.LegendText = name + " " + text;
        });
    }

    public void AddAnnotation(string name, string text, AnnotationPosition position = AnnotationPosition.UpperLeft)
    {
        if (!_plotOnce || !IsPlotEnabled) return;
        RxApp.MainThreadScheduler.Schedule(() => plot.Plot.Add.Annotation(text, DebugPlotHelper.Convert(position)));
    }

    

    public void End()
    {
        if (!_plotOnce || !IsPlotEnabled) return;
        RxApp.MainThreadScheduler.Schedule(plot.Refresh);
        _onTrigger.OnNext(Unit.Default);
        _plotOnce = false;
    }

    public IObservable<Unit> OnTrigger => _onTrigger;

    public void Dispose()
    {
        _onTrigger.Dispose();
    }
}

public class ScottDebugPlot(AvaPlot plot) : IDebugPlot
{
    private readonly Subject<Unit> _onTrigger = new();
    public bool IsEnabled => true;
    public bool IsPlotEnabled { get; set; } = true;

    public void Begin()
    {
        if (!IsPlotEnabled) return;
        RxApp.MainThreadScheduler.Schedule(()=>plot.Plot.Clear());
    }

    public void AddSignal(string name, double[] values)
    {
        if (!IsPlotEnabled) return;
        RxApp.MainThreadScheduler.Schedule(()=>
        {
            var plot1 = plot.Plot.Add.Signal(values);
            plot1.LegendText = name;
        });
    }

    public void AddHorizontalLine(string name, double value)
    {
        if (!IsPlotEnabled) return;
        RxApp.MainThreadScheduler.Schedule(()=>
        {
            var plot1 = plot.Plot.Add.HorizontalLine(value);
            plot1.LegendText = name;
        });

    }

    public void AddVerticalLine(string name, int xValue)
    {
        if (!IsPlotEnabled) return;
        RxApp.MainThreadScheduler.Schedule(()=>
        {
            var plot1 = plot.Plot.Add.VerticalLine(xValue);
            plot1.LegendText = name;
        });

    }

    public void AddMarker(string name, string text, int xValue, double yValue)
    {
        if (!IsPlotEnabled) return;
        RxApp.MainThreadScheduler.Schedule(()=>
        {
            var plot1 = plot.Plot.Add.Marker(xValue, yValue);
            plot1.LegendText = name +" "+ text;
        });

    }

    public void AddAnnotation(string name, string text, AnnotationPosition position = AnnotationPosition.UpperLeft)
    {
        if (!IsPlotEnabled) return;
        RxApp.MainThreadScheduler.Schedule(() => plot.Plot.Add.Annotation(text, DebugPlotHelper.Convert(position)));
    }

    public void End()
    {
        _onTrigger.OnNext(Unit.Default);
        if (!IsPlotEnabled) return;
        RxApp.MainThreadScheduler.Schedule(plot.Refresh);

    }

    public IObservable<Unit> OnTrigger => _onTrigger;

    public void Dispose()
    {
        _onTrigger.Dispose();
    }
}

public class ScottDebugTriggerPlot : IDebugPlot
{
    private readonly ConcurrentQueue<Action> _actions = new();
    private readonly Subject<Unit> _onTrigger = new();
    private readonly AvaPlot _plot;

    public ScottDebugTriggerPlot(AvaPlot plot, IObservable<Unit> drawTrigger)
    {
        _plot = plot;
        drawTrigger.Subscribe(_ =>
        {
            while (_actions.TryDequeue(out var action))
            {
                RxApp.MainThreadScheduler.Schedule(action);
            }
            _onTrigger.OnNext(Unit.Default);
        });
    }

    public bool IsEnabled => true;
    public bool IsPlotEnabled { get; set; } = true;

    public void Begin()
    {
        _actions.Clear();
        if (!IsPlotEnabled) return;
        _actions.Enqueue(()=>_plot.Plot.Clear());
    }

    public void AddSignal(string name, double[] values)
    {
        if (!IsPlotEnabled) return;
        _actions.Enqueue(()=>
        {
            var plot1 = _plot.Plot.Add.Signal(values);
            plot1.LegendText = name;
        });
    }

    public void AddHorizontalLine(string name, double value)
    {
        if (!IsPlotEnabled) return;
        _actions.Enqueue(()=>
        {
            var plot1 = _plot.Plot.Add.HorizontalLine(value);
            plot1.LegendText = name;
        });
    }

    public void AddVerticalLine(string name, int xValue)
    {
        if (!IsPlotEnabled) return;
        _actions.Enqueue(()=>
        {
            var plot1 = _plot.Plot.Add.VerticalLine(xValue);
            plot1.LegendText = name;
        });
    }

    public void AddMarker(string name, string text, int xValue, double yValue)
    {
        if (!IsPlotEnabled) return;
        _actions.Enqueue(()=>
        {
            var plot1 = _plot.Plot.Add.Marker(xValue, yValue);
            plot1.LegendText = name +" "+ text;
        });
    }

    public void AddAnnotation(string name, string text, AnnotationPosition position = AnnotationPosition.UpperLeft)
    {
        if (!IsPlotEnabled) return;
        _actions.Enqueue(() => _plot.Plot.Add.Annotation(text, DebugPlotHelper.Convert(position)));
    }

    public void End()
    {
        if (!IsPlotEnabled) return;
        _actions.Enqueue(_plot.Refresh);
    }

    public IObservable<Unit> OnTrigger => _onTrigger;

    public void Dispose()
    {
        _onTrigger.Dispose();
    }
}