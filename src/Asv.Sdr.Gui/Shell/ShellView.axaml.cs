using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace Asv.Sdr.Gui;

[ExportView(typeof(ShellViewModel))]
public partial class ShellView : ReactiveUserControl<ShellViewModel>
{
    public ShellView()
    {
        InitializeComponent();
    }
}
