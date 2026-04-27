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
using SecretBlast;
using TaskBlaster.Ai;
using TaskBlaster.Dialogs;
using TaskBlaster.Engine;
using TaskBlaster.Externals;
using TaskBlaster.Forms;
using TaskBlaster.Interfaces;
using TaskBlaster.Views;

namespace TaskBlaster;

public partial class MainWindow : Window
{
    private readonly ToolbarView _toolbar;
    private readonly ScriptFormActionsView _scriptFormActions;
    private readonly SidebarView _sidebar;
    private readonly EditorView _editor;
    private readonly FormDesignerView _designer;
    private readonly TerminalView _terminal;
    private readonly StatusBarView _statusBar;
    private readonly SecretsView _secrets;
    private readonly ConnectionsView _connections;
    private readonly AssistantView _assistant;
    private readonly ScriptChatView _chat;
    private readonly GridSplitter _chatSplitter;
    private readonly Grid _scriptsFormsWorkspace;
    private readonly Grid _workspaceGrid;
    private readonly GridSplitter _terminalSplitter;
    private GridLength _lastTerminalRowHeight = new(180, GridUnitType.Pixel);
    private GridLength _lastChatColumnWidth = new(380, GridUnitType.Pixel);

    private AppMode _mode = AppMode.Scripts;
    private string? _currentFilePath;

    private readonly IScriptBlaster _blaster;
    private readonly IConfigStore _config;
    private readonly IPromptService _prompts;
    private readonly IThemeService _themes;
    private readonly IFormDocumentFactory _formDocFactory;
    private readonly IVaultService _vaultService;
    private readonly IConnectionStore _connectionStore;
    private readonly ExternalReferenceManager _externals;
    private readonly IKnowledgeBlockStore _knowledge;
    private readonly LoadedReferenceCatalog _catalog;
    private readonly PromptArtifactWriter _artifacts;
    private readonly AiClient _ai;
    private CancellationTokenSource? _runCts;
    private IFormDocument? _currentFormDoc;

    // Required by Avalonia's XAML runtime loader; not used at runtime.
    public MainWindow() => throw new InvalidOperationException(
        "MainWindow must be constructed via the DI container.");

