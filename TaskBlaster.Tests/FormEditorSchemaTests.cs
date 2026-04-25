using System.Linq;
using TaskBlaster.Forms;

namespace TaskBlaster.Tests;

public class FormEditorSchemaTests
{
    [Fact]
    public void FromJson_ParsesVisibilityRules_EqShowHide()
    {
        var json = """
        {
          "title": "T",
          "fields": [{ "key": "role", "type": "select" }, { "key": "quota", "type": "number" }],
          "visibility": [
            { "field": "role", "eq": "Admin", "show": ["quota"] },
            { "field": "role", "neq": "Admin", "hide": ["quota"] }
          ]
        }
        """;

        var form = FormEditor.FromJson(json);

        Assert.Equal(2, form.Visibility.Count);
        Assert.Equal("role", form.Visibility[0].Field);
        Assert.Equal("Admin", form.Visibility[0].Eq);
        Assert.Equal(new[] { "quota" }, form.Visibility[0].Show.ToArray());
        Assert.Equal("Admin", form.Visibility[1].Neq);
        Assert.Equal(new[] { "quota" }, form.Visibility[1].Hide.ToArray());
    }

    [Fact]
    public void FromJson_ParsesVisibilityRules_ShowTagsHideTags_SnakeCase()
    {
        var json = """
        {
          "title": "T",
          "fields": [{ "key": "role", "type": "select" }],
          "visibility": [
            { "field": "role", "eq": "Admin", "show_tags": ["privileged"], "hide_tags": ["basic"] }
          ]
        }
        """;

        var form = FormEditor.FromJson(json);
        var rule = form.Visibility.Single();

        Assert.Equal(new[] { "privileged" }, rule.ShowTags.ToArray());
        Assert.Equal(new[] { "basic" },      rule.HideTags.ToArray());
    }

    [Fact]
    public void ToJson_RoundTripsVisibilityRules()
    {
        var form = FormEditor.CreateDefault();
        form.Fields.Add(new FieldEditor { Key = "role", Type = "select" });
        form.Fields.Add(new FieldEditor { Key = "quota", Type = "number" });

        var rule = new VisibilityRuleEditor { Field = "role", Eq = "Admin" };
        rule.Show.Add("quota");
        rule.ShowTags.Add("privileged");
        rule.HideTags.Add("basic");
        form.Visibility.Add(rule);

        var roundTripped = FormEditor.FromJson(form.ToJson());
        var r = roundTripped.Visibility.Single();

        Assert.Equal("role",  r.Field);
        Assert.Equal("Admin", r.Eq);
        Assert.Equal(new[] { "quota" },      r.Show.ToArray());
        Assert.Equal(new[] { "privileged" }, r.ShowTags.ToArray());
        Assert.Equal(new[] { "basic" },      r.HideTags.ToArray());
    }

    [Fact]
    public void ToJson_WritesSnakeCase_ForShowTagsHideTags()
    {
        var form = FormEditor.CreateDefault();
        var rule = new VisibilityRuleEditor { Field = "role", Eq = "Admin" };
        rule.ShowTags.Add("x");
        form.Visibility.Add(rule);

        var json = form.ToJson();

        Assert.Contains("show_tags", json);
        // Must not accidentally use camelCase for these snake-cased keys.
        Assert.DoesNotContain("showTags", json);
    }

    [Fact]
    public void ToJson_OmitsVisibility_WhenEmpty()
    {
        var form = FormEditor.CreateDefault();
        form.Fields.Add(new FieldEditor { Key = "a", Type = "text" });

        var json = form.ToJson();

        Assert.DoesNotContain("visibility", json);
    }

    [Fact]
    public void Resizable_RoundTrips_WhenTrue()
    {
        var form = FormEditor.CreateDefault();
        form.Resizable = true;

        var json = form.ToJson();
        Assert.Contains("\"resizable\": true", json);

        var rt = FormEditor.FromJson(json);
        Assert.True(rt.Resizable);
    }

    [Fact]
    public void Resizable_IsOmitted_WhenFalse()
    {
        var form = FormEditor.CreateDefault();
        // default Resizable is false
        var json = form.ToJson();

        Assert.DoesNotContain("resizable", json);

        var rt = FormEditor.FromJson(json);
        Assert.False(rt.Resizable);
    }

