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
| Command Palette | `command-palette.md` | ✅ written 2026-07-07 (CmdPal AppX foreground-lock / TextChanged-broken / alias-keystroke / Esc-filtered quirks — moved out of the global SKILL.md pitfalls) |
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

A profile holds **only module-specific logic** an agent can't infer from the SKILL engine. It has **4 required sections + 2 optional**. Do NOT pad it with sections that have no content — omit them. No Ceiling/Don'ts sections: a PASS-rate number drifts every release, and "don'ts" are just traps phrased negatively (put them in BLOCKED traps).

**Required (always):** ① metadata header · ② Entry-paths · ③ Recipes · ④ BLOCKED traps.
**Optional (include only if non-empty):** Fixtures · Source citations.

```markdown
# <Module> — module verification profile

# ① metadata header (REQUIRED) — bootstrap facts. Drop any line that doesn't apply.
**PT module**: `<ModuleKey>` (one-line description)
**Source**: `src\modules\<dir>\`
**Settings file**: `%LOCALAPPDATA%\Microsoft\PowerToys\<dir>\settings.json`
**Exe**: `<full path>`
**Default hotkey**: `<keys>` (+ settings ActivationShortcut path)
**Named Event**: `Local\<name>` (friendly name in pt-shared-events.ps1 catalog)
**DSC resource**: `Microsoft.PowerToys/<Name>Settings`

## Entry-paths (try in order)        # ② REQUIRED — how to launch & reach the UI, fastest first
### 1. <fastest path>  <code + when to use + source citation>
### 2. <alternate path>
### 3. <last-resort path>

## Recipes — control/observation map, NOT an answer key   # ③ REQUIRED
| # | Capability | Drive (control / settings key) | Observe (where result shows) |
|---|---|---|---|
| 1 | <module capability> | <AutomationId / control / settings key> | <preview / settings.json / disk / log / menu> |

> Mapping: read item → find capability row → drive the control, design your OWN inputs+assertions. No canned inputs/expected values (they go stale + invite copying). New capability ⇒ add a row.

## BLOCKED traps                      # ④ REQUIRED — false-block + gotcha prevention (absorbs old "Don'ts"/"gotchas")
- <mistake prior agents made → the fix>; <module quirk that misleads driving>

## Fixtures                           # OPTIONAL — only if the module needs canned files (else omit)
## Source citations                   # OPTIONAL — PT-repo file:line for surprising behavior (else inline in traps)
```

## Hygiene

- **4 required + 2 optional sections only** (header · entry-paths · recipes · BLOCKED traps; fixtures + source citations if non-empty). No Ceiling, no Don'ts — fold negative guidance into BLOCKED traps. Omit empty sections rather than writing "None".
- **Keep each profile under ~10 KB.** If it grows beyond that, the module has too many quirks — escalate to maintainer review of the upstream checklist.
- **The recipe table is a control/observation MAP, not an answer key.** Columns are *Capability → Drive (control/key) → Observe*. **Do NOT bake in concrete inputs or expected-output assertions** — they go stale when a checklist item changes and let the agent copy without understanding. The agent designs inputs + assertions at runtime from the actual checklist item.
- **Tables are capability-keyed, NOT line-keyed.** Upstream checklist line numbers (`L<n>`) **must not appear** — they drift between releases. PT-source-code file:line citations (e.g. `dllmain.cpp:73`) ARE allowed; version-pinned, different purpose.
- **Cite source-code line numbers** where module behavior surprises (CLI guards, debounce timings, fallback chains) so reviewers can verify.
- **Update the profile after every verification round**; promote any new technique into the right helper script if it generalizes beyond this module.
