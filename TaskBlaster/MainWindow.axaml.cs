using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using GuiBlast.Forms.Rendering;
using GuiBlast.Forms.Result;
using TaskBlaster.Dialogs;
using TaskBlaster.Engine;
using TaskBlaster.Forms;
using TaskBlaster.Interfaces;
using TaskBlaster.Views;

namespace TaskBlaster;

public partial class MainWindow : Window
{
    private readonly ToolbarView _toolbar;
    private readonly SidebarView _sidebar;
    private readonly EditorView _editor;
    private readonly FormDesignerView _designer;
    private readonly TerminalView _terminal;
    private readonly StatusBarView _statusBar;

    private AppMode _mode = AppMode.Scripts;
    private string? _currentFilePath;

    private readonly IScriptBlaster _blaster = new ScriptBlaster();
    private readonly IConfigStore _config = new ConfigStore();
    private readonly IPromptService _prompts;
    private CancellationTokenSource? _runCts;
    private IFormDocument? _currentFormDoc;

    public MainWindow()
    {
        InitializeComponent();
        Title = $"{AppInfo.Name} - v{AppInfo.Version}";
        _prompts = new AvaloniaPromptService(this);

        _toolbar   = this.FindControl<ToolbarView>("Toolbar")!;
        _sidebar   = this.FindControl<SidebarView>("Sidebar")!;
        _editor    = this.FindControl<EditorView>("Editor")!;
        _designer  = this.FindControl<FormDesignerView>("Designer")!;
        _terminal  = this.FindControl<TerminalView>("Terminal")!;
        _statusBar = this.FindControl<StatusBarView>("StatusBar")!;

        _config.Load();
        Directory.CreateDirectory(_config.ScriptsFolder);
        Directory.CreateDirectory(_config.FormsFolder);
        SeedMissingFromFolder("DemoScripts", _config.ScriptsFolder, "*.csx");
        SeedMissingFromFolder("DemoForms",   _config.FormsFolder,   "*.json");

        _sidebar.ScriptSelected += OnFileSelected;

        _toolbar.RunClicked    += OnRunClicked;
        _toolbar.StopClicked   += OnStopClicked;
        _toolbar.ThemeClicked  += OnThemeClicked;
        _toolbar.ConfigClicked += OnConfigClicked;
        _toolbar.NewClicked    += OnNewClicked;
        _toolbar.SaveClicked   += (_, _) => SaveCurrent();
        _toolbar.RenameClicked += OnRenameClicked;
        _toolbar.DeleteClicked += OnDeleteClicked;
        _toolbar.ModeChanged   += (_, mode) => SwitchMode(mode);

        _editor.DirtyChanged   += (_, _) => UpdateDirtyUi();
        _editor.FontSizeChanged += (_, _) => UpdateFontSizeUi();
        _designer.FormSettingsClicked += OnFormSettingsClicked;

        ActualThemeVariantChanged += (_, _) => ApplyCurrentTheme();

        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S, KeyModifiers.Control),        Command = new Command(SaveCurrent) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.OemPlus, KeyModifiers.Control),  Command = new Command(_editor.ZoomIn) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Add, KeyModifiers.Control),      Command = new Command(_editor.ZoomIn) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.OemMinus, KeyModifiers.Control), Command = new Command(_editor.ZoomOut) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Subtract, KeyModifiers.Control), Command = new Command(_editor.ZoomOut) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.D0, KeyModifiers.Control),       Command = new Command(_editor.ResetZoom) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.L, KeyModifiers.Control),        Command = new Command(_terminal.Clear) });

        SwitchMode(AppMode.Scripts);
        _terminal.Log($"Scripts folder: {_config.ScriptsFolder}");
        _terminal.Log($"Forms folder:   {_config.FormsFolder}");
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ApplyCurrentTheme();
        UpdateFontSizeUi();
    }

    private void ApplyCurrentTheme()
    {
        var variant = ActualThemeVariant ?? ThemeVariant.Default;
        _statusBar.ThemeName = variant.ToString();
        _editor.ApplyTheme(variant);
    }

    private void UpdateFontSizeUi() => _statusBar.FontSizeText = $"{_editor.EditorFontSize:0}px";

    private static void SeedMissingFromFolder(string demoFolderName, string targetFolder, string pattern)
    {
        var demoDir = Path.Combine(AppContext.BaseDirectory, demoFolderName);
        if (!Directory.Exists(demoDir)) return;
        foreach (var src in Directory.EnumerateFiles(demoDir, pattern))
        {
            var dst = Path.Combine(targetFolder, Path.GetFileName(src));
            if (!File.Exists(dst)) File.Copy(src, dst);
        }
    }

    // ==================== Mode switching ====================

    private void SwitchMode(AppMode mode)
    {
        _mode = mode;
        _toolbar.Mode = mode;

        _currentFilePath = null;
        _toolbar.CanModify = false;
        UpdateDirtyUi();

        switch (mode)
        {
            case AppMode.Scripts:
                _editor.IsVisible = true;
                _designer.IsVisible = false;
                _sidebar.Header = "Scripts";
                _sidebar.Pattern = "*.csx";
                _sidebar.Folder = _config.ScriptsFolder;
                _toolbar.SetRunLabel("▶ Run");
                break;

            case AppMode.Forms:
                _editor.IsVisible = false;
                _designer.IsVisible = true;
                _sidebar.Header = "Forms";
                _sidebar.Pattern = "*.json";
                _sidebar.Folder = _config.FormsFolder;
                _toolbar.SetRunLabel("👁 Preview");
                break;
        }
    }

    private string CurrentFolder => _mode == AppMode.Forms ? _config.FormsFolder : _config.ScriptsFolder;
    private string CurrentExtension => _mode == AppMode.Forms ? ".json" : ".csx";

    // ==================== Sidebar selection ====================

    private void OnFileSelected(object? sender, string path)
    {
        _currentFilePath = path;
        _toolbar.CanModify = true;

        if (_mode == AppMode.Scripts)
        {
            _editor.LoadFile(path);
            _terminal.Log($"Loaded: {Path.GetFileName(path)} ({_editor.Text.Length} chars)");
        }
        else
        {
            // Detach old document, build a fresh one from the file, attach to designer.
            DetachCurrentFormDoc();
            var doc = FormDocument.LoadFromFile(path);
            doc.DirtyChanged += OnFormDocDirtyChanged;
            _designer.Document = doc;
            _currentFormDoc = doc;
            _terminal.Log($"Loaded form: {Path.GetFileName(path)}");
        }
        UpdateDirtyUi();
    }

    private void DetachCurrentFormDoc()
    {
        if (_currentFormDoc is null) return;
        _currentFormDoc.DirtyChanged -= OnFormDocDirtyChanged;
        _currentFormDoc = null;
    }

    private void OnFormDocDirtyChanged(object? sender, EventArgs e) => UpdateDirtyUi();

    // ==================== Dirty state ====================

    private bool IsDirty => _mode == AppMode.Forms ? (_currentFormDoc?.IsDirty ?? false) : _editor.IsDirty;

    private void UpdateDirtyUi()
    {
        var name = _currentFilePath is null ? string.Empty : Path.GetFileName(_currentFilePath);
        _statusBar.CurrentFile = IsDirty && !string.IsNullOrEmpty(name) ? name + " •" : name;
        _toolbar.CanSave = _currentFilePath is not null && IsDirty;
    }

    // ==================== Save ====================

    private void SaveCurrent()
    {
        if (_currentFilePath is null) return;
        if (_mode == AppMode.Scripts)
        {
            _editor.SaveTo(_currentFilePath);
        }
        else
        {
            if (_currentFormDoc is null) return;
            (_currentFormDoc as FormDocument)?.SaveToFile(_currentFilePath);
        }
        _terminal.Log($"Saved: {Path.GetFileName(_currentFilePath)}");
        UpdateDirtyUi();
    }

    // ==================== Run / Preview ====================

    private async void OnRunClicked(object? sender, EventArgs e)
    {
        if (_currentFilePath is null)
        {
            _terminal.Log(_mode == AppMode.Forms ? "No form selected." : "No script selected.");
            return;
        }

        if (_mode == AppMode.Scripts) await RunScriptAsync(_currentFilePath);
        else                          await PreviewFormAsync();
    }

    private async Task RunScriptAsync(string path)
    {
        if (_runCts is not null) return;
        if (_editor.IsDirty) SaveCurrent();

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
            case BlastStatus.Ok:        _terminal.Log($"✓ {name} finished.");           _statusBar.Status = "Ready";     break;
            case BlastStatus.Cancelled: _terminal.Log($"⊘ {name} cancelled.");           _statusBar.Status = "Cancelled"; break;
            case BlastStatus.Error:     _terminal.Log($"✗ {name} failed: {result.Message}"); _statusBar.Status = "Error"; break;
        }
    }

    private async Task PreviewFormAsync()
    {
        if (_currentFormDoc is null) { _terminal.Log("No form loaded."); return; }
        var json = _currentFormDoc.Snapshot().ToJson();
        _terminal.Log("👁 Previewing form…");
        _statusBar.Status = "Previewing…";
        try
        {
            var result = await DynamicForm.ShowJsonAsync(json);
            _terminal.Log($"Form {(result.Submitted ? "submitted" : "cancelled")}.");
            if (result.Submitted) _terminal.Log(result.ToJson(indented: true));
        }
        catch (Exception ex)
        {
            _terminal.Log($"✗ Preview failed: {ex.Message}");
        }
        finally
        {
            _statusBar.Status = "Ready";
        }
    }

    private void OnStopClicked(object? sender, EventArgs e) => _runCts?.Cancel();

    private void OnThemeClicked(object? sender, EventArgs e)
    {
        var next = ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
        Application.Current!.RequestedThemeVariant = next;
        _terminal.Log($"Theme: {next}");
    }

    private async void OnFormSettingsClicked(object? sender, EventArgs e)
    {
        if (_currentFormDoc is null)
        {
            await _prompts.MessageAsync("Form settings", "Open a form first to edit its settings.");
            return;
        }
        var dlg = new FormSettingsDialog(_currentFormDoc);
        await dlg.ShowDialog(this);
    }

    private async void OnConfigClicked(object? sender, EventArgs e)
    {
        var result = await new ConfigDialog(_config.ScriptsFolder, _config.FormsFolder).ShowDialog<ConfigDialogResult?>(this);
        if (result is null) return;

        var scriptsChanged = await TryApplyFolder(
            result.ScriptsFolder, _config.ScriptsFolder, "Scripts folder",
            path => _config.ScriptsFolder = path);

        var formsChanged = await TryApplyFolder(
            result.FormsFolder, _config.FormsFolder, "Forms folder",
            path => _config.FormsFolder = path);

        if (!scriptsChanged && !formsChanged) return;

        _config.Save();

        // Any folder change invalidates the current selection.
        _currentFilePath = null;
        _editor.Text = string.Empty;
        DetachCurrentFormDoc();
        _designer.Document = null;
        _toolbar.CanModify = false;
        UpdateDirtyUi();

        // Refresh the sidebar for the mode we're in.
        _sidebar.Folder = _mode == AppMode.Forms ? _config.FormsFolder : _config.ScriptsFolder;
    }

    private async System.Threading.Tasks.Task<bool> TryApplyFolder(string? raw, string current, string label, Action<string> apply)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var normalized = Path.GetFullPath(raw);
        if (string.Equals(normalized, current, StringComparison.Ordinal)) return false;

        try { Directory.CreateDirectory(normalized); }
        catch (Exception ex)
        {
            await _prompts.MessageAsync("Invalid folder", $"Could not use '{normalized}' for {label}:\n{ex.Message}");
            return false;
        }

        apply(normalized);
        _terminal.Log($"{label}: {normalized}");
        return true;
    }

    // ==================== New / Rename / Delete (mode-aware) ====================

    private async void OnNewClicked(object? sender, EventArgs e)
    {
        var ext = CurrentExtension;
        var defName = _mode == AppMode.Forms ? "new-form" : "new-script";
        var name = await _prompts.InputAsync("New " + (_mode == AppMode.Forms ? "Form" : "Script"),
            $"File name (without extension):", defName);
        if (name is null) return;

        var safe = SanitizeFileName(name);
        if (string.IsNullOrWhiteSpace(safe))
        {
            await _prompts.MessageAsync("Invalid name", "File name cannot be empty or contain invalid characters.");
            return;
        }

        var path = Path.Combine(CurrentFolder, safe + ext);
        if (File.Exists(path))
        {
            await _prompts.MessageAsync("Already exists", $"A file named '{safe}{ext}' already exists.");
            return;
        }

        if (_mode == AppMode.Forms)
        {
            var blank = Forms.FormEditor.CreateDefault();
            File.WriteAllText(path, blank.ToJson());
        }
        else
        {
            File.WriteAllText(path, $"// {safe}{ext}\n");
        }

        _sidebar.Refresh();
        _sidebar.Select(safe + ext);
        _terminal.Log($"Created: {safe}{ext}");
    }

    private async void OnRenameClicked(object? sender, EventArgs e)
    {
        if (_currentFilePath is null) return;
        var ext = CurrentExtension;
        var oldName = Path.GetFileNameWithoutExtension(_currentFilePath);
        var name = await _prompts.InputAsync("Rename", "New file name (without extension):", oldName);
        if (name is null) return;

        var safe = SanitizeFileName(name);
        if (string.IsNullOrWhiteSpace(safe))
        {
            await _prompts.MessageAsync("Invalid name", "File name cannot be empty or contain invalid characters.");
            return;
        }

        var newPath = Path.Combine(CurrentFolder, safe + ext);
        if (string.Equals(newPath, _currentFilePath, StringComparison.OrdinalIgnoreCase)) return;
        if (File.Exists(newPath))
        {
            await _prompts.MessageAsync("Already exists", $"A file named '{safe}{ext}' already exists.");
            return;
        }

        File.Move(_currentFilePath, newPath);
        _currentFilePath = newPath;
        _sidebar.Refresh();
        _sidebar.Select(safe + ext);
        UpdateDirtyUi();
        _terminal.Log($"Renamed to: {safe}{ext}");
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_currentFilePath is null) return;
        var fileName = Path.GetFileName(_currentFilePath);
        var ok = await _prompts.ConfirmAsync("Delete", $"Delete '{fileName}'? This cannot be undone.");
        if (!ok) return;

        File.Delete(_currentFilePath);
        _terminal.Log($"Deleted: {fileName}");

        _currentFilePath = null;
        if (_mode == AppMode.Scripts)
        {
            _editor.Text = string.Empty;
        }
        else
        {
            DetachCurrentFormDoc();
            _designer.Document = null;
        }
        _toolbar.CanModify = false;
        UpdateDirtyUi();
        _sidebar.Refresh();
    }

    private static string SanitizeFileName(string raw)
    {
        var trimmed = raw.Trim();
        foreach (var e in new[] { ".csx", ".json" })
            if (trimmed.EndsWith(e, StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[..^e.Length];
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
