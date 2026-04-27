---
title: NetworkBlast usage notes
when: NetworkBlast.NetClient
priority: 40
tags: http
---

# NetworkBlast usage notes

Picked when `NetworkBlast.NetClient` (the PrimaryFacade) is loaded.
This is the FQN form of the `when:` rule — exact match against a
loaded type, more precise than the namespace prefix.

- Construct the client via the resolver path:
  `new NetClient(Secrets.Resolver, "github")`. The connection name
  carries the baseUrl and token; the client stays unaware of the vault.
- For OData endpoints, prefer the typed LINQ-flavored API over hand-
  building URL filter strings — the typed form handles encoding.
- Use `FirstPageAsync()` + `IAsyncEnumerable<T>` when iterating large
  result sets so paging is automatic and you don't materialise the
  whole collection.
