using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using System;
using TaskBlaster.Dialogs;
using TaskBlaster.Engine;
using TaskBlaster.Forms;
using TaskBlaster.Interfaces;
using TaskBlaster.UI;
using TaskBlaster.Views;

namespace TaskBlaster;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var provider = BuildServiceProvider();

        return AppBuilder.Configure(() => provider.GetRequiredService<App>())
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IConfigStore, ConfigStore>();
        services.AddSingleton<IScriptBlaster, ScriptBlaster>();
        services.AddSingleton<IPromptServiceFactory, AvaloniaPromptServiceFactory>();
        services.AddSingleton<IFormDocumentFactory, FormDocumentFactory>();

        services.AddSingleton<App>();
        services.AddTransient<SplashWindow>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
