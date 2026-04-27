---
title: Acme.Domain conventions
when: Acme.Domain
priority: 60
tags: models
---

# Acme.Domain conventions

Picked when the script imports anything from the `Acme.Domain`
namespace. The `when:` rule is a namespace prefix — any loaded type
under `Acme.Domain.*` triggers this block.

- `Customer.Code` is the stable external identifier. `Customer.Id` is a
  database surrogate; never expose it in API responses or logs.
- `Order.Status` follows the state machine in
  `Acme.Domain.Orders.OrderStatusTransitions`; reach for that helper
  instead of hand-rolling the transitions in scripts.
- Money lives on `Order.Total`, not on individual lines (the lines
  carry quantity + unit price). Sum from the lines if you need to
  reconstruct it.
