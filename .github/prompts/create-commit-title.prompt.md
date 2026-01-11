---
agent: 'agent'
model: 'GPT-5.1-Codex-Max'
description: 'Generate an 80-character git commit title for the local diff'
---

# Generate Commit Title

## Purpose
Provide a single-line, ready-to-paste git commit title (<= 80 characters) that reflects the most important local changes since `HEAD`.

## Input to collect
- Run exactly one command to view the local diff:
  ```@terminal
  git diff HEAD
  ```

## How to decide the title
1. From the diff, find the dominant area (e.g., `src/modules/*`, `doc/devdocs/**`) and the change type (bug fix, docs update, config tweak).
2. Draft an imperative, plain-ASCII title that:
   - Mentions the primary component when obvious (e.g., `FancyZones:` or `Docs:`)
   - Stays within 80 characters and has no trailing punctuation

## Final output
- Reply with only the commit title on a single line—no extra text.

## PR title convention (when asked)
Use Conventional Commits style:

`<type>(<scope>): <summary>`

**Allowed types**
- feat, fix, docs, refactor, perf, test, build, ci, chore

**Scope rules**
- Use a short, PowerToys-focused scope (one word preferred). Common scopes:
  - Core: `runner`, `settings-ui`, `common`, `docs`, `build`, `ci`, `installer`, `gpo`, `dsc`
  - Modules: `fancyzones`, `powerrename`, `awake`, `colorpicker`, `imageresizer`, `keyboardmanager`, `mouseutils`, `peek`, `hosts`, `file-locksmith`, `screen-ruler`, `text-extractor`, `cropandlock`, `paste`, `powerlauncher`
- If unclear, pick the closest module or subsystem; omit only if unavoidable

**Summary rules**
- Imperative, present tense (“add”, “update”, “remove”, “fix”)
- Keep it <= 72 characters when possible; be specific, avoid “misc changes”

**Examples**
- `feat(fancyzones): add canvas template duplication`
- `fix(mouseutils): guard crosshair toggle when dpi info missing`
- `docs(runner): document tray icon states`
- `build(installer): align wix v5 suffix flag`
- `ci(ci): cache pipeline artifacts for x64`
