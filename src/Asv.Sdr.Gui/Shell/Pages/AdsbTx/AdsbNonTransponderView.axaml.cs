using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Asv.Sdr.Gui;


[ExportView(typeof(AdsbNonTransponderViewModel))]
public partial class AdsbNonTransponderView : ReactiveUserControl<AdsbNonTransponderViewModel>
{
    public AdsbNonTransponderView()
    {
        InitializeComponent();
    }
}