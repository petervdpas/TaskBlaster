---
title: Shared SQL conventions
priority: 30
tags: db
---

# Shared SQL conventions

This block has no `when:` rule on purpose — it never gets picked as an
entry point on its own. It only shows up when another block (e.g.
`mssql-conventions`, `sqlite-conventions`) lists it under `includes:`.

- Identifiers in our schema are `snake_case`, not `camelCase`.
- Soft-delete columns are `deleted_at TIMESTAMPTZ NULL`; never repurpose
  a boolean for the same meaning.
- Read-only reports should run against the replica, not the primary.
