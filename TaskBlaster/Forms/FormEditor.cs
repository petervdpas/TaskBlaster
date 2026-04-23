using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public ObservableCollection<FieldEditor> Fields { get; } = new();
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

        var editor = new FormEditor { Title = dto.Title ?? "Untitled" };
        if (dto.Fields is not null)
            foreach (var f in dto.Fields) editor.Fields.Add(FieldEditor.FromDto(f));
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
            Fields = new List<FieldDto>(),
            Actions = new List<ActionDto>(),
        };
        foreach (var f in Fields)  dto.Fields.Add(f.ToDto());
        foreach (var a in Actions) dto.Actions.Add(a.ToDto());
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
        public List<FieldDto>? Fields { get; set; }
        public List<ActionDto>? Actions { get; set; }
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
        public List<OptionDto>? Options { get; set; }
        public string? Description { get; set; }
    }

    internal sealed class OptionDto
    {
        public string? Value { get; set; }
        public string? Label { get; set; }
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
    public ObservableCollection<OptionEditor> Options { get; } = new();

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
        };
        if (dto.Options is not null)
            foreach (var o in dto.Options)
                f.Options.Add(new OptionEditor { Value = o.Value ?? "", Label = o.Label });
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
        };
        if (SupportsOptions(Type) && Options.Count > 0)
        {
            dto.Options = new List<FormEditor.OptionDto>();
            foreach (var o in Options)
                dto.Options.Add(new FormEditor.OptionDto { Value = o.Value, Label = o.Label });
        }
        return dto;
    }

    public static bool SupportsPlaceholder(string type) => type is "text" or "textarea" or "password" or "email" or "number";
    public static bool SupportsNumeric(string type) => type is "number";
    public static bool SupportsOptions(string type) => type is "select" or "multiselect" or "radio";
}

public sealed class OptionEditor
{
    public string Value { get; set; } = "";
    public string? Label { get; set; }
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
