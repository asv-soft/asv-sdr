using System;
using System.Reactive;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace Asv.Sdr.Viewer
{
    public partial class MainShell : ReactiveWindow<MainShellViewModel>
    {
        public MainShell()
        {
            InitializeComponent();

            MaxMinWindowCommand = ReactiveCommand.Create(MaxMinWindow);
            CloseCommand = ReactiveCommand.Create(() => Close());
#if DEBUG
            this.AttachDevTools(KeyGesture.Parse("Ctrl+F12"));
#endif

            this.WhenActivated(disp =>
            {
                PageList.SelectedIndex = -1;
            });
        }

        public ReactiveCommand<Unit, Unit> CloseCommand { get; }

        public ReactiveCommand<Unit, Unit> MaxMinWindowCommand { get; }


        private void SelectedPage(int index)
        {
            PageCarousel.SelectedIndex = index;
            NavDrawerSwitch.IsChecked = false;
        }

        private void MaxMinWindow()
        {
            WindowState = WindowState switch
            {
                WindowState.Normal => WindowState.Maximized,
                WindowState.Minimized => WindowState.Maximized,
                WindowState.Maximized => WindowState.Normal,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

    }
}
