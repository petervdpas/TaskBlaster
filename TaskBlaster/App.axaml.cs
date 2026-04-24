using System;
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
    private readonly IServiceProvider _services;

    // Required by Avalonia's XAML runtime loader. Not used: we always construct
    // App via the DI container in Program.BuildAvaloniaApp so services are injected.
    public App() => throw new InvalidOperationException(
        "App must be constructed via Program.BuildAvaloniaApp so services are injected.");

    public App(IThemeService themes, IServiceProvider services)
    {
        _themes = themes;
        _services = services;
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
            desktop.MainWindow = _services.GetRequiredService<SplashWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
