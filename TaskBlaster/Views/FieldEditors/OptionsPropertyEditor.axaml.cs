using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TaskBlaster.Forms;

namespace TaskBlaster.Views.FieldEditors;

public partial class OptionsPropertyEditor : UserControl, IFieldPropertyEditor
{
    private readonly ListBox _optionList;
    private readonly TextBox _valueBox;
    private readonly TextBox _labelBox;

    private FieldEditor? _field;
    private OptionEditor? _selected;
    private bool _suppress;

    public event EventHandler? Changed;

    public OptionsPropertyEditor()
    {
        InitializeComponent();
        _optionList = this.FindControl<ListBox>("OptionList")!;
        _valueBox   = this.FindControl<TextBox>("OptionValueBox")!;
        _labelBox   = this.FindControl<TextBox>("OptionLabelBox")!;

        _valueBox.TextChanged += (_, _) => CommitValue();
        _labelBox.TextChanged += (_, _) => CommitLabel();
    }

    public void Bind(FieldEditor field)
    {
        _field = field;
        _selected = null;
        _suppress = true;
        _valueBox.Text = "";
        _labelBox.Text = "";
        RefreshOptionList();
        _suppress = false;
    }

    private void RefreshOptionList()
    {
        if (_field is null)
        {
            _optionList.ItemsSource = Array.Empty<string>();
            return;
        }
        _optionList.ItemsSource = _field.Options
            .Select(o => string.IsNullOrEmpty(o.Label) ? o.Value : $"{o.Label} ({o.Value})")
            .ToList();
    }

    private void OnOptionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppress || _field is null) return;
        var idx = _optionList.SelectedIndex;
        _selected = idx >= 0 && idx < _field.Options.Count ? _field.Options[idx] : null;
        _suppress = true;
        _valueBox.Text = _selected?.Value ?? "";
        _labelBox.Text = _selected?.Label ?? "";
        _suppress = false;
    }

    private void CommitValue()
    {
        if (_suppress || _selected is null) return;
        _selected.Value = _valueBox.Text ?? "";
        RefreshOptionList();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void CommitLabel()
    {
        if (_suppress || _selected is null) return;
        _selected.Label = _labelBox.Text;
        RefreshOptionList();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnAddOption(object? sender, RoutedEventArgs e)
    {
        if (_field is null) return;
        var o = new OptionEditor { Value = "value", Label = "Label" };
        _field.Options.Add(o);
        RefreshOptionList();
        _optionList.SelectedIndex = _field.Options.Count - 1;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnRemoveOption(object? sender, RoutedEventArgs e)
    {
        if (_field is null || _selected is null) return;
        _field.Options.Remove(_selected);
        _selected = null;
        RefreshOptionList();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
