---
title: Mssql conventions (AzureBlast)
when: AzureBlast.MssqlDatabase
priority: 50
tags: db, mssql
includes: sql-shared
---

# Mssql conventions

Picked when the script has `AzureBlast.MssqlDatabase` in scope (the
PrimaryFacade for the Mssql side of AzureBlast). Pulls in the shared
SQL block transitively via `includes:`.

- Always wrap the database in `using (...)` with the block form, never
  rely on the script process to dispose it.
- Set up via the resolver path:
  `db.SetupAsync(Secrets.Resolver, "prod-sql")`. Don't pass a raw
  connection string — the named connection is the source of truth.
- Parameterise everything (`@id`, `@name`); never string-concatenate
  user input into SQL.
- Long-running queries belong inside an explicit transaction with a
  clear commit / rollback path; don't lean on connection auto-commit.
