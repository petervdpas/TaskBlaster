using System;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using TaskBlaster.Interfaces;

namespace TaskBlaster.UI;

public sealed class ThemeService : IThemeService
{
    public string DefaultTheme => "Industrial";
    public string CurrentTheme { get; private set; }

    public event EventHandler<string>? ThemeChanged;

    public ThemeService()
    {
        CurrentTheme = DefaultTheme;
    }

    public void Apply(string themeName)
    {
        var app = Application.Current
            ?? throw new InvalidOperationException("Application not ready");

        CurrentTheme = themeName;

        app.Resources.MergedDictionaries.Clear();

        app.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://TaskBlaster"))
        {
            Source = new Uri("avares://TaskBlaster/Themes/Base.axaml")
        });

        app.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://TaskBlaster"))
        {
            Source = new Uri($"avares://TaskBlaster/Themes/{themeName}.axaml")
        });

        app.RequestedThemeVariant = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase)
            ? ThemeVariant.Light
            : ThemeVariant.Dark;

        ThemeChanged?.Invoke(this, themeName);
    }
}
