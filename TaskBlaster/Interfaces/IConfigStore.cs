using System.Collections.Generic;
using TaskBlaster.Externals;

namespace TaskBlaster.Interfaces;

/// <summary>
/// Stores and retrieves TaskBlaster's persisted configuration
/// (scripts folder, forms folder, etc.). Implementations decide where
/// to persist — a JSON file on disk, in memory for tests, etc.
/// </summary>
public interface IConfigStore
{
    string ScriptsFolder { get; set; }
    string FormsFolder   { get; set; }
    string VaultFolder   { get; set; }

    /// <summary>
    /// Persisted theme name. Defaults to <see cref="IThemeService.DefaultTheme"/>
    /// on a fresh install. Validated against <see cref="IThemeService.AvailableThemes"/>
    /// at apply-time; an unknown value falls back to the default.
    /// </summary>
    string Theme { get; set; }

    /// <summary>
    /// Whether the bottom Terminal panel is shown. Persists across sessions
    /// so the user's last choice is restored on next launch.
    /// </summary>
    bool TerminalVisible { get; set; }

    /// <summary>
    /// Which highlighter the script editor uses: <c>"Native"</c> for
    /// AvaloniaEdit's xshd highlighter (lighter, smoother scrolling) or
    /// <c>"TextMate"</c> for the VS Code-style colours (richer, heavier).
    /// Defaults to Native on a fresh install.
    /// </summary>
    string EditorHighlighter { get; set; }

    /// <summary>
    /// Whether the editor shows the folding margin and collapses multi-line
    /// brace pairs. Defaults to on.
    /// </summary>
    bool CodeFolding { get; set; }

    /// <summary>
    /// Loose <c>.dll</c> file paths the user has added on the External tab.
    /// Each is loaded into the script engine at startup via
    /// <c>Assembly.LoadFrom</c>.
    /// </summary>
    IList<string> ExternalDlls { get; }

    /// <summary>
    /// NuGet packages the user has imported via the External tab. The
    /// actual files live under <c>~/.taskblaster/packages/&lt;Id&gt;/&lt;Version&gt;/</c>;
    /// this list is just the identities so we can rediscover them on next
    /// launch.
    /// </summary>
    IList<ExternalPackageRef> ExternalPackages { get; }

    /// <summary>Load values from the backing store. No-op if nothing persisted yet.</summary>
    void Load();

    /// <summary>Persist current values to the backing store.</summary>
    void Save();
}
