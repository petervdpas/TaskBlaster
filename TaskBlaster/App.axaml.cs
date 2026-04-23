using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TaskBlaster.Interfaces;
using TaskBlaster.Views;

namespace TaskBlaster;

public partial class App : Application
{
    private readonly IThemeService _themes;

    // Required by Avalonia's XAML runtime loader. Not used: we always construct
    // App via the factory in Program.BuildAvaloniaApp so services are injected.
    public App() => throw new InvalidOperationException(
        "App must be constructed via Program.BuildAvaloniaApp so IThemeService is injected.");

    public App(IThemeService themes)
    {
        _themes = themes;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _themes.Apply(_themes.DefaultTheme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new SplashWindow(_themes);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
