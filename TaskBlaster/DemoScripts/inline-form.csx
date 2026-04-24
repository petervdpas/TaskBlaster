// inline-form.csx
// Build a GuiBlast form entirely in code, show it as a modal, and act
// on the result. No form file on disk; good pattern for one-off
// interactive scripts where a full form file would be overkill.

using GuiBlast.Forms.Rendering;

const string formJson = """
{
  "title": "Ship a release",
  "size": { "width": 440, "height": 320 },
  "fields": [
    { "key": "tag",     "type": "text",   "label": "Version tag",
      "placeholder": "v1.2.3", "required": true },
    { "key": "channel", "type": "select", "label": "Channel",
      "options": [
        { "value": "stable", "label": "Stable" },
        { "value": "beta",   "label": "Beta" },
        { "value": "canary", "label": "Canary" }
      ] },
    { "key": "notes",   "type": "textarea", "label": "Release notes",
      "rows": 4 },
    { "key": "dryrun",  "type": "switch",   "label": "Dry run" }
  ],
  "actions": [
    { "id": "ship",   "label": "Ship",   "submit":  true },
    { "id": "cancel", "label": "Cancel", "dismiss": true }
  ]
}
""";

var result = await DynamicForm.ShowJsonAsync(formJson);
if (!result.Submitted) { Console.WriteLine("Cancelled."); return; }

var tag     = (string)result.Values["tag"];
var channel = (string)result.Values["channel"];
var notes   = (string?)result.Values["notes"] ?? "";
var dryrun  = (bool)result.Values["dryrun"];

Console.WriteLine($"{(dryrun ? "[DRY RUN] " : "")}Shipping {tag} to {channel}");
if (notes.Length > 0)
{
    Console.WriteLine("Notes:");
    foreach (var line in notes.Split('\n')) Console.WriteLine($"  {line}");
}
