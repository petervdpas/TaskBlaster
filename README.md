# TaskBlaster

<p align="center">
  <img src="TaskBlaster/Images/spacegun.png" alt="TaskBlaster" width="320">
</p>

**TaskBlaster** is a cross-platform desktop app for writing, organizing, and running C# maintenance scripts (`.csx`) with real user interfaces — not command-line prompts.

It exists to solve a recurring, awkward problem: you need a one-off (or rarely-run) maintenance task — rotate a Key Vault secret, re-queue dead-lettered Service Bus messages, patch a few rows in a database, generate a report — and the options are all bad:

* A **console app** means parsing args, hand-rolling prompts, and re-compiling every time a parameter changes.
* A **full GUI app** is overkill for a 40-line chore.
* A **shared script dumping ground** has no input validation, no UI, and no safety net.

TaskBlaster takes a middle path: you write a plain `.csx` file, optionally pair it with a JSON form describing its inputs, and TaskBlaster handles the rest — rendering the form, validating input, running the script under Roslyn, streaming output into the built-in terminal, and resolving any secrets the script asks for out of an encrypted local vault.

---

## How it works

1. **Write a script.** Drop a `.csx` file into your scripts folder. It can `#r` NuGets and `using` any namespace, just like `dotnet-script`.
2. **Describe its inputs (optional).** Pair the script with a GuiBlast JSON form — text fields, dropdowns, checkboxes, conditional visibility, button actions. Build it by hand or use the built-in **visual form designer**.
3. **Store credentials in the local vault.** Add API tokens, connection strings, or anything else into the **Secrets** tab. Everything is encrypted at rest with Argon2id + AES-GCM via [SecretBlast](https://www.nuget.org/packages/SecretBlast).
4. **Run it.** TaskBlaster prompts the user with the form, injects the answers, executes the script via Roslyn, and streams stdout/stderr live to the terminal panel. Scripts pull secrets from the vault on demand via the `Secrets` global; the user is prompted to unlock if the vault is locked.
5. **Connect to Azure without secrets in code.** Scripts can call into [AzureBlast](https://www.nuget.org/packages/AzureBlast) to talk to Azure SQL, Service Bus, and Key Vault, handing it the vault resolver instead of a hard-coded connection string.
6. **Direct an agent against your code.** Add an Anthropic API key to the vault, point the **Settings → Agent** tab at it, and a per-script chat panel powered by [AgentBlast](https://www.nuget.org/packages/AgentBlast) becomes available. User-authored **knowledge blocks** (markdown files under `~/.taskblaster/knowledge/`) steer the model with project-specific conventions; every assembled prompt is audited to disk. The agent only emits text, never auto-applies — you copy what you want into the editor yourself.

This is the successor to the legacy `ScriptRunner.Plugins` package, rebuilt on .NET 10 + Avalonia 12 and the **Blast** library family.

## Who it's for

* **DBAs / data engineers** running ad-hoc SQL maintenance, bulk updates, or data fixes
* **Platform / DevOps engineers** managing Azure resources, rotating secrets, draining queues
* **Support engineers** running well-defined recovery scripts safely, with typed inputs
* **Anyone** who keeps a folder full of "scratch" C# scripts and wants them to feel like real tools

## Features

* Integrated `.csx` editor with two switchable highlighters (AvaloniaEdit's native xshd, default; TextMateSharp + VS Code Dark+/Light+ as the richer alternative), brace-based code folding, and Ctrl+wheel font zoom
* Roslyn-based script host with live stdout/stderr streaming into a terminal panel that can be toggled on or off from the toolbar
* Inline filter box on the scripts/forms sidebar, the secrets grid, and the connections list — case-insensitive, all whitespace tokens must match
* Status bar surfaces script errors in red so a failed run can't be missed at a glance
* **Visual form designer** for GuiBlast JSON forms — field list, per-type property editor, visibility rules, action buttons, live preview
* **Encrypted secrets vault** (Argon2id + AES-GCM), surfaced as a `Secrets` global inside scripts
* **Named connections** — a `connections.json` file maps a friendly name to a bag of fields where each field is either a plaintext literal (URL, server, account name) or a pointer into the vault (token, password). Scripts grab the whole bag with `Secrets.GetConnection("name")` (dynamic) or `Secrets.GetConnection<T>("name")` (typed); Blast libraries that take a resolver delegate (NetworkBlast, AzureBlast) receive the wrapped resolver via `Secrets.Resolver`.
* **Vault-backed select fields** in forms — declare a select's options as "vault keys in category X" and TaskBlaster materialises them at form-load time
* **External references** — drop a `.nupkg` (or a loose `.dll`) into Settings → External and TaskBlaster validates it (TFM compatibility, unresolved deps, version conflicts against already-loaded assemblies), extracts to `~/.taskblaster/packages/`, and surfaces the types to scripts as standard `using` namespaces
* **Directed AI agent** — per-script chat panel (💬) and a knowledge-blocks editor under a dedicated 🧠 Assistant tab, powered by [AgentBlast](https://www.nuget.org/packages/AgentBlast). Bring-your-own-key against Anthropic; every assembled prompt is audited to disk; the agent never auto-applies — it can only emit text you copy-paste yourself. Both surfaces are hidden until a provider is configured
* Graceful abort when the user cancels a vault-unlock prompt mid-script (no stack dump; the run ends as `Cancelled`)
* Configurable scripts folder, forms folder, vault folder, theme, editor highlighter, code folding, and terminal visibility — all persisted in `~/.taskblaster/config.json`
* Demo scripts, forms, and a sample `.nupkg` (the `Acme.Domain` canonical-models fixture) shipped out of the box, plus a dev-only `--seed-demos` flag to refresh them in place

## Stack

* .NET 10
* Avalonia 12, Avalonia.Controls.DataGrid, AvaloniaEdit + TextMateSharp.Grammars
* Microsoft.Extensions.DependencyInjection (singletons + transients wired in `Program.cs`)
* Microsoft.CodeAnalysis.CSharp.Scripting (Roslyn) for `.csx` execution
* [UtilBlast](https://www.nuget.org/packages/UtilBlast) 2.0.0 — common utilities, JSON ⇆ CSV, JObject flatten / GetByPath, plus the `Blast` display DSL (heading / status / table / kv) for structured script output. v2.0 dropped the dynamic-class generator and builder; that surface now lives in **AssemblyBlast**.
* [AssemblyBlast](https://www.nuget.org/packages/AssemblyBlast) 1.2.0 — runtime C# type generation **and** reflection. Build a type programmatically via `DynamicClassBuilder` (Reflection.Emit), compile a whole assembly from a JSON schema via `DynamicClassGenerator` (Roslyn), reflect any loaded `Assembly` into `ClassDefinition[]` / `EnumDefinition[]` shapes via `AssemblyReader` (1.1+), or render those shapes back to `.cs` source via `AssemblyWriter` (1.2+). Closes the loop *Assembly → ClassDefinition → C# source*.
* [AzureBlast](https://www.nuget.org/packages/AzureBlast) 2.1.1 — SQL / Service Bus / Key Vault, with vault-aware resolver overloads
* [GuiBlast](https://www.nuget.org/packages/GuiBlast) 2.1.1 — form specs and modal prompts
* [NetworkBlast](https://www.nuget.org/packages/NetworkBlast) 1.0.1 — REST / OData / SOAP, vault-aware via the same resolver shape
* [SqliteBlast](https://www.nuget.org/packages/SqliteBlast) 1.0.1 — local SQLite for staging/caching/migrations, vault-aware path
* [SecretBlast](https://www.nuget.org/packages/SecretBlast) 1.0.3 — encrypted local vault
* [AgentBlast](https://www.nuget.org/packages/AgentBlast) 1.1.0 — programmable LLM client + directing layer (knowledge blocks, picker, prompt builder, audit log); vault-agnostic via the same resolver shape

## Quick start

```bash
git clone https://github.com/petervdpas/TaskBlaster.git
cd TaskBlaster
dotnet run --project TaskBlaster
```

On first launch TaskBlaster creates `~/.taskblaster/` and seeds it with the bundled demo scripts and forms.

## Writing scripts

Scripts are plain `.csx` files. TaskBlaster preimports the usual BCL namespaces (`System`, `System.IO`, `System.Linq`, `System.Text`, `System.Collections.Generic`, `System.Threading`, `System.Threading.Tasks`) and force-loads the Blast assemblies so you can `using GuiBlast;` / `using AzureBlast;` / `using NetworkBlast;` / `using SqliteBlast;` / `using UtilBlast.Extensions;` / `using AssemblyBlast;` without a `#r`.

### Top-level identifiers

A `ScriptGlobals` object is handed to Roslyn on every run, surfacing one top-level identifier today:

| Identifier | Type            | Purpose                                 |
| ---------- | --------------- | --------------------------------------- |
| `Secrets`  | `ScriptSecrets` | Vault accessor (categories, keys, resolve) |

```csharp
// Sync — fine, scripts run off the UI thread.
var token = Secrets.Resolve("api", "github-token");

// Async form, with cancellation.
var conn  = await Secrets.ResolveAsync("azure", "prod-sql");

// Inventory (values are never returned by these).
var cats = Secrets.Categories();
var keys = Secrets.Keys("Azure");

// Delegate shape for libraries that take a named-connection resolver:
//   var db = new SomeClient(Secrets.Resolver, "prod-sql");
Func<string, string, CancellationToken, Task<string>> r = Secrets.Resolver;
```

If the vault is locked the first vault call pops the unlock dialog. Cancelling that prompt aborts the script as `Cancelled` rather than throwing a stack trace into the terminal.

### Prompts

`GuiBlast.Prompts` provides quick modal prompts when you don't want a full form file:

```csharp
using GuiBlast;

var name = Prompts.Input("Hello", "Your name?");
if (Prompts.Confirm("Proceed?", "Continue?")) {
    Console.WriteLine($"Hi {name}");
}
```

For richer inputs build a JSON form in code with `DynamicForm.ShowJsonAsync` (see `DemoScripts/inline-form.csx`).

## Forms

A form is a JSON document describing fields, layout, visibility, and action buttons. Forms live in their own folder (default `~/.taskblaster/forms/`) and are previewed standalone from the **Forms** tab; the visual designer round-trips the same JSON.

To use a form *from* a script, either build the JSON inline (see `inline-form.csx`) or load a form file from disk (see `quick-task-demo.csx`) and pass it to `DynamicForm.ShowJsonAsync`. TaskBlaster does not auto-pair scripts and forms by filename.

### Vault-backed select options

Any `select` field can declare its options as vault keys in a category. The expander materialises them at form-load time; GuiBlast itself never sees the vault.

```jsonc
{
  "key": "secret",
  "type": "select",
  "label": "Connection secret",
  "optionsFrom": { "source": "vault", "category": "Azure" }
}
```

If `options[]` is empty the expander populates it from the vault. If you've already picked a subset in the designer those are kept verbatim and the `optionsFrom` hint is stripped on its way to GuiBlast. Forms with no hints round-trip through the expander unchanged.

See `DemoForms/deploy.json` for a worked example with a vault-backed select and conditional visibility.

## Vault

The **Secrets** tab manages an encrypted local vault. Each entry has a `category`, `key`, `value`, and optional description; on disk, every secret is stored under an opaque GUID with the (category, key, value, description, timestamps) packed into a JSON envelope that's encrypted as a single SecretBlast value. Filenames leak nothing about the structure.

* **KDF:** Argon2id, 256 MiB / 3 iterations / 4 lanes for production; tests override to a fast profile.
* **Cipher:** AES-GCM (12-byte nonces, 16-byte tags), with a per-secret AAD bound to the vault id and entry name so swapping `*.secret` files in from another vault fails authentication loudly.
* **Auto-lock:** 15 min idle by default.
* **Password change:** rewrites the whole vault under the new key with an atomic-rename rollback path.

Scripts read from the vault via the `Secrets` global; values never leave SecretBlast except as a returned string in the script's own memory.

## Connections

The **🔗 Connections** tab manages a `connections.json` file that maps a connection name to a bag of fields. Each field is either a plaintext literal or a pointer into the vault, so non-secret config (URLs, server names, timeouts) doesn't have to live behind the unlock prompt while real secrets stay encrypted.

```jsonc
// ~/.taskblaster/connections.json
{
  "github": {
    "baseUrl": { "value":    "https://api.github.com" },
    "token":   { "fromVault": { "category": "github-secrets", "key": "pat" } }
  },
  "prod-sql": {
    "server":   { "value":    "tcp:my-server.database.windows.net,1433" },
    "database": { "value":    "main" },
    "user":     { "value":    "tb-runtime" },
    "password": { "fromVault": { "category": "azure-sql", "key": "prod-pw" } }
  }
}
```

The library convention (per `NetworkBlast` and `AzureBlast 2.1+`): the connection name is the resolver "category", and field keys are the well-known names the library asks for. NetworkBlast wants `baseUrl` + `token`; AzureBlast SQL wants `server` / `database` / `user` / `password`. Resolver semantics:

* A declared connection is **authoritative for its name** — only fields it declares are honored; asking for an undeclared key returns an empty string without ever calling the vault.
* If the connection contains **any** `fromVault` field, the resolver primes the vault as soon as the connection is consulted, so the unlock prompt fires up-front rather than deferred until a specific vault-backed field happens to be read. A pure-plaintext connection (every field a `value`) never touches the vault.
* If a name has no entry in `connections.json` at all, lookups fall through to the raw vault resolver — all-vault scripts that predate the connections layer keep working unchanged.

Three ways scripts use a connection:

```csharp
// 1) Direct field lookup, sync.
var url = Secrets.Resolve("github", "baseUrl");

// 2) Whole-bag dynamic — `var conn` infers `dynamic`.
var conn = Secrets.GetConnection("github");
var url2  = conn.baseUrl;     // DynamicObject member access
var token = conn.token;       // case-insensitive fallback also matches "Token"

// 3) Whole-bag typed — bind to a record / class.
record GithubConn(string BaseUrl, string Token);
var c = Secrets.GetConnection<GithubConn>("github");

// 4) Hand the resolver to a Blast library — name = category, library asks for its field keys.
using NetworkBlast;
var api = new NetClient(Secrets.Resolver, "github");
```

If a connection isn't in the file, the resolver falls through to the vault directly so all-vault setups keep working unchanged. `Secrets.Connections()` returns the registered names so a script can build a quick picker.

## Agent assistant (Directed AI)

TaskBlaster has an in-app **agent** for writing and refining scripts, called **Directed AI**. The pattern: the user actively *directs* the agent via explicit, visible **knowledge blocks**, and can see exactly which blocks fired on any given response. The agent runs against your own API key — there's no TaskBlaster-hosted proxy and prompts never leave your machine except to the configured provider.

The transport, knowledge-block store, picker, prompt builder, and audit log all live in [AgentBlast](https://www.nuget.org/packages/AgentBlast); TaskBlaster is the host that wires them up. Today the agent is configured against **Anthropic** (Claude Opus 4.7, Sonnet 4.6, Haiku 4.5). Other providers (OpenAI, Ollama) are on the roadmap; the architecture is provider-agnostic.

### Provider setup

The agent talks to a provider via a regular entry in `connections.json`. Add a connection with these fields:

```jsonc
"ai-anthropic": {
  "kind":      { "value": "anthropic" },
  "baseUrl":   { "value": "https://api.anthropic.com" },
  "model":     { "value": "claude-sonnet-4-6" },
  "maxTokens": { "value": "8192" },
  "apikey":    { "fromVault": { "category": "ai", "key": "anthropic" } }
}
```

Then pick the connection name in **Settings → Agent**. The choice is persisted as `AiDefaultProvider` in `~/.taskblaster/config.json` (the JSON key kept the `Ai` prefix for back-compat with existing user configs; the UI label is "Agent"). Field semantics:

* **`kind`** — selects the provider (`anthropic` today).
* **`baseUrl`** — endpoint root. Anthropic's is `https://api.anthropic.com`; the provider tolerates `/v1/messages` either appended or omitted.
* **`model`** — model id, e.g. `claude-opus-4-7`, `claude-sonnet-4-6`, `claude-haiku-4-5-20251001`.
* **`maxTokens`** — optional; defaults to 8192 when omitted, validated strictly when present (positive integer).
* **`apikey`** — vault-backed; the API key never lives in plaintext on disk.

The Settings → Agent tab includes a **Test** button that does a 5-token round-trip ping so you can verify connectivity before opening the chat panel.

### 🧠 Assistant tab — knowledge blocks

The Assistant tab manages **knowledge blocks**: small markdown files under `~/.taskblaster/knowledge/` that get injected into the agent's system prompt. Each block is a `.md` with YAML frontmatter:

```markdown
---
title: Acme.Domain conventions
when: Acme.Domain
priority: 60
tags: domain, models
includes: sql-shared
---
# body — free-form markdown the model reads as context
```

Frontmatter keys:

* **`title`** — display name; defaults to a humanised filename if omitted.
* **`when`** — comma-separated rules; ANY rule matching fires the block. Forms recognised today:
  * `always` — always matches.
  * `tag:foo` — matches when the caller hands the picker a `foo` tag.
  * `Namespace.Type` — matches when the open script has that exact loaded type FQN in scope.
  * `Namespace` — matches when a loaded namespace equals the rule, or any loaded FQN starts with it.
  * Blocks with no `when:` are never picked as entry points; they only appear when another block pulls them in via `includes:`. That gives a clean shape for shared/base blocks.
* **`priority`** — integer; higher fires first when budgeted truncation matters.
* **`includes`** — comma-separated block ids to pull in transitively (cycle-safe).
* **`tags`** — comma-separated; lowercased and deduplicated on save.

The editor pane lets you add, save, and delete blocks; switching selections discards pending edits, same convention as Scripts and Forms. Seven demo blocks ship in `~/.taskblaster/knowledge/` on first launch — see the bundled-demos table below.

> **Note:** the 🧠 Assistant tab and the 💬 Chat toggle on the toolbar are hidden when no agent provider is configured. Pick one in **Settings → Agent** to surface them.

### Per-script chat (💬)

Each `.csx` gets its own chat panel, toggled by the **Chat** switch on the toolbar (visible in Scripts mode when an agent provider is configured). Each turn:

1. Runs the **`KnowledgeBlockPicker`** against the open script + loaded references (Blast facades, namespaces, vault category names, connection names from `LoadedReferenceCatalog`) and selects the matching blocks plus their transitive includes.
2. Composes a system prompt from those blocks plus the loaded-reference summary via `PromptBuilder`.
3. Sends the conversation history — user/assistant turns since you opened the script — to the configured provider.
4. Renders the response as Markdown in the chat panel.

Conversation state is per-script and in-memory; switching scripts swaps the visible history. There's no auto-suggest and no auto-apply: the model emits text only, you copy what you want into the editor yourself.

### What gets sent

* **Vault context**: category and key *names* (organisational structure), never values.
* **Connections context**: connection names and field names, never resolved vault references.
* **Externals context**: type signatures from loaded `.nupkg` / `.dll` packages.
* **Knowledge blocks**: whichever blocks matched the picker for this turn.
* **Conversation**: the user and assistant messages for the active script.

API keys leave the vault only as request headers to the configured provider.

### Audit trail

Every assembled prompt is written to `~/.taskblaster/ai-history/` as a Markdown file with frontmatter (kind, timestamp, picked blocks, loaded types and namespaces, tags) plus the system message and user message verbatim. Useful for "why did it answer like that?" retrospectives and for tuning blocks.

## Data layout

```
~/.taskblaster/
├── config.json        # scripts/forms/vault folder paths, editor prefs, theme, externals
├── connections.json   # named connections (plaintext + fromVault pointers)
├── scripts/           # your .csx scripts
├── forms/             # your .json form specs
├── knowledge/         # markdown directing-context blocks (Assistant tab)
├── ai-history/        # audit trail of every assembled agent prompt
├── packages/          # imported .nupkgs, one folder per id/version (External tab)
├── demo-nugets/       # bundled sample .nupkgs (Acme.Domain) seeded on first launch
└── vault/
    ├── vault.json     # SecretBlast header (KDF params, canary, vault id)
    └── secrets/       # *.secret files, opaque GUID-named
```

All three folders, the active theme, the editor highlighter (Native / TextMate), and the code-folding toggle are configurable from the **Settings** dialog. The terminal panel toggle lives on the toolbar next to Settings; its state is persisted to `config.json` alongside the rest.

## External references

The **External** tab in Settings lets you load arbitrary `.nupkg` packages or loose `.dll` files into the script engine. Use it to surface a private canonical-models package (or any third-party assembly) to scripts as standard `using` namespaces.

Add flow:

1. Pick a `.nupkg` (or `.dll`) via the file picker.
2. TaskBlaster validates the candidate via `MetadataLoadContext` without polluting the AppDomain — picks the best TFM from `lib/` (`net10.0 → net9.0 → net8.0 → netstandard2.1` precedence), walks `GetReferencedAssemblies`, and flags identity conflicts against everything already loaded.
3. The validation dialog renders the report, color-coded: ✓ no issues, ⚠ warnings (unresolved reference, version skew), ✗ errors (TFM incompatible, identity conflict).
4. Click **Add** for clean reports or **Add anyway** to override warnings/errors.
5. On success, TaskBlaster `Assembly.LoadFrom`s the DLLs and runs `GetTypes()` to surface any remaining type-load issues immediately.

Imported packages live under `~/.taskblaster/packages/<id>/<version>/` so they survive across launches; the `(id, version)` pairs persist in `config.json`.

Limitations to know:

* **Upgrades require a restart.** The default `AssemblyLoadContext` won't host two assemblies with the same simple name in one process. If you re-import an already-loaded package at a different version, the entry is staged for next launch but the live load is skipped.
* **Removal is also next-launch.** The default AppDomain doesn't support unload, so removing an entry from the External tab takes effect on the next start.
* **Imports are still global.** Every script sees every loaded external — there's no per-script scoping yet (see TODO).

A bundled fixture, **`Acme.Domain.1.0.0.nupkg`**, ships in `~/.taskblaster/demo-nugets/` so you can exercise the flow end-to-end. The `acme-domain-demo.csx` demo script uses its `Customer`, `Person`, `Address`, and `Order` types.

## Refreshing the bundled demos (developer)

The first-run seeder only copies *missing* files into your scripts and forms folders, so updates to the shipped demos in the repo don't reach an existing install. While iterating on the demos you can force-overwrite with:

```bash
dotnet run --project TaskBlaster -- --seed-demos
```

This copies every `DemoScripts/*.csx` and `DemoForms/*.json` from the build output into the configured target folders, overwriting existing files. It's a developer convenience, not a user feature.

### Bundled demos

| File                                | What it shows                                        |
| ----------------------------------- | ---------------------------------------------------- |
| `DemoScripts/hello.csx`             | Smallest possible script.                            |
| `DemoScripts/sum-numbers.csx`       | Arithmetic / output streaming.                       |
| `DemoScripts/input-demo.csx`        | `Prompts.Input` modal.                               |
| `DemoScripts/confirm-demo.csx`      | `Prompts.Confirm` modal.                             |
| `DemoScripts/env-report.csx`        | Runtime / loaded-Blast-assemblies report.            |
| `DemoScripts/inline-form.csx`       | A full GuiBlast form built and shown from code.      |
| `DemoScripts/quick-task-demo.csx`   | Load `DemoForms/quick-task.json` from disk and show it. |
| `DemoScripts/secret-resolve.csx`    | Pick a key from a vault category, print its value.   |
| `DemoScripts/vault-report.csx`      | Inventory of vault categories and key counts.        |
| `DemoScripts/azure-sql-template.csx`| Template for an AzureBlast SQL query (inline + vault-backed). |
| `DemoScripts/network-demo.csx`      | Anonymous httpbin GET via NetworkBlast.            |
| `DemoScripts/network-odata-demo.csx`| Typed LINQ-flavored OData against the public Northwind service. |
| `DemoScripts/sqlite-demo.csx`       | Local SQLite store via SqliteBlast — insert / query / transaction. |
| `DemoScripts/json-csv-demo.csx`     | UtilBlast JSON ⇆ CSV bridge + JObject helpers.       |
| `DemoScripts/blast-display-demo.csx`| UtilBlast `Blast` display DSL — heading / status / table / kv. |
| `DemoScripts/connections-demo.csx`  | Named-connection layer end-to-end: dynamic field access, typed binding, vault-ref dereferencing. |
| `DemoScripts/acme-domain-demo.csx`  | Walks the bundled `Acme.Domain` canonical-models package — Customer / Person / Address / Order. Requires importing the .nupkg via Settings → External first. |
| `DemoScripts/acme-domain-to-formidable.csx` | Reflects any loaded External assembly via `AssemblyBlast.AssemblyReader` and POSTs its classes / enums to a running [Formidable](https://github.com/petervdpas/Formidable) instance as FCDM model entries. Idempotent (deterministic GUIDs from FQN) and uses the named `Formidable` connection for its base URL. |
| `DemoForms/quick-task.json`         | Plain form: text / select / number / textarea.       |
| `DemoForms/peer.json`               | Plain form: switch + bounded number.                 |
| `DemoForms/deploy.json`             | Vault-backed select + conditional visibility.        |
| `DemoNugets/Acme.Domain.1.0.0.nupkg`| Sample canonical-models package (built from `TaskBlaster.SampleModels/`). Import via Settings → External to make the types available to scripts. |
| `DemoKnowledge/00-house-rules.md`   | Always-on baseline directing rules every script in this install should follow. |
| `DemoKnowledge/acme-domain-rules.md`| Knowledge block scoped to scripts that load the `Acme.Domain` namespace. |
| `DemoKnowledge/mssql-conventions.md`| MS-SQL conventions block; fires when `AzureBlast.MssqlDatabase` is in scope. Pulls in `sql-shared` via `includes:`. |
| `DemoKnowledge/networkblast-when-loaded.md` | NetworkBlast usage notes; fires when `NetworkBlast.NetClient` is loaded. |
| `DemoKnowledge/agentblast-when-loaded.md` | AgentBlast usage notes; fires when any `AgentBlast.*` namespace is loaded. Tells the model what the package is and the host-glue pattern, since it post-dates the training cutoff. |
| `DemoKnowledge/runbook-queue-drain.md` | Operational runbook block; only fires when the caller hands the picker a `runbook` tag (`tag:runbook` rule shape). |
| `DemoKnowledge/sql-shared.md`       | Shared SQL helper text with no `when:` rule; never picked as an entry point, only pulled in via another block's `includes:`. |

## Downloads

Tag-triggered GitHub Actions produce self-contained binaries and installers for:

* **Windows x64** — Inno Setup installer (`TaskBlaster-Setup-x.y.z.exe`)
* **Linux x64 / arm64** — `.deb` packages (arm64 covers Raspberry Pi 4/5 on 64-bit OS)
* **Fedora x64** — `.rpm` package
* **macOS x64 / arm64** — `.app` bundle + `.dmg`

See [Releases](https://github.com/petervdpas/TaskBlaster/releases) for the latest build.

## License

GPL v2 (or later) — see [LICENSE](LICENSE).
