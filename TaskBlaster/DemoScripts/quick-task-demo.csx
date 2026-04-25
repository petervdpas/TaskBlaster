// quick-task-demo.csx
// Pair a script with a form on disk: load DemoForms/quick-task.json out
// of the configured forms folder, show it, and act on the result.
//
// TaskBlaster doesn't auto-pair scripts and forms; this is the explicit
// pattern when you want a script-side form file rather than building
// the JSON inline (see inline-form.csx for the inline variant).

using System.Text.Json;
using GuiBlast.Forms.Rendering;

// Resolve the forms folder. Default is ~/.taskblaster/forms, but honour
// a custom path if the user changed FormsFolder in the Settings dialog.
var home   = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var root   = Path.Combine(home, ".taskblaster");
var folder = Path.Combine(root, "forms");

var configPath = Path.Combine(root, "config.json");
if (File.Exists(configPath))
{
    using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
    if (doc.RootElement.TryGetProperty("FormsFolder", out var ff) &&
        ff.ValueKind == JsonValueKind.String)
    {
        var s = ff.GetString();
        if (!string.IsNullOrWhiteSpace(s)) folder = s!;
    }
}

var formPath = Path.Combine(folder, "quick-task.json");
if (!File.Exists(formPath))
{
    Console.WriteLine($"Form not found: {formPath}");
    Console.WriteLine("Open the Forms tab once so TaskBlaster seeds the demo files.");
    return;
}

var json   = await File.ReadAllTextAsync(formPath);
var result = await DynamicForm.ShowJsonAsync(json);
if (!result.Submitted) { Console.WriteLine("Cancelled."); return; }

var title    = (string)result.Values["title"];
var priority = (string?)result.Values["priority"] ?? "normal";
var estimate = result.Values.TryGetValue("estimate", out var eVal) ? eVal : null;
var notes    = (string?)result.Values["notes"] ?? "";

Console.WriteLine($"Task: {title}");
Console.WriteLine($"  priority : {priority}");
if (estimate is not null) Console.WriteLine($"  estimate : {estimate} h");
if (notes.Length > 0)
{
    Console.WriteLine("  notes    :");
    foreach (var line in notes.Split('\n')) Console.WriteLine($"    {line}");
}
