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

    /// <summary>Move the field at <paramref name="fromIndex"/> to <paramref name="toIndex"/>. No-op if either index is out of range or equal.</summary>
    void MoveField(int fromIndex, int toIndex);

    /// <summary>
    /// Rename the selected field's key. Throws if the new key is empty, already in use, or contains invalid characters.
    /// Fires <see cref="FieldsChanged"/> (so the displayed list label updates) and dirties the document.
    /// </summary>
    void RenameSelectedKey(string newKey);

    /// <summary>
    /// Validate a candidate key for the selected field. Returns null if valid, otherwise an error message.
    /// </summary>
    string? ValidateKey(string candidate);

    // --- Actions (form-level buttons) ---

    /// <summary>Snapshot of the form's action list.</summary>
    IReadOnlyList<ActionEditor> Actions { get; }

    /// <summary>Append a new action. Returns the created <see cref="ActionEditor"/>.</summary>
    ActionEditor AddAction();

    /// <summary>Remove the given action. No-op if not present.</summary>
    void RemoveAction(ActionEditor action);

    /// <summary>Validate a candidate action id. Returns null if valid, otherwise an error message.</summary>
    string? ValidateActionId(string candidate, ActionEditor? ignore = null);

    /// <summary>Notify the document that an action property changed (via view). Dirties the form.</summary>
    void MarkActionChanged();

    /// <summary>Raised when the actions collection changes (add/remove/reorder).</summary>
    event EventHandler? ActionsChanged;

    // --- Visibility rules ---

    /// <summary>Snapshot of the form's visibility rules.</summary>
    IReadOnlyList<VisibilityRuleEditor> Visibility { get; }

    /// <summary>Append a new visibility rule. Returns the created editor.</summary>
    VisibilityRuleEditor AddVisibilityRule();

    /// <summary>Remove the given rule. No-op if not present.</summary>
    void RemoveVisibilityRule(VisibilityRuleEditor rule);

    /// <summary>Notify the document that a rule's properties were edited (dirties only).</summary>
    void MarkVisibilityChanged();

    /// <summary>Raised when the visibility rules collection changes (add/remove).</summary>
    event EventHandler? VisibilityChanged;

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
