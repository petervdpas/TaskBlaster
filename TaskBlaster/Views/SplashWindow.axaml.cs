using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Views;

public partial class SplashWindow : Window
{
    private const int AutoDismissSeconds = 5;

    private readonly IServiceProvider _services;
    private DispatcherTimer? _countdownTimer;
    private int _secondsLeft;
    private bool _dismissed;

    // Required by Avalonia's XAML runtime loader; not used at runtime.
    public SplashWindow() => throw new InvalidOperationException(
        "SplashWindow must be constructed via the DI container.");

    public SplashWindow(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        VersionText.Text = $"Version {AppInfo.Version}";
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // Auto-advance to the main window if the user doesn't click through.
        // Tick every second so the hint can count down visibly; dispatcher
        // timer fires on the UI thread so Dismiss() can touch windows safely.
        _secondsLeft = AutoDismissSeconds;
        UpdateHint();
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) =>
        {
            _secondsLeft--;
            if (_secondsLeft <= 0) { Dismiss(); return; }
            UpdateHint();
        };
        _countdownTimer.Start();
    }

    private void UpdateHint()
        => ContinueHint.Text = $"starting in {_secondsLeft} seconds... or click to continue";

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

        _countdownTimer?.Stop();
        _countdownTimer = null;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var main = _services.GetRequiredService<MainWindow>();
            main.Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://TaskBlaster/Images/taskblaster.ico")));
            desktop.MainWindow = main;
            main.Show();
        }

        Close();
    }
}
