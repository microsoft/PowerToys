# Common – Copilot guide (short)

Scope
- Shared libs used by Runner and modules (logging, IPC, settings, DPI, telemetry, utilities).

Tests / checks
- If you alter public headers/APIs, search usages and update all callers.
- Run at least one consumer build (Runner) to validate link/ABI and a simple smoke run.

Gotchas
- Avoid breaking changes in shared headers without updating all modules.
- Watch for perf in hot utilities; avoid heavy allocations in hooks/timers.

Acceptance
- No ABI breaks, all downstreams build, basic Runner smoke OK, no noisy logs.