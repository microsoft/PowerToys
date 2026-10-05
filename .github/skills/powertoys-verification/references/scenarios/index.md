# Scenario router

This skill runs in **two scenarios**. They share ~80% of the machinery (the `winapp ui` drive
techniques in `../winapp-ui-testing.md`, the helper scripts, the per-module profiles in `../modules/`,
the classification taxonomy, state hygiene, and the report format). They differ on only a couple of
axes. **Pick the scenario first, read its doc, then run the shared engine in `SKILL.md`.**

| Axis | A — Module checklist | B — PR validation |
|---|---|---|
| **Checklist source** | **Supplied** file (`../release-checklist/<module>.md`) | **Derived** by the agent from each PR's description + diff |
| **Scope** | 1 module, exhaustive (e.g. 88 items) | 1 PR (deep) — or a release/hotfix set, one folder per PR |
| **Bits under test** | Installed shipped artifact (READ-ONLY) | **Depends on a sub-decision** — see below |
| **Discipline** | Mutate only via user-facing UI; restore in `finally{}` | Installed-path: same as A · Build-path: build & sideload unreleased bits, then restore |

| If the task is… | Scenario | Read |
|---|---|---|
| "verify all `<Module>` checklist items", "sign off Color Picker", a supplied checklist file | **A** | [`module-checklist.md`](./module-checklist.md) |
| "validate PR #N" (open **or** merged), "review this fix before merge", "build it and test the fix", "verify the PRs in this release / hotfix / milestone", "sign off 0.X.Y" | **B** | [`pr-validation.md`](./pr-validation.md) |

**PR validation is one scenario** whether the PR is open, merged, or part of a release set — the hard
part (deriving drivable claims from the PR) is identical. What varies is only *which bits you run*,
resolved by the sub-decision below.

---

## The one contract that differs — declare `BITS` first

The only real conflict is **what "the bits under test" are** and therefore what you're allowed to
touch. Resolve it explicitly at the start of every run and **echo it in the report header** so a
reviewer can trust the evidence chain.

**Scenario A** — always the installed artifact:
```
BITS: installed shipped artifact <version> (read-only)
```

**Scenario B** — the sub-decision is **"is the PR's code already in the build under test?"**
```
BITS: installed shipped artifact <version> (read-only)          # yes — merged AND shipped in the build
BITS: local build of <Module> @ <sha/branch>, sideloaded        # no  — unmerged, or merged-but-unreleased
```

> **"Merged" is not the deciding word — "in the build under test" is.** A PR merged into `main` but
> not yet in the installed build is still the **build + sideload** case (e.g. #45242).

- **Installed / read-only (A, and B when the code is already shipped)** — the artifact is immutable.
  Forbidden: copying source-built files into the install or `%LOCALAPPDATA%\...\PowerToys\...`,
  pre-seeding caches the app/installer owns, editing module `settings.json` to bypass a Settings-UI
  step, registering/unregistering COM/MSIX, killing helper processes except as a documented user
  action. Allowed: anything a real user does through the shipped UI (toggles, hotkeys, set-value into
  Settings fields), read-only probes, screenshots — always capture pre-state and restore in
  `finally{}`. If the documented user flow does not produce the claimed outcome, that is **FAIL** —
  do not "rescue" it by editing install state.
- **Build + sideload (B when the code isn't in the build)** — testing unreleased code is the point,
  so the immutability rule does **not** apply to *your* build. It still applies to *unrelated*
  installed bits you didn't build. Restore the machine to the shipped build when done (see
  `pr-validation.md` Step 4b).

> A run that mutates the wrong `BITS` (sideloads when the code is already installed, or drives the
> stale installed binary when validating unreleased code) is **invalid regardless of the verdict.**
> Set `BITS` before the first drive command.

---

## Verdict vocabulary (one taxonomy, two label sets)

The shared engine in `SKILL.md` Step 3 uses **PASS / FAIL(product|checklist) / BLOCKED(reason)**.
Older PR-validation reports sometimes use **PASS / FAIL / Don't-know-what-to-test / Incapable**. They
map 1:1 — use the `SKILL.md` set and treat the legacy labels as aliases:

| Engine (`SKILL.md` §3) | Legacy alias | Meaning |
|---|---|---|
| PASS | PASS | Drove the behavior; matches the claim. |
| FAIL (cause=product) | FAIL | Shipped/built behavior contradicts the claim → file a bug. |
| FAIL (cause=checklist) | Don't know what to test | The item/spec is too vague or stale to judge → fix the checklist, quote the ambiguity. |
| BLOCKED (`BLK-*`) | Incapable of Testing | Couldn't run the check after ≥2 entry-paths → name the concrete obstacle. |

Whichever label set the caller asks for, keep the **evidence rules identical**: no "source verified /
live deferred / pre-existing / probably / unlikely" weasel-words to justify a PASS — those flip the
verdict to FAIL or BLOCKED-with-a-named-environmental-reason.
