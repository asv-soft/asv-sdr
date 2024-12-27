using System;
using System.Reactive.Disposables;
using Asv.Common;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Asv.Sdr.Gui;

[ExportView(typeof(SignalHoundViewModel))]
public partial class SignalHoundView : ReactiveUserControl<SignalHoundViewModel>
{
    public SignalHoundView()
    {
        InitializeComponent();
        this.WhenActivated(disp =>
        {
            this.WhenAnyValue(x => x.ViewModel)
                .WhereNotNull()
                .Subscribe(x => x.InitCharts(AvaPlot00))
                .DisposeWith(disp);
        });
    }
}
