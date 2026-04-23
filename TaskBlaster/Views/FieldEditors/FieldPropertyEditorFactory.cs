using Avalonia.Controls;

namespace TaskBlaster.Views.FieldEditors;

/// <summary>
/// Builds the right type-specific property editor for a given field type.
/// Returns null for types that have no extra properties beyond the common set
/// (e.g. switch, checkbox, datetime).
/// </summary>
public static class FieldPropertyEditorFactory
{
    public static Control? Create(string type) => type switch
    {
        "text" or "textarea" or "password" or "email" => new TextPropertyEditor(),
        "number"                                      => new NumberPropertyEditor(),
        "select" or "multiselect" or "radio"          => new OptionsPropertyEditor(),
        _ => null,
    };
}
