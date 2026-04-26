using System;
using System.Collections.Generic;

namespace TaskBlaster.Interfaces;

public interface IThemeService
{
    string DefaultTheme { get; }
    string CurrentTheme { get; }

    /// <summary>Theme names the app knows how to apply. Used by Settings to populate the Theme dropdown.</summary>
    IReadOnlyList<string> AvailableThemes { get; }

    event EventHandler<string>? ThemeChanged;
    void Apply(string themeName);
}
