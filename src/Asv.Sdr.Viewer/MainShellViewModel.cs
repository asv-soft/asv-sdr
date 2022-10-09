using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using Material.Icons;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Asv.Sdr.Viewer
{
    [Export]
    public class MainShellViewModel:ReactiveObject, IActivatableViewModel
    {
        [ImportingConstructor]
        public MainShellViewModel([ImportMany]IEnumerable<IMainShellPage> pages)
        {
            foreach (var mainShellPage in pages)
            {
                Pages.Add(mainShellPage);
            }

           
        }

        public ObservableCollection<IMainShellPage> Pages { get; set; } = new();

        [Reactive]
        public MaterialIconKind Icon { get; set; }

        [Reactive]
        public string Title { get; set; }

        public ViewModelActivator Activator { get; } = new();
    }
}