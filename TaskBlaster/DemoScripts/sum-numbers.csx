// sum-numbers.csx
// Prompt for a list of numbers and print their sum.

using GuiBlast;

var raw = Prompts.Input("Numbers", "Enter numbers separated by spaces:", "1 2 3 4 5");
if (raw is null) return;

var total = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries)
               .Select(double.Parse)
               .Sum();

Console.WriteLine($"Sum: {total}");
