using System;
using System.Reactive.Disposables;
using Asv.Common;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Asv.Sdr.Gui;

[ExportView(typeof(LimeSdrViewModel))]
public partial class LimeSdrView : ReactiveUserControl<LimeSdrViewModel>
{
    public LimeSdrView()
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
