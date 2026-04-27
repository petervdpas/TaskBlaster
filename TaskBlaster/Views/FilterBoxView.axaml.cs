using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TaskBlaster.Views;

/// <summary>
/// Reusable inline filter box (text input + clear button). Hosts wire to
/// <see cref="FilterChanged"/> and re-render their list/grid using
/// <see cref="Matches"/> as the predicate so every filtered surface in the
/// app behaves identically: case-insensitive, whitespace-trimmed, all
/// whitespace tokens must match (substring).
/// </summary>
public partial class FilterBoxView : UserControl
{
    private readonly TextBox _box;
    private readonly Button _clearButton;
    private bool _suppressEvent;

    /// <summary>
    /// Fires when the effective filter text changes. The argument is the
    /// already-trimmed value; an empty string means "no filter".
    /// </summary>
    public event EventHandler<string>? FilterChanged;

    public FilterBoxView()
    {
        InitializeComponent();
        _box         = this.FindControl<TextBox>("FilterBox")!;
        _clearButton = this.FindControl<Button>("ClearButton")!;
    }

    /// <summary>The current trimmed filter text. Empty when no filter is set.</summary>
    public string FilterText => (_box.Text ?? string.Empty).Trim();

    /// <summary>Override the placeholder shown when the box is empty.</summary>
    public string Watermark
    {
        get => _box.PlaceholderText ?? string.Empty;
        set => _box.PlaceholderText = value;
    }

    /// <summary>Move keyboard focus into the filter box (e.g. for a Ctrl+F shortcut).</summary>
    public void FocusBox() => _box.Focus();

    /// <summary>Clear the filter without raising <see cref="FilterChanged"/>.</summary>
    public void ClearSilently()
    {
        _suppressEvent = true;
        _box.Text = string.Empty;
        _suppressEvent = false;
        _clearButton.IsVisible = false;
    }

    /// <summary>
    /// Test whether <paramref name="haystack"/> matches the current filter.
    /// Every whitespace-separated token in the filter must appear (case-insensitively)
    /// somewhere in the haystack; an empty filter matches everything.
    /// </summary>
    public bool Matches(string? haystack)
        => Matches(haystack, FilterText);

    /// <summary>Token-based substring match used by <see cref="Matches(string?)"/>.</summary>
    public static bool Matches(string? haystack, string filter)
    {
        if (string.IsNullOrEmpty(filter)) return true;
        if (string.IsNullOrEmpty(haystack)) return false;
        var tokens = filter.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        foreach (var t in tokens)
        {
            if (haystack.IndexOf(t, StringComparison.OrdinalIgnoreCase) < 0) return false;
        }
        return true;
    }

    private void OnFilterTextChanged(object? sender, TextChangedEventArgs e)
    {
        _clearButton.IsVisible = !string.IsNullOrEmpty(_box.Text);
        if (_suppressEvent) return;
        FilterChanged?.Invoke(this, FilterText);
    }

    private void OnFilterKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !string.IsNullOrEmpty(_box.Text))
        {
            _box.Text = string.Empty;
            e.Handled = true;
        }
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e)
    {
        _box.Text = string.Empty;
        _box.Focus();
    }
}
