# TODO

## Next session — MEDI DI rollout (planned 2026-04-25)

Move from the current hand-wired DI (just `IThemeService` threaded through constructors) to a proper `Microsoft.Extensions.DependencyInjection` container.

### Starting state

- `Program.cs` news up a `ThemeService` and hands it to `App` via `AppBuilder.Configure(() => new App(themes))`.
- `App` → `SplashWindow` → `MainWindow` all take `IThemeService` via constructor.
- The other services (`IConfigStore`, `IScriptBlaster`, `IPromptService`) are still `new`'d inside `MainWindow`'s constructor.

### Scope

- [ ] Add `Microsoft.Extensions.DependencyInjection` `PackageReference` to `TaskBlaster/TaskBlaster.csproj`.
- [ ] In `Program.BuildAvaloniaApp`, build a `ServiceCollection` and register:
  - `IThemeService` → `ThemeService` (singleton)
  - `IConfigStore` → `ConfigStore` (singleton)
  - `IScriptBlaster` → `ScriptBlaster` (singleton or transient — pick when wiring)
  - `IPromptService` → `AvaloniaPromptService` (needs owning `Window`, see wrinkle below)
  - `IFormDocument` → factory for `FormDocument` (transient; one per loaded form)
- [ ] Resolve `App` (and through it `SplashWindow` / `MainWindow`) from the provider instead of `new`.
- [ ] Drop `new ScriptBlaster()` / `new ConfigStore()` / `new AvaloniaPromptService(this)` from `MainWindow`'s ctor; take them via constructor injection.

### Wrinkle — `IPromptService` owner-window

`AvaloniaPromptService` needs the `Window` it shows dialogs over (currently `this` from inside `MainWindow`). Options:

1. Register as a factory that depends on `MainWindow` — but `MainWindow` is also resolved from the container, which creates a cycle.
2. Create `IPromptService` late in `MainWindow`'s ctor from a small `IPromptServiceFactory` registered in the container. Cleaner.
3. Expose an `IDialogHost` singleton that `MainWindow` sets itself on, and have `AvaloniaPromptService` pull the current host from it. Decouples the prompt service from any specific window, useful later for prompts launched from non-main windows.

Pick when we get there; (2) is the smallest change.

### Not in scope (yet)

- View-models / MVVM refactor
- `Microsoft.Extensions.Hosting` (builder, lifetime, config, logging wiring)
- Keyed services
- Scoped lifetimes / disposable scopes

Bring these in as/when we need them, not preemptively.
