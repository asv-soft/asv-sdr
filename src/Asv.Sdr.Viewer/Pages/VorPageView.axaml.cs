using System;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Mixins;
using Avalonia.ReactiveUI;
using ReactiveUI;
using ScottPlot;
using ScottPlot.Plottable;

namespace Asv.Sdr.Viewer
{
    [ExportView(typeof(VorPageViewModel))]
    public partial class VorPageView : ReactiveUserControl<VorPageViewModel>
    {
        private SignalPlot _signal;

        public VorPageView()
        {
            InitializeComponent();
            this.WhenActivated(disp =>
            {
                AvaPlot00.Plot.Style(Style.Gray1);
                AvaPlot01.Plot.Style(Style.Gray1);
                AvaPlot10.Plot.Style(Style.Gray1);
                AvaPlot11.Plot.Style(Style.Gray1);
                this.WhenAnyValue(_ => _.ViewModel)
                    .WhereNotNull()
                    .Subscribe(_ => Init())
                    .DisposeWith(disp);

            });
        }

        private void Init()
        {
            if (ViewModel == null) return;
            AvaPlot00.Plot.Clear();
            ViewModel.InitGraph((AvaPlot00, AvaPlot01, AvaPlot10, AvaPlot11));
        }
    }
}
