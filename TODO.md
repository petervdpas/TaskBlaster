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

## Roadmap (separate repos)

- **NetworkBlaster** — future Blast nuget for REST/HTTP integrations, a
  "programmable Postman" sibling to AzureBlast. Lives in its own repo
  (analogous to `~/Projects/AzureBlast`, `~/Projects/GuiBlast`, etc.).
  Will consume the existing `Func<category, key, ct, Task<string>>`
  resolver shape so it stays free of any SecretBlast / TaskBlaster
  dependency, e.g. `new NetClient(Secrets.Resolver, "github-token")`.
  Not a TaskBlaster-internal concern; nothing lands in this repo for it.

## Done

### 2026-04-25 — Resizable forms (GuiBlast 2.1.0 + designer toggle)

GuiBlast 2.1.0 adds `FormSpec.Resizable`; TaskBlaster persists the bit
and exposes a checkbox in the designer.

- **GuiBlast 2.1.0** (in `~/Projects/GuiBlast/`): added
  `FormSpec.Resizable` (root-level JSON property `resizable`).
  `DynamicForm.ShowAsync` OR-merges it with the existing `canResize`
  argument so either spec or caller can opt in.
- **TaskBlaster.csproj**: bumped `GuiBlast` 2.0.0 → 2.1.0.
- **`FormEditor.Resizable`** (default false). Round-trips through
  `FormDto.Resizable` (nullable bool) — written as `"resizable": true`
  only when set; omitted otherwise.
- **`IFormDocument.Resizable`** + dirty-flag wiring in `FormDocument`,
  matching the `Width` / `Height` pattern.
- **Size tab UI**: a "Allow user to resize" checkbox under the
  Width/Height grid, with a small note that the option needs
  GuiBlast 2.1.0+. "Reset to auto" deliberately leaves the toggle
  alone (size and resizability are unrelated user choices).
- **3 new round-trip tests** in `FormEditorSchemaTests`. 176/176 green.
- Preview path needed no change; `MainWindow.PreviewFormAsync` already
  hands the JSON straight to `DynamicForm.ShowJsonAsync`, which now
  reads `spec.Resizable` itself.

### 2026-04-25 — Vault-backed form options (`optionsFrom` hint)

Select-style fields can now declare their options as vault keys in a
category. The designer offers a per-field Static / From-vault toggle;
TaskBlaster expands the JSON before handing it to GuiBlast, which stays
vault-agnostic.

- **`Forms/FormJsonExpander.ExpandAsync(json, vault)`** walks the JSON,
  finds `optionsFrom` hints, materialises options from
  `IVaultService.ListAsync` filtered by category, then strips the hint.
  Empty `options[]` → expander auto-fills all keys; pre-picked subset
  passes through verbatim. Forms with no hint round-trip unchanged.
- **`OptionsPropertyEditor`** in the designer: radio for Static / From
  vault. Vault mode keeps the options list visible (manual subset pick)
  but constrains Value to a ComboBox of vault keys. Auto-prefills label
  from key on first pick. Triggers the supplied `ensureUnlocked`
  callback when the vault is locked, so the user gets the standard
  password dialog instead of an empty list.
- **`FormDesignerView.Initialize(IVaultService, Func<CancellationToken,
  Task>)`** wires the vault and the unlock-on-demand callback through
  to the field editors.
- **`MainWindow.PreviewFormAsync`** runs `FormJsonExpander.ExpandAsync`
  before `DynamicForm.ShowJsonAsync` and only triggers an unlock when
  the JSON actually contains an `optionsFrom` hint.
- **`FormEditor.OptionsSourceEditor`** + `OptionsFromDto` persist the
  hint as `"optionsFrom": { "source": "vault", "category": "..." }`.
- **Demo**: `DemoForms/deploy.json` (vault-backed select + visibility
  rules).
- Tests: 5 in `FormJsonExpanderTests` (passthrough, materialisation,
  pre-picked subset, missing category, unknown source).

### 2026-04-25 — Graceful script abort on cancelled vault unlock

When a `.csx` script hits `Secrets.Resolve` against a locked vault and
the user cancels the unlock dialog, the script ends as `Cancelled`
rather than dumping a stack trace.

- **`IFriendlyScriptException`** marker interface in
  `Engine/ScriptExceptions.cs`. Implemented by `VaultLockedException
  : InvalidOperationException`.
- **`BlastResult.Cancelled(string? message)`** overload so the terminal
  can show a concise reason ("Vault is locked, cannot resolve secret.")
  without a stack dump. Status renders as ⊘, not ✗.
- **`ScriptBlaster.RunAsync`** catches `IFriendlyScriptException` and
  returns `Cancelled(ex.Message)`; other exceptions still go through
  the normal `Error` path with a stack trace.
- Tests: `ScriptSecretsTests.Script_WhenVaultStaysLocked_AbortsAsCancelled_WithoutStackDump`
  asserts both the status and the absence of a stack trace.

### 2026-04-25 — Dev / UX small batch

- **`--seed-demos` CLI flag.** `dotnet run --project TaskBlaster --
  --seed-demos` force-overwrites every shipped `DemoScripts/*.csx` and
  `DemoForms/*.json` into the user's configured folders. The first-run
  seeder still only copies *missing* files; this flag is the dev
  refresh path. Implemented in `Program.SeedDemos` + `ForceCopyDemos`.
- **Splash auto-advance.** `SplashWindow` now starts a 5-second
  `DispatcherTimer` and shows a small countdown ("starting in X
  seconds... or click to continue"). User click still skips immediately.
- **Sparse-punctuation pass on UI strings.** Em-dashes and stray commas
  before "or" / "and" stripped from terminal/log/dialog text. Memory
  rule recorded so the next pass stays consistent.
- **More demos.** Added `env-report.csx`, `inline-form.csx`,
  `secret-resolve.csx`, `vault-report.csx`, `quick-task-demo.csx`;
  added `DemoForms/deploy.json`, `peer.json`, `quick-task.json`.

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
