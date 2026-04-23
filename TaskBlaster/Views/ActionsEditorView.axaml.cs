using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TaskBlaster.Forms;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Views;

/// <summary>
/// Edits the form's Actions collection: add/remove + inline edit of id/label/submit/dismiss.
/// Each row is built programmatically (no per-row DataTemplate) to keep binding simple.
/// </summary>
public partial class ActionsEditorView : UserControl
{
    private readonly ItemsControl _list;

    private IFormDocument? _document;

    public ActionsEditorView()
    {
        InitializeComponent();
        _list = this.FindControl<ItemsControl>("ActionsList")!;
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

            Rebuild();
        }
    }

    private void OnDocActionsChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild()
    {
        var items = new System.Collections.Generic.List<Control>();
        if (_document is not null)
            foreach (var action in _document.Actions)
                items.Add(BuildRow(action));
        _list.ItemsSource = items;
    }

    private Control BuildRow(ActionEditor action)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,Auto,Auto,Auto")
        };

        var idBox = new TextBox { Text = action.Id, Margin = new Thickness(0, 0, 8, 0) };
        idBox.TextChanged += (_, _) =>
        {
            if (_document is null) return;
            var error = _document.ValidateActionId(idBox.Text ?? "", ignore: action);
            idBox.BorderBrush = error is null ? null! : new SolidColorBrush(Colors.OrangeRed);
            if (error is null)
            {
                action.Id = idBox.Text ?? "";
                _document.MarkActionChanged();
            }
        };
        Grid.SetColumn(idBox, 0);
        grid.Children.Add(idBox);

        var labelBox = new TextBox { Text = action.Label ?? "", Margin = new Thickness(0, 0, 8, 0) };
        labelBox.TextChanged += (_, _) =>
        {
            if (_document is null) return;
            action.Label = labelBox.Text;
            _document.MarkActionChanged();
        };
        Grid.SetColumn(labelBox, 1);
        grid.Children.Add(labelBox);

        var submit = new CheckBox
        {
            IsChecked = action.Submit, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        submit.IsCheckedChanged += (_, _) =>
        {
            if (_document is null) return;
            action.Submit = submit.IsChecked == true;
            _document.MarkActionChanged();
        };
        Grid.SetColumn(submit, 2);
        grid.Children.Add(submit);

        var dismiss = new CheckBox
        {
            IsChecked = action.Dismiss, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        dismiss.IsCheckedChanged += (_, _) =>
        {
            if (_document is null) return;
            action.Dismiss = dismiss.IsChecked == true;
            _document.MarkActionChanged();
        };
        Grid.SetColumn(dismiss, 3);
        grid.Children.Add(dismiss);

        var remove = new Button { Content = "×", Padding = new Thickness(8, 2) };
        ToolTip.SetTip(remove, "Remove action");
        remove.Click += (_, _) => _document?.RemoveAction(action);
        Grid.SetColumn(remove, 4);
        grid.Children.Add(remove);

        return grid;
    }

    private void OnAdd(object? sender, RoutedEventArgs e) => _document?.AddAction();
}
