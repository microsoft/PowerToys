# Scenario A — Single-module release checklist

**Use when:** the task supplies (or points at) a module's checklist and asks you to verify every
item — e.g. "verify all 18 Color Picker items", "sign off Command Palette's 88 items".

This is the skill's original scenario; `SKILL.md` Steps 1–7 are written for it, so this doc is
short — it only nails down the inputs and the report shape.

## Bits under test

```
BITS: installed shipped artifact <version> (read-only)
```

Installed artifact is immutable; mutate only through the shipped UI and restore in `finally{}`
(see `index.md` → "bits contract" and `../pre-flight.md` §Hard rules).

## Inputs

| Input | Source |
|---|---|
| Checklist (the set of items) | `../release-checklist/<module>.md` — **this file IS the items to verify.** Each item carries `[ADMIN: …]` + `[CLARITY: …]` metadata. See `../release-checklist/index.md` for the full module list. |
| Per-module recipes | `../modules/<module>.md` if it exists (entry-paths, item recipes, BLOCKED traps, fixtures). **Check this first.** If absent, fall back to the `SKILL.md` §2 drive-stack and author one afterwards (template in `../modules/README.md`). |

## Run order

1. `../pre-flight.md` — pre-flight + bootstrap (`SKILL.md` Step 1).
2. For each checklist item: pick a bucket from the verb (`SKILL.md` §2.A/§2.B/§2.C), drive it, then
   classify (`SKILL.md` §3). One verdict + evidence per item.
3. `../reporting-format.md` — per-item table + top-of-report summary + §G retrospective.
4. `../pre-flight.md` §State hygiene + §Final wrap-up.
5. `SKILL.md` Step 7 — archive the workspace to the sign-off folder.

## Scope rule

**One module per run.** Never chain multiple modules into one report. (For "verify a whole
release", that's **Scenario B — PR validation**, which fans out across the release's PRs with per-PR folders.)

## Report

Use `../reporting-format.md` verbatim. Header must include the `BITS:` line and the module's total
item count `<N>`.
