using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Linq;
using System.Reflection;
using Asv.Cfg;
using Asv.Cfg.ImMemory;
using Asv.Cfg.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Material.Dialog;

namespace Asv.Sdr.Viewer
{
    public partial class App : Application
    {
        private readonly CompositionContainer _container;

        public App()
        {
            _container = new CompositionContainer(new AggregateCatalog(Catalogs().ToArray()), CompositionOptions.IsThreadSafe);
            var batch = new CompositionBatch();
            batch.AddExportedValue(new ViewLocator(_container));
            RegisterDefaultServices(batch);
            _container.Compose(batch);
        }

        private void RegisterDefaultServices(CompositionBatch batch)
        {
            batch.AddExportedValue(_container);

            if (Design.IsDesignMode)
            {
                batch.AddExportedValue<IConfiguration>(new InMemoryConfiguration());
            }
            else
            {
                batch.AddExportedValue<IConfiguration>(new JsonOneFileConfiguration("config.json", true, null));
            }
        }

        private IEnumerable<Assembly> Assemblies()
        {
            yield return typeof(MainShell).Assembly;
        }

        private IEnumerable<ComposablePartCatalog> Catalogs()
        {
            foreach (var assembly in Assemblies().Distinct())
            {
                yield return new AssemblyCatalog(assembly);
            }

            // Enable this feature to load plugins at runtime

            // var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            // if (dir != null)
            // {
            //     var cat = new DirectoryCatalog(dir, "Asv.Drones.Plugins.*.dll");
            //     cat.Refresh();
            //     yield return cat;
            // }
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            DataTemplates.Add(_container.GetExportedValue<ViewLocator>());
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainShell{DataContext = _container.GetExportedValue<MainShellViewModel>()};
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
