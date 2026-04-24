# TODO

## Next — post-Secrets-tab follow-ups

The Secrets tab is live (SecretBlast 1.0.0 wired in). Scripts can now
pull values out of the vault via the `Secrets` global (see the Done
section below). Still open:

1. **Migrate named-connection config.** Today: plaintext JSON. Target:
   `{ name → { category, key } }` pointing into the vault. One-shot migration
   helper so existing users don't lose connections. The resolver shape
   (`Func<category, key, ct, Task<string>>`) is already what
   `Secrets.Resolver` hands out, so AzureBlast / NetBlast stay free of any
   SecretBlast dependency.
2. **Name-reveal confirm.** The 👁 toggle in the secret-entry dialog is free
   — consider gating the DataGrid value column behind a per-row reveal too,
   or a "reveal for 30 s" pattern.
3. **Category rename.** UI-only today — user has to edit each secret. A
   bulk-rename (right-click category → rename) would rewrite envelopes under
   the hood and leave filenames untouched.
4. **Search / filter box** on the Secrets DataGrid.

## Done

### 2026-04-25 — Script-side vault access (`Secrets` global)

`.csx` scripts can now resolve vault entries directly:

```csharp
var token = Secrets.Resolve("api", "token");          // sync
var conn  = await Secrets.ResolveAsync("azure", "prod-sql"); // async
// Delegate form for libraries (AzureBlast / planned NetBlast / …):
var db = new SomeClient(Secrets.Resolver, "prod-sql");
```

- New `TaskBlaster.Engine.ScriptGlobals` is passed to Roslyn as the
  script-globals object; its public `Secrets` property (a
  `ScriptSecrets`) surfaces as a top-level identifier inside every
  script.
- `ScriptSecrets.Resolver` is a `Func<category, key, ct, Task<string>>`
  shaped for any third-party library that takes a named-connection
  resolver — no SecretBlast / TaskBlaster coupling on the library side.
- `IScriptBlaster.RunAsync` gained a `ScriptGlobals?` parameter; when
  non-null Roslyn is called with `globalsType: typeof(ScriptGlobals)`.
- If a script hits `Secrets.Resolve` against a locked vault,
  `MainWindow.EnsureVaultUnlockedAsync` hops to the UI thread and
  reuses the normal create/unlock dialog flow. Cancelling the prompt
  surfaces as a runtime `InvalidOperationException` inside the script.
- Demo: `DemoScripts/secret-resolve.csx`.
- Tests: 4 new (`ScriptSecretsTests`), 164 green. Script-touching
  tests moved into a shared `[Collection("ScriptBlaster")]` because
  `ScriptBlaster` swaps `Console.Out` globally and parallel tests were
  stomping on each other's captured output.

### 2026-04-24 — SecretBlast integration (Secrets tab)

SecretBlast 1.0.0 NuGet package is wired into TaskBlaster and live behind a
new 🔐 Secrets toolbar mode.

- **Envelope format.** Each SecretBlast secret is stored under an opaque
  32-char hex id; the *value* is a JSON envelope with `schemaVersion`,
  `category`, `key`, `value`, `description`, `createdUtc`, `updatedUtc`.
  Category and key names are encrypted at rest; nothing on disk leaks the
  organisational structure. Codec in `TaskBlaster/Secrets/SecretEnvelope.cs`,
  ids in `SecretId`.
- **`IVaultService`.** Stateful wrapper over `ISecretVault` that hides the
  envelope marshalling and exposes `category/key/value` CRUD plus
  `ResolveAsync(category, key)` for integrations. Registered as a singleton.
  Production KDF is 256 MiB / 3 / 4 (Argon2id); tests override to 1 MiB / 1 / 1
  so the suite stays under a second.
- **VaultFolder config.** New `IConfigStore.VaultFolder` property (default
  `~/.taskblaster/vault`), wired through `ConfigDialog` with a third folder
  row. Legacy configs without the field still load cleanly.
- **Unlock / create flow.** First time the user clicks 🔐 Secrets → Unlock,
  `MainWindow` detects whether `vault.json` exists at the configured path,
  then pops either a two-field `PasswordDialog` (create) or single-field
  (unlock, with retry on wrong password). `IPromptService` grew a
  `PasswordAsync(title, prompt, confirm)` method so the flow stays testable
  via `FakePromptService`.
