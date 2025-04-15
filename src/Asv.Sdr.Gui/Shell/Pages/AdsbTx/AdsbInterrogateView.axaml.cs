using System;
using System.Reactive.Disposables;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Asv.Sdr.Gui;

[ExportView(typeof(AdsbInterrogateViewModel))]
public partial class AdsbInterrogateView : ReactiveUserControl<AdsbInterrogateViewModel>
{
    public AdsbInterrogateView()
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