using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AgentBlast;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TaskBlaster.Connections;
using TaskBlaster.Externals;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Dialogs;

/// <summary>
/// Result returned by <see cref="ConfigDialog"/>. Null fields mean
/// "leave the existing value alone". External-tab changes are NOT in
/// here: those are applied immediately via <see cref="ExternalReferenceManager"/>
/// because they touch on-disk state (nupkg extraction) and cannot be
/// rolled back by clicking Cancel.
/// </summary>
public sealed record ConfigDialogResult(
    string? ScriptsFolder,
    string? FormsFolder,
    string? VaultFolder,
    string? Theme,
    string? Highlighter,
    bool? CodeFolding,
    string? AiDefaultProvider,
    bool AiDefaultProviderCleared);

public partial class ConfigDialog : Window
{
    private readonly ComboBox _themeBox;
    private readonly ComboBox _highlighterBox;
    private readonly CheckBox _codeFoldingBox;
    private readonly TextBox _scriptsBox;
    private readonly TextBox _formsBox;
    private readonly TextBox _vaultBox;
    private readonly ListBox _nugetsList;
    private readonly ListBox _dllsList;
    private readonly Button _removeNugetButton;
    private readonly Button _removeDllButton;
    private readonly ComboBox _aiProviderBox;
    private readonly Button _aiTestButton;
    private readonly TextBlock _aiTestStatus;

    private readonly ExternalReferenceManager? _externals;
    private readonly IConnectionStore? _connectionStore;
    private readonly IVaultService? _vault;
    private readonly Lazy<AgentClient>? _ai;
    private readonly Func<CancellationToken, System.Threading.Tasks.Task>? _ensureVaultUnlocked;

    /// <summary>Sentinel item shown in the Agent provider dropdown when no connection is selected.</summary>
    private const string AiProviderNone = "(none — Agent disabled)";

    public ConfigDialog() : this(
        currentScriptsFolder: "",
        currentFormsFolder:   "",
        currentVaultFolder:   "",
        availableThemes:      new[] { "Industrial" },
        currentTheme:         "Industrial",
        currentHighlighter:   "Native",
        currentCodeFolding:   true,
        externals:            null,
        availableAiProviders: Array.Empty<string>(),
        currentAiProvider:    null,
        connectionStore:      null,
        vault:                null,
        ai:                   null,
        ensureVaultUnlocked:  null)
    { }

