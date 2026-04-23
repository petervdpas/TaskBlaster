using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using TaskBlaster.Dialogs;
using TaskBlaster.Views;

namespace TaskBlaster;

public partial class MainWindow : Window
{
    private static readonly string ScriptsFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".taskblaster", "scripts");

    private readonly ToolbarView _toolbar;
    private readonly SidebarView _sidebar;
    private readonly EditorView _editor;
    private readonly TerminalView _terminal;
    private readonly StatusBarView _statusBar;

    private string? _currentFilePath;

    public MainWindow()
    {
        InitializeComponent();

        _toolbar = this.FindControl<ToolbarView>("Toolbar")!;
        _sidebar = this.FindControl<SidebarView>("Sidebar")!;
        _editor = this.FindControl<EditorView>("Editor")!;
        _terminal = this.FindControl<TerminalView>("Terminal")!;
        _statusBar = this.FindControl<StatusBarView>("StatusBar")!;

        EnsureScriptsFolder();
        _sidebar.Folder = ScriptsFolder;
        _sidebar.ScriptSelected += OnScriptSelected;

        _toolbar.RunClicked    += OnRunClicked;
        _toolbar.StopClicked   += OnStopClicked;
        _toolbar.ThemeClicked  += OnThemeClicked;
        _toolbar.NewClicked    += OnNewClicked;
        _toolbar.RenameClicked += OnRenameClicked;
        _toolbar.DeleteClicked += OnDeleteClicked;

        ActualThemeVariantChanged += (_, _) => ApplyCurrentTheme();

        _terminal.Log($"Scripts folder: {ScriptsFolder}");
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ApplyCurrentTheme();
    }

    private void ApplyCurrentTheme()
    {
        var variant = ActualThemeVariant ?? ThemeVariant.Default;
        _statusBar.ThemeName = variant.ToString();
        _editor.ApplyTheme(variant);
    }

    private static void EnsureScriptsFolder()
    {
        if (Directory.Exists(ScriptsFolder)) return;
        Directory.CreateDirectory(ScriptsFolder);
        var sample = Path.Combine(ScriptsFolder, "hello.csx");
        File.WriteAllText(sample,
            "// Sample TaskBlaster script\n" +
            "Console.WriteLine(\"Hello from TaskBlaster!\");\n");
    }

    private void OnScriptSelected(object? sender, string path)
    {
        _editor.LoadFile(path);
        _currentFilePath = path;
        _statusBar.CurrentFile = Path.GetFileName(path);
        _toolbar.CanModify = true;
    }

    private void OnRunClicked(object? sender, EventArgs e)
    {
        if (_currentFilePath is null)
        {
            _terminal.Log("[stub] no script selected.");
            return;
        }
        _statusBar.Status = "Running…";
        _terminal.Log($"[stub] would run: {Path.GetFileName(_currentFilePath)}");
        _statusBar.Status = "Ready";
    }

    private void OnStopClicked(object? sender, EventArgs e)
    {
        _terminal.Log("[stub] stop requested.");
    }

    private void OnThemeClicked(object? sender, EventArgs e)
    {
        var next = ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
        Application.Current!.RequestedThemeVariant = next;
        _terminal.Log($"Theme: {next}");
    }

    private async void OnNewClicked(object? sender, EventArgs e)
    {
        var name = await PromptService.InputAsync(this, "New Script", "File name (without extension):", "new-script");
        if (name is null) return;

        var safe = SanitizeFileName(name);
        if (string.IsNullOrWhiteSpace(safe))
        {
            await PromptService.MessageAsync(this, "Invalid name", "File name cannot be empty or contain invalid characters.");
            return;
        }

        var path = Path.Combine(ScriptsFolder, safe + ".csx");
        if (File.Exists(path))
        {
            await PromptService.MessageAsync(this, "Already exists", $"A script named '{safe}.csx' already exists.");
            return;
        }

        File.WriteAllText(path, $"// {safe}.csx\n");
        _sidebar.Refresh();
        _sidebar.Select(safe + ".csx");
        _terminal.Log($"Created: {safe}.csx");
    }

    private async void OnRenameClicked(object? sender, EventArgs e)
    {
        if (_currentFilePath is null) return;
        var oldName = Path.GetFileNameWithoutExtension(_currentFilePath);
        var name = await PromptService.InputAsync(this, "Rename Script", "New file name (without extension):", oldName);
        if (name is null) return;

        var safe = SanitizeFileName(name);
        if (string.IsNullOrWhiteSpace(safe))
        {
            await PromptService.MessageAsync(this, "Invalid name", "File name cannot be empty or contain invalid characters.");
            return;
        }

        var newPath = Path.Combine(ScriptsFolder, safe + ".csx");
        if (string.Equals(newPath, _currentFilePath, StringComparison.OrdinalIgnoreCase)) return;
        if (File.Exists(newPath))
        {
            await PromptService.MessageAsync(this, "Already exists", $"A script named '{safe}.csx' already exists.");
            return;
        }

        File.Move(_currentFilePath, newPath);
        _currentFilePath = newPath;
        _sidebar.Refresh();
        _sidebar.Select(safe + ".csx");
        _statusBar.CurrentFile = safe + ".csx";
        _terminal.Log($"Renamed to: {safe}.csx");
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_currentFilePath is null) return;
        var fileName = Path.GetFileName(_currentFilePath);
        var ok = await PromptService.ConfirmAsync(this, "Delete Script", $"Delete '{fileName}'? This cannot be undone.");
        if (!ok) return;

        File.Delete(_currentFilePath);
        _terminal.Log($"Deleted: {fileName}");

        _currentFilePath = null;
        _editor.Text = string.Empty;
        _statusBar.CurrentFile = string.Empty;
        _toolbar.CanModify = false;
        _sidebar.Refresh();
    }

    private static string SanitizeFileName(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.EndsWith(".csx", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^4];
        var invalid = Path.GetInvalidFileNameChars();
        if (trimmed.Any(c => invalid.Contains(c))) return string.Empty;
        return trimmed;
    }
}
