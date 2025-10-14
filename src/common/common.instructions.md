---
applyTo: "**/*.cs,**/*.cpp,**/*.c,**/*.h,**/*.hpp"
---
# Common – shared libraries guidance (concise)

Scope
- Logging, IPC, settings, DPI, telemetry, utilities consumed by multiple modules.

Guidelines
- Avoid breaking public headers/APIs; if changed, search & update all callers.
- Coordinate ABI-impacting struct/class layout changes; keep binary compatibility.
- Watch perf in hot paths (hooks, timers, serialization); avoid avoidable allocations.
- Ask before adding third‑party deps or changing serialization formats.

Acceptance
- No unintended ABI breaks, no noisy logs, new non-obvious symbols briefly commented.