# Reporting format

This doc defines the **required** report shape for every per-module verification run. Modeled on `PR-validation\Round1\PR-47211-validation\report.md` style — table-driven, reproducible, no prose narratives.

## §A — Per-item table (one per checklist item)

```markdown
## Item L<line_num> — <verbatim description from the module's checklist> — **<PASS|FAIL|BLOCKED>** <emoji>

**Admin**: <NO|COND|YES>  |  **Clarity**: <CLEAR|VAGUE-*|REWRITTEN>  |  **Category**: <PASS: verification method (free text)  ·  FAIL: cause = product | checklist-stale | checklist-ambiguous  ·  BLOCKED: a BLK-* reason>

### Verification steps performed

| # | Step | winapp / probe commands | Evidence / result |
|---|---|---|---|
| 1 | <what step 1 does> | `<exact command>`<br>`<another command if multiple>` | <what you observed; reference artifact filename> |
| 2 | <what step 2 does> | `<command>` | <evidence>; screenshot: `artifacts/L<line>/step-02-<name>.png` |
| 3 | ... | ... | ... |

### Artifacts produced
- `artifacts/L<line>/step-01-<name>.png` — <one-line description>
- `artifacts/L<line>/step-02-<name>.txt` — full inspect dump
- ...

### Verdict reasoning
- ✅ <assertion 1 that PASSed, with reference to the line of code / settings key / log line that proves it>
- ✅ <assertion 2>
- ❌ <if BLOCKED, the specific obstacle: "BLK-HARDWARE because MWB needs 2 physical PCs; this session has 1 ([System.Windows.Forms.Screen]::AllScreens.Count = 1)">

### Caveats (optional)
- <Any deviation from the user-documented flow, e.g. "Tested via settings.json write rather than UI checkbox because SelectionItemPattern.Select clobbers other selections in ListView.">
```

## §B — Top-of-report summary (write LAST, after all per-item tables)

```markdown
# <Module> verification report — <YYYY-MM-DD HH:MM>

## Summary
- **PASS**: <n>  ·  **FAIL (product)**: <n>  ·  **FAIL (checklist)**: <n>  ·  **BLOCKED**: <n>  ·  **Total**: <n>  ·  **PASS%**: <n>
- **Top blocker categories**: <category>: <count>, <category>: <count>, ...
- **Items needing follow-up**: L<line> (<reason>), L<line> (<reason>), ...
- **State mutations performed + restored**: <count> settings.json edits restored, <count> registry keys removed, <count> fixture files deleted

## Pre-flight
- IsAdmin: <true|false>
- PT runner: PID=<n> Elevated=<true|false>
- <Module> settings file: <path> (exists=<true|false>)
- Interactive desktop: ForegroundOk=<true|false>  ShellComOk=<true|false>

## Items
<all per-item tables here, in line_num order>

## Cleanup performed
- <list of every restore action taken>

## Retrospective (self-reflection on the run — write LAST)
<Per §G. If the whole run was frictionless, write exactly: **Everything was smooth — no friction encountered.**>
```

## §C — Required rules for step tables

1. **Every `winapp ui ...` command goes in the "winapp / probe commands" cell, verbatim, in backticks**, including `-w <hwnd>` / `-a <appId>` arguments and full selector strings. Reviewers will paste these into their own shell to reproduce.
2. **Every screenshot path goes in the "Evidence" cell** of the step that produced it, formatted as `screenshot: artifacts/L<line>/step-NN-<name>.png`. Never embed screenshots as `![...](...)` in the table body (breaks GitHub markdown rendering inside cells); just give the path.
3. **If a step has multiple commands**, separate them in the same cell with `<br>` so they render as one cell with multiple lines.
4. **PowerShell scriptlets > 3 lines**: write them to a separate `.ps1` in the artifacts folder and reference as ``script: `artifacts/L<line>/step-NN.ps1` `` in the cell. Keep the table cell to 1-3 lines.
5. **`—` (em dash) is allowed for non-CLI steps** like "Read sign-off entry + diff", "Create validation folder", "Cleanup notepad". Don't fabricate a command for steps that were purely cognitive or file-system level.
6. **Numbered steps must be contiguous** (1, 2, 3, ...). Don't skip numbers.
7. **At least one screenshot per PASS item if the item is a user-visible behavioral test**. Schema-only assertions (settings.json key check) don't need screenshots; behavioral tests (popup shown, dialog appeared, theme switched) do.

