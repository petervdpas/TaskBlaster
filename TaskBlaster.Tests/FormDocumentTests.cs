using TaskBlaster.Forms;
using TaskBlaster.Interfaces;

namespace TaskBlaster.Tests;

public class FormDocumentTests
{
    private static FormDocument NewFormWithFields(params string[] keys)
    {
        var form = FormEditor.CreateDefault();
        foreach (var k in keys) form.Fields.Add(new FieldEditor { Key = k, Type = "text", Label = k });
        return new FormDocument(form);
    }

    [Fact]
    public void NewDocument_IsNotDirty()
    {
        var doc = new FormDocument();
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void SelectField_Same_FiresNothing()
    {
        // This is the test that would have caught the selection-cascade hang.
        var doc = NewFormWithFields("name", "role");
        var name = doc.Fields[0];

        doc.SelectField(name); // first selection
        var fires = 0;
        doc.SelectionChanged += (_, _) => fires++;

        doc.SelectField(name); // selecting the SAME instance again
        doc.SelectField(name);
        doc.SelectField(name);

        Assert.Equal(0, fires);
    }

    [Fact]
    public void SelectField_Different_FiresOnce()
    {
        var doc = NewFormWithFields("name", "role");
        var name = doc.Fields[0];
        var role = doc.Fields[1];

        doc.SelectField(name);
        var fires = 0;
        doc.SelectionChanged += (_, _) => fires++;

        doc.SelectField(role);

        Assert.Equal(1, fires);
        Assert.Same(role, doc.SelectedField);
    }

    [Fact]
    public void SelectField_NullWhenAlreadyNull_FiresNothing()
    {
        var doc = new FormDocument();  // no fields, no selection
        var fires = 0;
        doc.SelectionChanged += (_, _) => fires++;

        doc.SelectField(null);
        doc.SelectField(null);

        Assert.Equal(0, fires);
    }

    [Fact]
    public void SelectField_OutsideDocument_Throws()
    {
        var doc = NewFormWithFields("a");
        var stranger = new FieldEditor { Key = "stranger" };

        Assert.Throws<System.ArgumentException>(() => doc.SelectField(stranger));
    }

    [Fact]
    public void AddField_AppendsUnique_Selects_MarksDirty()
    {
        var doc = new FormDocument();
        var selectionFires = 0;
        var fieldsFires = 0;
        var dirtyFires = 0;
        doc.SelectionChanged += (_, _) => selectionFires++;
        doc.FieldsChanged += (_, _) => fieldsFires++;
        doc.DirtyChanged += (_, _) => dirtyFires++;

        var first  = doc.AddField();
        var second = doc.AddField();

        Assert.NotEqual(first.Key, second.Key);
        Assert.Equal(2, doc.Fields.Count);
        Assert.Same(second, doc.SelectedField);
        Assert.True(doc.IsDirty);

        Assert.Equal(2, selectionFires);  // one per add (auto-select)
        Assert.Equal(2, fieldsFires);
        Assert.Equal(1, dirtyFires);      // dirty flipped false→true once
    }

    [Fact]
    public void RemoveSelected_PicksNextField_FiresOnce()
    {
        var doc = NewFormWithFields("a", "b", "c");
        doc.SelectField(doc.Fields[1]); // select 'b'

        var selectionFires = 0;
        var fieldsFires = 0;
        doc.SelectionChanged += (_, _) => selectionFires++;
        doc.FieldsChanged += (_, _) => fieldsFires++;

        doc.RemoveSelected();

        Assert.Equal(2, doc.Fields.Count);
        Assert.Equal("a", doc.Fields[0].Key);
        Assert.Equal("c", doc.Fields[1].Key);
        Assert.Same(doc.Fields[1], doc.SelectedField); // 'c' at new index 1 (was 2)
        Assert.Equal(1, selectionFires);
        Assert.Equal(1, fieldsFires);
    }

    [Fact]
    public void RemoveSelected_LastField_ClearsSelection()
    {
        var doc = NewFormWithFields("only");
        doc.SelectField(doc.Fields[0]);

        doc.RemoveSelected();

        Assert.Empty(doc.Fields);
        Assert.Null(doc.SelectedField);
    }

    [Fact]
    public void MoveUp_ReordersAndMarksDirty()
    {
        var doc = NewFormWithFields("a", "b", "c");
        doc.SelectField(doc.Fields[2]); // 'c'
        doc.MarkClean();

        doc.MoveUp();

        Assert.Equal("a", doc.Fields[0].Key);
        Assert.Equal("c", doc.Fields[1].Key);
        Assert.Equal("b", doc.Fields[2].Key);
        Assert.True(doc.IsDirty);
    }

    [Fact]
    public void MoveUp_AtTop_DoesNothing()
    {
        var doc = NewFormWithFields("a", "b");
        doc.SelectField(doc.Fields[0]);
        doc.MarkClean();
        var fieldsFires = 0;
        doc.FieldsChanged += (_, _) => fieldsFires++;

        doc.MoveUp();

        Assert.Equal("a", doc.Fields[0].Key);
        Assert.False(doc.IsDirty);
        Assert.Equal(0, fieldsFires);
    }

    [Fact]
    public void Load_ResetsEverythingAndClearsDirty()
    {
        var doc = NewFormWithFields("a");
        doc.AddField(); // make it dirty
        Assert.True(doc.IsDirty);

        var replacement = FormEditor.CreateDefault();
        replacement.Title = "Replaced";
        replacement.Fields.Add(new FieldEditor { Key = "x", Type = "text" });

        doc.Load(replacement);

        Assert.Equal("Replaced", doc.Title);
        Assert.Single(doc.Fields);
        Assert.Equal("x", doc.Fields[0].Key);
        Assert.Same(doc.Fields[0], doc.SelectedField);
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void Title_SetSame_FiresNoDirty()
    {
        var doc = new FormDocument();
        doc.MarkClean();
        var dirtyFires = 0;
        doc.DirtyChanged += (_, _) => dirtyFires++;

        doc.Title = doc.Title;

        Assert.False(doc.IsDirty);
        Assert.Equal(0, dirtyFires);
    }

    [Fact]
    public void Title_SetNew_MarksDirty()
    {
        var doc = new FormDocument();
        doc.MarkClean();

        doc.Title = "Something new";

        Assert.True(doc.IsDirty);
    }
}
