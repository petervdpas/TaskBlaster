// input-demo.csx
// Prompt the user with a GuiBlast modal and greet them by name.

using GuiBlast;

var name = Prompts.Input("Greeting", "What's your name?", "world");
if (name is null) return;

Console.WriteLine($"Hello, {name}!");
