using System;
using Avalonia.Controls;
using TaskBlaster.Forms;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Views.FieldEditors;

public partial class TextPropertyEditor : UserControl, IFieldPropertyEditor
{
    private readonly TextBox _placeholderBox;
    private FieldEditor? _field;
    private bool _suppress;

    public event EventHandler? Changed;

    public TextPropertyEditor()
    {
        InitializeComponent();
        _placeholderBox = this.FindControl<TextBox>("PlaceholderBox")!;
        _placeholderBox.TextChanged += (_, _) =>
        {
            if (_suppress || _field is null) return;
            _field.Placeholder = _placeholderBox.Text;
            Changed?.Invoke(this, EventArgs.Empty);
        };
    }

    public void Bind(FieldEditor field)
    {
        _field = field;
        _suppress = true;
        _placeholderBox.Text = field.Placeholder ?? "";
        _suppress = false;
    }
}
