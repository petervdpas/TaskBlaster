using System;
using System.Collections.Generic;
using TaskBlaster.Forms;

namespace TaskBlaster.Interfaces;

/// <summary>
/// In-memory, UI-agnostic document model for editing a GuiBlast form.
/// Owns the form state and raises well-defined events when things change.
/// Views subscribe and reflect; they do NOT mutate the form directly.
/// </summary>
public interface IFormDocument
{
    /// <summary>Current form title.</summary>
    string Title { get; set; }

    /// <summary>Snapshot of the fields in display order.</summary>
    IReadOnlyList<FieldEditor> Fields { get; }

    /// <summary>Currently selected field, or null.</summary>
    FieldEditor? SelectedField { get; }

    /// <summary>True when unsaved changes exist.</summary>
    bool IsDirty { get; }

    // --- Mutations ---
    void SelectField(FieldEditor? field);
    void SelectFieldAt(int index);
    FieldEditor AddField();
    void RemoveSelected();
    void MoveUp();
    void MoveDown();

    /// <summary>Replace the whole form (e.g. when loading a file).</summary>
    void Load(FormEditor form);

    /// <summary>Get the current form for saving / serialization.</summary>
    FormEditor Snapshot();

    /// <summary>Reset the dirty flag after a save.</summary>
    void MarkClean();

    /// <summary>
    /// Notify the document that the user edited a property on the currently selected field.
    /// Flips <see cref="IsDirty"/> without raising <see cref="FieldsChanged"/>.
    /// </summary>
    void MarkFieldChanged();

    // --- Events ---
    /// <summary>Raised when the selected field changes — fires exactly once per genuine change, never on reselection of the same instance.</summary>
    event EventHandler? SelectionChanged;

    /// <summary>Raised when fields are added/removed/reordered or their identity changes.</summary>
    event EventHandler? FieldsChanged;

    /// <summary>Raised when <see cref="IsDirty"/> flips.</summary>
    event EventHandler? DirtyChanged;
}
