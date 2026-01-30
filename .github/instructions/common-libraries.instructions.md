---
description: 'Guidelines for shared libraries including logging, IPC, settings, DPI, telemetry, and utilities consumed by multiple modules'
applyTo: 'src/common/**'
---

# Common Libraries – Shared Code Guidance

Guidelines for modifying shared code in `src/common/`. Changes here can have wide-reaching impact across the entire PowerToys codebase.

## Scope

- Logging infrastructure (`src/common/logger/`)
- IPC primitives and named pipe utilities
- Settings serialization and management
- DPI awareness and scaling utilities
- Telemetry helpers
- General utilities (JSON parsing, string helpers, etc.)

## Guidelines

### API Stability

- Avoid breaking public headers/APIs; if changed, search & update all callers
- Coordinate ABI-impacting struct/class layout changes; keep binary compatibility
- When modifying public interfaces, grep the entire codebase for usages

### Performance

- Watch perf in hot paths (hooks, timers, serialization)
- Avoid avoidable allocations in frequently called code
- Profile changes that touch performance-sensitive areas

### Dependencies

- Ask before adding third-party deps or changing serialization formats
- New dependencies must be MIT-licensed or approved by PM team
- Add any new external packages to `NOTICE.md`

### Logging

- C++ logging uses spdlog (`Logger::info`, `Logger::warn`, `Logger::error`, `Logger::debug`)
- Initialize with `init_logger()` early in startup
- Keep hot paths quiet – no logging in tight loops or hooks

## Acceptance Criteria

- No unintended ABI breaks
- No noisy logs in hot paths
- New non-obvious symbols briefly commented
- All callers updated when interfaces change

## Code Style

- **C++**: Follow `.clang-format` in `src/`; use Modern C++ patterns per C++ Core Guidelines
- **C#**: Follow `src/.editorconfig`; enforce StyleCop.Analyzers

## Validation

- Build: `tools\build\build.cmd` from `src/common/` folder
- Verify no ABI breaks: grep for changed function/struct names across codebase
- Check logs: ensure no new logging in performance-critical paths
