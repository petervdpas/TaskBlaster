// network-demo.csx
// Pull JSON from a public API using NetworkBlaster. Two flavours:
//   1) Anonymous client — works out of the box, no vault required.
//   2) Vault-backed client — uses Secrets.Resolver. Add a vault entry
//      under category "github" with key "baseUrl" = "https://api.github.com/"
//      (and optionally key "token" for a higher rate limit) before running.

using NetworkBlaster;

// 1) Anonymous — httpbin always returns whatever you send it.
var test = NetClient.Anonymous("https://httpbin.org/");
var echo = await test.GetJsonAsync<HttpBinIp>("ip");
Console.WriteLine($"Your IP, per httpbin: {echo?.Origin}");

// 2) Vault-backed — uncomment after adding the github connection to the vault.
// var gh = new NetClient(Secrets.Resolver, "github");
// var repo = await gh.GetJsonAsync<GhRepo>("repos/octocat/hello-world");
// Console.WriteLine($"{repo?.FullName} — {repo?.Description}");

record HttpBinIp(string Origin);
record GhRepo(string FullName, string? Description);
