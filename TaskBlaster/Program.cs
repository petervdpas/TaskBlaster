using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using TaskBlaster.Connections;
using TaskBlaster.Dialogs;
using TaskBlaster.Engine;
using TaskBlaster.Externals;
using TaskBlaster.Forms;
using TaskBlaster.Interfaces;
using TaskBlaster.Secrets;
using TaskBlaster.UI;
using TaskBlaster.Views;

namespace TaskBlaster;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--seed-demos")
            return SeedDemos();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    /// <summary>
    /// Dev-only helper: copy every file under the app's shipped
    /// DemoScripts/ and DemoForms/ output directories into the user's
    /// configured scripts and forms folders, overwriting existing files.
    /// The regular first-run seeder in MainWindow only copies *missing*
    /// files; this forces an update after the shipped demos change in
    /// the repo.
    ///
    /// Usage: <c>dotnet run --project TaskBlaster -- --seed-demos</c>
    /// </summary>
    private static int SeedDemos()
    {
        var config = new ConfigStore();
        config.Load();
        Directory.CreateDirectory(config.ScriptsFolder);
        Directory.CreateDirectory(config.FormsFolder);
        var demoNugetsFolder = Path.Combine(
            Path.GetDirectoryName(config.VaultFolder)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "demo-nugets");
        Directory.CreateDirectory(demoNugetsFolder);

        var copied = 0;
        copied += ForceCopyDemos("DemoScripts", config.ScriptsFolder, "*.csx");
        copied += ForceCopyDemos("DemoForms",   config.FormsFolder,   "*.json");
        copied += ForceCopyDemos("DemoNugets",  demoNugetsFolder,     "*.nupkg");
        Console.WriteLine($"Done. {copied} file(s) written.");
        return 0;
    }

    private static int ForceCopyDemos(string sourceName, string targetFolder, string pattern)
    {
        var src = Path.Combine(AppContext.BaseDirectory, sourceName);
        if (!Directory.Exists(src))
        {
            Console.Error.WriteLine($"skipping {sourceName}: source folder not found at {src}");
            return 0;
        }
        Console.WriteLine($"{sourceName} -> {targetFolder}");
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(src, pattern))
        {
            var name = Path.GetFileName(file);
            var dst = Path.Combine(targetFolder, name);
            var verb = File.Exists(dst) ? "updated" : "added  ";
            File.Copy(file, dst, overwrite: true);
            Console.WriteLine($"  {verb} {name}");
            count++;
        }
        return count;
    }

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
        services.AddSingleton<IVaultService, VaultService>();
        services.AddSingleton<IConnectionStore>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfigStore>();
            // Connections live next to the vault folder by convention. Anchoring
            // on VaultFolder's parent puts the file under ~/.taskblaster/ for
            // the default config and follows the user if they relocate the
            // TaskBlaster home (the three folders move together via Settings).
            var anchor = Path.GetDirectoryName(cfg.VaultFolder)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new ConnectionStore(Path.Combine(anchor, "connections.json"));
        });
        services.AddSingleton<ExternalReferenceManager>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfigStore>();
            // Imported NuGet packages live under ~/.taskblaster/packages/ by
            // the same anchoring convention as connections.json.
            var anchor = Path.GetDirectoryName(cfg.VaultFolder)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new ExternalReferenceManager(cfg, Path.Combine(anchor, "packages"));
        });
        services.AddSingleton<LoadedReferenceCatalog>();

        services.AddSingleton<App>();
        services.AddTransient<SplashWindow>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
