using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TaskBlaster.Forms;
using TaskBlaster.Interfaces;
using TaskBlaster.Views.FieldEditors;

namespace TaskBlaster.Views;

/// <summary>
/// Thin view over an <see cref="IFormDocument"/>. Subscribes to the document's
/// events and reflects its state; pushes user actions back to the document.
/// Does NOT own form state — the document is the single source of truth.
/// </summary>
public partial class FormDesignerView : UserControl
{
    private readonly TextBox _titleBox;
    private readonly ListBox _fieldList;
    private readonly TextBox _keyBox;
    private readonly TextBox _labelBox;
    private readonly ComboBox _typeBox;
    private readonly CheckBox _requiredBox;
    private readonly ContentControl _typeSpecificHost;

    private IFormDocument? _document;
    private IFieldPropertyEditor? _currentExtra;
    private string? _currentExtraType;
    private bool _updatingFromDocument;

    public FormDesignerView()
    {
        InitializeComponent();

        _titleBox         = this.FindControl<TextBox>("TitleBox")!;
        _fieldList        = this.FindControl<ListBox>("FieldList")!;
        _keyBox           = this.FindControl<TextBox>("KeyBox")!;
        _labelBox         = this.FindControl<TextBox>("LabelBox")!;
        _typeBox          = this.FindControl<ComboBox>("TypeBox")!;
        _requiredBox      = this.FindControl<CheckBox>("RequiredBox")!;
        _typeSpecificHost = this.FindControl<ContentControl>("TypeSpecificHost")!;

        _titleBox.TextChanged         += (_, _) => { if (_updatingFromDocument || _document is null) return; _document.Title = _titleBox.Text ?? ""; };
        _keyBox.TextChanged           += (_, _) => { if (_updatingFromDocument || _document?.SelectedField is null) return; _document.SelectedField.Key = _keyBox.Text ?? ""; _document.MarkFieldChanged(); RefreshFieldListKeepSelection(); };
        _labelBox.TextChanged         += (_, _) => { if (_updatingFromDocument || _document?.SelectedField is null) return; _document.SelectedField.Label = _labelBox.Text; _document.MarkFieldChanged(); };
        _requiredBox.IsCheckedChanged += (_, _) => { if (_updatingFromDocument || _document?.SelectedField is null) return; _document.SelectedField.Required = _requiredBox.IsChecked == true; _document.MarkFieldChanged(); };
        _typeBox.SelectionChanged     += OnTypeBoxChanged;
    }

    /// <summary>
    /// Set the document this view is bound to. Wires up event subscriptions; pass
    /// a new document (e.g. after loading a file) to reset the view.
    /// </summary>
    public IFormDocument? Document
    {
        get => _document;
        set
        {
            if (ReferenceEquals(_document, value)) return;

            if (_document is not null)
            {
                _document.SelectionChanged -= OnDocSelectionChanged;
                _document.FieldsChanged    -= OnDocFieldsChanged;
            }

            _document = value;

            if (_document is not null)
            {
                _document.SelectionChanged += OnDocSelectionChanged;
                _document.FieldsChanged    += OnDocFieldsChanged;
            }

            RefreshAll();
        }
    }

    // === Document → view updates ===

    private void OnDocSelectionChanged(object? sender, EventArgs e) => BindSelectedField();
    private void OnDocFieldsChanged(object? sender, EventArgs e)    => RefreshFieldList();

    private void RefreshAll()
    {
        _updatingFromDocument = true;
        _titleBox.Text = _document?.Title ?? "";
        _updatingFromDocument = false;
        RefreshFieldList();
        BindSelectedField();
    }

    private void RefreshFieldList()
    {
        _updatingFromDocument = true;
        var fields = _document?.Fields ?? Array.Empty<FieldEditor>();
        _fieldList.ItemsSource = fields.Select(f => $"{f.Key}  [{f.Type}]").ToList();

        // Re-apply the selected index from the document (no event storm because _updatingFromDocument=true).
        var sel = _document?.SelectedField;
        if (sel is not null)
        {
            var idx = -1;
            for (int i = 0; i < fields.Count; i++) if (ReferenceEquals(fields[i], sel)) { idx = i; break; }
            _fieldList.SelectedIndex = idx;
        }
        else
        {
            _fieldList.SelectedIndex = -1;
        }
        _updatingFromDocument = false;
    }

    private void RefreshFieldListKeepSelection()
    {
        // Used after a key-rename: field list display changed but the selected field object didn't.
        RefreshFieldList();
    }

    private void BindSelectedField()
    {
        var field = _document?.SelectedField;
        _updatingFromDocument = true;

        if (field is null)
        {
            _keyBox.Text = "";
            _labelBox.Text = "";
            _typeBox.SelectedIndex = -1;
            _requiredBox.IsChecked = false;
            ShowExtraFor(null);
        }
        else
        {
            _keyBox.Text = field.Key;
            _labelBox.Text = field.Label ?? "";
            _typeBox.SelectedIndex = FindTypeIndex(field.Type);
            _requiredBox.IsChecked = field.Required;
            ShowExtraFor(field);
        }

        _updatingFromDocument = false;
    }

    private void ShowExtraFor(FieldEditor? field)
    {
        var targetType = field?.Type;

        // Same type? Just rebind values into the existing editor — no Content swap, no layout thrash.
        if (_currentExtraType == targetType)
        {
            if (_currentExtra is not null && field is not null) _currentExtra.Bind(field);
            else if (field is null) { _typeSpecificHost.Content = null; _currentExtra = null; _currentExtraType = null; }
            return;
        }

        _currentExtraType = targetType;

        if (_currentExtra is not null)
        {
            _currentExtra.Changed -= OnExtraChanged;
            _currentExtra = null;
        }

        if (field is null)
        {
            _typeSpecificHost.Content = null;
            return;
        }

        var editor = FieldPropertyEditorFactory.Create(field.Type);
        if (editor is IFieldPropertyEditor extra)
        {
            extra.Bind(field);
            extra.Changed += OnExtraChanged;
            _currentExtra = extra;
        }
        _typeSpecificHost.Content = editor;
    }

    private void OnExtraChanged(object? sender, EventArgs e)
    {
        if (_document is null) return;
        _document.MarkFieldChanged();
    }

    private int FindTypeIndex(string type)
    {
        var idx = 0;
        foreach (var item in _typeBox.Items.OfType<ComboBoxItem>())
        {
            if ((item.Content as string) == type) return idx;
            idx++;
        }
        return 0;
    }

    // === View → document actions ===

    private void OnFieldSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingFromDocument || _document is null) return;
        _document.SelectFieldAt(_fieldList.SelectedIndex);
    }

    private void OnTypeBoxChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingFromDocument || _document?.SelectedField is null) return;
        if (_typeBox.SelectedItem is not ComboBoxItem c) return;
        var newType = c.Content?.ToString() ?? "text";
        if (_document.SelectedField.Type == newType) return;
        _document.SelectedField.Type = newType;
        _document.MarkFieldChanged();
        BindSelectedField();          // rebuild extras for the new type
        RefreshFieldList();           // reflect [type] in the list label
    }

    private void OnAddField(object? sender, RoutedEventArgs e)    => _document?.AddField();
    private void OnRemoveField(object? sender, RoutedEventArgs e) => _document?.RemoveSelected();
    private void OnMoveUp(object? sender, RoutedEventArgs e)      => _document?.MoveUp();
    private void OnMoveDown(object? sender, RoutedEventArgs e)    => _document?.MoveDown();
}
