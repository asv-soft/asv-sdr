using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Asv.Sdr.Gui;

[ExportView(typeof(ModeACInterrogateViewModel))]
public partial class ModeACInterrogateView : ReactiveUserControl<ModeACInterrogateViewModel>
{
    public ModeACInterrogateView()
    {
        InitializeComponent();
        this.WhenActivated(disp =>
        {
            this.WhenAnyValue(x => x.ViewModel)
                .WhereNotNull()
                .Subscribe(x => x.InitCharts(AvaPlotLms,AvaPlotSh))
                .DisposeWith(disp);

        });
    }
}