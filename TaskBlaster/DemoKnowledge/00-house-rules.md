---
title: House rules (always on)
when: always
priority: 100
tags: baseline
---

# House rules

Every script in this TaskBlaster install should follow these rules
unless a more specific block overrides them. They're injected on every
AI call because `when: always`.

- Never hardcode credentials. Read them with `Secrets.Resolve(category, key)`
  or, if the script needs more than one related value, with
  `Secrets.GetConnection("name")`.
- Prefer the `Blast` display DSL (`Blast.WriteHeading`, `Blast.WriteKv`,
  `Blast.WriteTable`) over hand-built `Console.WriteLine` separators —
  the output formatting stays consistent across scripts.
- When a form would clarify intent, use `DynamicForm.ShowJsonAsync` with
  a JSON file under `~/.taskblaster/forms/` rather than chaining
  `Prompts.Input` / `Prompts.Confirm` calls inline.
- Roslyn `.csx` does not support `using var` declarations — use the
  block form (`using (var x = ...) { ... }`) or call `Dispose` explicitly.
