// connections-demo.csx
// Walks the named-connection layer end-to-end. The connections file
// (~/.taskblaster/connections.json) maps a connection name to a bag of
// fields where each field is either plaintext or a vault pointer.
// Open the 🔗 Connections tab to define your own; this script just
// demonstrates the read-side ergonomics.
//
// Sample connections.json that lines up with this script:
// {
//   "github": {
//     "baseUrl": { "value":    "https://api.github.com" },
//     "token":   { "fromVault": { "category": "github-secrets", "key": "pat" } }
//   }
// }

using UtilBlast.Tabular;

// 1) Inventory — names registered in connections.json.
var names = Secrets.Connections();
if (names.Count == 0)
{
    Blast.WriteStatus("No connections defined yet. Open 🔗 Connections to add one.", BlastLevel.Warn);
    return;
}

Blast.WriteHeading("Connections");
foreach (var n in names) Console.WriteLine($"  • {n}");

// Pick the first connection for the rest of the demo.
var first = names[0];
Console.WriteLine();
Blast.WriteHeading($"Connection '{first}'");

// 2) Dynamic form — script-style member access.
//    `var conn` infers `dynamic` because GetConnection returns dynamic.
//    Field access goes through DynamicObject → Fields[name] under the hood.
//    Plaintext fields skip the vault entirely; vault-ref fields trigger the
//    unlock-on-demand handshake exactly once.
var conn = Secrets.GetConnection(first);
foreach (var (key, value) in ((TaskBlaster.Connections.ConnectionSnapshot)conn).Fields)
{
    var preview = value.Length <= 40 ? value : value[..40] + "…";
    Console.WriteLine($"  {key,-12} = {preview}");
}

// 3) Typed form — bind to a record by property name (case-insensitive).
//    Numbers in plaintext fields bind to int/double targets via
//    JsonNumberHandling.AllowReadingFromString. Missing fields stay default.
//    Adjust GithubConn to match the fields you defined for your connection.
if (string.Equals(first, "github", StringComparison.OrdinalIgnoreCase))
{
    var gh = Secrets.GetConnection<GithubConn>("github");
    Blast.WriteKv("baseUrl", gh.BaseUrl);
    if (!string.IsNullOrEmpty(gh.Token))
        Blast.WriteKv("token",   gh.Token[..Math.Min(8, gh.Token.Length)] + "…");
}

// 4) Hand the resolver to a Blast library — the connection name becomes
//    the resolver "category" and the library asks for its well-known field
//    keys. NetworkBlast wants baseUrl + token; AzureBlast variants want
//    server / database / user / password (or connectionString) etc.
//
// using NetworkBlast;
// var api = new NetClient(Secrets.Resolver, "github");
// var me  = await api.GetJsonAsync<GitHubMe>("user");
// Console.WriteLine($"Hello {me?.Login}");

record GithubConn(string BaseUrl, string Token);
record GitHubMe(string Login, string? Name);
