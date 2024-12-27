using System;
using System.Reactive.Disposables;
using Asv.Common;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Asv.Sdr.Gui;

[ExportView(typeof(AdsbTxViewModel))]
public partial class AdsbTxView : ReactiveUserControl<AdsbTxViewModel>
{
    public AdsbTxView()
    {
        InitializeComponent();
        this.WhenActivated(disp =>
        {
            this.WhenAnyValue(x => x.ViewModel)
                .WhereNotNull()
                .Subscribe(x => x.InitCharts(AvaPlotLms, AvaPlotSh))
                .DisposeWith(disp);
        });
    }
}
