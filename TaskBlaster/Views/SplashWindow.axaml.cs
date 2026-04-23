using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform;

namespace TaskBlaster.Views;

public partial class SplashWindow : Window
{
    private bool _dismissed;

    public SplashWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {AppInfo.Version}";
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Dismiss();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Enter or Key.Space or Key.Escape) Dismiss();
    }

    private void Dismiss()
    {
        if (_dismissed) return;
        _dismissed = true;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var main = new MainWindow
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://TaskBlaster/Images/taskblaster.ico")))
            };
            desktop.MainWindow = main;
            main.Show();
        }

        Close();
    }
}
