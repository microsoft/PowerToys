---
name: "squad-conventions"
description: "Core conventions and patterns used in the Squad codebase"
domain: "project-conventions"
confidence: "high"
source: "manual"
---

## Context
These conventions apply to all work on the Squad CLI tool (`create-squad`). Squad is a zero-dependency Node.js package that adds AI agent teams to any project. Understanding these patterns is essential before modifying any Squad source code.

## Patterns

### Zero Dependencies
Squad has zero runtime dependencies. Everything uses Node.js built-ins (`fs`, `path`, `os`, `child_process`). Do not add packages to `dependencies` in `package.json`. This is a hard constraint, not a preference.

### Node.js Built-in Test Runner
Tests use `node:test` and `node:assert/strict` — no test frameworks. Run with `npm test`. Test files live in `test/`. The test command is `node --test test/`.

### Error Handling — `fatal()` Pattern
All user-facing errors use the `fatal(msg)` function which prints a red `✗` prefix and exits with code 1. Never throw unhandled exceptions or print raw stack traces. The global `uncaughtException` handler calls `fatal()` as a safety net.

### ANSI Color Constants
Colors are defined as constants at the top of `index.js`: `GREEN`, `RED`, `DIM`, `BOLD`, `RESET`. Use these constants — do not inline ANSI escape codes.

### File Structure
- `.squad/` — Team state (user-owned, never overwritten by upgrades)
- `.squad/templates/` — Template files copied from `templates/` (Squad-owned, overwritten on upgrade)
- `.github/agents/squad.agent.md` — Coordinator prompt (Squad-owned, overwritten on upgrade)
- `templates/` — Source templates shipped with the npm package
- `.squad/skills/` — Team skills in SKILL.md format (user-owned)
- `.squad/decisions/inbox/` — Drop-box for parallel decision writes

### Windows Compatibility
Always use `path.join()` for file paths — never hardcode `/` or `\` separators. Squad must work on Windows, macOS, and Linux. All tests must pass on all platforms.

### Init Idempotency
The init flow uses a skip-if-exists pattern: if a file or directory already exists, skip it and report "already exists." Never overwrite user state during init. The upgrade flow overwrites only Squad-owned files.

### Copy Pattern
`copyRecursive(src, target)` handles both files and directories. It creates parent directories with `{ recursive: true }` and uses `fs.copyFileSync` for files.

## Examples

```javascript
// Error handling
function fatal(msg) {
  console.error(`${RED}✗${RESET} ${msg}`);
  process.exit(1);
}

// File path construction (Windows-safe)
const agentDest = path.join(dest, '.github', 'agents', 'squad.agent.md');

// Skip-if-exists pattern
if (!fs.existsSync(ceremoniesDest)) {
  fs.copyFileSync(ceremoniesSrc, ceremoniesDest);
  console.log(`${GREEN}✓${RESET} .squad/ceremonies.md`);
} else {
  console.log(`${DIM}ceremonies.md already exists — skipping${RESET}`);
}
```

## Anti-Patterns
- **Adding npm dependencies** — Squad is zero-dep. Use Node.js built-ins only.
- **Hardcoded path separators** — Never use `/` or `\` directly. Always `path.join()`.
- **Overwriting user state on init** — Init skips existing files. Only upgrade overwrites Squad-owned files.
- **Raw stack traces** — All errors go through `fatal()`. Users see clean messages, not stack traces.
- **Inline ANSI codes** — Use the color constants (`GREEN`, `RED`, `DIM`, `BOLD`, `RESET`).
