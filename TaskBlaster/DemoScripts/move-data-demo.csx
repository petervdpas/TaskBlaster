// move-data-demo.csx
// End-to-end pattern: load a form file by logical name, materialise its
// vault-backed dropdowns, show the modal, then act on the result.
//
// Setup before running:
//   1. Open the Secrets tab and add a category called "sql-dbs".
//   2. Add one key per database. The value is a full SQL connection
//      string, e.g.:
//        Key:   sis-prod
//        Value: Server=tcp:contoso.database.windows.net,1433;
//               Database=Sis;Authentication=Active Directory Default;
//   3. Repeat for the other databases you want in the picker (e.g.
//      hr-prod, fin-prod). The form's two dropdowns auto-populate
//      from every key in this category.
//
// Without that category the form will preview with empty dropdowns —
// the script then exits with a hint instead of attempting a connection.

using AzureBlast;
using GuiBlast.Forms.Rendering;
using UtilBlast.Tabular;

// 1. Load + expand the form. Folders.ReadTextAsync resolves
//    "move-data" against the registered "forms" folder and appends
//    the default extension (.json). ExpandFormJsonAsync rewrites
//    optionsFrom hints into concrete options arrays using the live
//    vault — triggers the unlock prompt only if the form actually
//    needs it.
var raw      = await Folders.ReadTextAsync("forms", "move-data");
var expanded = await Secrets.ExpandFormJsonAsync(raw);

var result = await DynamicForm.ShowJsonAsync(expanded);
if (!result.Submitted) { Blast.WriteStatus("Cancelled.", BlastLevel.Warn); return; }

var sourceKey = (string)result.Values["sourceDb"];
var targetKey = (string)result.Values["targetDb"];
var table     = (string)result.Values["table"];
var limit     = Convert.ToInt32(result.Values["limit"]);
var dryRun    = (bool)result.Values["dryRun"];

if (string.IsNullOrEmpty(sourceKey) || string.IsNullOrEmpty(targetKey))
{
    Blast.WriteStatus(
        "No DBs picked. Add keys under the 'sql-dbs' vault category first " +
        "(see the comment block at the top of this script).",
        BlastLevel.Warn);
    return;
}

// 2. Resolve both connection strings out of the vault.
var sourceCs = await Secrets.ResolveAsync("sql-dbs", sourceKey);
var targetCs = await Secrets.ResolveAsync("sql-dbs", targetKey);

Blast.WriteHeading($"{sourceKey}.{table}  →  {targetKey}.{table}  (limit {limit}{(dryRun ? ", dry run" : "")})");

// 3. Read top N rows from the source.
var src = new MssqlDatabase();
src.Setup(sourceCs);
var rows = src.ExecuteQuery($"SELECT TOP {limit} * FROM {table}");
Blast.WriteKv("Read", $"{rows.Rows.Count} rows from {sourceKey}.{table}");

if (dryRun)
{
    Blast.WriteStatus("Dry run — skipping insert. Toggle 'Dry run' off to actually write.", BlastLevel.Info);
    return;
}

// 4. Insert into the target. Real implementations would batch + map
//    schemas; this demo just shows the round-trip.
var dst = new MssqlDatabase();
dst.Setup(targetCs);
// dst.BulkInsert(table, rows);   // ← wire your real insert here
Blast.WriteStatus($"Would write {rows.Rows.Count} rows to {targetKey}.{table}.", BlastLevel.Ok);
