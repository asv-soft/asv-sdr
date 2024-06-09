using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Reactive;
using Asv.Cfg;
using Asv.Common;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Asv.Sdr.Gui;


public class ShellViewModelConfig
{
    public string? SelectedPage { get; set; }
}

[Export(typeof(IShell))]
public class ShellViewModel : ViewModelBase, IShell
{
    private readonly ShellViewModelConfig _config;


    public ShellViewModel() : base(WellKnownUri.UndefinedUri)
    {
        Items = new IShellPage[]
        {
           new SignalHoundViewModel()
        };
    }

    [ImportingConstructor]
    public ShellViewModel([ImportMany]IEnumerable<IShellPage> pages, IConfiguration cfg)
        :base(WellKnownUri.ShellUri)
    {
        Items = pages;
        _config = cfg.Get<ShellViewModelConfig>();

        this.WhenValueChanged(x => x.SelectedPage, false)
            .Subscribe(x => _config.SelectedPage = x?.Id)
            .DisposeItWith(Disposable);
        
        SelectedPage = Items.FirstOrDefault(x => x.Id == _config.SelectedPage);
        
    }
   

    [Reactive]
    public IShellPage? SelectedPage { get; set; }
    public IEnumerable<IShellPage> Items { get; }
}