## §D — Reporting style

- Be specific. "Verified via UIA inspect returned `itm-calculator-XXXX`" beats "verified UIA".
- Include exact UIA selectors, log line text, settings.json keys, and screenshot filenames so the user can audit.
- For BLOCKED items, the 1-sentence reason should name **what specifically blocks**, e.g.:
  - "BLK-HARDWARE: requires 2nd monitor; session has 1 (verified via `[System.Windows.Forms.Screen]::AllScreens.Count`)."
  - "BLK-DRAG-REQUIRED: synthetic mouse drag insufficient for FZ snap-and-drag; needs real cursor motion."
  - "BLK-ENV: SendInput returned ACCESS_DENIED (5) because Session $agentSession ≠ console Session $consoleSession. See `references/environment-setup.md`."
  - "BLK-EXTERNAL-APP: requires real OpenAI API key; no key provisioned in test env."

## §E — Reporting anti-patterns (extra strict)

- Do NOT collapse multiple probe commands into a single English sentence like "verified via UIA". List every `winapp ui ...` command verbatim in a step row.
- Do NOT skip the step table for "trivial" items. Even a 1-step item (e.g. "Get-CmdPalSettings shows EnableDock=true") gets a 1-row table.
- Do NOT write screenshot references as `![alt](path)` inside table cells (GitHub renders markdown images poorly in cells). Write them as plain text path: `screenshot: artifacts/L<line>/step-NN-<name>.png`.
- Do NOT use "the test passed" as a screenshot caption — describe what's visible (e.g. "Settings page with FZ template grid showing 7 templates").
- Do NOT reference screenshots that you didn't actually capture. The final wrap-up `Test-Path` loop (see `references/pre-flight.md` §Final wrap-up step 3) will catch missing files; failing that check means the report is invalid.
- Do NOT cite source code line numbers (e.g. `CharacterMappings.cs:273`) without having actually read that line. If you cite source, the path must be real and the line number must contain what you claim.

## §F — Example item (reference: PR-47211 validation report style)

```markdown
## Item L455 — Activate Quick Accent (left Alt + arrow key) on a character, verify accents popup — **PASS** ✅

**Admin**: NO  |  **Clarity**: CLEAR  |  **Category**: drove full UIA flow + asserted accents popup

### Verification steps performed

| # | Step | winapp / probe commands | Evidence / result |
|---|---|---|---|
| 1 | Locate Settings window | `winapp ui list-windows --json` | `hwnd=263304`, `PowerToys.Settings` PID 31740 |
| 2 | Navigate to Quick Accent + expand language flyout | `winapp ui invoke QuickAccentNavItem -w 263304`<br>`winapp ui invoke btn-choosecharacter-1c4d -w 263304` | Page loaded; flyout expanded |
| 3 | Enumerate language list + screenshot | `winapp ui inspect btn-choosecharacter-1c4d -w 263304 --depth 5`<br>`winapp ui screenshot -w 263304 -o "artifacts/L455/step-03-language-list.png"` | 38 spoken + 6 special languages, alphabetic. screenshot: `artifacts/L455/step-03-language-list.png` |
| 4 | Single-language (French) popup test | `winapp ui invoke itm-french-1cac -w 263304`<br>`winapp ui inspect characters -w <popupHwnd> --depth 3`<br>`winapp ui screenshot -w <popupHwnd> -o "artifacts/L455/step-04-popup-FR-E.png"` | Popup chars for **E** = `é è ê ë €` (5), matches `FR.VK_E` in `CharacterMappings.cs:273`. screenshot: `artifacts/L455/step-04-popup-FR-E.png` |
| 5 | Restore baseline | — | settings.json reverted to `selected_lang="ALL"` |

### Artifacts produced
- `artifacts/L455/step-03-language-list.png` — Settings page with expanded language flyout
- `artifacts/L455/step-03-language-list.txt` — full UIA inspect dump of the list
- `artifacts/L455/step-04-popup-FR-E.png` — Popup with French only: `é è ê ë €`

### Verdict reasoning
- ✅ Popup characters match `CharacterMappings.cs` entries exactly (5/5 for FR.VK_E)
- ✅ Popup appeared within 500ms of hold-A; no crash
- ✅ Language list ordering is alphabetic by localized name
```

