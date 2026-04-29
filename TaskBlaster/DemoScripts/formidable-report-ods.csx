// formidable-report-ods.csx
// Reports on the "ods-uitfasering" template in a locally running Formidable
// instance (default :8383). Defines a typed model, fetches the full
// collection in one call (include=all), then summarises t-shirt sizing,
// use status, FCDM coverage and the widest tables.

using System.Text.Json;
using System.Text.Json.Serialization;
using NetworkBlast;
using UtilBlast.Tabular;

// Formidable returns some booleans as "true"/"false" strings (option-list defaults),
// so register a tolerant bool converter on the client's serializer options.
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
jsonOptions.Converters.Add(new LooseBoolConverter());

var api = NetClient.Anonymous("http://localhost:8383/api/", jsonOptions: jsonOptions);

var page = await api.GetJsonAsync<ListResponse>(
    "collections/ods-uitfasering?include=all&limit=500");

if (page is null || page.Items is null || page.Items.Count == 0)
{
    Blast.WriteStatus("No items returned from Formidable.", BlastLevel.Error);
    return;
}

Blast.WriteHeading($"ODS Uitfasering: {page.Total} entities");
Console.WriteLine();

// Effort breakdown.
var sizeOrder = new[] { "small", "medium", "big" };
var bySize = page.Items
    .GroupBy(i => i.Data?.TshirtSize ?? "(leeg)")
    .OrderBy(g => Array.IndexOf(sizeOrder, g.Key))
    .Select(g => new { Size = g.Key, Count = g.Count() })
    .ToTabular();
Blast.WriteTable(bySize, "T-shirt size");

// Use-status breakdown.
var byStatus = page.Items
    .GroupBy(i => string.IsNullOrWhiteSpace(i.Data?.Status) ? "(leeg)" : i.Data!.Status!)
    .OrderByDescending(g => g.Count())
    .Select(g => new { Status = g.Key, Count = g.Count() })
    .ToTabular();
Blast.WriteTable(byStatus, "Gebruiksstatus");

// Coverage / completeness counters.
var total      = page.Items.Count;
var fcdmAvail  = page.Items.Count(i => i.Data?.FcdmAvailable == true);
var fcdmComp   = page.Items.Count(i => i.Data?.FcdmComprehensive == true);
var noPurpose  = page.Items.Count(i => string.IsNullOrWhiteSpace(i.Data?.FunctionalPurpose));
var ongebruikt = page.Items.Count(i => i.Meta?.Tags?.Contains("ongebruikt") == true);
var flagged   = page.Items.Count(i => i.Meta?.Flagged == true);

Blast.WriteHeading("Dekking");
Blast.WriteKv("FCDM beschikbaar",     $"{fcdmAvail}/{total}");
Blast.WriteKv("FCDM dekkend",         $"{fcdmComp}/{total}");
Blast.WriteKv("Zonder functioneel doel", noPurpose.ToString());
Blast.WriteKv("Tag 'ongebruikt'",     ongebruikt.ToString());
Blast.WriteKv("Gemarkeerd",           flagged.ToString());
Console.WriteLine();

// Widest tables by field count.
var widest = page.Items
    .Where(i => i.Data is not null)
    .OrderByDescending(i => i.Data!.BaseFields?.Count ?? 0)
    .Take(10)
    .Select(i => new
    {
        Schema = i.Data!.Schema ?? "",
        Table  = i.Data!.BaseTable ?? i.Title,
        Fields = i.Data!.BaseFields?.Count ?? 0,
        Size   = i.Data!.TshirtSize ?? "",
        Status = i.Data!.Status ?? "",
    })
    .ToTabular();
Blast.WriteTable(widest, "Breedste tabellen (op aantal velden)");

// Tables still missing a functional-purpose write-up.
var missingPurpose = page.Items
    .Where(i => i.Data is not null
                && string.IsNullOrWhiteSpace(i.Data.FunctionalPurpose)
                && i.Meta?.Tags?.Contains("ongebruikt") != true)
    .OrderBy(i => i.Data!.Schema)
    .ThenBy(i => i.Data!.BaseTable)
    .Take(15)
    .Select(i => new
    {
        Schema = i.Data!.Schema ?? "",
        Table  = i.Data!.BaseTable ?? i.Title,
        Size   = i.Data!.TshirtSize ?? "",
        Status = i.Data!.Status ?? "",
    })
    .ToTabular();
Blast.WriteTable(missingPurpose, "Zonder functioneel doel (top 15)");

Blast.WriteStatus(
    $"{total} entiteiten verwerkt; {fcdmAvail} met FCDM-equivalent, {noPurpose} zonder doelomschrijving.",
    BlastLevel.Ok);

// ── Model ──────────────────────────────────────────────────

record ListResponse(
    [property: JsonPropertyName("template")] string Template,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("items")] List<OdsItem> Items);

record OdsItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("filename")] string Filename,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("meta")] OdsMeta? Meta,
    [property: JsonPropertyName("data")] OdsData? Data);

record OdsMeta(
    [property: JsonPropertyName("tags")] List<string>? Tags,
    [property: JsonPropertyName("flagged")] bool? Flagged,
    [property: JsonPropertyName("updated")] string? Updated);

record OdsData(
    [property: JsonPropertyName("schema")] string? Schema,
    [property: JsonPropertyName("base-table")] string? BaseTable,
    [property: JsonPropertyName("table-tags")] List<string>? TableTags,
    [property: JsonPropertyName("functional-purpose")] string? FunctionalPurpose,
    [property: JsonPropertyName("tshirt-size")] string? TshirtSize,
    [property: JsonPropertyName("base-fields")] List<List<string>>? BaseFields,
    [property: JsonPropertyName("direct-use")] bool? DirectUse,
    [property: JsonPropertyName("indirect-use")] bool? IndirectUse,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("related-views")] List<string>? RelatedViews,
    [property: JsonPropertyName("fcdm-available")] bool? FcdmAvailable,
    [property: JsonPropertyName("fcdm-comprehensive")] bool? FcdmComprehensive);

class LooseBoolConverter : JsonConverter<bool?>
{
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True   => true,
            JsonTokenType.False  => false,
            JsonTokenType.Null   => null,
            JsonTokenType.Number => reader.GetInt64() != 0,
            JsonTokenType.String => reader.GetString() switch
            {
                null or ""    => null,
                "true"  or "1" or "yes" or "ja" => true,
                "false" or "0" or "no"  or "nee" => false,
                var s => bool.TryParse(s, out var b) ? b : null,
            },
            _ => null,
        };

    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteBooleanValue(value.Value);
    }
}
