// vault-report.csx
// Print an inventory of the vault: categories and key counts. Values
// stay in the vault. Uses Secrets.Categories() and Secrets.Keys(cat)
// so if the vault is locked you'll get the standard unlock prompt.

var categories = Secrets.Categories();
if (categories.Count == 0)
{
    Console.WriteLine("Vault is empty. Add a secret in the Secrets tab first.");
    return;
}

Console.WriteLine($"Vault has {categories.Count} categor{(categories.Count == 1 ? "y" : "ies")}.");
Console.WriteLine();

foreach (var cat in categories)
{
    var keys = Secrets.Keys(cat);
    Console.WriteLine($"[{cat}] {keys.Count} key(s)");
    foreach (var k in keys) Console.WriteLine($"  - {k}");
}
