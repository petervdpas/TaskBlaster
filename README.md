# TaskBlaster

**TaskBlaster** is a desktop app for editing and running `.csx` maintenance scripts with rich, form-driven input — built on [Avalonia](https://avaloniaui.net/) and the **Blast** library family.

Scripts use [GuiBlast](https://www.nuget.org/packages/GuiBlast) for modal prompts and dynamic forms, [AzureBlast](https://www.nuget.org/packages/AzureBlast) for Azure SQL / Service Bus / Key Vault, and [UtilBlast](https://www.nuget.org/packages/UtilBlast) for common utilities — no boilerplate, no window management.

---

## Status

Early scaffold. Empty Avalonia shell; script host, editor, and runner not yet wired up.

## Stack

* .NET 10
* Avalonia 12
* UtilBlast 1.0.2
* AzureBlast 2.0.2
* GuiBlast 2.0.0

## Quick start

```bash
git clone https://github.com/petervdpas/TaskBlaster.git
cd TaskBlaster
dotnet run --project TaskBlaster
```

## Goals

* Edit `.csx` files in an integrated editor
* Launch scripts with typed, validated inputs via GuiBlast forms
* Manage connections (SQL, Service Bus, Key Vault) without hardcoding secrets
* Replacement for the legacy `ScriptRunner.Plugins` package

## License

GPL v2 (or later) — see [LICENSE](LICENSE).
