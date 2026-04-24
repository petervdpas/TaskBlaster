# TODO

## Next — TaskBlaster ↔ SecretBlast integration

SecretBlast v0.1 is implemented at `~/Projects/SecretBlast/` (27 tests green).
Time to wire it into TaskBlaster.

1. **Reference SecretBlast.** Add a `PackageReference` once SecretBlast is
   published, or temporarily a `ProjectReference` against `~/Projects/SecretBlast`
   for local iteration.
2. **`ISecretVault` singleton in DI.** Register in `Program.BuildServiceProvider`.
   The factory reads the vault path from `IConfigStore`.
3. **`VaultFolder` setting.** Add to `IConfigStore` (default `~/.taskblaster/vault`)
   and to the config dialog (single vault in v1, matches SecretBlast's decision).
4. **Unlock dialog.** On first secret access in a session, pop a password
   prompt via `IPromptService`. Wrong password → re-prompt; cancel → surface
   to the caller.
5. **AzureBlast resolver adapter.** Wire
   `Func<string, CancellationToken, Task<string>>` to call
   `ISecretVault.GetAsync`, triggering the unlock dialog on the first call.
   AzureBlast stays pure — no SecretBlast dependency.
6. **Migrate named-connection config.** Today: plaintext JSON. Target:
   `{ name → { vaultRef, secretName } }`. Write a small one-shot migration
   helper so existing users don't lose connections.
7. **Vault-create flow.** First-run UX: if the configured vault path doesn't
   exist, offer to create it with a new master password.

## Done

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
