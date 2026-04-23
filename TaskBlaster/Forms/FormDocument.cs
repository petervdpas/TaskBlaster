using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Forms;

/// <summary>
/// Canonical, UI-agnostic form-editing document. Owns the form state
/// (title, fields, selection, dirty flag) and raises narrow events.
/// The view subscribes to these events and never mutates state directly.
/// Guards against self-firing on no-op operations (the class of bug that
/// caused the selection cascade hang in the designer).
/// </summary>
public sealed class FormDocument : IFormDocument
{
    private FormEditor _form;
    private readonly ObservableCollection<FieldEditor> _fields = new();
    private FieldEditor? _selected;
    private bool _dirty;

    public FormDocument() : this(FormEditor.CreateDefault()) { }

    public FormDocument(FormEditor initial)
    {
        _form = initial;
        foreach (var f in initial.Fields) _fields.Add(f);
    }

    public string Title
    {
        get => _form.Title;
        set
        {
            if (_form.Title == value) return;
            _form.Title = value;
            MarkDirty();
        }
    }

    public IReadOnlyList<FieldEditor> Fields => _fields;

    public FieldEditor? SelectedField => _selected;

    public bool IsDirty => _dirty;

    public event EventHandler? SelectionChanged;
    public event EventHandler? FieldsChanged;
    public event EventHandler? DirtyChanged;

    public void SelectField(FieldEditor? field)
    {
        // Field must be in our list, or null.
        if (field is not null && !_fields.Contains(field))
            throw new ArgumentException("Field is not part of this document.", nameof(field));

        if (ReferenceEquals(field, _selected)) return;  // <-- kills the cascade at the source
        _selected = field;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectFieldAt(int index)
    {
        var target = index >= 0 && index < _fields.Count ? _fields[index] : null;
        SelectField(target);
    }

    public FieldEditor AddField()
    {
        var baseName = "field";
        var n = _fields.Count + 1;
        string key;
        do { key = $"{baseName}{n++}"; } while (_fields.Any(f => f.Key == key));

        var field = new FieldEditor { Key = key, Type = "text", Label = key };
        _fields.Add(field);
        _form.Fields.Add(field);
        FieldsChanged?.Invoke(this, EventArgs.Empty);
        MarkDirty();
        SelectField(field);
        return field;
    }

    public void RemoveSelected()
    {
        if (_selected is null) return;
        var idx = _fields.IndexOf(_selected);
        var removed = _selected;
        _fields.Remove(removed);
        _form.Fields.Remove(removed);

        // Pick a sensible next selection BEFORE firing FieldsChanged so subscribers see a consistent state.
        var next = _fields.Count == 0
            ? null
            : _fields[Math.Min(idx, _fields.Count - 1)];
        _selected = next;

        FieldsChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        MarkDirty();
    }

    public void MoveUp() => Move(-1);
    public void MoveDown() => Move(+1);

    private void Move(int delta)
    {
        if (_selected is null) return;
        var idx = _fields.IndexOf(_selected);
        var newIdx = idx + delta;
        if (newIdx < 0 || newIdx >= _fields.Count) return;
        _fields.Move(idx, newIdx);
        _form.Fields.Move(idx, newIdx);
        FieldsChanged?.Invoke(this, EventArgs.Empty);
        MarkDirty();
    }

    public void Load(FormEditor form)
    {
        _form = form;
        _fields.Clear();
        foreach (var f in form.Fields) _fields.Add(f);
        _selected = _fields.Count > 0 ? _fields[0] : null;
        FieldsChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        SetDirty(false);
    }

    public FormEditor Snapshot() => _form;

    public void MarkClean() => SetDirty(false);

    public void MarkFieldChanged()
    {
        // Called by the view when the user edits a field-level property
        // (e.g. through a type-specific editor).
        MarkDirty();
    }

    private void MarkDirty() => SetDirty(true);

    private void SetDirty(bool value)
    {
        if (_dirty == value) return;
        _dirty = value;
        DirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    // --- Convenience I/O (caller passes the path; the document doesn't know about files) ---

    public static FormDocument LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        FormEditor form;
        try   { form = FormEditor.FromJson(json); }
        catch { form = FormEditor.CreateDefault(); }
        return new FormDocument(form);
    }

    public void SaveToFile(string path)
    {
        File.WriteAllText(path, _form.ToJson());
        MarkClean();
    }
}
