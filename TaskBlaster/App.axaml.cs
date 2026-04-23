using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TaskBlaster.Interfaces;
using TaskBlaster.Views;

namespace TaskBlaster;

public partial class App : Application
{
    private readonly IThemeService _themes;

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
