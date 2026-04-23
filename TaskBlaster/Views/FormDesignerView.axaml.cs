using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TaskBlaster.Forms;
using TaskBlaster.Views.FieldEditors;

namespace TaskBlaster.Views;

public partial class FormDesignerView : UserControl
{
    private readonly TextBox _titleBox;
    private readonly ListBox _fieldList;
    private readonly TextBox _keyBox;
    private readonly TextBox _labelBox;
    private readonly ComboBox _typeBox;
    private readonly CheckBox _requiredBox;
    private readonly ContentControl _typeSpecificHost;

    private FormEditor _form = FormEditor.CreateDefault();
    private FieldEditor? _selectedField;
    private FieldEditor? _boundField;          // last field actually bound — guards BindSelectedField
    private string? _currentExtraType;         // last type rendered in the extras host — guards ShowExtraFor
    private IFieldPropertyEditor? _currentExtra;
    private bool _suppressBinding;

    public event EventHandler? DirtyChanged;
    public bool IsDirty { get; private set; }

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

        _titleBox.TextChanged         += (_, _) => { if (_suppressBinding) return; _form.Title = _titleBox.Text ?? ""; MarkDirty(); };
        _keyBox.TextChanged           += (_, _) => { if (_suppressBinding || _selectedField is null) return; _selectedField.Key = _keyBox.Text ?? ""; RefreshFieldList(); MarkDirty(); };
        _labelBox.TextChanged         += (_, _) => { if (_suppressBinding || _selectedField is null) return; _selectedField.Label = _labelBox.Text; MarkDirty(); };
        _requiredBox.IsCheckedChanged += (_, _) => { if (_suppressBinding || _selectedField is null) return; _selectedField.Required = _requiredBox.IsChecked == true; MarkDirty(); };
        _typeBox.SelectionChanged     += OnTypeBoxChanged;
    }

    // === Public API ===

    public void LoadFile(string path)
    {
        DebugLog.Write($"FormDesignerView.LoadFile({path}) BEGIN");
        _form = File.Exists(path)
            ? SafeFromJson(File.ReadAllText(path))
            : FormEditor.CreateDefault();
        DebugLog.Write($"  parsed form: title={_form.Title} fields={_form.Fields.Count}");
        LoadFromEditor();
        MarkClean();
        DebugLog.Write("FormDesignerView.LoadFile END");
    }

    public void SaveTo(string path)
    {
        File.WriteAllText(path, _form.ToJson());
        MarkClean();
    }

    public string ToJson() => _form.ToJson();

    public void MarkClean()
    {
        if (!IsDirty) return;
        IsDirty = false;
        DirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    private static FormEditor SafeFromJson(string json)
    {
        try { return FormEditor.FromJson(json); }
        catch { return FormEditor.CreateDefault(); }
    }

    // === Binding ===

    private void LoadFromEditor()
    {
        DebugLog.Write("  LoadFromEditor BEGIN");
        _suppressBinding = true;
        _titleBox.Text = _form.Title;
        DebugLog.Write("    RefreshFieldList");
        RefreshFieldList();
        if (_form.Fields.Count > 0)
        {
            DebugLog.Write("    select first field");
            _selectedField = _form.Fields[0];
            _fieldList.SelectedIndex = 0;
            DebugLog.Write("    BindSelectedField");
            BindSelectedField();
        }
        else
        {
            _selectedField = null;
            ClearCommonFields();
            ShowExtraFor(null);
        }
        _suppressBinding = false;
        DebugLog.Write("  LoadFromEditor END");
    }

    private void MarkDirty()
    {
        if (IsDirty) return;
        IsDirty = true;
        DirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshFieldList()
    {
        _fieldList.ItemsSource = _form.Fields
            .Select(f => $"{f.Key}  [{f.Type}]")
            .ToList();
        if (_selectedField is not null)
        {
            var idx = _form.Fields.IndexOf(_selectedField);
            if (idx >= 0) _fieldList.SelectedIndex = idx;
        }
    }

    private void ClearCommonFields()
    {
        var prev = _suppressBinding;
        _suppressBinding = true;
        _keyBox.Text = "";
        _labelBox.Text = "";
        _typeBox.SelectedIndex = -1;
        _requiredBox.IsChecked = false;
        _suppressBinding = prev;
    }

    private void BindSelectedField()
    {
        // Short-circuit: already showing this exact field (prevents event cascade loops).
        if (ReferenceEquals(_selectedField, _boundField)) return;
        _boundField = _selectedField;

        DebugLog.Write($"BindSelectedField → {_selectedField?.Key ?? "null"}");

        if (_selectedField is null)
        {
            ClearCommonFields();
            ShowExtraFor(null);
            return;
        }

        var prev = _suppressBinding;
        _suppressBinding = true;
        _keyBox.Text = _selectedField.Key;
        _labelBox.Text = _selectedField.Label ?? "";
        _typeBox.SelectedIndex = FindTypeIndex(_selectedField.Type);
        _requiredBox.IsChecked = _selectedField.Required;
        ShowExtraFor(_selectedField);
        _suppressBinding = prev;
    }

    private void ShowExtraFor(FieldEditor? field)
    {
        var targetType = field?.Type;

        // Same type already hosted: just rebind values into the existing editor, don't rebuild.
        if (_currentExtraType == targetType)
        {
            if (_currentExtra is not null && field is not null) _currentExtra.Bind(field);
            else if (field is null) { AssignContentSuppressed(null); _currentExtra = null; }
            return;
        }

        _currentExtraType = targetType;
        DebugLog.Write($"  ShowExtraFor → {targetType ?? "null"}");

        if (_currentExtra is not null)
        {
            _currentExtra.Changed -= OnExtraChanged;
            _currentExtra = null;
        }

        if (field is null)
        {
            AssignContentSuppressed(null);
            return;
        }

        var editor = FieldPropertyEditorFactory.Create(field.Type);
        if (editor is IFieldPropertyEditor extra)
        {
            extra.Bind(field);
            extra.Changed += OnExtraChanged;
            _currentExtra = extra;
        }
        AssignContentSuppressed(editor);
    }

    /// <summary>
    /// Assigning <see cref="ContentControl.Content"/> triggers a layout pass that can make
    /// <c>FieldList</c> briefly flip its SelectedIndex (-1 then back to the previous value).
    /// Suppress our own selection handler across that pass so it doesn't re-enter this code.
    /// </summary>
    private void AssignContentSuppressed(object? content)
    {
        var prev = _suppressBinding;
        _suppressBinding = true;
        _typeSpecificHost.Content = content;
        // Keep suppression active until Avalonia finishes the queued layout work,
        // then restore. DispatcherPriority.Loaded runs after layout but before idle.
        Dispatcher.UIThread.Post(() => _suppressBinding = prev, DispatcherPriority.Loaded);
    }

    private void OnExtraChanged(object? sender, EventArgs e) => MarkDirty();

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

    // === Event handlers ===

    private void OnFieldSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressBinding) return;
        var idx = _fieldList.SelectedIndex;
        var newField = idx >= 0 && idx < _form.Fields.Count ? _form.Fields[idx] : null;
        if (ReferenceEquals(newField, _selectedField)) return; // no-op selection — short-circuit
        DebugLog.Write($"OnFieldSelectionChanged idx={idx} → {newField?.Key ?? "null"}");
        _selectedField = newField;
        BindSelectedField();
    }

    private void OnTypeBoxChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressBinding || _selectedField is null) return;
        if (_typeBox.SelectedItem is not ComboBoxItem c) return;
        var newType = c.Content?.ToString() ?? "text";
        if (_selectedField.Type == newType) return;
        _selectedField.Type = newType;
        ShowExtraFor(_selectedField);
        RefreshFieldList();
        MarkDirty();
    }

    private void OnAddField(object? sender, RoutedEventArgs e)
    {
        var baseName = "field";
        var n = _form.Fields.Count + 1;
        string key;
        do { key = $"{baseName}{n++}"; } while (_form.Fields.Any(f => f.Key == key));
        var field = new FieldEditor { Key = key, Type = "text", Label = key };
        _form.Fields.Add(field);
        _selectedField = field;
        RefreshFieldList();
        BindSelectedField();
        MarkDirty();
    }

    private void OnRemoveField(object? sender, RoutedEventArgs e)
    {
        if (_selectedField is null) return;
        var idx = _form.Fields.IndexOf(_selectedField);
        _form.Fields.Remove(_selectedField);
        if (_form.Fields.Count > 0)
        {
            var newIdx = Math.Min(idx, _form.Fields.Count - 1);
            _selectedField = _form.Fields[newIdx];
        }
        else
        {
            _selectedField = null;
        }
        RefreshFieldList();
        BindSelectedField();
        MarkDirty();
    }

    private void OnMoveUp(object? sender, RoutedEventArgs e)
    {
        if (_selectedField is null) return;
        var idx = _form.Fields.IndexOf(_selectedField);
        if (idx <= 0) return;
        _form.Fields.Move(idx, idx - 1);
        RefreshFieldList();
        MarkDirty();
    }

    private void OnMoveDown(object? sender, RoutedEventArgs e)
    {
        if (_selectedField is null) return;
        var idx = _form.Fields.IndexOf(_selectedField);
        if (idx < 0 || idx >= _form.Fields.Count - 1) return;
        _form.Fields.Move(idx, idx + 1);
        RefreshFieldList();
        MarkDirty();
    }
}
