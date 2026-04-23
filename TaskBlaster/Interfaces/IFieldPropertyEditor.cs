using System;
using TaskBlaster.Forms;

namespace TaskBlaster.Interfaces;

/// <summary>
/// Property editor for a specific field type. Implementations are Avalonia
/// UserControls that know how to render and edit the extra properties for
/// one category of field (text, number, options, etc.).
/// </summary>
public interface IFieldPropertyEditor
{
    /// <summary>Populate the editor with the field's current values.</summary>
    void Bind(FieldEditor field);

    /// <summary>Raised when the user edits a property on this editor.</summary>
    event EventHandler? Changed;
}
