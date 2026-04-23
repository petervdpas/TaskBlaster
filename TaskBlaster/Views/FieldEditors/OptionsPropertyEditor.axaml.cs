using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TaskBlaster.Forms;
using TaskBlaster.Interfaces;

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
        // Release suppression AFTER the queued TextChanged/SelectionChanged events
        // have been processed. TextBox.TextChanged fires asynchronously, so a sync
        // `_suppress = false` at the end of this method is too early.
        Dispatcher.UIThread.Post(() => _suppress = false, DispatcherPriority.Loaded);
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
        var newSelected = idx >= 0 && idx < _field.Options.Count ? _field.Options[idx] : null;
        if (ReferenceEquals(newSelected, _selected)) return;  // no-op short-circuit
        _selected = newSelected;
        _suppress = true;
        _valueBox.Text = _selected?.Value ?? "";
        _labelBox.Text = _selected?.Label ?? "";
        Dispatcher.UIThread.Post(() => _suppress = false, DispatcherPriority.Loaded);
    }

    private void CommitValue()
    {
        if (_suppress || _selected is null) return;
        _selected.Value = _valueBox.Text ?? "";
        // Note: intentionally NOT calling RefreshOptionList here. Rebuilding
        // ItemsSource on every keystroke causes ListBox to bounce its SelectedIndex
        // (-1 then back), which re-fires SelectionChanged, which sets the text
        // again, triggering an infinite cascade. The displayed "Value (Label)"
        // in the list will be refreshed next time selection changes.
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void CommitLabel()
    {
        if (_suppress || _selected is null) return;
        _selected.Label = _labelBox.Text;
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
