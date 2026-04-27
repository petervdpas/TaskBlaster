// acme-domain-demo.csx
// Exercises the Acme.Domain canonical-models package, which ships as a
// fixture under ~/.taskblaster/demo-nugets/ on first launch. To make the
// types below resolve:
//   Settings → External → "Add .nupkg…"
//   pick ~/.taskblaster/demo-nugets/Acme.Domain.1.0.0.nupkg
//   restart TaskBlaster
// After that the script compiles and runs against the loaded assembly.

using Acme.Domain;
using UtilBlast.Tabular;

Blast.WriteHeading("Customers");
foreach (var c in SampleData.Customers)
    Blast.WriteKv($"{c.Id}  {c.Name}", $"{c.Tier}  ({c.Email})");

Console.WriteLine();
Blast.WriteHeading("People");
foreach (var p in SampleData.People)
{
    Console.WriteLine($"  {p.Id}  {p.FullName,-15}  born {p.DateOfBirth}");
    foreach (var ch in p.Channels)
        Console.WriteLine($"      {ch.Kind,-6} {(ch.IsPrimary ? "*" : " ")} {ch.Value}");
    foreach (var addr in p.Addresses)
        Console.WriteLine($"      {addr.Kind,-8} {addr.Line1}, {addr.City} {addr.PostalCode} ({addr.Country})");
}

Console.WriteLine();
Blast.WriteHeading("Orders");
foreach (var o in SampleData.Orders)
{
    Console.WriteLine($"  {o.Id}  customer={o.CustomerId}  status={o.Status}  total={o.Total:C}");
    foreach (var line in o.Lines)
        Console.WriteLine($"      {line.Sku}  x{line.Quantity}  @ {line.UnitPrice:C}  = {line.LineTotal:C}");
}

Console.WriteLine();
Blast.WriteStatus(
    $"Loaded {SampleData.Customers.Count} customers, {SampleData.People.Count} people, {SampleData.Orders.Count} order(s) from Acme.Domain.",
    BlastLevel.Ok);
