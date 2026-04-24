using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TaskBlaster.Secrets;

namespace TaskBlaster.Dialogs;

/// <summary>
/// Result returned by <see cref="SecretEntryDialog"/>. Null means cancelled.
/// </summary>
public sealed record SecretEntryDialogResult(string Category, string Key, string Value, string? Description);

public partial class SecretEntryDialog : Window
{
    private readonly AutoCompleteBox _category;
    private readonly TextBox _key;
    private readonly TextBox _value;
    private readonly TextBox _description;
    private readonly TextBlock _error;
    private readonly TextBlock _metadata;
    private readonly Button _reveal;
    private bool _valueRevealed;

    public SecretEntryDialog() : this("New secret", null, System.Array.Empty<string>()) { }

    public SecretEntryDialog(string title, SecretEntry? existing, IReadOnlyList<string> knownCategories)
    {
        InitializeComponent();
        Title = title;
        _category    = this.FindControl<AutoCompleteBox>("CategoryBox")!;
        _key         = this.FindControl<TextBox>("KeyBox")!;
        _value       = this.FindControl<TextBox>("ValueBox")!;
        _description = this.FindControl<TextBox>("DescriptionBox")!;
        _error       = this.FindControl<TextBlock>("ErrorText")!;
        _metadata    = this.FindControl<TextBlock>("MetadataText")!;
        _reveal      = this.FindControl<Button>("ToggleRevealButton")!;

        _category.ItemsSource = knownCategories
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c, System.StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (existing is not null)
        {
            _category.Text   = existing.Category;
            _key.Text        = existing.Key;
            _value.Text      = existing.Value;
            _description.Text = existing.Description ?? string.Empty;
            _metadata.IsVisible = true;
            _metadata.Text = $"Created {existing.CreatedUtc:yyyy-MM-dd HH:mm} UTC · Updated {existing.UpdatedUtc:yyyy-MM-dd HH:mm} UTC";
        }

        Opened += (_, _) =>
        {
            if (existing is null) _category.Focus();
            else                  _value.Focus();
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close((SecretEntryDialogResult?)null); };
    }

    private void OnToggleReveal(object? sender, RoutedEventArgs e)
    {
        _valueRevealed = !_valueRevealed;
        _value.PasswordChar = _valueRevealed ? default : '•';
        _reveal.Content = _valueRevealed ? "🙈" : "👁";
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        var category = (_category.Text ?? string.Empty).Trim();
        var key      = (_key.Text      ?? string.Empty).Trim();
        var value    =  _value.Text    ?? string.Empty;
        var desc     = (_description.Text ?? string.Empty).Trim();

        if (category.Length == 0) { ShowError("Category is required."); return; }
        if (key.Length      == 0) { ShowError("Key is required.");      return; }

        Close(new SecretEntryDialogResult(category, key, value, desc.Length == 0 ? null : desc));
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close((SecretEntryDialogResult?)null);

    private void ShowError(string message)
    {
        _error.Text = message;
        _error.IsVisible = true;
    }
}
