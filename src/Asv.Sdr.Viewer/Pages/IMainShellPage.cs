using System;
using System.ComponentModel;
using JetBrains.Annotations;
using Material.Dialog;
using Material.Icons;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Asv.Sdr.Viewer
{
    public interface IMainShellPage : INotifyPropertyChanged
    {
        MaterialIconKind Icon { get; }
        bool IsEnabled { get; }
        string Title { get; }
        string Id { get; }
        int Order { get; }
    }

    public class MainShellPageBase : ReactiveObject, IMainShellPage, IActivatableViewModel
    {
        protected MainShellPageBase([NotNull] string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(id));
            Id = id;
            IsEnabled = true;
            Order = int.MinValue;
        }

        public string Id { get; }

        [Reactive]
        public MaterialIconKind Icon { get; set; }

        [Reactive]
        public bool IsEnabled { get; set; }

        [Reactive]
        public string Title { get; set; }

        [Reactive]
        public int Order { get; set; }

        public ViewModelActivator Activator { get; } = new();


    }
}