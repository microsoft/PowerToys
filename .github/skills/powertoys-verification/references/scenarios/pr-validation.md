# Scenario B — PR validation

**Use when:** you're asked to validate **one or more PRs** by deriving each PR's checklist from its
own description + diff, then driving it. This one scenario covers all the PR shapes:

- a single **open / unmerged** PR ("validate PR #N", "review this fix before merge"),
- a single **merged** PR ("check that #N actually works"),
- a whole **release / hotfix set** of merged PRs ("sign off 0.100.1", "verify the 14 PRs in this draft release").

The *hard, interesting* part — turning a PR into concrete, drivable claims — is identical for all of
them. They differ on only one thing: **what bits you run.**

---

## The bits sub-decision (the only real fork)

Ask one question up front and **echo the answer in the report header**:

> **Is the PR's code already in the build under test?**

| Answer | Bits under test | `BITS:` line |
|---|---|---|
| **Yes** — merged **and** present in the installed / shipped build you're testing | Drive the **installed** bits, **read-only** | `BITS: installed shipped artifact <version> (read-only)` |
| **No** — unmerged/open, **or** merged-but-not-yet-released (not in any shipped build) | **Build** the affected module + **sideload** it | `BITS: local build of <Module> @ <sha/branch>, sideloaded` |

> **"Merged" is not the deciding word — "in the build under test" is.** A PR that merged into `main`
> but hasn't shipped in the installed build is still the **build + sideload** case (e.g. validating
> a fix ahead of the next release — PR #45242 was exactly this). Only drive the installed bits when
> the code you're validating is genuinely in them.

- **Installed path — the artifact is immutable.** Forbidden: copying source-built files into the
  install or `%LOCALAPPDATA%\...\PowerToys\...`, editing module `settings.json` to bypass a
  Settings-UI step, registering/unregistering COM/MSIX, killing helpers except as a documented user
  action. Allowed: anything a real user does through the shipped UI, read-only probes, screenshots —
  capture pre-state and restore in `finally{}`. If the documented user flow doesn't produce the
  claimed outcome, that's **FAIL** — never "rescue" it by editing install state.
- **Build + sideload path — building unreleased code is the whole point,** so the immutability rule
  does **not** apply to *your* build. It still applies to *unrelated* installed bits you didn't
  build. **Restore the machine to the shipped build when done** (Step 4b).

> A run that mutates the wrong `BITS` (sideloads when the code is already installed, or drives the
> stale installed binary when validating unreleased code) is **invalid regardless of the verdict.**
> Set `BITS` before the first drive command. Full forbidden/allowed list: `index.md` → bits contract
> and `../pre-flight.md` §Hard rules.

---

## Step 0 — Acquire the PR set (discovery model)

For a **single PR**, `N = 1` — skip straight to Step 1. For a **release / hotfix**, a full release
can carry ~100 PRs and a hotfix ~10–25; blindly looping 100 is the wrong default. Resolve the set
with the **smallest, most explicit source available**, then apply the size gate.

### Input modes (prefer the most explicit one the caller gave you)

| Mode | Source | How to enumerate |
|---|---|---|
| **1. Explicit set** *(preferred)* | PR numbers, a sign-off doc, or a draft/published release whose notes list the PRs | Parse the PR numbers directly. For a release: `gh api repos/microsoft/PowerToys/releases/<id>` → extract `#NNNNN` from `.body`. |
| **2. Discoverable source** | A milestone, a release tag, or a commit/tag range | `gh pr list --repo microsoft/PowerToys --search "milestone:0.X" --state merged --limit 200 --json number,title,labels,files` · or for a range: `git log <tagA>..<tagB> --oneline` then extract `(#NNNNN)` merge refs. |
| **3. Caller defers entirely** | "verify the latest release" with no list | Resolve to a concrete release/milestone first (mode 1/2). If you cannot, **ask** rather than guess. |

### Size gate (the guardrail)

Let `N` = enumerated candidate count, `MAX_AUTO = 25` (hotfix-sized; tune per request).

