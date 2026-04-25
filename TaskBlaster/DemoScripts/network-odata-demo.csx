// network-odata-demo.csx
// Typed OData v4 query against the public Northwind sample service.
// Demonstrates the LINQ-flavoured chain plus auto-paging IAsyncEnumerable.

using NetworkBlaster;
using NetworkBlaster.OData;

var northwind = NetClient.Anonymous("https://services.odata.org/V4/Northwind/Northwind.svc/");

// One page, raw — pulls $count and @odata.nextLink alongside Value.
var page = await northwind.OData<Customer>("Customers")
    .Where(c => c.Country == "Germany")
    .OrderBy(c => c.CompanyName)
    .Select(c => c.CustomerID, c => c.CompanyName, c => c.City)
    .Top(5)
    .WithCount()
    .FirstPageAsync();

Console.WriteLine($"Total German customers: {page.Count}");
foreach (var c in page.Value)
    Console.WriteLine($"  {c.CustomerID,-6} {c.CompanyName} ({c.City})");

// Auto-paging stream — follows @odata.nextLink until exhausted.
var seen = 0;
await foreach (var c in northwind.OData<Customer>("Customers").OrderBy(c => c.CustomerID))
{
    seen++;
    if (seen <= 3) Console.WriteLine($"  page-stream → {c.CustomerID} {c.CompanyName}");
}
Console.WriteLine($"Streamed {seen} customers across all pages.");

record Customer(string CustomerID, string? CompanyName, string? City, string? Country);
