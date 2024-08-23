using System;
using Asv.Common;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Asv.Sdr.Gui;

[ExportView(typeof(AdsbRxViewModel))]
public partial class AdsbRxView : ReactiveUserControl<AdsbRxViewModel>
{
    public AdsbRxView()
    {
        InitializeComponent();
        this.WhenActivated(disp =>
        {
            this.WhenAnyValue(x => x.ViewModel)
                .WhereNotNull()
                .Subscribe(x => x.InitCharts(Plot1,Plot2,Plot3))
                .DisposeItWith(disp);

        });
    }
}