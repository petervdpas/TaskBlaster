using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using TaskBlaster.Interfaces;
using TaskBlaster.Views;

namespace TaskBlaster;

public partial class App : Application
{
    private readonly IThemeService _themes;
    private readonly IConfigStore _config;
    private readonly IServiceProvider _services;

    // Required by Avalonia's XAML runtime loader. Not used: we always construct
    // App via the DI container in Program.BuildAvaloniaApp so services are injected.
    public App() => throw new InvalidOperationException(
        "App must be constructed via Program.BuildAvaloniaApp so services are injected.");

    public App(IThemeService themes, IConfigStore config, IServiceProvider services)
    {
        _themes = themes;
        _config = config;
        _services = services;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Load persisted prefs before applying the theme so a user choice
        // sticks across restarts. An unknown theme name falls back to the
        // service default (e.g. on first run, or after an upgrade dropped a theme).
        _config.Load();
        var requested = _config.Theme;
        var theme = _themes.AvailableThemes.Contains(requested, StringComparer.OrdinalIgnoreCase)
            ? requested
            : _themes.DefaultTheme;
        _themes.Apply(theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = _services.GetRequiredService<SplashWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
