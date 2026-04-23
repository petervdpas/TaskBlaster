using System.Linq;
using TaskBlaster.Forms;

namespace TaskBlaster.Tests;

public class FormEditorTests
{
    [Fact]
    public void CreateDefault_HasSaveAndCancelActions()
    {
        var form = FormEditor.CreateDefault();

        Assert.Equal("New Form", form.Title);
        Assert.Equal(2, form.Actions.Count);
        Assert.Contains(form.Actions, a => a.Id == "save"   && a.Submit);
        Assert.Contains(form.Actions, a => a.Id == "cancel" && a.Dismiss);
        Assert.Empty(form.Fields);
    }

    [Fact]
    public void FromJson_ParsesTitleAndFields()
    {
        var json = """
        {
          "title": "Peer",
          "fields": [
            { "key": "name", "type": "text", "label": "Name", "required": true, "placeholder": "Alice" },
            { "key": "quota", "type": "number", "min": 0, "max": 100, "step": 1 }
          ]
        }
        """;

        var form = FormEditor.FromJson(json);

        Assert.Equal("Peer", form.Title);
        Assert.Equal(2, form.Fields.Count);

        var name = form.Fields[0];
        Assert.Equal("name", name.Key);
        Assert.Equal("text", name.Type);
        Assert.Equal("Name", name.Label);
        Assert.True(name.Required);
        Assert.Equal("Alice", name.Placeholder);

        var quota = form.Fields[1];
        Assert.Equal("quota", quota.Key);
        Assert.Equal("number", quota.Type);
        Assert.Equal(0, quota.Min);
        Assert.Equal(100, quota.Max);
        Assert.Equal(1, quota.Step);
    }

    [Fact]
    public void FromJson_ParsesSelectOptions()
    {
        var json = """
        {
          "title": "T",
          "fields": [
            {
              "key": "role", "type": "select", "label": "Role",
              "options": [
                { "value": "User",  "label": "User" },
                { "value": "Admin", "label": "Admin" }
              ]
            }
          ]
        }
        """;

        var form = FormEditor.FromJson(json);
        var role = form.Fields.Single();

        Assert.Equal(2, role.Options.Count);
        Assert.Equal("User",  role.Options[0].Value);
        Assert.Equal("User",  role.Options[0].Label);
        Assert.Equal("Admin", role.Options[1].Value);
        Assert.Equal("Admin", role.Options[1].Label);
    }

    [Fact]
    public void FromJson_FillsDefaultActions_WhenMissing()
    {
        var json = """{ "title": "T", "fields": [] }""";
        var form = FormEditor.FromJson(json);

        Assert.Equal(2, form.Actions.Count);
        Assert.Contains(form.Actions, a => a.Submit);
        Assert.Contains(form.Actions, a => a.Dismiss);
    }

    [Fact]
    public void ToJson_RoundTripsBasicForm()
    {
        var original = new FormEditor { Title = "Peer" };
        original.Fields.Add(new FieldEditor
        {
            Key = "name", Type = "text", Label = "Name",
            Required = true, Placeholder = "Alice"
        });
        original.Fields.Add(new FieldEditor
        {
            Key = "quota", Type = "number", Min = 0, Max = 100, Step = 1
        });

        var json = original.ToJson();
        var roundTripped = FormEditor.FromJson(json);

        Assert.Equal(original.Title, roundTripped.Title);
        Assert.Equal(original.Fields.Count, roundTripped.Fields.Count);

        for (int i = 0; i < original.Fields.Count; i++)
        {
            var a = original.Fields[i];
            var b = roundTripped.Fields[i];
            Assert.Equal(a.Key, b.Key);
            Assert.Equal(a.Type, b.Type);
            Assert.Equal(a.Label, b.Label);
            Assert.Equal(a.Required, b.Required);
            Assert.Equal(a.Placeholder, b.Placeholder);
            Assert.Equal(a.Min, b.Min);
            Assert.Equal(a.Max, b.Max);
            Assert.Equal(a.Step, b.Step);
        }
    }

    [Fact]
    public void ToJson_RoundTripsOptions()
    {
        var original = new FormEditor { Title = "T" };
        var field = new FieldEditor { Key = "role", Type = "select", Label = "Role" };
        field.Options.Add(new OptionEditor { Value = "User",  Label = "User"  });
        field.Options.Add(new OptionEditor { Value = "Admin", Label = "Admin" });
        original.Fields.Add(field);

        var roundTripped = FormEditor.FromJson(original.ToJson());
        var role = roundTripped.Fields.Single();

        Assert.Equal(2, role.Options.Count);
        Assert.Equal("User",  role.Options[0].Value);
        Assert.Equal("Admin", role.Options[1].Value);
    }

    [Fact]
    public void ToJson_OmitsOptions_ForNonOptionTypes()
    {
        // A text field that somehow has Options set should NOT serialize them.
        var form = new FormEditor { Title = "T" };
        var field = new FieldEditor { Key = "name", Type = "text" };
        field.Options.Add(new OptionEditor { Value = "leaked", Label = "leaked" });
        form.Fields.Add(field);

        var json = form.ToJson();

        Assert.DoesNotContain("options", json);
        Assert.DoesNotContain("leaked", json);
    }

    [Fact]
    public void ToJson_OmitsMinMaxStep_ForNonNumericTypes()
    {
        var form = new FormEditor { Title = "T" };
        var field = new FieldEditor { Key = "name", Type = "text", Min = 1, Max = 10, Step = 1 };
        form.Fields.Add(field);

        var json = form.ToJson();

        Assert.DoesNotContain("\"min\"", json);
        Assert.DoesNotContain("\"max\"", json);
        Assert.DoesNotContain("\"step\"", json);
    }

    [Fact]
    public void ToJson_OmitsPlaceholder_ForNonTextTypes()
    {
        var form = new FormEditor { Title = "T" };
        form.Fields.Add(new FieldEditor { Key = "enabled", Type = "switch", Placeholder = "leaked" });

        var json = form.ToJson();

        Assert.DoesNotContain("placeholder", json);
    }
}