    public MainWindow(
        IThemeService themes,
        IConfigStore config,
        IScriptBlaster blaster,
        IPromptServiceFactory promptFactory,
        IFormDocumentFactory formDocFactory,
        IVaultService vaultService,
        IConnectionStore connectionStore,
        ExternalReferenceManager externals,
        IKnowledgeBlockStore knowledge,
        LoadedReferenceCatalog catalog,
        PromptArtifactWriter artifacts,
        AiClient ai)
    {
        InitializeComponent();
        Title = $"{AppInfo.Name} - v{AppInfo.Version}";
        _themes = themes;
        _config = config;
        _blaster = blaster;
        _formDocFactory = formDocFactory;
        _vaultService = vaultService;
        _connectionStore = connectionStore;
        _externals = externals;
        _knowledge = knowledge;
        _catalog = catalog;
        _artifacts = artifacts;
        _ai = ai;
        _prompts = promptFactory.Create(this);

        _toolbar   = this.FindControl<ToolbarView>("Toolbar")!;
        _sidebar   = this.FindControl<SidebarView>("Sidebar")!;
        _editor    = this.FindControl<EditorView>("Editor")!;
        _designer  = this.FindControl<FormDesignerView>("Designer")!;
        _terminal  = this.FindControl<TerminalView>("Terminal")!;
        _statusBar = this.FindControl<StatusBarView>("StatusBar")!;
        _secrets     = this.FindControl<SecretsView>("Secrets")!;
        _connections = this.FindControl<ConnectionsView>("Connections")!;
        _assistant   = this.FindControl<AssistantView>("Assistant")!;
        _chat        = this.FindControl<ScriptChatView>("Chat")!;
        _chatSplitter = this.FindControl<GridSplitter>("ChatSplitter")!;
        _scriptsFormsWorkspace = this.FindControl<Grid>("ScriptsFormsWorkspace")!;
        _workspaceGrid    = this.FindControl<Grid>("WorkspaceGrid")!;
        _terminalSplitter = this.FindControl<GridSplitter>("TerminalSplitter")!;

        _config.Load();
        Directory.CreateDirectory(_config.ScriptsFolder);
        Directory.CreateDirectory(_config.FormsFolder);
        var anchor = Path.GetDirectoryName(_config.VaultFolder)
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var demoNugetsFolder = Path.Combine(anchor, "demo-nugets");
        var knowledgeFolder = Path.Combine(anchor, "knowledge");
        Directory.CreateDirectory(demoNugetsFolder);
        Directory.CreateDirectory(knowledgeFolder);
        SeedMissingFromFolder("DemoScripts",   _config.ScriptsFolder, "*.csx");
        SeedMissingFromFolder("DemoForms",     _config.FormsFolder,   "*.json");
        SeedMissingFromFolder("DemoNugets",    demoNugetsFolder,      "*.nupkg");
        SeedMissingFromFolder("DemoKnowledge", knowledgeFolder,       "*.md");

        _secrets.Initialize(_vaultService, _prompts, line => _terminal.Log(line));
        _connections.Initialize(_connectionStore, _prompts, line => _terminal.Log(line));
        _assistant.Initialize(_knowledge, _prompts, line => _terminal.Log(line), _catalog, _artifacts);
        _chat.Initialize(_config, _connectionStore, _knowledge, _catalog, _ai, _vaultService,
            EnsureVaultUnlockedAsync, line => _terminal.Log(line));
        _secrets.UnlockRequested += OnVaultUnlockRequested;
        _designer.Initialize(_vaultService, EnsureVaultUnlockedAsync);

        _sidebar.ScriptSelected += OnFileSelected;

        _scriptFormActions = new ScriptFormActionsView();
        _scriptFormActions.RunClicked    += OnRunClicked;
        _scriptFormActions.StopClicked   += OnStopClicked;
        _scriptFormActions.NewClicked    += OnNewClicked;
        _scriptFormActions.SaveClicked   += (_, _) => SaveCurrent();
        _scriptFormActions.RenameClicked += OnRenameClicked;
        _scriptFormActions.DeleteClicked += OnDeleteClicked;

        _toolbar.ConfigClicked             += OnConfigClicked;
        _toolbar.ModeChanged               += (_, mode)    => SwitchMode(mode);
        _toolbar.TerminalVisibilityChanged += (_, visible) => OnTerminalVisibilityChanged(visible);
        _toolbar.ChatVisibilityChanged     += (_, visible) => ApplyChatVisibility(visible);

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
        ApplyTerminalVisibility(_config.TerminalVisible);
        _toolbar.IsTerminalVisible = _config.TerminalVisible;
        _editor.SetHighlighter(_config.EditorHighlighter);
        _editor.SetCodeFoldingEnabled(_config.CodeFolding);
        _terminal.Log($"Scripts folder: {_config.ScriptsFolder}");
        _terminal.Log($"Forms folder:   {_config.FormsFolder}");

        var externalLoad = _externals.LoadAll();
        if (externalLoad.LoadedDllCount > 0)
            _terminal.Log($"External references: {externalLoad.LoadedDllCount} DLL(s) loaded.");
        foreach (var err in externalLoad.Errors)
            _terminal.Log($"⚠ {err}");
    }

    private void OnTerminalVisibilityChanged(bool visible)
    {
        ApplyTerminalVisibility(visible);
        _config.TerminalVisible = visible;
        _config.Save();
    }

    private void ApplyChatVisibility(bool visible)
    {
        // The chat panel only makes sense in Scripts mode (it's tied to
        // the open .csx). Forms / Secrets / Connections / Assistant don't
        // have a "current script" to chat about.
        var enabled = visible && _mode == AppMode.Scripts;
        _chat.IsVisible = enabled;
        _chatSplitter.IsVisible = enabled;

        // Collapse the column itself when hidden — otherwise the splitter
        // is gone but the 380 px slot stays reserved. When re-showing,
        // restore the user's last chosen width so dragging the splitter
        // wider survives a hide/show cycle.
        var chatColumn = _scriptsFormsWorkspace.ColumnDefinitions[4];
        if (enabled)
        {
            if (chatColumn.Width.Value == 0) chatColumn.Width = _lastChatColumnWidth;
        }
        else
        {
            if (chatColumn.Width.Value > 0) _lastChatColumnWidth = chatColumn.Width;
            chatColumn.Width = new GridLength(0);
        }
    }

