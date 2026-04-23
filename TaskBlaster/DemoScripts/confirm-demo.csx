// confirm-demo.csx
// Ask a yes/no question before doing work.

using GuiBlast;

var ok = Prompts.Confirm("Proceed?", "Are you sure you want to continue?");
Console.WriteLine(ok ? "Continuing..." : "Cancelled.");
