using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using SecretBlast;
using TaskBlaster.Dialogs;
using TaskBlaster.Interfaces;
using TaskBlaster.Secrets;

namespace TaskBlaster.Views;

public partial class SecretsView : UserControl
{
    private readonly Grid _lockedPanel;
    private readonly Grid _unlockedPanel;
    private readonly ListBox _categoriesListBox;
    private readonly DataGrid _grid;
    private readonly Button _editButton;
    private readonly Button _deleteButton;
    private readonly Button _copyButton;
    private readonly TextBlock _emptyLabel;
    private readonly TextBlock _lockedHint;
    private readonly Button _unlockButton;
    private readonly Button _resetButton;
    private string? _hintBeforeVerifying;

    private IVaultService? _vault;
    private IPromptService? _prompts;
    private Action<string>? _log;

    /// <summary>All entries currently in the vault.</summary>
    private List<SecretEntry> _all = new();
    /// <summary>User-configured categories (persisted in the vault catalog).</summary>
    private List<string> _categories = new();
    private const string AllCategoriesLabel = "(All)";

    public event EventHandler? UnlockRequested;

    public SecretsView()
    {
        InitializeComponent();
        _lockedPanel       = this.FindControl<Grid>("LockedPanel")!;
        _unlockedPanel     = this.FindControl<Grid>("UnlockedPanel")!;
        _categoriesListBox = this.FindControl<ListBox>("CategoriesList")!;
        _grid              = this.FindControl<DataGrid>("EntriesGrid")!;
        _editButton    = this.FindControl<Button>("EditButton")!;
        _deleteButton  = this.FindControl<Button>("DeleteButton")!;
        _copyButton    = this.FindControl<Button>("CopyButton")!;
        _emptyLabel    = this.FindControl<TextBlock>("EmptyLabel")!;
        _lockedHint    = this.FindControl<TextBlock>("LockedHint")!;
        _unlockButton  = this.FindControl<Button>("UnlockButton")!;
        _resetButton   = this.FindControl<Button>("ResetButton")!;
    }

    /// <summary>
    /// Toggle the locked panel between "ready to unlock" and "running Argon2"
    /// states. Disables the buttons and shows a verifying hint so the user
    /// gets feedback during the multi-second key derivation, and so a second
    /// click can't fire a parallel unlock chain.
    /// </summary>
    public void SetVerifying(bool verifying)
    {
        if (verifying)
        {
            _hintBeforeVerifying ??= _lockedHint.Text;
            _lockedHint.Text = "Verifying password…";
            _unlockButton.IsEnabled = false;
            _resetButton.IsEnabled = false;
        }
        else
        {
            if (_hintBeforeVerifying is not null)
            {
                _lockedHint.Text = _hintBeforeVerifying;
                _hintBeforeVerifying = null;
            }
            _unlockButton.IsEnabled = true;
            _resetButton.IsEnabled = true;
        }
    }

    /// <summary>Wire the view to the vault and to the main-window prompts.</summary>
    public void Initialize(IVaultService vault, IPromptService prompts, Action<string> log)
    {
        _vault = vault;
        _prompts = prompts;
        _log = log;
        vault.Locked += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(OnVaultLocked);
        RefreshLockedState();
    }

    /// <summary>
    /// Called by MainWindow whenever Secrets mode becomes active. Reloads the
    /// entry list if the vault is unlocked; otherwise shows the locked pane.
    /// </summary>
    public async Task ActivateAsync()
    {
        if (_vault is null) return;
        if (!_vault.IsUnlocked)
        {
            ShowLocked("Unlock to view secrets.");
            return;
        }
        await ReloadAsync();
    }

    public bool IsUnlocked => _vault is { IsUnlocked: true };

    private void OnVaultLocked()
    {
        _all = new();
        _categories = new();
        _categoriesListBox.ItemsSource = Array.Empty<string>();
        _grid.ItemsSource = Array.Empty<SecretRow>();
        UpdateActionState();
        ShowLocked("Vault locked. Unlock to view secrets.");
    }

    private void ShowLocked(string hint)
    {
        _lockedHint.Text = hint;
        _lockedPanel.IsVisible = true;
        _unlockedPanel.IsVisible = false;
    }

    private void ShowUnlocked()
    {
        _lockedPanel.IsVisible = false;
        _unlockedPanel.IsVisible = true;
    }

    private void RefreshLockedState()
    {
        if (_vault is { IsUnlocked: true }) ShowUnlocked();
        else ShowLocked("Unlock to view secrets.");
    }

    private async Task ReloadAsync()
    {
        if (_vault is null) return;
        _all = (await _vault.ListAsync()).ToList();
        _categories = (await _vault.GetCategoriesAsync()).ToList();
        RefreshCategoryList();
        RefreshGrid();
        ShowUnlocked();
    }

