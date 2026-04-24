using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskBlaster.Forms;

/// <summary>
/// Mutable in-memory form model used by the designer. Round-trips to/from the
/// GuiBlast JSON schema (camelCase property names, documented in GuiBlast README).
/// </summary>
public sealed class FormEditor
{
    public string Title { get; set; } = "Untitled";
    public double? Width { get; set; }
    public double? Height { get; set; }
    public ObservableCollection<FieldEditor> Fields { get; } = new();
    public ObservableCollection<VisibilityRuleEditor> Visibility { get; } = new();
    public ObservableCollection<ActionEditor> Actions { get; } = new();

    public static FormEditor CreateDefault()
    {
        var f = new FormEditor { Title = "New Form" };
        f.Actions.Add(new ActionEditor { Id = "save",   Label = "Save",   Submit = true });
        f.Actions.Add(new ActionEditor { Id = "cancel", Label = "Cancel", Dismiss = true });
        return f;
    }

    public static FormEditor FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<FormDto>(json, ReadOptions);
        if (dto is null) return CreateDefault();

        var editor = new FormEditor
        {
            Title = dto.Title ?? "Untitled",
            Width = dto.Size?.Width,
            Height = dto.Size?.Height,
        };
        if (dto.Fields is not null)
            foreach (var f in dto.Fields) editor.Fields.Add(FieldEditor.FromDto(f));
        if (dto.Visibility is not null)
            foreach (var v in dto.Visibility) editor.Visibility.Add(VisibilityRuleEditor.FromDto(v));
        if (dto.Actions is not null)
            foreach (var a in dto.Actions) editor.Actions.Add(ActionEditor.FromDto(a));

        if (editor.Actions.Count == 0)
        {
            editor.Actions.Add(new ActionEditor { Id = "save",   Label = "Save",   Submit = true });
            editor.Actions.Add(new ActionEditor { Id = "cancel", Label = "Cancel", Dismiss = true });
        }
        return editor;
    }

    public string ToJson()
    {
        var dto = new FormDto
        {
            Title = Title,
            Size = (Width is not null || Height is not null)
                ? new SizeDto { Width = Width, Height = Height }
                : null,
            Fields = new List<FieldDto>(),
            Visibility = Visibility.Count > 0 ? new List<VisibilityDto>() : null,
            Actions = new List<ActionDto>(),
        };
        foreach (var f in Fields)     dto.Fields.Add(f.ToDto());
        if (dto.Visibility is not null)
            foreach (var v in Visibility) dto.Visibility.Add(v.ToDto());
        foreach (var a in Actions)    dto.Actions.Add(a.ToDto());
        return JsonSerializer.Serialize(dto, WriteOptions);
    }

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // --- DTOs that define the on-disk JSON shape ---

    internal sealed class FormDto
    {
        public string? Title { get; set; }
        public SizeDto? Size { get; set; }
        public List<FieldDto>? Fields { get; set; }
        public List<VisibilityDto>? Visibility { get; set; }
        public List<ActionDto>? Actions { get; set; }
    }

    internal sealed class SizeDto
    {
        public double? Width { get; set; }
        public double? Height { get; set; }
    }

    internal sealed class FieldDto
    {
        public string? Key { get; set; }
        public string? Type { get; set; }
        public string? Label { get; set; }
        public string? Placeholder { get; set; }
        public bool? Required { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public double? Step { get; set; }
        public int? Rows { get; set; }
        public string? Pattern { get; set; }
        public bool? Email { get; set; }
        public List<OptionDto>? Options { get; set; }
        public OptionsFromDto? OptionsFrom { get; set; }
        public string? Description { get; set; }
        public string[]? Tags { get; set; }
    }

    internal sealed class OptionDto
    {
        public string? Value { get; set; }
        public string? Label { get; set; }
        public string[]? Tags { get; set; }
    }

    /// <summary>
    /// Points a select-style field's options at a dynamic source. When
    /// present, <see cref="FormJsonExpander"/> replaces it with a
    /// materialised <see cref="FieldDto.Options"/> array before GuiBlast
    /// ever sees the JSON. Today only <c>source = "vault"</c> is
    /// understood; the string-typed field leaves room for future
    /// sources (file, env, command, …) without a breaking change.
    /// </summary>
    internal sealed class OptionsFromDto
    {
        public string? Source { get; set; }
        public string? Category { get; set; }
    }

    internal sealed class VisibilityDto
    {
        public string? Field { get; set; }
        public string? Eq { get; set; }
        public string? Neq { get; set; }
        public string[]? Show { get; set; }
        public string[]? Hide { get; set; }
        [JsonPropertyName("show_tags")] public string[]? ShowTags { get; set; }
        [JsonPropertyName("hide_tags")] public string[]? HideTags { get; set; }
    }

    internal sealed class ActionDto
    {
        public string? Id { get; set; }
        public string? Label { get; set; }
        public bool? Submit { get; set; }
        public bool? Dismiss { get; set; }
    }
}

