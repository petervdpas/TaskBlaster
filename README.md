# TaskBlaster

<p align="center">
  <img src="TaskBlaster/Images/spacegun.png" alt="TaskBlaster" width="320">
</p>

**TaskBlaster** is a cross-platform desktop app for writing, organizing, and running C# maintenance scripts (`.csx`) with real user interfaces — not command-line prompts.

It exists to solve a recurring, awkward problem: you need a one-off (or rarely-run) maintenance task — rotate a Key Vault secret, re-queue dead-lettered Service Bus messages, patch a few rows in a database, generate a report — and the options are all bad:

* A **console app** means parsing args, hand-rolling prompts, and re-compiling every time a parameter changes.
* A **full GUI app** is overkill for a 40-line chore.
* A **shared script dumping ground** has no input validation, no UI, and no safety net.

TaskBlaster takes a middle path: you write a plain `.csx` file, optionally pair it with a JSON form describing its inputs, and TaskBlaster handles the rest — rendering the form, validating input, running the script under Roslyn, and streaming output into the built-in terminal.

---

## How it works

1. **Write a script.** Drop a `.csx` file into your scripts folder. It can `#r` NuGets and `using` any namespace, just like `dotnet-script`.
2. **Describe its inputs (optional).** Pair the script with a GuiBlast JSON form — text fields, dropdowns, checkboxes, conditional visibility, button actions. Build it by hand or use the built-in **visual form designer**.
3. **Run it.** TaskBlaster prompts the user with the form, injects the answers, executes the script via Roslyn scripting, and streams stdout/stderr live to the terminal panel.
4. **Connect to Azure without secrets in code.** Scripts can call into [AzureBlast](https://www.nuget.org/packages/AzureBlast) to talk to Azure SQL, Service Bus, and Key Vault using named connections configured in TaskBlaster — no connection strings checked into git.

This is the successor to the legacy `ScriptRunner.Plugins` package, rebuilt on .NET 10 + Avalonia 12 and the **Blast** library family.

## Who it's for

* **DBAs / data engineers** running ad-hoc SQL maintenance, bulk updates, or data fixes
* **Platform / DevOps engineers** managing Azure resources, rotating secrets, draining queues
* **Support engineers** running well-defined recovery scripts safely, with typed inputs
* **Anyone** who keeps a folder full of "scratch" C# scripts and wants them to feel like real tools

## Features

* Integrated `.csx` editor with syntax highlighting (AvaloniaEdit + TextMate)
* Roslyn-based script host with live stdout/stderr streaming into a terminal panel
* **Visual form designer** for GuiBlast JSON forms — field list, per-type property editor, visibility rules, action buttons, live preview
* Named Azure connections (SQL, Service Bus, Key Vault) — no secrets in scripts
* Configurable scripts folder, editor font size, and appearance
* Demo scripts and forms shipped out of the box

## Stack

* .NET 10
* Avalonia 12
* [UtilBlast](https://www.nuget.org/packages/UtilBlast) 1.0.2 — common utilities
* [AzureBlast](https://www.nuget.org/packages/AzureBlast) 2.0.2 — SQL / Service Bus / Key Vault
* [GuiBlast](https://www.nuget.org/packages/GuiBlast) 2.0.0 — form specs and modal prompts
* Roslyn Scripting (Microsoft.CodeAnalysis.CSharp.Scripting) for `.csx` execution

## Quick start

```bash
git clone https://github.com/petervdpas/TaskBlaster.git
cd TaskBlaster
dotnet run --project TaskBlaster
```

## Downloads

Tag-triggered GitHub Actions produce self-contained binaries and installers for:

* **Windows x64** — Inno Setup installer (`TaskBlaster-Setup-x.y.z.exe`)
* **Linux x64 / arm64** — `.deb` packages (arm64 covers Raspberry Pi 4/5 on 64-bit OS)
* **Fedora x64** — `.rpm` package
* **macOS x64 / arm64** — `.app` bundle + `.dmg`

See [Releases](https://github.com/petervdpas/TaskBlaster/releases) for the latest build.

## License

GPL v2 (or later) — see [LICENSE](LICENSE).
