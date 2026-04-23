using System;

namespace TaskBlaster.Interfaces;

public interface IThemeService
{
    string DefaultTheme { get; }
    string CurrentTheme { get; }
    event EventHandler<string>? ThemeChanged;
    void Apply(string themeName);
}