    private void RefreshCategoryList()
    {
        var currentSelection = _categoriesListBox.SelectedItem as string;

        // Union the user-configured catalog with any category actually in use
        // on a secret (covers the "category referenced by a secret but not yet
        // in the catalog" case — shouldn't happen in normal flow but keeps
        // the sidebar honest).
        var cats = _categories
            .Concat(_all.Select(e => e.Category).Where(c => !string.IsNullOrWhiteSpace(c)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var items = new List<string> { AllCategoriesLabel };
        items.AddRange(cats);
        _categoriesListBox.ItemsSource = items;

        // Preserve selection if possible.
        if (!string.IsNullOrEmpty(currentSelection) && items.Contains(currentSelection))
            _categoriesListBox.SelectedItem = currentSelection;
        else
            _categoriesListBox.SelectedItem = AllCategoriesLabel;
    }

    private void RefreshGrid()
    {
        var selectedCat = _categoriesListBox.SelectedItem as string ?? AllCategoriesLabel;
        IEnumerable<SecretEntry> filtered = _all;
        if (!string.Equals(selectedCat, AllCategoriesLabel, StringComparison.Ordinal))
        {
            filtered = filtered.Where(e => string.Equals(e.Category, selectedCat, StringComparison.OrdinalIgnoreCase));
        }

        var rows = filtered
            .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Key,      StringComparer.OrdinalIgnoreCase)
            .Select(e => new SecretRow(e))
            .ToList();

        _grid.ItemsSource = rows;
        _emptyLabel.IsVisible = rows.Count == 0;
        UpdateActionState();
    }

    private void OnCategorySelectionChanged(object? sender, SelectionChangedEventArgs e) => RefreshGrid();

    private void OnEntrySelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateActionState();

    private void UpdateActionState()
    {
        var hasSelection = _grid.SelectedItem is SecretRow;
        _editButton.IsEnabled   = hasSelection;
        _deleteButton.IsEnabled = hasSelection;
        _copyButton.IsEnabled   = hasSelection;
    }

    private void OnUnlockClicked(object? sender, RoutedEventArgs e) => UnlockRequested?.Invoke(this, EventArgs.Empty);

    private async void OnAddClicked(object? sender, RoutedEventArgs e)
    {
        if (_vault is null) return;
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var result = await new SecretEntryDialog("New secret", existing: null, categories: _categories)
            .ShowDialog<SecretEntryDialogResult?>(owner);
        if (result is null) return;

        try
        {
            var entry = await _vault.AddAsync(result.Category, result.Key, result.Value, result.Description);
            _log?.Invoke($"Added secret: {entry.Category}/{entry.Key}");
            await ReloadAsync();
            SelectEntry(entry.Id);
        }
        catch (Exception ex)
        {
            await (_prompts?.MessageAsync("Add failed", ex.Message) ?? Task.CompletedTask);
        }
    }

    private async void OnEditClicked(object? sender, RoutedEventArgs e) => await EditSelectedAsync();

    private async void OnEntryDoubleTapped(object? sender, TappedEventArgs e) => await EditSelectedAsync();

    private async Task EditSelectedAsync()
    {
        if (_vault is null) return;
        if (_grid.SelectedItem is not SecretRow row) return;
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var result = await new SecretEntryDialog("Edit secret", existing: row.Entry, categories: _categories)
            .ShowDialog<SecretEntryDialogResult?>(owner);
        if (result is null) return;

        try
        {
            var updated = await _vault.UpdateAsync(row.Entry.Id, result.Category, result.Key, result.Value, result.Description);
            _log?.Invoke($"Updated secret: {updated.Category}/{updated.Key}");
            await ReloadAsync();
            SelectEntry(updated.Id);
        }
        catch (Exception ex)
        {
            await (_prompts?.MessageAsync("Update failed", ex.Message) ?? Task.CompletedTask);
        }
    }

    private async void OnDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (_vault is null || _prompts is null) return;
        if (_grid.SelectedItem is not SecretRow row) return;

        var ok = await _prompts.ConfirmAsync("Delete secret", $"Delete '{row.Entry.Category}/{row.Entry.Key}'? This cannot be undone.");
        if (!ok) return;

        try
        {
            await _vault.DeleteAsync(row.Entry.Id);
            _log?.Invoke($"Deleted secret: {row.Entry.Category}/{row.Entry.Key}");
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            await _prompts.MessageAsync("Delete failed", ex.Message);
        }
    }

