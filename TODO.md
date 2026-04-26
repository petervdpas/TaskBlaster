# TODO

## Next — post-Secrets-tab follow-ups

The Secrets tab is live (SecretBlast 1.0.0 wired in). Scripts can now
pull values out of the vault via the `Secrets` global (see the Done
section below). Still open:

1. **Connections: legacy import wizard.** Phase 3 of the multipart
   connections work (Phase 1 model+resolver, Phase 2 Connections tab
   landed 2026-04-26). Wizard would read a flat / fully-plaintext
   legacy connections JSON, let the user mark which fields per
   connection should move into the vault, write those vault entries,
   and rewrite the connections file with `fromVault` pointers in
   their place. Only worth building if there's a real ScriptRunner.Plugins
   data set to migrate from.
2. **Connections UI: vault picker.** The Connections tab currently
   uses two free-text TextBoxes for the (category, key) pair on
   FromVault fields. Replace with a pair of ComboBoxes populated from
   `IVaultService.GetCategoriesAsync` + a keys-by-category lookup,
   reusing the OptionsPropertyEditor pattern (vault unlock on demand).
3. **Connections UI: reveal toggle for plaintext fields.** Long
   plaintext values (especially anything that looks secret-shaped)
   could use a 👁 toggle similar to `SecretEntryDialog`. Optional, low
   priority.
4. **Value-column reveal (open question).** The Secrets grid currently shows
   Category / Key / Description / Updated only; the value lives behind 📋 Copy
   and the 👁 toggle in `SecretEntryDialog`. If we ever add a value column,
   gate it behind per-row reveal or a "reveal for 30 s" pattern. Otherwise
   close this item.
5. **Search / filter box** on the Secrets DataGrid.

## Roadmap (separate repos)

*(empty; all currently-planned siblings have shipped: NetworkBlast 1.0.2,
AzureBlast 2.1.0, GuiBlast 2.1.0, SecretBlast 1.0.2.)*

## Done

### 2026-04-26 (cont. 3) — Multipart named connections

End-to-end vertical slice for the Connections feature. A connection
is a named bag of fields; each field is either a plaintext literal
(URL, server, account name, timeout) or a pointer into the vault
(token, password, secret key). Phases 1+2 of the original 3-phase
plan; Phase 3 (legacy import wizard) is in the open list and will
only get built if a real ScriptRunner.Plugins data set shows up.

Phase 1: model + resolver:

- **`TaskBlaster.Connections`** namespace: `Connection`,
  `ConnectionField`, `ConnectionVaultRef` records;
  `ConnectionFieldEditor` INPC viewmodel for the UI;
  `ConnectionSnapshot` (`DynamicObject`) for the resolved view scripts
  see; `ConnectionStore` (JSON-backed) reads/writes
  `~/.taskblaster/connections.json`; `ConnectionsResolver` wraps a
  vault resolver with a connections overlay.
- **Resolver semantics:** for each `(category, key)` lookup,
  `connections[category][key]` is consulted first. `{ "value": ... }`
  returns the literal (no vault unlock); `{ "fromVault": ... }`
  dispatches to the underlying vault resolver against the pointed-to
  pair. An absent connection or absent field falls through to the
  vault resolver directly so all-vault scripts keep working
  unchanged.
- **`ScriptSecrets`** grew an `IConnectionStore` ctor parameter and
  uses it to wrap the script-facing `Resolver`. New API:
  `Connections()` lists registered connection names;
  `GetConnection(name)` returns a `dynamic` snapshot
  (`var conn = Secrets.GetConnection("github"); var url = conn.baseUrl;`);
  `GetConnection<T>(name)` deserialises the snapshot into a record /
  class via JsonSerializer with case-insensitive name match and
  `JsonNumberHandling.AllowReadingFromString`.
- **DI:** `IConnectionStore` registered as a singleton in `Program.cs`,
  path anchored on `Path.GetDirectoryName(VaultFolder)` so the file
  follows the user when the TaskBlaster home moves.
