// json-csv-demo.csx
// UtilBlast 1.1 helpers: round-trip between JSON and CSV, flatten
// nested JSON, query JSON by dot/bracket path.

using UtilBlast.Csv;
using UtilBlast.Extensions;
using Newtonsoft.Json.Linq;

// 1) JSON → CSV. Nested objects flatten with dot notation; arrays use [i].
var people = """
[
  { "id": 1, "name": "Alice",  "addr": { "city": "NYC", "zip": "10001" }, "tags": ["vip", "early"] },
  { "id": 2, "name": "Bob",    "addr": { "city": "LA",  "zip": "90001" }, "tags": ["new"] }
]
""";

var csv = people.JsonToCsv();
Console.WriteLine("--- CSV ---");
Console.WriteLine(csv);
Console.WriteLine();

// 2) CSV → JSON (round-trip). Values come back as strings (no type inference).
var roundTripped = csv.CsvToJson();
Console.WriteLine("--- JSON (round-trip) ---");
Console.WriteLine(roundTripped);
Console.WriteLine();

// 3) Flatten a deeply-nested JObject into a single-level lookup.
var flat = JObject.Parse("""{"order":{"id":42,"customer":{"name":"Alice","city":"NYC"}}}""").Flatten();
Console.WriteLine($"flat[\"order.customer.name\"] = {flat["order.customer.name"]}");

// 4) Pluck a value by path — supports dotted keys and array indices.
var token = JToken.Parse(people);
Console.WriteLine($"users[1].addr.city = {token.GetByPath("[1].addr.city")}");
Console.WriteLine($"users[0].tags[0]   = {token.GetByPath("[0].tags[0]")}");
