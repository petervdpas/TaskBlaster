using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
    private readonly Grid _titleRow;
    private readonly Grid _fieldsRow;
    private readonly TextBox _titleBox;
    private readonly ListBox _fieldList;
    private readonly TextBox _keyBox;
    private readonly TextBox _labelBox;
    private readonly ComboBox _typeBox;
    private readonly CheckBox _requiredBox;
    private readonly TextBox _descriptionBox;
    private readonly ContentControl _typeSpecificHost;

    private IFormDocument? _document;
    private IFieldPropertyEditor? _currentExtra;
    private string? _currentExtraType;
    private bool _updatingFromDocument;
    private IVaultService? _vault;
    private Func<CancellationToken, Task>? _ensureUnlocked;

    public event EventHandler? FormSettingsClicked;

    /// <summary>
    /// Hands the designer an <see cref="IVaultService"/> plus the
    /// owner-supplied unlock callback. Option-bearing editors
    /// (select/multiselect/radio) use the callback to pop the password
    /// prompt in place when the user picks "From vault" against a locked
    /// vault. Safe to call once after the view is constructed.
    /// </summary>
    public void Initialize(IVaultService vault, Func<CancellationToken, Task> ensureUnlocked)
    {
        _vault = vault;
        _ensureUnlocked = ensureUnlocked;
    }

    public FormDesignerView()
    {
        InitializeComponent();

        _titleRow         = this.FindControl<Grid>("TitleRow")!;
        _fieldsRow        = this.FindControl<Grid>("FieldsRow")!;
        _titleBox         = this.FindControl<TextBox>("TitleBox")!;
        _fieldList        = this.FindControl<ListBox>("FieldList")!;
        _keyBox           = this.FindControl<TextBox>("KeyBox")!;
        _labelBox         = this.FindControl<TextBox>("LabelBox")!;
        _typeBox          = this.FindControl<ComboBox>("TypeBox")!;
        _requiredBox      = this.FindControl<CheckBox>("RequiredBox")!;
        _descriptionBox   = this.FindControl<TextBox>("DescriptionBox")!;
        _typeSpecificHost = this.FindControl<ContentControl>("TypeSpecificHost")!;

        // Drag-and-drop reorder on the fields list.
        _fieldList.AddHandler(PointerPressedEvent, OnFieldListPointerPressed, RoutingStrategies.Tunnel);
        _fieldList.AddHandler(DragDrop.DragOverEvent, OnFieldListDragOver);
        _fieldList.AddHandler(DragDrop.DropEvent,     OnFieldListDrop);

        _titleBox.TextChanged         += (_, _) => { if (_updatingFromDocument || _document is null) return; _document.Title = _titleBox.Text ?? ""; };
        _keyBox.TextChanged           += (_, _) => { if (_updatingFromDocument || _document?.SelectedField is null) return; _document.SelectedField.Key = _keyBox.Text ?? ""; _document.MarkFieldChanged(); RefreshFieldListKeepSelection(); };
        _labelBox.TextChanged         += (_, _) => { if (_updatingFromDocument || _document?.SelectedField is null) return; _document.SelectedField.Label = _labelBox.Text; _document.MarkFieldChanged(); };
        _requiredBox.IsCheckedChanged += (_, _) => { if (_updatingFromDocument || _document?.SelectedField is null) return; _document.SelectedField.Required = _requiredBox.IsChecked == true; _document.MarkFieldChanged(); };
        _descriptionBox.TextChanged   += (_, _) => { if (_updatingFromDocument || _document?.SelectedField is null) return; _document.SelectedField.Description = string.IsNullOrEmpty(_descriptionBox.Text) ? null : _descriptionBox.Text; _document.MarkFieldChanged(); };
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
        var hasDoc = _document is not null;
        _titleRow.IsVisible  = hasDoc;
        _fieldsRow.IsVisible = hasDoc;

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

        // Keep the listbox highlight in sync with the document. AddField fires
        // FieldsChanged before SelectField, so RefreshFieldList runs while
        // SelectedField is still null; without this, the new row never lights up.
        var fields = _document?.Fields;
        var idx = -1;
        if (field is not null && fields is not null)
            for (int i = 0; i < fields.Count; i++) if (ReferenceEquals(fields[i], field)) { idx = i; break; }
        if (_fieldList.SelectedIndex != idx) _fieldList.SelectedIndex = idx;

        if (field is null)
        {
            _keyBox.Text = "";
            _labelBox.Text = "";
            _typeBox.SelectedIndex = -1;
            _requiredBox.IsChecked = false;
            _descriptionBox.Text = "";
            ShowExtraFor(null);
        }
        else
        {
            _keyBox.Text = field.Key;
            _labelBox.Text = field.Label ?? "";
            _typeBox.SelectedIndex = FindTypeIndex(field.Type);
            _requiredBox.IsChecked = field.Required;
            _descriptionBox.Text = field.Description ?? "";
            ShowExtraFor(field);
        }

        // Release suppression only after Avalonia's layout pass completes.
        // Any spurious FieldList/OptionList SelectionChanged events fired during
        // that pass (a known Avalonia quirk when Content swaps) hit our handlers
        // while _updatingFromDocument is still true, and get ignored.
        Dispatcher.UIThread.Post(() => _updatingFromDocument = false, DispatcherPriority.Loaded);
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

        var editor = FieldPropertyEditorFactory.Create(field.Type, _vault, _ensureUnlocked);
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

    private void OnFormSettingsClicked(object? sender, RoutedEventArgs e) => FormSettingsClicked?.Invoke(this, EventArgs.Empty);

    private void OnAddField(object? sender, RoutedEventArgs e)
    {
        if (_document is null) return;
        _document.AddField();
        // Land focus in Key with the auto-generated name selected so the user
        // can just start typing the real name. Posted at Loaded priority so it
        // runs after BindSelectedField's own deferred unsuppression.
        Dispatcher.UIThread.Post(() =>
        {
            _keyBox.Focus();
            _keyBox.SelectAll();
        }, DispatcherPriority.Loaded);
    }
    private void OnRemoveField(object? sender, RoutedEventArgs e) => _document?.RemoveSelected();
    private void OnMoveUp(object? sender, RoutedEventArgs e)      => _document?.MoveUp();
    private void OnMoveDown(object? sender, RoutedEventArgs e)    => _document?.MoveDown();

    // ==================== Drag-and-drop reorder ====================

    private static readonly DataFormat<string> FieldIndexFormat =
        DataFormat.CreateStringApplicationFormat("taskblaster.field-index");

    private async void OnFieldListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_document is null) return;
        if (!e.GetCurrentPoint(_fieldList).Properties.IsLeftButtonPressed) return;

        var item = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>();
        if (item is null) return;
        var idx = _fieldList.IndexFromContainer(item);
        if (idx < 0 || idx >= _document.Fields.Count) return;

        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(FieldIndexFormat, idx.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        try
        {
            await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Move);
        }
        catch
        {
            // Swallow — user cancel / interruption during drag.
        }
    }

    private void OnFieldListDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = Contains(e, FieldIndexFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFieldListDrop(object? sender, DragEventArgs e)
    {
        if (_document is null) return;
        if (!TryGet(e, FieldIndexFormat, out var raw)) return;
        if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var from)) return;

        var targetItem = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>();
        int to = targetItem is not null
            ? _fieldList.IndexFromContainer(targetItem)
            : _document.Fields.Count - 1;
        if (to < 0) to = _document.Fields.Count - 1;

        _document.MoveField(from, to);
        e.Handled = true;
    }

    private static bool Contains(DragEventArgs e, DataFormat<string> format)
    {
        foreach (var item in e.DataTransfer.Items)
            foreach (var f in item.Formats)
                if (f == format) return true;
        return false;
    }

    private static bool TryGet(DragEventArgs e, DataFormat<string> format, out string? value)
    {
        foreach (var item in e.DataTransfer.Items)
        {
            if (item.TryGetRaw(format) is string s) { value = s; return true; }
        }
        value = null;
        return false;
    }
}
