using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TaskBlaster.Forms;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Views;

/// <summary>
/// Edits the form's Visibility rules. Bound to the document's
/// <see cref="VisibilityRuleEditor"/> ObservableCollection via a DataGrid;
/// per-cell templates own initial population and write-back so combo
/// state and Eq/Neq + Show/Hide stay in sync via the rule's mode bits.
/// </summary>
public partial class VisibilityEditorView : UserControl
{
    private readonly DataGrid _grid;

    private IFormDocument? _document;

    public VisibilityEditorView()
    {
        InitializeComponent();
        _grid = this.FindControl<DataGrid>("RulesGrid")!;
    }

    public IFormDocument? Document
    {
        get => _document;
        set
        {
            if (ReferenceEquals(_document, value)) return;
            if (_document is not null) _document.VisibilityChanged -= OnDocChanged;
            _document = value;
            if (_document is not null) _document.VisibilityChanged += OnDocChanged;
            Rebind();
        }
    }

    private void OnDocChanged(object? sender, EventArgs e) => Rebind();

    private void Rebind() => _grid.ItemsSource = _document?.Visibility;

    private void OnFieldChanged(object? sender, TextChangedEventArgs e)
    {
        if (_document is null) return;
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not VisibilityRuleEditor rule) return;

        var newText = tb.Text ?? "";
        if (string.Equals(rule.Field, newText, StringComparison.Ordinal)) return;

        rule.Field = newText;
        _document.MarkVisibilityChanged();
    }

    private void OnOpAttached(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        if (cb.DataContext is not VisibilityRuleEditor rule) return;
        cb.SelectedIndex = rule.IsNeq ? 1 : 0;
    }

    private void OnOpChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_document is null) return;
        if (sender is not ComboBox cb) return;
        if (cb.DataContext is not VisibilityRuleEditor rule) return;

        var wantNeq = cb.SelectedIndex == 1;
        if (rule.IsNeq == wantNeq) return;

        rule.IsNeq = wantNeq;
        _document.MarkVisibilityChanged();
    }

    private void OnValueChanged(object? sender, TextChangedEventArgs e)
    {
        if (_document is null) return;
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not VisibilityRuleEditor rule) return;

        var newText = tb.Text ?? "";
        if (string.Equals(rule.Value, newText, StringComparison.Ordinal)) return;

        rule.Value = newText;
        _document.MarkVisibilityChanged();
    }

    private void OnActionAttached(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        if (cb.DataContext is not VisibilityRuleEditor rule) return;
        cb.SelectedIndex = rule.IsHide ? 1 : 0;
    }

    private void OnActionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_document is null) return;
        if (sender is not ComboBox cb) return;
        if (cb.DataContext is not VisibilityRuleEditor rule) return;

        var wantHide = cb.SelectedIndex == 1;
        if (rule.IsHide == wantHide) return;

        rule.IsHide = wantHide;
        _document.MarkVisibilityChanged();
    }

    private void OnTargetsChanged(object? sender, TextChangedEventArgs e)
    {
        if (_document is null) return;
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not VisibilityRuleEditor rule) return;

        var newText = tb.Text ?? "";
        if (string.Equals(rule.TargetsCsv, newText, StringComparison.Ordinal)) return;

        rule.TargetsCsv = newText;
        _document.MarkVisibilityChanged();
    }

    private void OnRemove(object? sender, RoutedEventArgs e)
    {
        if (_document is null) return;
        if (sender is not Button btn) return;
        if (btn.DataContext is not VisibilityRuleEditor rule) return;
        _document.RemoveVisibilityRule(rule);
    }

    private void OnAdd(object? sender, RoutedEventArgs e) => _document?.AddVisibilityRule();
}
