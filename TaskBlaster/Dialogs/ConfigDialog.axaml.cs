using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace TaskBlaster.Dialogs;

/// <summary>
/// Result returned by <see cref="ConfigDialog"/>. Null fields mean "leave the existing value alone".
/// </summary>
public sealed record ConfigDialogResult(string? ScriptsFolder, string? FormsFolder, string? VaultFolder);

public partial class ConfigDialog : Window
{
    private readonly TextBox _scriptsBox;
    private readonly TextBox _formsBox;
    private readonly TextBox _vaultBox;

    public ConfigDialog() : this("", "", "") { }

    public ConfigDialog(string currentScriptsFolder, string currentFormsFolder, string currentVaultFolder)
    {
        InitializeComponent();
        _scriptsBox = this.FindControl<TextBox>("ScriptsFolderBox")!;
        _formsBox   = this.FindControl<TextBox>("FormsFolderBox")!;
        _vaultBox   = this.FindControl<TextBox>("VaultFolderBox")!;
        _scriptsBox.Text = currentScriptsFolder;
        _formsBox.Text   = currentFormsFolder;
        _vaultBox.Text   = currentVaultFolder;

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close((ConfigDialogResult?)null); };
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
        var scripts = (_scriptsBox.Text ?? "").Trim();
        var forms   = (_formsBox.Text   ?? "").Trim();
        var vault   = (_vaultBox.Text   ?? "").Trim();
        Close(new ConfigDialogResult(
            ScriptsFolder: string.IsNullOrEmpty(scripts) ? null : scripts,
            FormsFolder:   string.IsNullOrEmpty(forms)   ? null : forms,
            VaultFolder:   string.IsNullOrEmpty(vault)   ? null : vault));
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close((ConfigDialogResult?)null);
}
