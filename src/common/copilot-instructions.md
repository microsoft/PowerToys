# Common – Copilot guide (short)

Scope
- Shared libs used by Runner and modules (logging, IPC, settings, DPI, telemetry, utilities).

Specific Guideline
- Avoid breaking changes in shared headers without updating all modules.
- If you alter public headers/APIs, search usages and update all callers.
- Watch for perf in hot utilities; avoid heavy allocations in hooks/timers.

Acceptance
- No ABI breaks, no noisy logs.