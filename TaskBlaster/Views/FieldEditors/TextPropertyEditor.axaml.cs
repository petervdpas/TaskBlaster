using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using TaskBlaster.Forms;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Views.FieldEditors;

public partial class TextPropertyEditor : UserControl, IFieldPropertyEditor
{
    private readonly TextBox _placeholderBox;
    private readonly TextBox _patternBox;
    private readonly CheckBox _emailBox;
    private readonly StackPanel _rowsSection;
    private readonly TextBox _rowsBox;

    private FieldEditor? _field;
    private bool _suppress;

    public event EventHandler? Changed;

    public TextPropertyEditor()
    {
        InitializeComponent();
        _placeholderBox = this.FindControl<TextBox>("PlaceholderBox")!;
        _patternBox     = this.FindControl<TextBox>("PatternBox")!;
        _emailBox       = this.FindControl<CheckBox>("EmailBox")!;
        _rowsSection    = this.FindControl<StackPanel>("RowsSection")!;
        _rowsBox        = this.FindControl<TextBox>("RowsBox")!;

        _placeholderBox.TextChanged += (_, _) => { if (_suppress || _field is null) return; _field.Placeholder = _placeholderBox.Text; Changed?.Invoke(this, EventArgs.Empty); };
        _patternBox.TextChanged     += (_, _) => { if (_suppress || _field is null) return; _field.Pattern     = string.IsNullOrEmpty(_patternBox.Text) ? null : _patternBox.Text; Changed?.Invoke(this, EventArgs.Empty); };
        _emailBox.IsCheckedChanged  += (_, _) => { if (_suppress || _field is null) return; _field.Email       = _emailBox.IsChecked == true; Changed?.Invoke(this, EventArgs.Empty); };
        _rowsBox.TextChanged        += (_, _) => { if (_suppress || _field is null) return; _field.Rows        = ParseNullableInt(_rowsBox.Text); Changed?.Invoke(this, EventArgs.Empty); };
    }

    public void Bind(FieldEditor field)
    {
        _field = field;
        _suppress = true;

        _placeholderBox.Text = field.Placeholder ?? "";
        _patternBox.Text     = field.Pattern ?? "";
        _emailBox.IsChecked  = field.Email;
        _rowsBox.Text        = field.Rows?.ToString(CultureInfo.InvariantCulture) ?? "";

        // Per-type visibility: Email only makes sense on "text"; Rows on "textarea".
        _emailBox.IsVisible   = field.Type == "text";
        _rowsSection.IsVisible = field.Type == "textarea";

        // Release suppression after Avalonia's queued async events settle.
        Dispatcher.UIThread.Post(() => _suppress = false, DispatcherPriority.Loaded);
    }

    private static int? ParseNullableInt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