- **Tests:** 7 in `ConnectionStoreTests` (round-trip, case-insensitive
  Get, sorted List, Remove, malformed-field drop, malformed-JSON
  recovery), 5 in `ConnectionsResolverTests` (plaintext / fromVault /
  missing-field fall-through / missing-connection fall-through /
  delegate shape), 8 in `ScriptSecretsConnectionsTests`
  (Connections() listing, plaintext-only no-unlock, mixed fields
  dereference vault, missing-name throws, Has / GetOrDefault, no-store
  empty case, dynamic member access, case-insensitive dynamic, typed
  record binding, numeric-from-string typed binding).

Phase 2: Connections tab:

- **`AppMode.Connections`** + `🔗 Connections` toolbar toggle.
- **`ConnectionsView`** with two-pane layout: name list on the left
  (`➕ Add` / `🗑 Delete`); per-connection editor on the right with a
  `DataGrid` of fields (Name / Mode / Value / ×) and an `➕ Add field`
  button. Mode column is a combo (`Plaintext` / `From vault`); Value
  column flips between a single TextBox (plaintext) and a paired
  category / key TextBox grid (from-vault) via INPC-driven
  `IsVisible` bindings on the `ConnectionFieldEditor` viewmodel.
- **Implicit persistence**: every name / mode / value edit calls
  `PersistCurrentConnection()` which writes the whole connection
  back through `IConnectionStore.Save`, mirroring the live-edit
  feel of the Secrets tab.

Other:

- **Settings dialog:** Theme moved out of the toolbar into a Theme
  dropdown at the top of Settings. `IThemeService.AvailableThemes`,
  `IConfigStore.Theme`, `App` now applies the persisted theme on
  startup. `🌓 Theme` toolbar button removed along with its event
  plumbing.
- **`DemoScripts/connections-demo.csx`** showing inventory + dynamic
  + typed forms, plus a commented-out NetworkBlast handoff. Bundled
  via the existing `DemoScripts/*.csx` content glob.
- **README** Stack list bumped to SecretBlast 1.0.2; demo table grew a
  connections-demo entry; new "Connections" section explaining the
  feature and the field convention.
- 202/202 TaskBlaster tests green.

### 2026-04-26 (cont. 2) — Category rename moves the secrets

`CategoriesDialog` rename now actually re-tags the affected secrets;
previously it only updated the picker list and required a per-secret
edit to follow.

- **`IVaultService.RenameCategoryAsync(oldName, newName)`** added.
  Walks the live envelopes, rewrites `category` on those that match
  case-insensitively (`OrdinalIgnoreCase`), saves under the same id so
  filenames stay opaque. Skips the catalog reserved id. Returns the
  rewrite count. Idempotent: re-running after a partial failure is safe
  because already-renamed secrets no longer match the old name.
- **`CategoriesDialog`** tracks a display-name → original-name map so
  add / rename / re-rename ops collapse into a clean list of
  `(OldName, NewName)` pairs at Save time. Fresh adds map to `null`
  (no envelope rewrite). The rename prompt now says "*N secret(s)
  currently use this category; they will be re-tagged to the new name
  when you save*" instead of the previous "edit them one by one"
  caveat.
- **`CategoriesDialogResult`** carries `Renames` alongside `Categories`.
- **`SecretsView.OnCategoriesClicked`** rewrites envelopes first, then
  flips the catalog list. The terminal log includes the re-tag count
  when non-zero.
- **4 new VaultService tests** covering: case-insensitive match across
  multiple secrets with id preservation, no-op when no match, no-op
  when old == new, and the contract that the catalog isn't touched
  (caller pairs the rename with `SetCategoriesAsync`).

### 2026-04-26 (cont.) — Form Settings polish + vault unlock fixes

Round of designer-UX cleanup followed by chasing the intermittent
"right password rejected" report.

