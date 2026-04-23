using System.Collections.Generic;

namespace TaskBlaster;

internal static class DemoScripts
{
    public static readonly IReadOnlyList<(string Name, string Body)> All = new (string, string)[]
    {
        ("hello.csx", Hello),
        ("input-demo.csx", InputDemo),
        ("confirm-demo.csx", ConfirmDemo),
        ("sum-numbers.csx", SumNumbers),
        ("azure-sql-template.csx", AzureSqlTemplate),
    };

    private const string Hello = """
// hello.csx
// The simplest TaskBlaster script.

Console.WriteLine("Hello from TaskBlaster!");
""";

    private const string InputDemo = """
// input-demo.csx
// Prompt the user with a GuiBlast modal and greet them by name.

#r "nuget: GuiBlast, 2.0.0"
using GuiBlast;

var name = Prompts.Input("Greeting", "What's your name?", "world");
if (name is null) return;

Console.WriteLine($"Hello, {name}!");
""";

    private const string ConfirmDemo = """
// confirm-demo.csx
// Ask a yes/no question before doing work.

#r "nuget: GuiBlast, 2.0.0"
using GuiBlast;

var ok = Prompts.Confirm("Proceed?", "Are you sure you want to continue?");
Console.WriteLine(ok ? "Continuing..." : "Cancelled.");
""";

    private const string SumNumbers = """
// sum-numbers.csx
// Prompt for a list of numbers and print their sum.

#r "nuget: GuiBlast, 2.0.0"
using System.Linq;
using GuiBlast;

var raw = Prompts.Input("Numbers", "Enter numbers separated by spaces:", "1 2 3 4 5");
if (raw is null) return;

var total = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries)
               .Select(double.Parse)
               .Sum();

Console.WriteLine($"Sum: {total}");
""";

    private const string AzureSqlTemplate = """
// azure-sql-template.csx
// Template: use AzureBlast to query Azure SQL. Fill in the connection
// string and uncomment the body before running.

#r "nuget: AzureBlast, 2.0.2"
#r "nuget: GuiBlast, 2.0.0"
using AzureBlast;
using GuiBlast;

// var connStr = Prompts.Input("Azure SQL", "Connection string:");
// if (connStr is null) return;
//
// var db = new MssqlDatabase(connStr);
// var rows = db.Query("SELECT TOP 10 name FROM sys.tables ORDER BY name");
// foreach (var row in rows)
//     Console.WriteLine(row["name"]);

Console.WriteLine("Edit this script and uncomment the body to run a real query.");
""";
}
