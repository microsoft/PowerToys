# Per-module verification profiles (`references/modules/`)

This folder holds **one short profile per PowerToys module**. Each profile is self-contained guidance specific to that module — paths, entry-paths, capability/control recipes, common BLOCKED traps, fixture lists, source citations.

## When to read

When this skill runs for a specific module, check whether `references/modules/<module>.md` exists here. If yes: **read it BEFORE walking the SKILL.md drive-stack** — it tells you which entry-paths actually work for this module's quirks and which BLOCKED traps to avoid.

If no profile exists, fall back to SKILL.md + the helper scripts.

## Shared cross-module flows

Some flows are common to several modules and live in their own top-level docs (not per-module):
- **`../references/explorer-context-menu-flow.md`** — driving the real Win11 Explorer right-click context menu end-to-end (open + assert present/absent + launch). Referenced by File Locksmith and any future **Image Resizer / PowerRename / New+** profiles.

## Why per-module (not just one big SKILL.md)

- Each module has its own quirks (Peek's `_isFromCli` guard, CmdPal's TextChanged-broken state, PT Run's mini-popup HWND, Workspaces' snapshot-elevation rules). Bundling all of them into the global SKILL.md bloats context and forces every verification to load 25+ KB of mostly-irrelevant text.
- A profile lets a focused verification run with only the relevant 5-10 KB.
- New gotchas discovered during a module verification round get added to that module's profile, not the global one — keeps the global doc stable.

## Profile catalog

| Module | Profile | Status |
|---|---|---|
| Peek | `peek.md` | ✅ written 2026-06-08 |
| File Locksmith | `file-locksmith.md` | ✅ written 2026-06-08 |
| Image Resizer | `image-resizer.md` | ✅ written 2026-06-09 |
| PowerRename | `power-rename.md` | ✅ written 2026-06-10 (first to cite `../context-menu-cookbook.md` for shared mechanics) |
| New+ | `new-plus.md` | ✅ written 2026-06-18 (registration-gate for menu presence; Settings-UI toggle drives template auto-copy) |
| (other modules to be added as we encounter sign-off needs) | — | — |

## For Explorer-context-menu modules: read the canonical flow doc first

If you're writing a profile for a module that registers an entry in Explorer's Win11 right-click menu (PowerRename, File Locksmith, Image Resizer, New+, Preview Pane, RegistryPreview), **read `../references/explorer-context-menu-flow.md` first**. It has the canonical synthetic-right-click + UIA-invoke recipe with:

- Which-approach-first decision rule (CLI back-door vs synthetic menu, with the false-positive trap warning)
- Stability rules (UIA InvokePattern, retry on first right-click)
- Recipe (robust 5-step flow)
- Multi-file selection notes
- Module captions table (per-module menu-item display names)
- Common failure modes
- The unlocked-desktop requirement (BLK-ENV gating)

The shared helper is `scripts/pt-explorer-contextmenu.ps1` (`Test-PtDesktopInteractive`, `Open-PtExplorerContextMenu`, `Invoke-PtContextMenuItem`, `Get-PtContextMenuItems`).

Your module profile then only documents the **module-specific** quirks: settings.json schema keys, expected verb caption regex, capability/control recipes, source citations, ceiling.

`power-rename.md` is the model — ~9 KB despite covering 18 items because the generic mechanics live in the canonical flow doc.

## Profile template

When writing a new profile, use this skeleton:

```markdown
# <Module> — module verification profile

**PT module**: `<ModuleKey>` (one-line description)
**Source**: `<PT-repo>\src\modules\<dir>\` (PT repo)
**Settings file**: `%LOCALAPPDATA%\Microsoft\PowerToys\<dir>\settings.json`
**Logs**: `%LOCALAPPDATA%\Microsoft\PowerToys\<dir>\Logs\v<ver>\log_<date>.log`
**Exes**: `<full path>`
**Default hotkey**: `<keys>` (modifiers + code, plus path to ActivationShortcut in settings)
**Named Event**: `Local\<name>` (friendly name in pt-shared-events.ps1 catalog)
**DSC resource**: `Microsoft.PowerToys/<Name>Settings`

## Entry-paths (try in order)

### 1. <fastest path>
<powershell code + when to use + source citation>

### 2. <alternate path>
<...>

### 3. <last-resort path>
<...>

## Recipes — a control/observation map, NOT a per-test-case answer key

| # | Capability | Drive (control / settings key) | Observe (where the result shows) |
|---|---|---|---|
| 1 | <a module capability, e.g. "context-menu entry present when enabled"> | <which AutomationId / control / settings key drives it> | <where the result is visible: preview column, settings.json, disk, log, menu> |
| 2 | <next capability> | <...> | <...> |

> **Mapping process** (agent at runtime): read the actual checklist item → identify the capability → find its row → drive the named control and **design your own inputs + assertions for that item**. If no row matches, it's a NEW capability — drive ad-hoc and add a row (capability + control + observation point; no canned inputs).

> **Why a map, not an answer key**: the table must carry only **durable module knowledge** — which control drives a capability and where to observe the result. Concrete Search/Replace inputs and expected-output assertions are *per-test-case answers*; baking them in turns the profile into a cheat sheet that (a) lets the agent copy answers without understanding and (b) goes stale the moment a checklist item changes its wording or values. Keep inputs + assertions OUT. Only a real UI redesign (a renamed/moved/removed control) should force an edit to this table.

## Common BLOCKED traps

<list of mistakes prior agents made + how to avoid them>

## Fixture files needed

<list of pre-canned files the verification expects>

## Source citations

<paths in PT repo that explain module behavior or guards>

## Ceiling

<observed PASS rate / total>

## Don'ts

<list of common mistakes>
```

## Hygiene

- **Keep each profile under ~10 KB.** If it grows beyond that, the module has too many quirks — escalate to maintainer review of the upstream checklist.
- **The recipe table is a control/observation MAP, not an answer key.** Columns are *Capability → Drive (control/key) → Observe*. **Do NOT bake in concrete Search/Replace inputs or expected-output assertions** — those are per-test-case answers that go stale when a checklist item changes and let the agent copy without understanding. The agent designs inputs + assertions at runtime from the actual checklist item.
- **Tables are capability-keyed, NOT line-keyed.** Upstream checklist line numbers (`L<n>`) **must not appear** in the profile — they drift between releases (items added/removed/reordered) and turn the table into a silent mismatch trap. PT-source-code file:line citations (e.g. `dllmain.cpp:73`) ARE allowed; they're version-pinned and serve a different purpose.
- **Cite source-code line numbers** where module behavior surprises (e.g. CLI guards, debounce timings, fallback chains). Reviewers can verify your claims by reading those lines.
- **Update the profile after every verification round**; promote any new technique into the right helper script if it generalizes beyond this module.