- **`N ≤ MAX_AUTO`** → proceed: derive + verify each PR (this is the single-PR and hotfix case).
- **`N > MAX_AUTO`** → **STOP. Do not blind-loop.** Present the candidate list grouped by
  module/area with counts, and require the caller to **scope** before deep-verifying: by module, by
  label (e.g. `Needs-Verification`, priority), or an explicit subset. Confirm, then verify the scoped
  set. **Depth beats throughput** — deeply verifying 20 PRs and leaving a clear queue of the rest
  beats 100 shallow "source-verified" reports.

### Pre-filter non-runtime PRs

Before counting against the gate, set aside PRs with **no user-observable runtime surface** — list
them as `exempt` (not BLOCKED, not PASS) with the reason, don't spend drive cycles on them:
docs-only, CI/pipeline/build-only, dependency bumps, localization/translation-only,
release-signing/installer-plumbing with nothing to click. Detect via labels and changed-file paths
(`gh pr view N --json files,labels`). Everything else (anything a user opens/toggles/presses/types/
previews) goes into the deep-verify set and is subject to the live-drive floor below.

---

## Step 1 — Per PR: derive the checklist

For each PR in the (possibly scoped) set:

```powershell
gh pr view  <N> --repo microsoft/PowerToys --json title,body,files,labels,state,mergedAt
gh pr diff  <N> --repo microsoft/PowerToys      # read the diff — it tells you what actually changed
```

Turn the description + diff into **1–3 concrete checklist items** (the observable claim(s) the PR
makes), then drive each with the `SKILL.md` §2 bucket selector and classify with `SKILL.md` §3. From
`--json files`, also identify the **affected project(s)** (`.csproj` / `.vcxproj`) — you'll need
them for the build path (Step 2b). If, after the description AND the diff, you still cannot tell what
to verify → **FAIL (cause=checklist)** ("Don't know what to test"), quoting the ambiguity — do not
guess.

You may `grep`/`view` a **read-only** local clone/worktree for source context (XAML AutomationIds,
the `.cs`/`.cpp` the PR touched). On the **installed path** you must not run that code against the
install; on the **build + sideload path**, building it is exactly the point (Step 2b).

### Live-drive floor (anti-shallow-verification)

If the PR has a verb a real user performs (open/toggle/press/drag/right-click/type/preview/paste/
invoke/install/pin/search/record/scroll), the steps table MUST contain **≥4 `winapp ui …` rows** and
**≥1 `winapp ui screenshot` of the post-state**. A PR with a user-visible surface and zero
`winapp ui …` rows is **not validated**. "Source verified; live deferred" is a weasel-word that
downgrades the verdict (see `index.md` → verdict vocabulary).

---

## Step 2 — Get the bits under test

Follow **2a or 2b** per the bits sub-decision above.

### 2a — Installed path (code already in the build under test)

Nothing to deploy: the shipped runner/modules are already installed. Confirm the module is present
(`../pre-flight.md`), then drive the **installed** bits read-only. This is the path for a merged PR
that has shipped, and for a release/hotfix sign-off where you're validating the released artifact.

### 2b — Build + sideload path (code not in the build under test)

Building & deploying unreleased code is the point. Build **only the affected project** (identified in
Step 1) — do **not** build the whole solution just to populate every module.

```powershell
cd <PT_REPO>
gh pr checkout <N>                                          # PR branch locally (open PRs); for a merged-unreleased PR, check out its merge commit / the branch
tools\build\New-WorktreeFromBranch.ps1 -Branch <pr-branch>  # isolated worktree
git submodule update --init --recursive                     # once
tools\build\build-essentials.cmd                            # first build / NuGet restore (runner + settings only)
tools\build\build.ps1 -Platform x64 -Configuration Release  # run from the changed .csproj/.vcxproj dir
```

**Exit code 0 = success (absolute).** On non-zero, read `build.<config>.<platform>.errors.log` next
to the project. If the toolchain is missing (VS 2022 17.4+/2026, Windows SDK) and the build can't
complete → **BLOCKED (`BLK-ENV`)** — never PASS on an unbuilt PR.