- **Actions + Visibility editors → `DataGrid`.** Both views were
  hand-rolling rows in an `ItemsControl` with column definitions
  duplicated between header and rows; headers were drifting because
  `ItemsPresenter` introduces a small horizontal offset. Replaced with
  `DataGrid` + `DataGridTemplateColumn`s so the grid owns column
  alignment. Per-cell controls keep the always-editable feel via
  OneWay bindings + `TextChanged` / `IsCheckedChanged` /
  `SelectionChanged` handlers that look up the row's editor via
  `DataContext`.
- **`VisibilityRuleEditor.IsNeq` / `IsHide` mode flags.** The previous
  code inferred mode from "is `Neq` non-null?" / "is `Hide` non-empty?",
  which failed for new rules where both sides were empty (combo would
  show "not equal" but writes still went to `Eq`). Added explicit mode
  bits with `Value` / `TargetsCsv` accessors that route writes
  correctly; `FromDto` initialises the bits from whichever side of the
  toggle is populated, so loaded JSON behaves as before.
- **Form Settings tabs.** Added an explainer line under the Actions
  header. Fixed the Size tab so Width/Height labels sit *above* their
  inputs (they shared a row with auto-width columns before, putting
  them left of the boxes). Removed the `Dispatcher.Post(...
  DispatcherPriority.Loaded)` suppress flag in `SizeEditorView` that
  was eating the first toggle of "Allow user to resize" if the user
  clicked before the deferred reset ran; replaced with
  doc-equality guards in each `Commit*` method.
- **Status-bar dot + dividers.** Replaced the implicit
  filename-bullet dirty signal with an explicit `●` indicator in the
  right-hand status segments (left of the existing Ready/Running…
  status). Three states: `DangerBrush` (red) when dirty, `SuccessBrush`
  (green) when saved, muted when no file is open. Added a `Color.Success`
  to both themes plus a `SuccessBrush` semantic brush in `Base.axaml`.
  Switched all status-bar dividers from `SystemControlForegroundBaseMediumLowBrush`
  (no override; rendered invisible against our backgrounds) to
  `BorderBrush` so they actually show.
- **Vault unlock: NFC normalisation in SecretBlast.**
  `Argon2Kdf.DeriveAsync` now runs `password.Normalize(NormalizationForm.FormC)`
  before UTF-8 encoding so callers who type the same characters via
  different input methods (composed vs decomposed) derive the same key.
  ASCII passwords unaffected. Shipped as **SecretBlast 1.0.2**;
  TaskBlaster bumped to match.
- **Vault unlock: serialise concurrent attempts.** `VaultService` grew
  a `SemaphoreSlim(1, 1)` `_stateGate` that wraps `InitializeAsync`
  and `UnlockAsync`. Parallel clicks (or a click plus a script-triggered
  unlock) used to run `SecretVault.Open` twice with `_vault` still null
  on the second one, then both `AttachVault`-ed in some order with the
  loser's instance leaked but still wired to the `Locked` event. Late
  callers now also early-return when the vault is already open at the
  same path so the queued retry doesn't redo Argon2.
- **Vault unlock: "Verifying password…" busy state.**
  `SecretsView.SetVerifying(bool)` swaps the locked-panel hint and
  disables the Unlock + Reset buttons while `UnlockAsync` /
  `InitializeAsync` are running. Wired in `MainWindow.UnlockOrCreateVaultAsync`
  around both calls. Argon2 at 256 MiB / 3 / 4 takes 1-3 seconds; with
  no feedback the user couldn't tell the dialog had accepted the
  password and was re-clicking, spawning parallel chains.
- 176/176 TaskBlaster tests still green; 149/149 SecretBlast tests
  still green.

### 2026-04-26 — UtilBlast 1.1 + SqliteBlast 1.0 wired in

Two more siblings on the same day. Both bring real-world data tooling
to scripts without dragging in heavy dependencies.

- **UtilBlast 1.1.0** (in `~/Projects/UtilBlast/`): JSON ⇆ CSV bridge
  (`string.JsonToCsv()` / `string.CsvToJson()`, RFC 4180 compliant with
  quoted fields / embedded newlines / doubled quotes), `DataTable.ToCsv()` /
  `string.ParseCsvToDataTable()`, `IEnumerable<T>.ToCsv()` (reflection),
  `JObject.Flatten()`, `JToken.GetByPath("a.b[0].c")`. The pre-existing
  broken `DataTable.ToCsv(bool)` (no escaping) was replaced. 53 new tests,
  270/270 total green.
