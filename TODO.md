# TODO

## Next session — SecretBlast (planned)

Design discussion scheduled for 2026-04-24. Goal: a cross-platform encrypted
vault for user secrets (connection strings, API keys, etc.) that deliberately
avoids OS-default secret stores (DPAPI / Keychain / libsecret / kwallet).

Open questions to settle before a first cut:

- Scope — new `SecretBlast` nuget, or an add-on inside UtilBlast / AzureBlast?
- Key derivation — master password → Argon2id? Or KDF from an external
  key file the user places out-of-band?
- At-rest format — single JSON file encrypted with AES-GCM? One-file-per-secret
  for simpler diffs / Git-trackable vaults?
- Unlock model — unlock per TaskBlaster session, per script, or per secret?
- TaskBlaster integration — swap AzureBlast named-connection config to pull
  secrets from SecretBlast instead of plaintext JSON.
- Threat model — what exactly are we defending against that DPAPI/Keychain
  don't already cover? (Cross-platform portability, not trusting the OS
  account boundary, auditable vault format, …)

## Done

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
