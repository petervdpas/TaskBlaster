using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using TaskBlaster.Dialogs;
using TaskBlaster.Engine;
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

    private readonly ScriptBlaster _blaster = new();
    private CancellationTokenSource? _runCts;

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
        _toolbar.SaveClicked   += (_, _) => SaveCurrent();
        _toolbar.RenameClicked += OnRenameClicked;
        _toolbar.DeleteClicked += OnDeleteClicked;

        _editor.DirtyChanged += (_, _) => UpdateDirtyUi();
        _editor.FontSizeChanged += (_, _) => UpdateFontSizeUi();

        ActualThemeVariantChanged += (_, _) => ApplyCurrentTheme();

        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S, KeyModifiers.Control),        Command = new Command(SaveCurrent) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.OemPlus, KeyModifiers.Control),  Command = new Command(_editor.ZoomIn) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Add, KeyModifiers.Control),      Command = new Command(_editor.ZoomIn) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.OemMinus, KeyModifiers.Control), Command = new Command(_editor.ZoomOut) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Subtract, KeyModifiers.Control), Command = new Command(_editor.ZoomOut) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.D0, KeyModifiers.Control),       Command = new Command(_editor.ResetZoom) });

        _terminal.Log($"Scripts folder: {ScriptsFolder}");
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ApplyCurrentTheme();
        UpdateFontSizeUi();
    }

    private void UpdateFontSizeUi()
    {
        _statusBar.FontSizeText = $"{_editor.EditorFontSize:0}px";
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
        foreach (var (name, body) in DemoScripts.All)
            File.WriteAllText(Path.Combine(ScriptsFolder, name), body);
    }

    private void OnScriptSelected(object? sender, string path)
    {
        _editor.LoadFile(path);
        _currentFilePath = path;
        _toolbar.CanModify = true;
        UpdateDirtyUi();
        _terminal.Log($"Loaded: {Path.GetFileName(path)} ({_editor.Text.Length} chars)");
    }

    private void SaveCurrent()
    {
        if (_currentFilePath is null) return;
        _editor.SaveTo(_currentFilePath);
        _terminal.Log($"Saved: {Path.GetFileName(_currentFilePath)}");
        UpdateDirtyUi();
    }

    private void UpdateDirtyUi()
    {
        var name = _currentFilePath is null ? string.Empty : Path.GetFileName(_currentFilePath);
        _statusBar.CurrentFile = _editor.IsDirty && !string.IsNullOrEmpty(name) ? name + " •" : name;
        _toolbar.CanSave = _currentFilePath is not null && _editor.IsDirty;
    }

    private async void OnRunClicked(object? sender, EventArgs e)
    {
        if (_currentFilePath is null)
        {
            _terminal.Log("No script selected.");
            return;
        }
        if (_runCts is not null) return; // already running

        if (_editor.IsDirty) SaveCurrent();

        var path = _currentFilePath;
        var name = Path.GetFileName(path);
        var scriptText = File.ReadAllText(path);

        _runCts = new CancellationTokenSource();
        _toolbar.CanRun = false;
        _toolbar.CanStop = true;
        _statusBar.Status = "Running…";
        _terminal.Log($"▶ {name}");

        BlastResult result;
        try
        {
            result = await _blaster.RunAsync(
                scriptText,
                path,
                line => Dispatcher.UIThread.Post(() => _terminal.Log(line)),
                _runCts.Token);
        }
        finally
        {
            _runCts?.Dispose();
            _runCts = null;
            _toolbar.CanRun = true;
            _toolbar.CanStop = false;
        }

        switch (result.Status)
        {
            case BlastStatus.Ok:
                _terminal.Log($"✓ {name} finished.");
                _statusBar.Status = "Ready";
                break;
            case BlastStatus.Cancelled:
                _terminal.Log($"⊘ {name} cancelled.");
                _statusBar.Status = "Cancelled";
                break;
            case BlastStatus.Error:
                _terminal.Log($"✗ {name} failed: {result.Message}");
                _statusBar.Status = "Error";
                break;
        }
    }

    private void OnStopClicked(object? sender, EventArgs e)
    {
        _runCts?.Cancel();
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
        UpdateDirtyUi();
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
        _toolbar.CanModify = false;
        UpdateDirtyUi();
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

    private sealed class Command : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public Command(Action action) => _action = action;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _action();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
