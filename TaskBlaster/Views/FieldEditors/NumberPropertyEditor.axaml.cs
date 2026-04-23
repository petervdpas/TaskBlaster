using System;
using System.Globalization;
using Avalonia.Controls;
using TaskBlaster.Forms;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Views.FieldEditors;

public partial class NumberPropertyEditor : UserControl, IFieldPropertyEditor
{
    private readonly TextBox _minBox;
    private readonly TextBox _maxBox;
    private readonly TextBox _stepBox;
    private FieldEditor? _field;
    private bool _suppress;

    public event EventHandler? Changed;

    public NumberPropertyEditor()
    {
        InitializeComponent();
        _minBox  = this.FindControl<TextBox>("MinBox")!;
        _maxBox  = this.FindControl<TextBox>("MaxBox")!;
        _stepBox = this.FindControl<TextBox>("StepBox")!;

        _minBox.TextChanged  += (_, _) => Commit(v => _field!.Min  = v, _minBox.Text);
        _maxBox.TextChanged  += (_, _) => Commit(v => _field!.Max  = v, _maxBox.Text);
        _stepBox.TextChanged += (_, _) => Commit(v => _field!.Step = v, _stepBox.Text);
    }

    public void Bind(FieldEditor field)
    {
        _field = field;
        _suppress = true;
        _minBox.Text  = field.Min?.ToString()  ?? "";
        _maxBox.Text  = field.Max?.ToString()  ?? "";
        _stepBox.Text = field.Step?.ToString() ?? "";
        _suppress = false;
    }

    private void Commit(Action<double?> setter, string? raw)
    {
        if (_suppress || _field is null) return;
        setter(ParseNullableDouble(raw));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static double? ParseNullableDouble(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
