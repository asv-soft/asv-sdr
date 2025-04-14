using System;
using System.Reactive.Disposables;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Asv.Sdr.Gui;

[ExportView(typeof(AdsbReplyViewModel))]
public partial class AdsbReplyView : ReactiveUserControl<AdsbReplyViewModel>
{
    public AdsbReplyView()
    {
        InitializeComponent();
    }
}