- **Secrets UI.** `SecretsView` — category list on the left, a DataGrid
  on the right with Category / Key / Description / Updated columns. Toolbar
  actions inside the view: ➕ Add / ✏ Edit / 🗑 Delete / 📋 Copy value / 🔒 Lock.
  Add + Edit go through `SecretEntryDialog` with a 👁 value-reveal toggle
  and existing-category autocomplete. `Avalonia.Controls.DataGrid` 12.0.0
  package added and its Fluent style pulled into `App.axaml`.
- **139 tests green** — 11 new for the envelope codec, 13 for `VaultService`
  (round-trip, lock/unlock, rename keeps id, opaque filenames, resolve
  case-insensitive), 1 new for legacy-config load.

Still explicitly out of scope for this session: named-connection migration
for AzureBlast callers, bulk category rename, search box on the grid.

## Older

### 2026-04-24 — SecretBlast v0.1 implementation

Full crypto implementation landed on top of the stub. Still at
`~/Projects/SecretBlast/`, still not committed / published.

- **KDF:** `Konscious.Security.Cryptography.Argon2` 1.3.1, 32-byte key derivation.
- **Symmetric:** `System.Security.Cryptography.AesGcm`, 12-byte nonces, 16-byte tags.
- **Header canary** (`"canary-v1"`) encrypted with the derived key under
  `AAD = "SecretBlast" || vaultId || "canary"`. Wrong-password detection via
  `AuthenticationTagMismatchException` → `InvalidMasterPasswordException`.
- **Per-secret AAD** = `"SecretBlast" || vaultId || secretName` — swapping
  a `*.secret` file in from another vault fails authentication loudly.
- **Atomic writes** via `*.tmp` → `File.Move(overwrite: true)`.
- **Auto-lock timer** resets on every op; zero-length / infinite disables it.
- **New exceptions:** `VaultNotFoundException`, `VaultCorruptException`.
- Stub tests replaced with 27 real round-trip tests covering: full Create →
  Set → Close → Open → Unlock → Get cycle, wrong password, already-unlocked
  no-op, Lock event ordering, tampered ciphertext, cross-vault swap,
  auto-lock firing, atomic-write cleanup, header shape on disk.

Decisions taken during implementation (now in `DESIGN.md`):

- `UnlockAsync` on an already-unlocked vault is a **no-op** — no re-derive,
  no password revalidation. Callers holding the vault have already proven it.
- `Create` on a directory containing non-`vault.json` files is allowed.
- `Open` throws `VaultNotFoundException` eagerly (before unlock) so bad
  paths surface immediately.
- `CancellationToken` on `UnlockAsync` cancels the *wait* but not the
  in-flight Argon2 derivation (Konscious limitation).

### 2026-04-24 — SecretBlast stub

Scaffolded `~/Projects/SecretBlast/` as a standalone Blast nuget matching the
UtilBlast template. Public API surface + state machine + plaintext-filename
`ListAsync`. Crypto paths threw `NotImplementedException` (now replaced).

### 2026-04-24 — MEDI DI rollout

Migrated from hand-wired DI (only `IThemeService` threaded through constructors)
to `Microsoft.Extensions.DependencyInjection`.

- Added `Microsoft.Extensions.DependencyInjection` 10.0.7 package reference.
- `Program.BuildAvaloniaApp` now builds a `ServiceCollection`, registers
  singletons for `IThemeService`, `IConfigStore`, `IScriptBlaster`,
  `IPromptServiceFactory`, `IFormDocumentFactory`, and transients for `App`,
  `SplashWindow`, `MainWindow`. `App` is resolved from the provider.
- `App` and `SplashWindow` take `IServiceProvider` and resolve the next
  window from it — no more `new SplashWindow(themes)` / `new MainWindow(themes)`.
- `MainWindow` takes all its services via constructor injection; the
  `new ScriptBlaster()` / `new ConfigStore()` / `new AvaloniaPromptService(this)`
  field initializers are gone.
- Owner-window wrinkle solved via **option (2)**: `IPromptServiceFactory`
  registered as a singleton; `MainWindow` calls `Create(this)` in its ctor.
- `FormDocument.LoadFromFile` is now reached through `IFormDocumentFactory`,
  which also wraps `SaveToFile` so the cast in `MainWindow` is gone.
- 114/114 tests still pass.

Still explicitly out of scope (pull in when we actually need them):
view-models / MVVM, `Microsoft.Extensions.Hosting`, keyed services,
scoped lifetimes.