    private void ApplyTerminalVisibility(bool visible)
    {
        var terminalRow = _workspaceGrid.RowDefinitions[2];
        if (visible)
        {
            terminalRow.Height = _lastTerminalRowHeight;
        }
        else
        {
            // Cache the user's current size so the next show restores it
            // instead of reverting to the original 180px default.
            if (terminalRow.Height.Value > 0) _lastTerminalRowHeight = terminalRow.Height;
            terminalRow.Height = new GridLength(0);
        }
        _terminal.IsVisible         = visible;
        _terminalSplitter.IsVisible = visible;
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
        var current = _themes.CurrentTheme;
        _statusBar.ThemeName = current;
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

    private async void SwitchMode(AppMode mode)
    {
        _mode = mode;
        _toolbar.Mode = mode;

        _currentFilePath = null;
        _scriptFormActions.CanModify = false;
        UpdateDirtyUi();

        switch (mode)
        {
            case AppMode.Scripts:
                _scriptsFormsWorkspace.IsVisible = true;
                _secrets.IsVisible = false;
                _connections.IsVisible = false;
                _assistant.IsVisible = false;
                _editor.IsVisible = true;
                _designer.IsVisible = false;
                _sidebar.Header = "Scripts";
                _sidebar.Pattern = "*.csx";
                _sidebar.Folder = _config.ScriptsFolder;
                _scriptFormActions.SetRunLabel("▶ Run");
                _scriptFormActions.CanRun = false;
                _toolbar.ActionsContent = _scriptFormActions;
                _toolbar.IsChatToggleVisible = true;
                ApplyChatVisibility(_toolbar.IsChatVisible);
                break;

            case AppMode.Forms:
                _scriptsFormsWorkspace.IsVisible = true;
                _secrets.IsVisible = false;
                _connections.IsVisible = false;
                _assistant.IsVisible = false;
                _editor.IsVisible = false;
                _designer.IsVisible = true;
                _sidebar.Header = "Forms";
                _sidebar.Pattern = "*.json";
                _sidebar.Folder = _config.FormsFolder;
                _scriptFormActions.SetRunLabel("👁 Preview");
                _scriptFormActions.CanRun = false;
                _toolbar.ActionsContent = _scriptFormActions;
                _toolbar.IsChatToggleVisible = false;
                ApplyChatVisibility(false);
                break;

            case AppMode.Secrets:
                _scriptsFormsWorkspace.IsVisible = false;
                _secrets.IsVisible = true;
                _connections.IsVisible = false;
                _assistant.IsVisible = false;
                _toolbar.ActionsContent = _secrets.ToolbarActions;
                _toolbar.IsChatToggleVisible = false;
                ApplyChatVisibility(false);
                await _secrets.ActivateAsync();
                break;

            case AppMode.Connections:
                _scriptsFormsWorkspace.IsVisible = false;
                _secrets.IsVisible = false;
                _connections.IsVisible = true;
                _assistant.IsVisible = false;
                _toolbar.ActionsContent = _connections.ToolbarActions;
                _toolbar.IsChatToggleVisible = false;
                ApplyChatVisibility(false);
                _connections.Reload();
                break;

            case AppMode.Assistant:
                _scriptsFormsWorkspace.IsVisible = false;
                _secrets.IsVisible = false;
                _connections.IsVisible = false;
                _assistant.IsVisible = true;
                _toolbar.ActionsContent = _assistant.ToolbarActions;
                _toolbar.IsChatToggleVisible = false;
                ApplyChatVisibility(false);
                _assistant.Reload();
                break;
        }
    }

    private string CurrentFolder => _mode == AppMode.Forms ? _config.FormsFolder : _config.ScriptsFolder;
    private string CurrentExtension => _mode == AppMode.Forms ? ".json" : ".csx";

    // ==================== Sidebar selection ====================

    private void OnFileSelected(object? sender, string path)
    {
        _currentFilePath = path;
        _scriptFormActions.CanModify = true;
        _scriptFormActions.CanRun = true;

        if (_mode == AppMode.Scripts)
        {
            _editor.LoadFile(path);
            _terminal.Log($"Loaded: {Path.GetFileName(path)} ({_editor.Text.Length} chars)");
            _chat.SetCurrentScript(path, () => _editor.Text);
        }
        else
        {
            // Detach old document, build a fresh one from the file, attach to designer.
            DetachCurrentFormDoc();
            var doc = _formDocFactory.LoadFromFile(path);
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
        _statusBar.CurrentFile = name;
        _statusBar.SetDirty(_currentFilePath is null ? null : IsDirty);
        _scriptFormActions.CanSave = _currentFilePath is not null && IsDirty;
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
            _formDocFactory.SaveToFile(_currentFormDoc, _currentFilePath);
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
        _scriptFormActions.CanRun = false;
        _scriptFormActions.CanStop = true;
        _statusBar.Status = "Running…";
        _terminal.Log($"▶ {name}");

        BlastResult result;
        try
        {
            var globals = new ScriptGlobals(
                new ScriptSecrets(_vaultService, EnsureVaultUnlockedAsync, _connectionStore));

            result = await _blaster.RunAsync(
                scriptText,
                path,
                // Invoke (blocking) rather than Post so each line is on the
                // terminal before the script moves on — otherwise the trailing
                // "✓ finished" log can race ahead of queued output lines and
                // appear above them.
                line => Dispatcher.UIThread.Invoke(() => _terminal.Log(line)),
                globals,
                _runCts.Token);
        }
        finally
        {
            _runCts?.Dispose();
            _runCts = null;
            _scriptFormActions.CanRun = true;
            _scriptFormActions.CanStop = false;
        }

        switch (result.Status)
        {
            case BlastStatus.Ok:        _terminal.Log($"✓ {name} finished.");           _statusBar.Status = "Ready";     break;
            case BlastStatus.Cancelled:
                _terminal.Log(string.IsNullOrEmpty(result.Message)
                    ? $"⊘ {name} cancelled."
                    : $"⊘ {name} cancelled: {result.Message}");
                _statusBar.Status = "Cancelled";
                break;
            case BlastStatus.Error:
                _terminal.LogError($"{name} failed: {result.Message}", result.Details);
                _statusBar.SetStatus("Error", StatusLevel.Error);
                break;
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
            // Resolve any vault-backed options before GuiBlast sees the JSON.
            // If the form doesn't use dynamic options, FormJsonExpander returns
            // the input unchanged; unlocking is only needed when there IS work.
            if (FormNeedsVault(json) && !_vaultService.IsUnlocked)
                await EnsureVaultUnlockedAsync(CancellationToken.None);

            var expanded = await Forms.FormJsonExpander.ExpandAsync(json, _vaultService);

            var result = await DynamicForm.ShowJsonAsync(expanded);
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

    /// <summary>Cheap check for whether expansion will need a live vault.</summary>
    private static bool FormNeedsVault(string json)
        => json.Contains("\"optionsFrom\"", StringComparison.Ordinal);

    private void OnStopClicked(object? sender, EventArgs e) => _runCts?.Cancel();

    // ==================== Vault unlock / create ====================

    private async void OnVaultUnlockRequested(object? sender, EventArgs e) => await UnlockOrCreateVaultAsync();

    /// <summary>
    /// Called from a script-thread when a <c>Secrets.Resolve</c> hits a
    /// locked vault. Hops to the UI thread and reuses the normal
    /// create/unlock flow so the script gets the same password UX the
    /// Secrets tab does. Returns when the vault is unlocked, or when the
    /// user cancels — <see cref="ScriptSecrets"/> then surfaces the
    /// still-locked state as an <see cref="InvalidOperationException"/>.
    /// </summary>
    private Task EnsureVaultUnlockedAsync(CancellationToken ct)
    {
        if (_vaultService.IsUnlocked) return Task.CompletedTask;
        return Dispatcher.UIThread.InvokeAsync(UnlockOrCreateVaultAsync);
    }

    private async Task UnlockOrCreateVaultAsync()
    {
        if (_vaultService.IsUnlocked)
        {
            if (_mode == AppMode.Secrets) await _secrets.ActivateAsync();
            return;
        }

        if (!_vaultService.Exists)
        {
            var pw = await _prompts.PasswordAsync(
                "Create vault",
                $"No vault exists at:\n{_config.VaultFolder}\n\nChoose a master password for the new vault.",
                confirm: true);
            if (pw is null) return;

            _secrets.SetVerifying(true);
            try
            {
                await _vaultService.InitializeAsync(pw);
                _terminal.Log($"Created vault at {_config.VaultFolder}");
            }
            catch (Exception ex)
            {
                await _prompts.MessageAsync("Create failed", ex.Message);
                return;
            }
            finally
            {
                _secrets.SetVerifying(false);
            }
        }
        else
        {
            while (true)
            {
                var pw = await _prompts.PasswordAsync(
                    "Unlock vault",
                    $"Enter the master password for the vault at:\n{_config.VaultFolder}",
                    confirm: false);
                if (pw is null) return;

                _secrets.SetVerifying(true);
                try
                {
                    await _vaultService.UnlockAsync(pw);
                    _terminal.Log("Vault unlocked.");
                    break;
                }
                catch (InvalidMasterPasswordException)
                {
                    await _prompts.MessageAsync("Unlock failed", "Incorrect master password. Try again.");
                    // Loop, re-prompt.
                }
                catch (Exception ex)
                {
                    await _prompts.MessageAsync("Unlock failed", ex.Message);
                    return;
                }
                finally
                {
                    _secrets.SetVerifying(false);
                }
            }
        }

        if (_mode == AppMode.Secrets) await _secrets.ActivateAsync();
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
        var connectionNames = _connectionStore.List().Select(c => c.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        var result = await new ConfigDialog(
            _config.ScriptsFolder,
            _config.FormsFolder,
            _config.VaultFolder,
            _themes.AvailableThemes,
            _themes.CurrentTheme,
            _config.EditorHighlighter,
            _config.CodeFolding,
            _externals,
            connectionNames,
            _config.AiDefaultProvider,
            _connectionStore,
            _vaultService,
            _ai,
            EnsureVaultUnlockedAsync).ShowDialog<ConfigDialogResult?>(this);
        if (result is null) return;

        var scriptsChanged = await TryApplyFolder(
            result.ScriptsFolder, _config.ScriptsFolder, "Scripts folder",
            path => _config.ScriptsFolder = path);

        var formsChanged = await TryApplyFolder(
            result.FormsFolder, _config.FormsFolder, "Forms folder",
            path => _config.FormsFolder = path);

        var vaultChanged = await TryApplyFolder(
            result.VaultFolder, _config.VaultFolder, "Vault folder",
            path => _config.VaultFolder = path);

        var themeChanged = false;
        if (!string.IsNullOrEmpty(result.Theme)
            && !string.Equals(result.Theme, _themes.CurrentTheme, StringComparison.OrdinalIgnoreCase))
        {
            _themes.Apply(result.Theme);
            _config.Theme = result.Theme;
            themeChanged = true;
        }

        var highlighterChanged = false;
        if (!string.IsNullOrEmpty(result.Highlighter)
            && !string.Equals(result.Highlighter, _config.EditorHighlighter, StringComparison.OrdinalIgnoreCase))
        {
            _config.EditorHighlighter = result.Highlighter;
            _editor.SetHighlighter(result.Highlighter);
            highlighterChanged = true;
        }

        var foldingChanged = false;
        if (result.CodeFolding.HasValue && result.CodeFolding.Value != _config.CodeFolding)
        {
            _config.CodeFolding = result.CodeFolding.Value;
            _editor.SetCodeFoldingEnabled(result.CodeFolding.Value);
            foldingChanged = true;
        }

        // AI provider: AiDefaultProviderCleared distinguishes "user picked
        // (none)" from "user didn't touch this", so we only overwrite when
        // the user actually moved the dropdown.
        var aiProviderChanged = false;
        if (result.AiDefaultProviderCleared && _config.AiDefaultProvider is not null)
        {
            _config.AiDefaultProvider = null;
            aiProviderChanged = true;
        }
        else if (!result.AiDefaultProviderCleared
                 && !string.IsNullOrEmpty(result.AiDefaultProvider)
                 && !string.Equals(result.AiDefaultProvider, _config.AiDefaultProvider, StringComparison.Ordinal))
        {
            _config.AiDefaultProvider = result.AiDefaultProvider;
            aiProviderChanged = true;
        }

        if (!scriptsChanged && !formsChanged && !vaultChanged && !themeChanged && !highlighterChanged && !foldingChanged && !aiProviderChanged) return;

        // Changing the vault path invalidates the currently-unlocked vault;
        // next access will hit a locked view and re-prompt.
        if (vaultChanged) _vaultService.Lock();

        _config.Save();

        // Folder changes invalidate the current selection (different folder
        // → different files). Theme/highlighter changes should leave the
        // editor untouched.
        if (scriptsChanged || formsChanged || vaultChanged)
        {
            _currentFilePath = null;
            _editor.Text = string.Empty;
            DetachCurrentFormDoc();
            _designer.Document = null;
            _scriptFormActions.CanModify = false;
            _scriptFormActions.CanRun = false;
            UpdateDirtyUi();
            _sidebar.Folder = _mode == AppMode.Forms ? _config.FormsFolder : _config.ScriptsFolder;
        }
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
            _chat.SetCurrentScript(null, null);
        }
        else
        {
            DetachCurrentFormDoc();
            _designer.Document = null;
        }
        _scriptFormActions.CanModify = false;
        _scriptFormActions.CanRun = false;
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
