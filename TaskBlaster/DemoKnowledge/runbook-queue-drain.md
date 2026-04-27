---
title: Runbook — queue drain
when: tag:runbook
priority: 80
tags: runbook, ops
---

# Queue drain runbook

Picked only when the caller hands the picker a `runbook` tag (e.g. the
operation is "explain this script as a runbook"). Tag rules let the
user scope blocks to specific UI surfaces or named operations without
relying on what's loaded.

Steps:

1. Stop new producers. (`./ops/producer-pause.csx`)
2. Wait for the queue depth to settle (≤ 1 message/min for 5 min).
3. Drain consumers in pool order: `worker-a`, `worker-b`, `worker-c`.
4. Verify the dead-letter queue is empty before un-pausing producers.

Escalate if depth doesn't reach zero in 30 minutes.
