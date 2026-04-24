using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TaskBlaster.Interfaces;
using TaskBlaster.Secrets;

namespace TaskBlaster.Dialogs;

/// <summary>
/// Result returned by <see cref="CategoriesDialog"/> — the new list to persist.
/// Null means the user cancelled and no change should be written.
/// </summary>
public sealed record CategoriesDialogResult(IReadOnlyList<string> Categories);

public partial class CategoriesDialog : Window
{
    private readonly ListBox _list;
    private readonly Button _rename;
    private readonly Button _remove;
    private readonly ObservableCollection<string> _items = new();

    /// <summary>Map: category name (case-insensitive) → count of secrets using it.</summary>
    private readonly Dictionary<string, int> _usage;
    private readonly IPromptService _prompts;

    public CategoriesDialog() : this(Array.Empty<string>(), new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), new NoopPromptService()) { }

    public CategoriesDialog(IEnumerable<string> current, IReadOnlyDictionary<string, int> usage, IPromptService prompts)
    {
        InitializeComponent();
        _list   = this.FindControl<ListBox>("CategoriesList")!;
        _rename = this.FindControl<Button>("RenameButton")!;
        _remove = this.FindControl<Button>("RemoveButton")!;
        _prompts = prompts;

        _usage = new Dictionary<string, int>(usage, StringComparer.OrdinalIgnoreCase);

        foreach (var c in CategoryCatalog.Normalize(current))
            _items.Add(c);
        _list.ItemsSource = _items;

        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close((CategoriesDialogResult?)null); };
    }

    private string? SelectedName => _list.SelectedItem as string;

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var has = SelectedName is not null;
        _rename.IsEnabled = has;
        _remove.IsEnabled = has;
    }

    private async void OnAddClicked(object? sender, RoutedEventArgs e)
    {
        var name = await _prompts.InputAsync("New category", "Category name:", defaultValue: "");
        if (string.IsNullOrWhiteSpace(name)) return;
        var trimmed = name.Trim();

        if (_items.Any(c => string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            await _prompts.MessageAsync("Already exists", $"A category named '{trimmed}' already exists.");
            return;
        }
        // Insert in sorted position to match CategoryCatalog.Normalize's ordering.
        var idx = 0;
        while (idx < _items.Count && StringComparer.OrdinalIgnoreCase.Compare(_items[idx], trimmed) < 0) idx++;
        _items.Insert(idx, trimmed);
        _list.SelectedItem = trimmed;
    }

    private async void OnRenameClicked(object? sender, RoutedEventArgs e)
    {
        var current = SelectedName;
        if (current is null) return;

        var usage = _usage.TryGetValue(current, out var n) ? n : 0;
        var prompt = usage > 0
            ? $"Rename '{current}'.\n\n{usage} secret(s) use this category. Renaming here only updates the picker list; existing secrets keep the old category name until you edit them."
            : $"Rename '{current}'.";
        var raw = await _prompts.InputAsync("Rename category", prompt, current);
        if (raw is null) return;
        var trimmed = raw.Trim();
        if (trimmed.Length == 0) return;
        if (string.Equals(trimmed, current, StringComparison.Ordinal)) return;

        if (_items.Any(c => string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(c, current, StringComparison.OrdinalIgnoreCase)))
        {
            await _prompts.MessageAsync("Already exists", $"A category named '{trimmed}' already exists.");
            return;
        }

        var idx = _items.IndexOf(current);
        if (idx < 0) return;
        _items.RemoveAt(idx);

        var insertAt = 0;
        while (insertAt < _items.Count && StringComparer.OrdinalIgnoreCase.Compare(_items[insertAt], trimmed) < 0) insertAt++;
        _items.Insert(insertAt, trimmed);
        _list.SelectedItem = trimmed;
    }

    private async void OnRemoveClicked(object? sender, RoutedEventArgs e)
    {
        var current = SelectedName;
        if (current is null) return;

        if (_usage.TryGetValue(current, out var n) && n > 0)
        {
            await _prompts.MessageAsync(
                "Category in use",
                $"Can't remove '{current}': {n} secret(s) still use it. Edit those secrets to reassign, then remove the category.");
            return;
        }

        _items.Remove(current);
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        Close(new CategoriesDialogResult(_items.ToList()));
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close((CategoriesDialogResult?)null);

    // Parameterless ctor only — XAML preview compiles even without a real prompt service.
    private sealed class NoopPromptService : IPromptService
    {
        public Task<string?> InputAsync(string title, string prompt, string? defaultValue = null) => Task.FromResult<string?>(null);
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(false);
        public Task MessageAsync(string title, string message) => Task.CompletedTask;
        public Task<string?> PasswordAsync(string title, string prompt, bool confirm = false) => Task.FromResult<string?>(null);
    }
}