    [Fact]
    public void FromJson_ParsesResizable_True()
    {
        var json = """
        {
          "title": "T",
          "resizable": true,
          "fields": [{ "key": "a", "type": "text" }]
        }
        """;

        var form = FormEditor.FromJson(json);

        Assert.True(form.Resizable);
    }

    [Fact]
    public void FromJson_ParsesFieldPatternAndEmail()
    {
        var json = """
        {
          "title": "T",
          "fields": [
            { "key": "name", "type": "text", "pattern": "^[A-Z].*$" },
            { "key": "mail", "type": "text", "email": true }
          ]
        }
        """;

        var form = FormEditor.FromJson(json);

        Assert.Equal("^[A-Z].*$", form.Fields[0].Pattern);
        Assert.True(form.Fields[1].Email);
    }

    [Fact]
    public void ToJson_RoundTripsPatternEmailDescriptionRows()
    {
        var form = FormEditor.CreateDefault();
        form.Fields.Add(new FieldEditor
        {
            Key = "comments", Type = "textarea", Rows = 5, Description = "Extra notes"
        });
        form.Fields.Add(new FieldEditor
        {
            Key = "mail", Type = "text", Pattern = ".*@.*", Email = true
        });

        var r = FormEditor.FromJson(form.ToJson());

        Assert.Equal(5,           r.Fields[0].Rows);
        Assert.Equal("Extra notes", r.Fields[0].Description);
        Assert.Equal(".*@.*",     r.Fields[1].Pattern);
        Assert.True(r.Fields[1].Email);
    }

    [Fact]
    public void ToJson_OmitsRows_ForNonTextareaTypes()
    {
        var form = FormEditor.CreateDefault();
        form.Fields.Add(new FieldEditor { Key = "x", Type = "text", Rows = 5 });

        var json = form.ToJson();

        Assert.DoesNotContain("\"rows\"", json);
    }

    [Fact]
    public void ToJson_OmitsEmail_ForNonTextTypes()
    {
        var form = FormEditor.CreateDefault();
        form.Fields.Add(new FieldEditor { Key = "x", Type = "number", Email = true });

        var json = form.ToJson();

        Assert.DoesNotContain("\"email\"", json);
    }

    [Fact]
    public void ToJson_OmitsPattern_ForNonTextLikeTypes()
    {
        var form = FormEditor.CreateDefault();
        form.Fields.Add(new FieldEditor { Key = "x", Type = "select", Pattern = ".*" });

        var json = form.ToJson();

        Assert.DoesNotContain("\"pattern\"", json);
    }

    [Fact]
    public void FieldEditor_Tags_RoundTrip()
    {
        var form = FormEditor.CreateDefault();
        var f = new FieldEditor { Key = "name", Type = "text" };
        f.Tags.Add("privileged");
        f.Tags.Add("admin-only");
        form.Fields.Add(f);

        var r = FormEditor.FromJson(form.ToJson());

        Assert.Equal(new[] { "privileged", "admin-only" }, r.Fields[0].Tags.ToArray());
    }

    [Fact]
    public void OptionEditor_Tags_RoundTrip()
    {
        var form = FormEditor.CreateDefault();
        var field = new FieldEditor { Key = "role", Type = "select" };
        var opt = new OptionEditor { Value = "admin", Label = "Admin" };
        opt.Tags.Add("privileged");
        field.Options.Add(opt);
        form.Fields.Add(field);

        var r = FormEditor.FromJson(form.ToJson());
        var loadedOpt = r.Fields.Single().Options.Single();

        Assert.Equal("admin",  loadedOpt.Value);
        Assert.Equal("Admin",  loadedOpt.Label);
        Assert.Equal(new[] { "privileged" }, loadedOpt.Tags.ToArray());
    }

    [Fact]
    public void Actions_DefaultWhenMissing_OtherwisePreserved()
    {
        var original = FormEditor.FromJson("""
          { "title": "T", "fields": [], "actions": [
              { "id": "go", "label": "Go!", "submit": true }
          ] }
          """);
        Assert.Single(original.Actions);
        Assert.Equal("go", original.Actions[0].Id);
        Assert.True(original.Actions[0].Submit);

        var rebuilt = FormEditor.FromJson(original.ToJson());
        Assert.Single(rebuilt.Actions);
        Assert.Equal("Go!", rebuilt.Actions[0].Label);
    }
}