    private async void OnCopyClicked(object? sender, RoutedEventArgs e)
    {
        if (_grid.SelectedItem is not SecretRow row) return;
        var top = TopLevel.GetTopLevel(this);
        var clipboard = top?.Clipboard;
        if (clipboard is null) return;
        await clipboard.SetTextAsync(row.Entry.Value);
        _log?.Invoke($"Copied '{row.Entry.Category}/{row.Entry.Key}' value to clipboard.");
    }

    private void OnLockClicked(object? sender, RoutedEventArgs e)
    {
        _vault?.Lock();
        // OnVaultLocked runs via the event handler and updates UI state.
    }

    private async void OnCategoriesClicked(object? sender, RoutedEventArgs e)
    {
        if (_vault is null || _prompts is null) return;
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        // Count usages so the dialog can block remove and warn on rename.
        var usage = _all
            .GroupBy(entry => entry.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var result = await new CategoriesDialog(_categories, usage, _prompts)
            .ShowDialog<CategoriesDialogResult?>(owner);
        if (result is null) return;

        try
        {
            await _vault.SetCategoriesAsync(result.Categories);
            _log?.Invoke($"Categories updated ({result.Categories.Count} total).");
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            await _prompts.MessageAsync("Save failed", ex.Message);
        }
    }

    private async void OnResetClicked(object? sender, RoutedEventArgs e)
    {
        if (_vault is null || _prompts is null) return;

        if (!_vault.Exists)
        {
            await _prompts.MessageAsync("Reset vault", "No vault exists at the configured path. Click Unlock to create a new one.");
            return;
        }

        var ok = await _prompts.ConfirmAsync(
            "Reset vault",
            "This permanently deletes the vault and every secret in it. This cannot be undone.\n\nProceed?");
        if (!ok) return;

        try
        {
            await _vault.DestroyAsync();
            _log?.Invoke("Vault destroyed.");
            ShowLocked("Vault reset. Click Unlock to create a new one.");
        }
        catch (System.Exception ex)
        {
            await _prompts.MessageAsync("Reset failed", ex.Message);
        }
    }

    private async void OnDestroyClicked(object? sender, RoutedEventArgs e)
    {
        if (_vault is null || _prompts is null) return;

        var ok = await _prompts.ConfirmAsync(
            "Destroy vault",
            $"This permanently deletes the vault and every secret in it ({_all.Count} total). This cannot be undone.\n\nProceed?");
        if (!ok) return;

        try
        {
            await _vault.DestroyAsync();
            _log?.Invoke("Vault destroyed.");
            _all = new();
            // OnVaultLocked fires via the Locked event during DisposeCurrent,
            // but only when a vault was attached. Force the locked view
            // explicitly in case the service was already locked.
            ShowLocked("Vault reset. Click Unlock to create a new one.");
        }
        catch (System.Exception ex)
        {
            await _prompts.MessageAsync("Destroy failed", ex.Message);
        }
    }

    private async void OnChangePasswordClicked(object? sender, RoutedEventArgs e)
    {
        if (_vault is null || _prompts is null) return;
        if (!_vault.IsUnlocked)
        {
            await _prompts.MessageAsync("Change password", "Unlock the vault first.");
            return;
        }

        var newPw = await _prompts.PasswordAsync(
            "Change master password",
            "Choose a new master password. The current password is not required; the vault is already unlocked.",
            confirm: true);
        if (newPw is null) return;

        try
        {
            await _vault.ChangePasswordAsync(newPw);
            _log?.Invoke("Master password changed. Old backup was removed.");
            await _prompts.MessageAsync("Password changed", "The vault was re-encrypted with the new master password.");
            await ReloadAsync();
        }
        catch (System.Exception ex)
        {
            await _prompts.MessageAsync(
                "Change password failed",
                ex.Message + "\n\nA backup of the old vault may still be present at the configured path (files named vault.json.bak.* and secrets.bak.*/). Delete them only once you're sure nothing was lost.");
        }
    }

    private void SelectEntry(string id)
    {
        if (_grid.ItemsSource is not IEnumerable<SecretRow> rows) return;
        var match = rows.FirstOrDefault(r => r.Entry.Id == id);
        if (match is not null) _grid.SelectedItem = match;
    }

}

/// <summary>
/// Bind-friendly row wrapper for <see cref="SecretsView"/>'s DataGrid.
/// Public so compiled bindings can resolve it via <c>x:DataType</c>.
/// </summary>
public sealed class SecretRow
{
    public SecretRow(SecretEntry entry) => Entry = entry;
    public SecretEntry Entry { get; }
    public string Category          => Entry.Category;
    public string Key               => Entry.Key;
    public string? Description      => Entry.Description;
    public string UpdatedUtcDisplay => Entry.UpdatedUtc.ToString("yyyy-MM-dd HH:mm");
}
