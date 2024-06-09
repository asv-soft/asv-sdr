using System;
using System.Composition.Convention;
using System.Composition.Hosting;
using Asv.Cfg;
using Asv.Cfg.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;

namespace Asv.Sdr.Gui;

public partial class App : Application,IApp
{
    public App()
    {
        Configuration = new JsonOneFileConfiguration("config.json", true, TimeSpan.FromMilliseconds(100));
        var conventions = new ConventionBuilder();
        var containerCfg = new ContainerConfiguration()
            .WithAssembly(GetType().Assembly)
            .WithExport(typeof(IDataTemplateHost), this)
            .WithExport(typeof(IApp), this)
            .WithExport(typeof(IConfiguration), Configuration)
            .WithDefaultConventions(conventions);
        
        Container = containerCfg.CreateContainer();
        DataTemplates.Add(new ViewLocator(Container));
    }

    public CompositionHost Container { get; set; }

    public IConfiguration Configuration { get; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Container.GetExport<IShell>()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new ShellView
            {
                DataContext = Container.GetExport<IShell>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}