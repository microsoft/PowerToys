# Scenario router

This skill runs in **three scenarios**. They share ~80% of the machinery (the `winapp ui` drive
techniques in `../winapp-ui-testing.md`, the helper scripts, the per-module profiles in
`../modules/`, the classification taxonomy, state hygiene, and the report format). They differ on
only four axes. **Pick the scenario first, read its doc, then run the shared engine in `SKILL.md`.**

| Axis | A — Module checklist | B — Release/hotfix PR sign-off | C — Active PR validation |
|---|---|---|---|
| **PR state** | n/a | **merged** PRs already in a shipped/draft build | **unmerged / open** PR not yet in any build |
| **Checklist source** | Supplied file (`../release-checklist/<module>.md`) | **Derived** from the PRs in a release/hotfix | **Derived** from one PR's description + diff |
| **Bits under test** | Installed shipped artifact (READ-ONLY) | Installed shipped artifact (READ-ONLY) | **Your local build, sideloaded** (the point is unreleased code) |
| **Discipline** | Mutate only via user-facing UI; restore in `finally{}` | Same as A | **Inverted** — you MUST build & deploy unreleased bits |
| **Scope** | 1 module, exhaustive (e.g. 88 items) | N PRs, derive+verify each, per-PR folders | 1 PR, deep |

| If the task is… | Scenario | Read |
|---|---|---|
| "verify all `<Module>` checklist items", "sign off Color Picker", a supplied checklist file | **A** | [`module-checklist.md`](./module-checklist.md) |
| "verify the PRs in this release / hotfix / draft release / milestone", "sign off 0.X.Y" | **B** | [`release-pr-signoff.md`](./release-pr-signoff.md) |
| "validate an **unmerged / open** PR #N", "review this PR's fix before merge", "build it and test the fix" | **C** | [`active-pr-validation.md`](./active-pr-validation.md) |

If the task is ambiguous between B and C, the deciding question is **"is this PR already merged
and shipped in the installed build, or is it still open/unmerged?"** — merged & installed ⇒ B
(drive the shipped bits), open/unmerged ⇒ C (build & sideload it first). A bare "validate PR #N" is
**C** when the PR is still open; if it has already merged into the build under test, treat it as a
one-PR Scenario B instead.

---

## The one contract that differs by scenario — declare it first

The only real conflict between scenarios is **what "the bits under test" are** and therefore what
you are allowed to touch. Resolve it explicitly at the start of every run and **echo it in the
report header** so a reviewer can trust the evidence chain:

```
BITS: installed shipped artifact 0.100.1.0 (read-only)          # Scenario A / B
BITS: local build of <Module> @ <sha/branch>, sideloaded        # Scenario C
```

- **A / B — installed artifact is immutable.** Forbidden: copying source-built files into the
  install or `%LOCALAPPDATA%\...\PowerToys\...`, pre-seeding caches the app/installer owns,
  editing module `settings.json` to bypass a Settings-UI step, registering/unregistering
  COM/MSIX, killing helper processes except as a documented user action. Allowed: anything a real
  user does through the shipped UI (toggles, hotkeys, set-value into Settings fields), read-only
  probes, screenshots — always capture pre-state and restore in `finally{}`. If the documented
  user flow does not produce the claimed outcome, that is **FAIL** — do not "rescue" it by editing
  install state.
- **C — you are testing unreleased code, so building & sideloading is the whole point.** The A/B
  immutability rule does **not** apply to your own build. It still applies to *unrelated* installed
  bits you didn't build. Restore the machine to the shipped build when done (see scenario C doc).

> A run that mutates the wrong `BITS` (e.g. sideloads in B, or runs the shipped binary in C) is
> **invalid regardless of the verdict**. Set `BITS` before the first drive command.

---

## Verdict vocabulary (one taxonomy, two label sets)

The shared engine in `SKILL.md` Step 3 uses **PASS / FAIL(product|checklist) / BLOCKED(reason)**.
Scenario B's legacy reports sometimes use **PASS / FAIL / Don't-know-what-to-test / Incapable**.
They map 1:1 — use the `SKILL.md` set and treat the legacy labels as aliases:

| Engine (`SKILL.md` §3) | Legacy B alias | Meaning |
|---|---|---|
| PASS | PASS | Drove the behavior; matches the claim. |
| FAIL (cause=product) | FAIL | Shipped/built behavior contradicts the claim → file a bug. |
| FAIL (cause=checklist) | Don't know what to test | The item/spec is too vague or stale to judge → fix the checklist, quote the ambiguity. |
| BLOCKED (`BLK-*`) | Incapable of Testing | Couldn't run the check after ≥2 entry-paths → name the concrete obstacle. |

Whichever label set the caller asks for, keep the **evidence rules identical**: no "source verified
/ live deferred / pre-existing / probably / unlikely" weasel-words to justify a PASS — those flip
the verdict to FAIL or BLOCKED-with-a-named-environmental-reason.