- **SqliteBlast 1.0.0** (in `~/Projects/SqliteBlast/`): brand-new Blast
  nuget for local SQLite. `ISqliteStore` with `Execute` / `ExecuteScalar<T>` /
  `Query<T>` (typed row mapping with full coercion) / `QueryDataTable` /
  `BeginTransaction` (rollback-on-dispose, commit explicitly). Directory-
  based migration runner with a `__migrations__` table for idempotence.
  Vault-aware `SetupAsync(resolver, name)` mirroring AzureBlast 2.1's pattern.
  Script-friendly `SqliteBlastFactory.Open(path)` and `InMemory()` factories.
  31 tests, 0 warnings.
- **TaskBlaster.csproj**: bumped `UtilBlast` 1.0.2 → 1.1.0; added `SqliteBlast 1.0.0`.
- **`Engine/ScriptBlaster.cs`**: force-loads `SqliteBlast.SqliteStore` alongside the others.
- **Demo scripts**:
  - `DemoScripts/sqlite-demo.csx` — in-memory store + parameter binding +
    transaction + typed Query, plus a commented vault-backed path.
  - `DemoScripts/json-csv-demo.csx` — JSON-to-CSV with nested flatten,
    CSV-to-JSON round-trip, `JObject.Flatten()`, `JToken.GetByPath()`.
- 176/176 TaskBlaster tests still green.

### 2026-04-26 — NetworkBlast + AzureBlast resolver path wired in

Two siblings landed on the same day; both consume `Secrets.Resolver`
(shape `Func<category, key, ct, Task<string>>`) so the vault stays the
single source of connection truth.

- **NetworkBlast 1.0.2** (in `~/Projects/NetworkBlast/`): brand-new
  Blast nuget for REST/HTTP/SOAP/OData. The 0.1 → 1.0 arc landed 2026-04-25;
  1.0.2 followed up with a small ergonomic fix (relaxed the `NetClient`
  resolver parameter from a custom `SecretResolver` delegate to plain
  `Func<string, string, CancellationToken, Task<string>>`) so
  `Secrets.Resolver` flows in directly without `.Invoke` or wrapper
  lambdas. 246 tests, 0 warnings under Release.
- **AzureBlast 2.1.0** (in `~/Projects/AzureBlast/`): purely-additive
  resolver path. Each component grew an async overload —
  `MssqlDatabase.SetupAsync(resolver, name)`,
  `AzureServiceBus.SetupAsync(...)`,
  `AzureTableStorage.InitializeAsync(...)`,
  `AzureKeyVault.InitializeKeyVaultAsync(...)` — that pulls connection
  values via the resolver. `AzureBlastOptions` gained `Resolver` plus
  `SqlConnectionName` / `ServiceBusConnectionName` /
  `TableConnectionName` / `KeyVaultConnectionName`; `AddAzureBlast`
  picks the resolver path when those names are set, falls back to the
  existing string path otherwise. Mix-and-match supported. 17 new
  resolver tests, all green.
- **TaskBlaster.csproj**: bumped `AzureBlast` 2.0.2 → 2.1.0; added
  `NetworkBlast 1.0.2`.
- **`Engine/ScriptBlaster.cs`**: force-loads `NetworkBlast.NetClient`
  alongside the other Blast assemblies so Roslyn picks it up via
  `AppDomain.GetAssemblies()`.
- **Demo scripts**:
  - `DemoScripts/network-demo.csx` — anonymous httpbin call plus a
    commented vault-backed (`new NetClient(Secrets.Resolver, "github")`)
    follow-up.
  - `DemoScripts/network-odata-demo.csx` — typed LINQ-flavored OData
    against the public Northwind service, demonstrating
    `FirstPageAsync()` + `IAsyncEnumerable<T>` auto-paging.
- 176/176 TaskBlaster tests still green.

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