## §G — Retrospective (self-reflection)

After the run, reflect on the **process** (not the product) so the skill itself gets better over time. **If nothing slowed you down, write exactly one line: `Everything was smooth — no friction encountered.`** Otherwise, list each friction as a row and assign a source + severity.

```markdown
## Retrospective

| # | Friction (what slowed you / what was wrong) | Source | Severity | Cost | Suggested fix |
|---|---|---|---|---|---|
| 1 | <concrete description — what you expected vs what happened> | <one source tag below> | <HIGH/MED/LOW> | <~min wasted · N attempts> | <the doc line / helper function / tool behavior to change> |
```

**Source** — classify each friction into exactly one bucket so the right owner can fix it:

| Source tag | Meaning |
|---|---|
| `SKILL-UNCLEAR` | This skill's `SKILL.md` / `references/pre-flight.md` / module profile guidance was missing, ambiguous, or wrong. |
| `WINAPP-TOOL-BUG` | The `winapp` CLI itself misbehaved (crash, wrong output, flag not honored) — a product defect in the tool. |
| `WINAPP-DOC-UNCLEAR` | `references/winapp-ui-testing.md` was unclear/incorrect about how to use the tool (the tool worked; the docs misled you). |
| `HELPER-FLAW` | A shipped `scripts/*.ps1` had a logic bug, bad default, or wrong assumption. Name the function. |
| `PT-PRODUCT` | A PowerToys behavior/quirk made driving hard (distinct from a product **FAIL** — this is friction, not a checklist failure). |
| `CHECKLIST` | The checklist item itself was wrong/stale/ambiguous (e.g. describes a renamed or removed control). Note: this usually *also* produces a `FAIL (cause: checklist-*)` verdict on the item; log it here too so the checklist owner sees it as a process-improvement signal. |
| `ENVIRONMENT` | RDP/session/desktop/elevation friction not already covered by `references/environment-setup.md`. |

**Severity** — judge by *impact on future agents*, not just yourself:
- **HIGH** — most agents will hit it; blocks progress or wastes >10 min, or you needed a non-obvious workaround.
- **MED** — many agents may hit it; cost a few minutes or 2-3 retries; workaround exists once known.
- **LOW** — edge case or cosmetic; <1 min; noted for completeness.

**Cost** — be concrete: approximate minutes wasted **and** number of attempts (e.g. `~8 min · 3 attempts`). This is the raw signal for prioritizing skill fixes.

**Suggested fix** — point at the specific artifact to change: a doc line/section, a helper function name, or a `winapp` behavior to file. Vague reflections ("docs could be clearer") are not actionable — cite the line.

Example:
```markdown
## Retrospective

| # | Friction | Source | Severity | Cost | Suggested fix |
|---|---|---|---|---|---|
| 1 | `winapp ui inspect --depth 7 -w $hwnd` threw "Cannot bind argument" until I moved `-w` after `--depth`. | `WINAPP-TOOL-BUG` | MED | ~6 min · 3 attempts | Already noted in pitfall #8, but the tool should parse flag order — file against winapp. |
| 2 | SKILL.md §2.A says "wait 4s debounce" but PowerRename needed a full `Restart-PtRunner`; the module-owned-file note (pitfall #12) wasn't cross-linked from §2.A. | `SKILL-UNCLEAR` | HIGH | ~12 min · 4 attempts | Add an explicit "shell-ext modules → see pitfall #12" pointer inside §2.A. |
```
