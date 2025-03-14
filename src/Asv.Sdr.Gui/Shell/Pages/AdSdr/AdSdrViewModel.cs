using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition;
using System.Linq;
using System.Reactive;
using Asv.Sdr.AdSdr;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Asv.Sdr.Gui;

[Export(typeof(IShellPage))]
public class AdSdrViewModel: ShellPage
{
    private readonly SourceCache<KeyValuePair<string,string>,string> _deviceCache;
    private ReadOnlyObservableCollection<KeyValuePair<string,string>> _devices;
    private AdSdrDevice _device;

    public AdSdrViewModel() : base(WellKnownUri.Shell + ".ad")
    {
        _deviceCache = new SourceCache<KeyValuePair<string, string>,string>(x => x.Key);
        _deviceCache.Connect()
            .Bind(out _devices)
            .Subscribe();
        Update = ReactiveCommand.CreateRunInBackground(UpdateImpl);
        Connect = ReactiveCommand.CreateRunInBackground(ConnectImpl);
    }

    

    private void UpdateImpl()
    {
        try
        {
            foreach (var pair in AdSdrDevice.GetAllDevices())
            {
                _deviceCache.AddOrUpdate(pair);
            }
            
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private void ConnectImpl()
    {
        try
        {
            _device = new AdSdrDevice(SelectedDevice.Key);

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    public ReadOnlyObservableCollection<KeyValuePair<string,string>> Devices => _devices;
    public ReactiveCommand<Unit,Unit> Update { get; set; }
    public ReactiveCommand<Unit,Unit> Connect { get; }
    [Reactive]
    public KeyValuePair<string,string> SelectedDevice { get; set; }
}