> **Partial build ⇒ missing-module dialogs are EXPECTED.** A module-specific build produces only
> *your* module, so the build-output runner pops a modal **"Failed to load PowerToys.<Module>ModuleInterface.dll"**
> (`#32770`) for each un-built module. This is normal for a targeted build, **not** a build failure.

**Deploy — run the freshly built bits, don't overlay onto the install:**

- **Unpackaged module** (FancyZones, PT Run, ColorPicker, Peek, KBM, Advanced Paste, …): stop the
  installed runner, start the **build-output** runner, and dismiss the expected dialogs.
  ```powershell
  Get-Process PowerToys -EA SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force }
  Start-Process "<PT_REPO>\x64\Release\PowerToys.exe"
  (Get-Process PowerToys | Select-Object -First 1).Path      # must point under the build output
  ```
  The "Failed to load …" boxes are native `#32770` message boxes — `winapp ui invoke OK` is
  unreliable; `PostMessage`/`SendMessage` `WM_CLOSE (0x0010)` to each, or send Enter to the focused
  dialog, until none remain. (Only if dialogs keep racing your module → full solution build as a
  fallback.) **Never** overlay a partial build onto the installed layout — mixing `0.0.1` files with
  the shipped `0.100.x` install (esp. a WinUI module's `.dll` without its `.pri`) silently corrupts
  the test.
- **Packaged module** (Command Palette / CmdPal — MSIX): enable Developer Mode, remove the shipped
  package, `Add-AppxPackage -Register "<build-output>\AppxManifest.xml"`, and restore on cleanup.

**Prove it's your bits** before driving: the running module's path is under the build output, and/or
a dev version string (e.g. Settings shows `v0.0.1`, not `0.100.x`). A run that accidentally drove the
installed binary is invalid.

Because you control the build, you can make a PASS decisive rather than "the fix is present" — e.g.
exercise the **failing** state the PR fixes (revert-and-rebuild, or compare against the installed
shipped build).

---

## Step 3 — Drive + classify

Same engine as everything else: per item pick the `SKILL.md` §2 bucket, drive, classify with
`SKILL.md` §3. The live-drive floor (Step 1) applies.

## Step 4 — Artifacts, report, restore

**Artifacts + report.** One folder per PR: `{Module}-PR{Number}/` (e.g. `CmdPal-PR48689/`,
`AdvancedPaste-PR45242/`). All screenshots and the PR's `report.md` go there; the verdict lives
**inside** `report.md`, not in the folder name. Use the `../reporting-format.md` per-item table; the
`winapp invoke` column is a hard contract — a literal `winapp ui …` command or `—` (never a
`Select-String`/`gh`/`Test-Path` there). The header carries the `BITS:` line (incl. the sha/branch +
proof-of-your-bits path on the sideload path) and, on 2b, a build summary (exit code, project built).

**Roll-up** (multi-PR sets): top-level summary table — PR · Module · verdict · one-line evidence —
plus the `exempt` list and (if `N > MAX_AUTO`) the un-scoped **queue** of PRs not yet verified.
Include a §G retrospective (run friction).

**Restore (Step 4b / sideload path only).** Stop your built runner and bring the shipped build back:
```powershell
Get-Process PowerToys -EA SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force }
Start-Process "$env:LOCALAPPDATA\PowerToys\PowerToys.exe"     # or the Program Files install
# Packaged: re-add the shipped package (Add-AppxPackage -Register the shipped AppxManifest.xml)
```
Restore all mutated state, confirm the runner is healthy, and disclose any residue in the report.

---

## Worked references

- **Single unmerged/unreleased PR, build + sideload:** PR #45242 (Advanced Paste "Show AI paste
  section") — derived 3 claims from the diff, built only the AdvancedPaste project, sideloaded the
  dev `v0.0.1` runner (dismissed the partial-build dialogs), drove Settings + the AP window + a GPO
  gate, then restored the shipped build. 3/3 PASS.
- **Release/hotfix set, installed bits:** the 0.100.1 14-PR sign-off (8 PASS / 6 BLOCKED) — derived
  each PR's checklist from its release-notes line + diff, drove the installed bits, and blocked the
  hardware/visual-only items (DDC/CI, dual-GPU, audio, capture-excluded overlays) with named reasons.
