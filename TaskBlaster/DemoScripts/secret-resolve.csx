// secret-resolve.csx
// Pick a secret out of the "Azure" category via a GuiBlast select, then
// print it. Pattern for wiring vault contents into an interactive form.

using GuiBlast;

var keys = Secrets.Keys("Azure");
if (keys.Count == 0)
{
    Console.WriteLine("No secrets under 'Azure' — add one in the Secrets tab first.");
    return;
}

var chosen = Prompts.Select("Azure secret", "Key:", keys);
if (chosen is null) return;

Console.WriteLine($"{chosen} = {Secrets.Resolve("Azure", chosen)}");
