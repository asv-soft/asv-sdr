using System;
using System.Composition.Convention;
using System.Composition.Hosting;
using Asv.Cfg;
using Asv.Sdr.LimeSdr;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Logging;
using ZLogger;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Asv.Sdr.Gui;

public partial class App : Application,IApp
{
    public App()
    {
        var factory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.AddZLoggerConsole(options =>
            {
                options.IncludeScopes = true;
                options.OutputEncodingToUtf8 = false; // !!! Important for GUI applications
                options.UsePlainTextFormatter(formatter =>
                {
                    formatter.SetPrefixFormatter($"{0:HH:mm:ss.fff}|{1:short}|{2,-40}|", (in MessageTemplate template, in LogInfo info) => template.Format(info.Timestamp, info.LogLevel,info.Category));
                    formatter.SetExceptionFormatter((writer, ex) => Utf8StringInterpolation.Utf8String.Format(writer, $"{ex.Message}"));
                });
                
            });
            builder.AddZLoggerRollingFile((dt, index) => $"logs/{dt:yyyy-MM-dd}_{index}.logs", 1024 * 1024);
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        LmsLogManager.SetLoggerFactory(factory); // this is for static logging LMS devices
        
        Configuration = new JsonOneFileConfiguration("config.json", true, TimeSpan.FromMilliseconds(100));
        var conventions = new ConventionBuilder();
        var containerCfg = new ContainerConfiguration()
            .WithAssembly(GetType().Assembly)
            .WithExport(typeof(IDataTemplateHost), this)
            .WithExport(typeof(IApp), this)
            .WithExport(typeof(IConfiguration), Configuration)
            .WithExport(typeof(ILoggerFactory), factory)
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