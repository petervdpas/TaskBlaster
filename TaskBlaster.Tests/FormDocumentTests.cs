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

    // --- Key validation ---

    [Fact]
    public void ValidateKey_Empty_ReturnsError()
    {
        var doc = NewFormWithFields("a");
        doc.SelectField(doc.Fields[0]);

        Assert.NotNull(doc.ValidateKey(""));
        Assert.NotNull(doc.ValidateKey("   "));
    }

    [Fact]
    public void ValidateKey_InvalidCharacter_ReturnsError()
    {
        var doc = NewFormWithFields("a");
        doc.SelectField(doc.Fields[0]);

        Assert.NotNull(doc.ValidateKey("has space"));
        Assert.NotNull(doc.ValidateKey("has/slash"));
        Assert.NotNull(doc.ValidateKey("has$dollar"));
    }

    [Fact]
    public void ValidateKey_ValidChars_ReturnsNull()
    {
        var doc = NewFormWithFields("a");
        doc.SelectField(doc.Fields[0]);

        Assert.Null(doc.ValidateKey("first_name"));
        Assert.Null(doc.ValidateKey("field-1"));
        Assert.Null(doc.ValidateKey("data.value"));
        Assert.Null(doc.ValidateKey("f123"));
    }

    [Fact]
    public void ValidateKey_DuplicateOfOtherField_ReturnsError()
    {
        var doc = NewFormWithFields("name", "role");
        doc.SelectField(doc.Fields[0]); // editing "name"

        Assert.NotNull(doc.ValidateKey("role")); // used by the OTHER field
    }

    [Fact]
    public void ValidateKey_SameAsOwnKey_IsAllowed()
    {
        var doc = NewFormWithFields("name");
        doc.SelectField(doc.Fields[0]);

        Assert.Null(doc.ValidateKey("name")); // renaming to your own key is a no-op, not a conflict
    }

    [Fact]
    public void RenameSelectedKey_Valid_UpdatesKeyAndMarksDirty()
    {
        var doc = NewFormWithFields("a");
        doc.SelectField(doc.Fields[0]);
        doc.MarkClean();

        doc.RenameSelectedKey("new_key");

        Assert.Equal("new_key", doc.Fields[0].Key);
        Assert.True(doc.IsDirty);
    }

    [Fact]
    public void RenameSelectedKey_Invalid_Throws()
    {
        var doc = NewFormWithFields("a", "b");
        doc.SelectField(doc.Fields[0]);

        Assert.Throws<System.ArgumentException>(() => doc.RenameSelectedKey(""));
        Assert.Throws<System.ArgumentException>(() => doc.RenameSelectedKey("b"));   // duplicate
        Assert.Throws<System.ArgumentException>(() => doc.RenameSelectedKey("x y")); // space
    }

    [Fact]
    public void RenameSelectedKey_NoSelection_Throws()
    {
        var doc = new FormDocument();
        Assert.Throws<System.InvalidOperationException>(() => doc.RenameSelectedKey("x"));
    }

    [Fact]
    public void RenameSelectedKey_SameKey_DoesNothing()
    {
        var doc = NewFormWithFields("a");
        doc.SelectField(doc.Fields[0]);
        doc.MarkClean();
        var fieldsFires = 0;
        doc.FieldsChanged += (_, _) => fieldsFires++;

        doc.RenameSelectedKey("a");

        Assert.False(doc.IsDirty);
        Assert.Equal(0, fieldsFires);
    }

    [Fact]
    public void RenameSelectedKey_Valid_FiresFieldsChangedOnce()
    {
        var doc = NewFormWithFields("a");
        doc.SelectField(doc.Fields[0]);
        var fires = 0;
        doc.FieldsChanged += (_, _) => fires++;

        doc.RenameSelectedKey("renamed");

        Assert.Equal(1, fires);
    }

    // --- Actions ---

    [Fact]
    public void NewDocument_HasSaveAndCancelActions()
    {
        var doc = new FormDocument();
        Assert.Equal(2, doc.Actions.Count);
    }

    [Fact]
    public void AddAction_GeneratesUniqueId_FiresEvents_MarksDirty()
    {
        var doc = new FormDocument();
        var changes = 0; var dirtyFires = 0;
        doc.ActionsChanged += (_, _) => changes++;
        doc.DirtyChanged   += (_, _) => dirtyFires++;

        var a = doc.AddAction();
        var b = doc.AddAction();

        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(4, doc.Actions.Count); // save + cancel + 2 new
        Assert.Equal(2, changes);
        Assert.Equal(1, dirtyFires);        // dirty flip false→true once
        Assert.True(doc.IsDirty);
    }

    [Fact]
    public void RemoveAction_Removes_AndFires()
    {
        var doc = new FormDocument();
        var cancel = doc.Actions[1];
        var fires = 0;
        doc.ActionsChanged += (_, _) => fires++;

        doc.RemoveAction(cancel);

        Assert.Single(doc.Actions);
        Assert.DoesNotContain(cancel, doc.Actions);
        Assert.Equal(1, fires);
    }

    [Fact]
    public void RemoveAction_NotInList_IsNoOp()
    {
        var doc = new FormDocument();
        doc.MarkClean();
        var fires = 0;
        doc.ActionsChanged += (_, _) => fires++;

        doc.RemoveAction(new ActionEditor { Id = "stranger" });

        Assert.Equal(0, fires);
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void ValidateActionId_Empty_ReturnsError()
    {
        var doc = new FormDocument();
        Assert.NotNull(doc.ValidateActionId(""));
        Assert.NotNull(doc.ValidateActionId("   "));
    }

    [Fact]
    public void ValidateActionId_InvalidChars_ReturnsError()
    {
        var doc = new FormDocument();
        Assert.NotNull(doc.ValidateActionId("has space"));
        Assert.NotNull(doc.ValidateActionId("has/slash"));
    }

    [Fact]
    public void ValidateActionId_DuplicateWithinForm_ReturnsError()
    {
        var doc = new FormDocument();
        // doc.Actions[0] = save
        Assert.NotNull(doc.ValidateActionId("save"));
    }

    [Fact]
    public void ValidateActionId_DuplicateIgnoringSelf_IsAllowed()
    {
        var doc = new FormDocument();
        var save = doc.Actions[0];
        Assert.Null(doc.ValidateActionId("save", ignore: save));
    }

    // --- MoveField (arbitrary index) ---

    [Fact]
    public void MoveField_ReordersAndMarksDirty()
    {
        var doc = NewFormWithFields("a", "b", "c", "d");
        doc.MarkClean();

        doc.MoveField(0, 2); // move a from 0 to 2 → [b,c,a,d]

        Assert.Equal("b", doc.Fields[0].Key);
        Assert.Equal("c", doc.Fields[1].Key);
        Assert.Equal("a", doc.Fields[2].Key);
        Assert.Equal("d", doc.Fields[3].Key);
        Assert.True(doc.IsDirty);
    }

    [Fact]
    public void MoveField_InvalidIndices_NoOp()
    {
        var doc = NewFormWithFields("a", "b");
        doc.MarkClean();
        var fires = 0;
        doc.FieldsChanged += (_, _) => fires++;

        doc.MoveField(-1, 0);
        doc.MoveField(0, 10);
        doc.MoveField(5, 0);
        doc.MoveField(0, 0);  // no-op (same)

        Assert.False(doc.IsDirty);
        Assert.Equal(0, fires);
    }

    [Fact]
    public void MoveField_FiresFieldsChangedOnce()
    {
        var doc = NewFormWithFields("a", "b", "c");
        var fires = 0;
        doc.FieldsChanged += (_, _) => fires++;

        doc.MoveField(2, 0);

        Assert.Equal(1, fires);
    }

    // --- Visibility rules ---

    [Fact]
    public void NewDocument_HasNoVisibilityRules()
    {
        var doc = new FormDocument();
        Assert.Empty(doc.Visibility);
    }

    [Fact]
    public void AddVisibilityRule_Appends_FiresEvents_MarksDirty()
    {
        var doc = new FormDocument();
        var changes = 0; var dirty = 0;
        doc.VisibilityChanged += (_, _) => changes++;
        doc.DirtyChanged      += (_, _) => dirty++;

        var rule = doc.AddVisibilityRule();

        Assert.Single(doc.Visibility);
        Assert.Same(rule, doc.Visibility[0]);
        Assert.Equal(1, changes);
        Assert.Equal(1, dirty);
        Assert.True(doc.IsDirty);
    }

    [Fact]
    public void RemoveVisibilityRule_Removes_AndFires()
    {
        var doc = new FormDocument();
        var a = doc.AddVisibilityRule();
        var b = doc.AddVisibilityRule();
        var fires = 0;
        doc.VisibilityChanged += (_, _) => fires++;

        doc.RemoveVisibilityRule(a);

        Assert.Single(doc.Visibility);
        Assert.Same(b, doc.Visibility[0]);
        Assert.Equal(1, fires);
    }

    [Fact]
    public void RemoveVisibilityRule_NotInList_IsNoOp()
    {
        var doc = new FormDocument();
        doc.MarkClean();
        var fires = 0;
        doc.VisibilityChanged += (_, _) => fires++;

        doc.RemoveVisibilityRule(new VisibilityRuleEditor());

        Assert.Equal(0, fires);
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void AddVisibilityRule_RoundTripsThroughJson()
    {
        var doc = new FormDocument();
        var rule = doc.AddVisibilityRule();
        rule.Field = "role";
        rule.Eq = "Admin";
        rule.Show.Add("quota");

        var json = doc.Snapshot().ToJson();
        var reloaded = FormEditor.FromJson(json);

        Assert.Single(reloaded.Visibility);
        Assert.Equal("role",  reloaded.Visibility[0].Field);
        Assert.Equal("Admin", reloaded.Visibility[0].Eq);
        Assert.Contains("quota", reloaded.Visibility[0].Show);
    }
}