public sealed class FieldEditor
{
    public string Key { get; set; } = "field";
    public string Type { get; set; } = "text";
    public string? Label { get; set; }
    public string? Placeholder { get; set; }
    public bool Required { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Step { get; set; }
    public int? Rows { get; set; }
    public string? Pattern { get; set; }
    public bool Email { get; set; }
    public string? Description { get; set; }
    public ObservableCollection<OptionEditor> Options { get; } = new();

    /// <summary>
    /// When non-null, the static <see cref="Options"/> list is ignored at
    /// render time and the expander materialises the options from this
    /// source (e.g. vault category keys). Only meaningful on
    /// option-bearing field types (see <see cref="SupportsOptions"/>).
    /// </summary>
    public OptionsSourceEditor? OptionsSource { get; set; }

    public ObservableCollection<string> Tags { get; } = new();

    internal static FieldEditor FromDto(FormEditor.FieldDto dto)
    {
        var f = new FieldEditor
        {
            Key = dto.Key ?? "field",
            Type = dto.Type ?? "text",
            Label = dto.Label,
            Placeholder = dto.Placeholder,
            Required = dto.Required ?? false,
            Min = dto.Min,
            Max = dto.Max,
            Step = dto.Step,
            Rows = dto.Rows,
            Pattern = dto.Pattern,
            Email = dto.Email ?? false,
            Description = dto.Description,
        };
        if (dto.Options is not null)
            foreach (var o in dto.Options)
            {
                var opt = new OptionEditor { Value = o.Value ?? "", Label = o.Label };
                if (o.Tags is not null) foreach (var t in o.Tags) opt.Tags.Add(t);
                f.Options.Add(opt);
            }
        if (dto.OptionsFrom is not null && !string.IsNullOrWhiteSpace(dto.OptionsFrom.Source))
            f.OptionsSource = new OptionsSourceEditor
            {
                Source   = dto.OptionsFrom.Source!,
                Category = dto.OptionsFrom.Category ?? "",
            };
        if (dto.Tags is not null)
            foreach (var t in dto.Tags) f.Tags.Add(t);
        return f;
    }

    internal FormEditor.FieldDto ToDto()
    {
        var dto = new FormEditor.FieldDto
        {
            Key = Key,
            Type = Type,
            Label = Label,
            Placeholder = SupportsPlaceholder(Type) ? Placeholder : null,
            Required = Required ? true : null,
            Min = SupportsNumeric(Type) ? Min : null,
            Max = SupportsNumeric(Type) ? Max : null,
            Step = SupportsNumeric(Type) ? Step : null,
            Rows = Type == "textarea" ? Rows : null,
            Pattern = SupportsPattern(Type) ? Pattern : null,
            Email = Email && Type == "text" ? true : null,
            Description = string.IsNullOrEmpty(Description) ? null : Description,
            Tags = Tags.Count > 0 ? Tags.ToArray() : null,
        };
        if (SupportsOptions(Type))
        {
            if (OptionsSource is not null && !string.IsNullOrWhiteSpace(OptionsSource.Source))
            {
                dto.OptionsFrom = new FormEditor.OptionsFromDto
                {
                    Source   = OptionsSource.Source,
                    Category = string.IsNullOrWhiteSpace(OptionsSource.Category) ? null : OptionsSource.Category,
                };
            }
            // Options always persist when present — in vault mode they're the
            // user-picked subset of category keys; in static mode they're
            // free-form. Missing options + vault mode means "all keys in the
            // category"; the expander fills that in at render time.
            if (Options.Count > 0)
            {
                dto.Options = new List<FormEditor.OptionDto>();
                foreach (var o in Options)
                    dto.Options.Add(new FormEditor.OptionDto
                    {
                        Value = o.Value,
                        Label = o.Label,
                        Tags = o.Tags.Count > 0 ? o.Tags.ToArray() : null,
                    });
            }
        }
        return dto;
    }

    public static bool SupportsPlaceholder(string type) => type is "text" or "textarea" or "password" or "email" or "number";
    public static bool SupportsNumeric(string type) => type is "number";
    public static bool SupportsOptions(string type) => type is "select" or "multiselect" or "radio";
    public static bool SupportsPattern(string type) => type is "text" or "textarea" or "password" or "email";
}

public sealed class OptionEditor
{
    public string Value { get; set; } = "";
    public string? Label { get; set; }
    public ObservableCollection<string> Tags { get; } = new();
}

/// <summary>
/// Dynamic-options binding for select-style fields. Companion to
/// <see cref="FormEditor.OptionsFromDto"/>. Today only <c>Source =
/// "vault"</c> is interpreted; a Category then names the vault
/// category whose keys become the option list at render time.
/// </summary>
public sealed class OptionsSourceEditor
{
    public string Source { get; set; } = "vault";
    public string Category { get; set; } = "";
}

public sealed class VisibilityRuleEditor
{
    public string Field { get; set; } = "";
    public string? Eq { get; set; }
    public string? Neq { get; set; }
    public ObservableCollection<string> Show { get; } = new();
    public ObservableCollection<string> Hide { get; } = new();
    public ObservableCollection<string> ShowTags { get; } = new();
    public ObservableCollection<string> HideTags { get; } = new();