    public ConfigDialog(
        string currentScriptsFolder,
        string currentFormsFolder,
        string currentVaultFolder,
        IReadOnlyList<string> availableThemes,
        string currentTheme,
        string currentHighlighter,
        bool currentCodeFolding,
        ExternalReferenceManager? externals,
        IReadOnlyList<string> availableAiProviders,
        string? currentAiProvider,
        IConnectionStore? connectionStore,
        IVaultService? vault,
        Lazy<AgentClient>? ai,
        Func<CancellationToken, System.Threading.Tasks.Task>? ensureVaultUnlocked)
    {
        InitializeComponent();
        _themeBox        = this.FindControl<ComboBox>("ThemeBox")!;
        _highlighterBox  = this.FindControl<ComboBox>("HighlighterBox")!;
        _codeFoldingBox  = this.FindControl<CheckBox>("CodeFoldingBox")!;
        _scriptsBox      = this.FindControl<TextBox>("ScriptsFolderBox")!;
        _formsBox        = this.FindControl<TextBox>("FormsFolderBox")!;
        _vaultBox        = this.FindControl<TextBox>("VaultFolderBox")!;
        _nugetsList      = this.FindControl<ListBox>("NugetsList")!;
        _dllsList        = this.FindControl<ListBox>("DllsList")!;
        _removeNugetButton = this.FindControl<Button>("RemoveNugetButton")!;
        _removeDllButton   = this.FindControl<Button>("RemoveDllButton")!;
        _aiProviderBox     = this.FindControl<ComboBox>("AiProviderBox")!;
        _aiTestButton      = this.FindControl<Button>("AiTestButton")!;
        _aiTestStatus      = this.FindControl<TextBlock>("AiTestStatus")!;

        _externals           = externals;
        _connectionStore     = connectionStore;
        _vault               = vault;
        _ai                  = ai;
        _ensureVaultUnlocked = ensureVaultUnlocked;

        _themeBox.ItemsSource = availableThemes;
        _themeBox.SelectedItem = availableThemes.Contains(currentTheme) ? currentTheme : availableThemes[0];

        SelectHighlighter(currentHighlighter);
        _codeFoldingBox.IsChecked = currentCodeFolding;

        _scriptsBox.Text = currentScriptsFolder;
        _formsBox.Text   = currentFormsFolder;
        _vaultBox.Text   = currentVaultFolder;

        // AI provider dropdown — list every connection name plus a sentinel
        // "(none)" entry so the user can explicitly disable AI without
        // having to delete a connection.
        var aiItems = new List<string> { AiProviderNone };
        aiItems.AddRange(availableAiProviders);
        _aiProviderBox.ItemsSource = aiItems;
        _aiProviderBox.SelectedItem = !string.IsNullOrEmpty(currentAiProvider) && aiItems.Contains(currentAiProvider)
            ? currentAiProvider
            : AiProviderNone;
        // Test only makes sense when we have the runtime services AND a
        // real (non-sentinel) selection. Wire IsEnabled accordingly.
        _aiProviderBox.SelectionChanged += (_, _) => UpdateAiTestEnabled();
        UpdateAiTestEnabled();

        RefreshExternalsLists();

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

        // AI provider: sentinel "(none)" means the user explicitly cleared
        // the selection, distinct from "leave existing alone". Use the
        // separate Cleared flag so the result record doesn't conflate them.
        var aiPicked = _aiProviderBox.SelectedItem as string;
        var aiCleared = string.Equals(aiPicked, AiProviderNone, StringComparison.Ordinal);
        var aiProvider = aiCleared ? null : aiPicked;

        Close(new ConfigDialogResult(
            ScriptsFolder: string.IsNullOrEmpty(scripts)     ? null : scripts,
            FormsFolder:   string.IsNullOrEmpty(forms)       ? null : forms,
            VaultFolder:   string.IsNullOrEmpty(vault)       ? null : vault,
            Theme:         string.IsNullOrEmpty(theme)       ? null : theme,
            Highlighter:   string.IsNullOrEmpty(highlighter) ? null : highlighter,
            CodeFolding:   _codeFoldingBox.IsChecked,
            AiDefaultProvider: aiProvider,
            AiDefaultProviderCleared: aiCleared));
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close((ConfigDialogResult?)null);

    // ---------- External tab ----------

    private void RefreshExternalsLists()
    {
        if (_externals is null)
        {
            _nugetsList.ItemsSource = Array.Empty<string>();
            _dllsList.ItemsSource   = Array.Empty<string>();
            return;
        }

        // We bind to display strings (id + version, or full DLL path) and keep
        // the original objects retrievable via Tag on the ListBoxItem.
        _nugetsList.ItemsSource = BuildNugetItems();
        _dllsList.ItemsSource   = BuildDllItems();
    }

    private List<ListBoxItem> BuildNugetItems()
    {
        var items = new List<ListBoxItem>();
        if (_externals is null) return items;
        foreach (var p in _externals.Packages)
            items.Add(new ListBoxItem { Content = $"{p.Id}  {p.Version}", Tag = p });
        return items;
    }

    private List<ListBoxItem> BuildDllItems()
    {
        var items = new List<ListBoxItem>();
        if (_externals is null) return items;
        foreach (var d in _externals.Dlls)
            items.Add(new ListBoxItem { Content = d, Tag = d });
        return items;
    }

    private void OnNugetSelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _removeNugetButton.IsEnabled = _nugetsList.SelectedItem is ListBoxItem;

    private void OnDllSelectionChanged(object? sender, SelectionChangedEventArgs e)
        => _removeDllButton.IsEnabled = _dllsList.SelectedItem is ListBoxItem;

    private async void OnAddNuget(object? sender, RoutedEventArgs e)
    {
        if (_externals is null) return;
        var sp = StorageProvider;
        if (sp is null) return;

        var picked = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Pick a .nupkg",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("NuGet packages") { Patterns = new[] { "*.nupkg" } },
            },
        });
        if (picked.Count == 0) return;

        var nupkg = picked[0].Path.LocalPath;
        var outcome = _externals.AddPackage(nupkg, force: false);
        await HandleAddOutcome(outcome, $"NuGet: {Path.GetFileName(nupkg)}",
            retry: () => _externals.AddPackage(nupkg, force: true));
    }

    private async void OnAddDll(object? sender, RoutedEventArgs e)
    {
        if (_externals is null) return;
        var sp = StorageProvider;
        if (sp is null) return;

        var picked = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Pick a DLL",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Assemblies") { Patterns = new[] { "*.dll" } },
            },
        });
        if (picked.Count == 0) return;

        var dll = picked[0].Path.LocalPath;
        var outcome = _externals.AddDll(dll, force: false);
        await HandleAddOutcome(outcome, $"DLL: {Path.GetFileName(dll)}",
            retry: () => _externals.AddDll(dll, force: true));
    }

    private async System.Threading.Tasks.Task HandleAddOutcome(
        ExternalAddOutcome outcome,
        string header,
        Func<ExternalAddOutcome> retry)
    {
        // No reports at all means we never even got to validation (file
        // missing, nupkg malformed). Just message and bail.
        if (outcome.Reports.Count == 0)
        {
            await new ExternalValidationDialog().Yield(d =>
            {
                d.SetContent(header, new[]
                {
                    new AssemblyValidationReport(
                        DllPath:         "",
                        AssemblyName:    "Could not import",
                        AssemblyVersion: "",
                        Issues:          outcome.RuntimeErrors
                            .Select(m => new AssemblyIssue(IssueLevel.Error, m))
                            .ToList()),
                });
            }).ShowDialog<ExternalValidationChoice>(this);
            RefreshExternalsLists();
            return;
        }

        var dialog = new ExternalValidationDialog();
        dialog.SetContent(header, outcome.Reports);
        var choice = await dialog.ShowDialog<ExternalValidationChoice>(this);

        if (choice == ExternalValidationChoice.Cancel) return;
        if (choice == ExternalValidationChoice.AddAnyway) outcome = retry();
        // Successful no-issue Add path: outcome already loaded.

        RefreshExternalsLists();
    }

    private void OnRemoveNuget(object? sender, RoutedEventArgs e)
    {
        if (_externals is null) return;
        if (_nugetsList.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is not ExternalPackageRef pkg) return;

        _externals.RemovePackage(pkg);
        RefreshExternalsLists();
    }

    private void OnRemoveDll(object? sender, RoutedEventArgs e)
    {
        if (_externals is null) return;
        if (_dllsList.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is not string path) return;

        _externals.RemoveDll(path);
        RefreshExternalsLists();
    }

    // ---------- AI tab ----------

    private void UpdateAiTestEnabled()
    {
        // Disable test when we don't have the runtime services (e.g. the
        // designer-less default ctor) or when no real connection is picked.
        var picked = _aiProviderBox.SelectedItem as string;
        var realPick = !string.IsNullOrEmpty(picked) && !string.Equals(picked, AiProviderNone, StringComparison.Ordinal);
        _aiTestButton.IsEnabled = realPick && _ai is not null && _connectionStore is not null && _vault is not null;
    }

    private async void OnAiTest(object? sender, RoutedEventArgs e)
    {
        if (_ai is null || _connectionStore is null || _vault is null) return;
        if (_aiProviderBox.SelectedItem is not string name
            || string.Equals(name, AiProviderNone, StringComparison.Ordinal)) return;

        var conn = _connectionStore.Get(name);
        if (conn is null)
        {
            ShowAiTestStatus(false, $"Connection '{name}' not found in connections.json.");
            return;
        }

        // Lock the relevant controls while the request is in flight so
        // the user can't queue overlapping pings or change the picked
        // connection out from under us.
        _aiTestButton.IsEnabled = false;
        _aiProviderBox.IsEnabled = false;
        ShowAiTestStatus(null, "Pinging…");

        AgentPingResult result;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));

            // If the connection touches the vault and the vault is locked,
            // ask the host to pop the unlock prompt before we start the
            // ping. Cancelling the prompt either throws or leaves the
            // vault locked; we handle both as "couldn't unlock".
            if (ConnectionUsesVault(conn) && !_vault.IsUnlocked && _ensureVaultUnlocked is not null)
            {
                ShowAiTestStatus(null, "Unlocking vault…");
                await _ensureVaultUnlocked(cts.Token).ConfigureAwait(true);
                if (!_vault.IsUnlocked)
                {
                    ShowAiTestStatus(false, "Vault unlock cancelled.");
                    return;
                }
                ShowAiTestStatus(null, "Pinging…");
            }

            result = await _ai.Value.PingAsync(conn.Name, cts.Token).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            result = AgentPingResult.Fail($"Test crashed: {ex.Message}");
        }
        finally
        {
            _aiProviderBox.IsEnabled = true;
            UpdateAiTestEnabled();
        }

        ShowAiTestStatus(result.Success, result.Message);
    }

    private static bool ConnectionUsesVault(Connection conn)
        => conn.Fields.Values.Any(f => f.IsFromVault);

    private void ShowAiTestStatus(bool? success, string message)
    {
        _aiTestStatus.IsVisible = true;
        _aiTestStatus.Text = success switch
        {
            true  => "✓ " + message,
            false => "✗ " + message,
            null  => message,
        };
        var brushKey = success switch
        {
            true  => "SuccessBrush",
            false => "DangerBrush",
            null  => "TextMutedBrush",
        };
        if (this.TryFindResource(brushKey, out var brush) && brush is IBrush b)
            _aiTestStatus.Foreground = b;
    }
}

internal static class WindowExtensions
{
    /// <summary>Tiny helper so a single-expression dialog config reads as one chain.</summary>
    public static T Yield<T>(this T self, Action<T> configure)
    {
        configure(self);
        return self;
    }
}
