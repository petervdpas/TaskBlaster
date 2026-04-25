// azure-sql-template.csx
// Use AzureBlast to query Azure SQL. Two flavours:
//   1) Inline connection string — quick test path.
//   2) Vault-backed — picks the connection string out of the vault by name.
//      Add a secret under category "azure-prod-sql" with key "connectionString"
//      before running the second example.

using AzureBlast;
using AzureBlast.Interfaces;

// 1) Inline — uncomment and paste your own connection string.
// var db = new MssqlDatabase();
// db.Setup("Server=tcp:contoso.database.windows.net,1433;Database=App;Authentication=Active Directory Default;");
// foreach (var row in db.ExecuteQuery("SELECT TOP 10 name FROM sys.tables ORDER BY name").Rows.Cast<System.Data.DataRow>())
//     Console.WriteLine(row["name"]);

// 2) Vault-backed (AzureBlast 2.1+) — uncomment after adding the connection.
// var db = new MssqlDatabase();
// await db.SetupAsync(Secrets.Resolver, "azure-prod-sql");
// foreach (var row in db.ExecuteQuery("SELECT TOP 10 name FROM sys.tables ORDER BY name").Rows.Cast<System.Data.DataRow>())
//     Console.WriteLine(row["name"]);

Console.WriteLine("Edit this script and uncomment one of the examples to run a real query.");