    internal static VisibilityRuleEditor FromDto(FormEditor.VisibilityDto dto)
    {
        var r = new VisibilityRuleEditor
        {
            Field = dto.Field ?? "",
            Eq = dto.Eq,
            Neq = dto.Neq,
        };
        if (dto.Show     is not null) foreach (var s in dto.Show)     r.Show.Add(s);
        if (dto.Hide     is not null) foreach (var s in dto.Hide)     r.Hide.Add(s);
        if (dto.ShowTags is not null) foreach (var s in dto.ShowTags) r.ShowTags.Add(s);
        if (dto.HideTags is not null) foreach (var s in dto.HideTags) r.HideTags.Add(s);
        return r;
    }

    internal FormEditor.VisibilityDto ToDto() => new()
    {
        Field = string.IsNullOrEmpty(Field) ? null : Field,
        Eq = Eq,
        Neq = Neq,
        Show     = Show.Count     > 0 ? Show.ToArray()     : null,
        Hide     = Hide.Count     > 0 ? Hide.ToArray()     : null,
        ShowTags = ShowTags.Count > 0 ? ShowTags.ToArray() : null,
        HideTags = HideTags.Count > 0 ? HideTags.ToArray() : null,
    };
}

public sealed class ActionEditor
{
    public string Id { get; set; } = "action";
    public string? Label { get; set; }
    public bool Submit { get; set; }
    public bool Dismiss { get; set; }

    internal static ActionEditor FromDto(FormEditor.ActionDto dto) => new()
    {
        Id = dto.Id ?? "action",
        Label = dto.Label,
        Submit = dto.Submit ?? false,
        Dismiss = dto.Dismiss ?? false,
    };

    internal FormEditor.ActionDto ToDto() => new()
    {
        Id = Id,
        Label = Label,
        Submit = Submit ? true : null,
        Dismiss = Dismiss ? true : null,
    };
}
