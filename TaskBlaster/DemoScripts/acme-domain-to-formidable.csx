// acme-domain-to-formidable.csx
// Reflects the loaded Acme.Domain assembly via AssemblyBlast.AssemblyReader
// and POSTs every class as an FCDM entity and every enum as an FCDM enum to a
// locally running Formidable instance. Idempotent: each item's GUID is derived
// from its FQN, so reruns update in place.
//
// Prereqs:
//   1. Add Acme.Domain to TaskBlaster: Settings -> External -> "Add .nupkg..."
//      pick ~/.taskblaster/demo-nugets/Acme.Domain.1.0.0.nupkg, restart.
//   2. Run Formidable so its REST API is live (default :8383).
//   3. Add a "Formidable" connection: Connections tab -> +Add ->
//      field `baseUrl` (Plaintext) = http://localhost:8383/api/.
//   4. Make sure both fcdm-entities.yaml and fcdm-enums.yaml templates are
//      present in Formidable.

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AssemblyBlast;
using AssemblyBlast.Models;
using NetworkBlast;
using UtilBlast.Tabular;

// Change this to point the script at any loaded External assembly.
const string AssemblyName = "Acme.Domain";

// 1. Locate the assembly by name in the current AppDomain.
//    The External tab loader already injected it; no static type reference needed.
var asm = AppDomain.CurrentDomain.GetAssemblies()
    .FirstOrDefault(a => string.Equals(
        a.GetName().Name, AssemblyName, StringComparison.OrdinalIgnoreCase));

if (asm is null)
{
    Blast.WriteStatus(
        $"Assembly '{AssemblyName}' isn't loaded. Add the .nupkg via Settings → External, then restart.",
        BlastLevel.Error);
    return;
}

var classes = AssemblyReader.ReadClasses(asm);
var enums   = AssemblyReader.ReadEnums(asm);

Blast.WriteHeading($"Reflected {asm.GetName().Name} {asm.GetName().Version}");
Blast.WriteKv("Classes", classes.Count.ToString());
Blast.WriteKv("Enums",   enums.Count.ToString());
Console.WriteLine();

// 2. Map AssemblyBlast definitions -> Formidable batch items.
var entityItems = classes.Select(c => new
{
    data = new Dictionary<string, object?>
    {
        ["id"]          = DeterministicGuid($"{c.Namespace}.{c.Name}").ToString(),
        ["name"]        = c.Name,
        ["namespace"]   = c.Namespace,
        ["kind"]        = c.Kind,
        ["ontology"]    = "entity",
        ["base-entity"] = c.BaseType,
        ["interfaces"]  = c.Implements,
        // Capture the primary (first) public ctor's parameters. Multi-ctor
        // classes only surface their first ctor here — fine for FCDM domain
        // modelling where the primary signature is the canonical one.
        ["constructor-parameters"] = c.Constructors.FirstOrDefault()?.Parameters
            .Select(cp => new[] { cp.Type, cp.Name }).ToArray()
            ?? Array.Empty<string[]>(),
        ["attributes"]  = c.Properties.Select(p => new[]
        {
            p.Name,
            p.Type,
            BoolStr(p.IsKey),
            BoolStr(p.IsNullable),
            BoolStr(p.IsCollection),
            BoolStr(p.IsRequired),
            BoolStr(p.IsDerived),
            p.Summary,
            p.AccessorType, // get / init / set
        }).ToArray(),
    },
}).ToArray();

var enumItems = enums.Select(e => new
{
    data = new Dictionary<string, object?>
    {
        ["id"]              = DeterministicGuid($"{e.Namespace}.{e.Name}").ToString(),
        ["name"]            = e.Name,
        ["namespace"]       = e.Namespace,
        ["underlying-type"] = e.UnderlyingType,
        ["is-flags"]        = e.IsFlags,
        ["definition"]      = e.Summary,
        ["members"]         = e.Members.Select(m => new[]
        {
            m.Name,
            m.Value.ToString(),
            m.Summary,
        }).ToArray(),
    },
}).ToArray();

// 3. POST batches to Formidable with replace-mode for idempotent reruns.
//    `Formidable` is the connection name; NetworkBlast pulls baseUrl from it
//    via Secrets.Resolver (and would resolve any further fields, e.g. a token,
//    from the same bag if the connection grew them later).
var fmd = new NetClient(Secrets.Resolver, "Formidable");

await PostBatch(fmd, "fcdm-entities", entityItems);
await PostBatch(fmd, "fcdm-enums",    enumItems);

Blast.WriteStatus("Done.", BlastLevel.Ok);

// ── Helpers ────────────────────────────────────────────────

async Task PostBatch(NetClient client, string template, object items)
{
    var path = $"collections/{template}/batch?mode=replace";
    BatchResponse? result;
    try
    {
        result = await client.PostJsonAsync<BatchResponse>(path, new { items });
    }
    catch (Exception ex)
    {
        Blast.WriteStatus($"{template}: {ex.Message}", BlastLevel.Error);
        return;
    }

    Blast.WriteHeading(template);
    if (result is null)
    {
        Blast.WriteStatus("no response body", BlastLevel.Error);
        return;
    }

    Blast.WriteKv("created", (result.Created?.Length ?? 0).ToString());
    Blast.WriteKv("updated", (result.Updated?.Length ?? 0).ToString());
    Blast.WriteKv("errors",  (result.Errors?.Length  ?? 0).ToString());

    if (result.Errors is { Length: > 0 })
    {
        foreach (var err in result.Errors)
            Console.WriteLine($"  ✗ index={err.Index} id={err.Id} error={err.Error} {err.Message}");
    }
    Console.WriteLine();
}

static string BoolStr(bool b) => b ? "true" : "false";

static Guid DeterministicGuid(string s)
{
    var hash = MD5.HashData(Encoding.UTF8.GetBytes(s));
    return new Guid(hash);
}

record BatchResponse(BatchRow[]? Created, BatchRow[]? Updated, BatchError[]? Errors);
record BatchRow(string Id, string Filename);
record BatchError(int Index, string? Id, string Error, string? Message);
