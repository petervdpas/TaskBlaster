using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace TaskBlaster.Dialogs;

/// <summary>
/// Result returned by <see cref="ConfigDialog"/>. Null fields mean
/// "leave the existing value alone".
/// </summary>
public sealed record ConfigDialogResult(
    string? ScriptsFolder,
    string? FormsFolder,
    string? VaultFolder,
    string? Theme,
    string? Highlighter,
    bool? CodeFolding);

public partial class ConfigDialog : Window
{
    private readonly ComboBox _themeBox;
    private readonly ComboBox _highlighterBox;
    private readonly CheckBox _codeFoldingBox;
    private readonly TextBox _scriptsBox;
    private readonly TextBox _formsBox;
    private readonly TextBox _vaultBox;

    public ConfigDialog() : this("", "", "", new[] { "Industrial" }, "Industrial", "Native", true) { }

    public ConfigDialog(
        string currentScriptsFolder,
        string currentFormsFolder,
        string currentVaultFolder,
        IReadOnlyList<string> availableThemes,
        string currentTheme,
        string currentHighlighter,
        bool currentCodeFolding)
    {
        InitializeComponent();
        _themeBox        = this.FindControl<ComboBox>("ThemeBox")!;
        _highlighterBox  = this.FindControl<ComboBox>("HighlighterBox")!;
        _codeFoldingBox  = this.FindControl<CheckBox>("CodeFoldingBox")!;
        _scriptsBox      = this.FindControl<TextBox>("ScriptsFolderBox")!;
        _formsBox        = this.FindControl<TextBox>("FormsFolderBox")!;
        _vaultBox        = this.FindControl<TextBox>("VaultFolderBox")!;

        _themeBox.ItemsSource = availableThemes;
        _themeBox.SelectedItem = availableThemes.Contains(currentTheme) ? currentTheme : availableThemes[0];

        SelectHighlighter(currentHighlighter);
        _codeFoldingBox.IsChecked = currentCodeFolding;

        _scriptsBox.Text = currentScriptsFolder;
        _formsBox.Text   = currentFormsFolder;
        _vaultBox.Text   = currentVaultFolder;

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close((ConfigDialogResult?)null); };
    }

    private void SelectHighlighter(string current)
    {
        foreach (var item in _highlighterBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, current, StringComparison.OrdinalIgnoreCase))
            {
                _highlighterBox.SelectedItem = item;
                return;
            }
        }
        _highlighterBox.SelectedIndex = 0;
    }

    private async void OnBrowseScripts(object? sender, RoutedEventArgs e) => await Browse(_scriptsBox, "Select scripts folder");
    private async void OnBrowseForms  (object? sender, RoutedEventArgs e) => await Browse(_formsBox,   "Select forms folder");
    private async void OnBrowseVault  (object? sender, RoutedEventArgs e) => await Browse(_vaultBox,   "Select vault folder");

    private async System.Threading.Tasks.Task Browse(TextBox target, string title)
    {
        var sp = StorageProvider;
        if (sp is null) return;
        var start = !string.IsNullOrWhiteSpace(target.Text)
            ? await sp.TryGetFolderFromPathAsync(target.Text)
            : null;
        var picked = await sp.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });
        if (picked.Count == 0) return;
        target.Text = picked[0].Path.LocalPath;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        var scripts     = (_scriptsBox.Text ?? "").Trim();
        var forms       = (_formsBox.Text   ?? "").Trim();
        var vault       = (_vaultBox.Text   ?? "").Trim();
        var theme       = _themeBox.SelectedItem as string;
        var highlighter = (_highlighterBox.SelectedItem as ComboBoxItem)?.Tag as string;

        Close(new ConfigDialogResult(
            ScriptsFolder: string.IsNullOrEmpty(scripts)     ? null : scripts,
            FormsFolder:   string.IsNullOrEmpty(forms)       ? null : forms,
            VaultFolder:   string.IsNullOrEmpty(vault)       ? null : vault,
            Theme:         string.IsNullOrEmpty(theme)       ? null : theme,
            Highlighter:   string.IsNullOrEmpty(highlighter) ? null : highlighter,
            CodeFolding:   _codeFoldingBox.IsChecked));
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close((ConfigDialogResult?)null);
}
