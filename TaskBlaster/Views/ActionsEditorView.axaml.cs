using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using TaskBlaster.Forms;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Views;

/// <summary>
/// Edits the form's Actions collection. Bound to the document's
/// <see cref="ActionEditor"/> ObservableCollection via a DataGrid; per-cell
/// templates keep the original "always-editable" feel while letting the
/// DataGrid own column-header alignment.
/// </summary>
public partial class ActionsEditorView : UserControl
{
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Colors.OrangeRed);

    private readonly DataGrid _grid;

    private IFormDocument? _document;

    public ActionsEditorView()
    {
        InitializeComponent();
        _grid = this.FindControl<DataGrid>("ActionsGrid")!;
    }

    public IFormDocument? Document
    {
        get => _document;
        set
        {
            if (ReferenceEquals(_document, value)) return;

            if (_document is not null)
                _document.ActionsChanged -= OnDocActionsChanged;

            _document = value;

            if (_document is not null)
                _document.ActionsChanged += OnDocActionsChanged;

            Rebind();
        }
    }

    private void OnDocActionsChanged(object? sender, EventArgs e) => Rebind();

    private void Rebind() => _grid.ItemsSource = _document?.Actions;

    private void OnIdChanged(object? sender, TextChangedEventArgs e)
    {
        if (_document is null) return;
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not ActionEditor action) return;

        var newText = tb.Text ?? "";
        if (string.Equals(action.Id, newText, StringComparison.Ordinal))
        {
            tb.BorderBrush = null!;
            return;
        }

        var error = _document.ValidateActionId(newText, ignore: action);
        tb.BorderBrush = error is null ? null! : ErrorBrush;
        if (error is null)
        {
            action.Id = newText;
            _document.MarkActionChanged();
        }
    }

    private void OnLabelChanged(object? sender, TextChangedEventArgs e)
    {
        if (_document is null) return;
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not ActionEditor action) return;

        var newText = tb.Text ?? "";
        if (string.Equals(action.Label ?? "", newText, StringComparison.Ordinal)) return;

        action.Label = newText;
        _document.MarkActionChanged();
    }

    private void OnSubmitChanged(object? sender, RoutedEventArgs e)
    {
        if (_document is null) return;
        if (sender is not CheckBox cb) return;
        if (cb.DataContext is not ActionEditor action) return;

        var v = cb.IsChecked == true;
        if (action.Submit == v) return;
        action.Submit = v;
        _document.MarkActionChanged();
    }

    private void OnDismissChanged(object? sender, RoutedEventArgs e)
    {
        if (_document is null) return;
        if (sender is not CheckBox cb) return;
        if (cb.DataContext is not ActionEditor action) return;

        var v = cb.IsChecked == true;
        if (action.Dismiss == v) return;
        action.Dismiss = v;
        _document.MarkActionChanged();
    }

    private void OnRemove(object? sender, RoutedEventArgs e)
    {
        if (_document is null) return;
        if (sender is not Button btn) return;
        if (btn.DataContext is not ActionEditor action) return;
        _document.RemoveAction(action);
    }

    private void OnAdd(object? sender, RoutedEventArgs e) => _document?.AddAction();
}
