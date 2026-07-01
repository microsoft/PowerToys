# Scenario C — Active (unmerged) PR validation

**Use when:** the task asks you to validate a PR whose code is **not in the installed build** —
e.g. "verify PR #N", "build it and check the fix", an open/unmerged PR under review. Here you
**build the affected module and run your own bits**, then drive them with the same engine.

This is the only scenario whose discipline is **inverted** from A/B: building & deploying unreleased
code is the entire point, not a violation. The immutability rule still protects *unrelated*
installed bits you didn't build.

## Bits under test

```
BITS: local build of <Module> @ <sha/branch>, sideloaded
```

Before the first drive command, prove the running module is **your build**, not the shipped one
(path under the build output / a dev version string), and record it in the report header. A run
that accidentally drove the installed binary is invalid (see `index.md` → bits contract).

---

## Step 0 — Read the PR

```powershell
gh pr view <N> --repo microsoft/PowerToys --json title,body,files,headRefName,headRepositoryOwner,state
gh pr diff <N> --repo microsoft/PowerToys
```

Derive the observable claim(s) → 1–3 checklist items (same as Scenario B Step 1). From
`--json files`, identify the **affected project(s)** (`.csproj` / `.vcxproj`) so you build only what
changed, not the whole repo.

## Step 1 — Get the code + build

**Preferred (repo-portable):** materialize the PR's branch as an isolated worktree with the
in-repo helper, then build only the affected project (mirrors `AGENTS.md` → Build):

```powershell
cd <PT_REPO>
gh pr checkout <N>                                          # creates/locates the PR branch locally
tools\build\New-WorktreeFromBranch.ps1 -Branch <pr-branch>  # in-repo, isolated worktree (also used by FixIssue.agent.md)
git submodule update --init --recursive                     # once
tools\build\build-essentials.cmd                            # first build / NuGet restore
# then build ONLY the affected project folder:
tools\build\build.ps1 -Platform x64 -Configuration Release  # run from the changed .csproj/.vcxproj dir
```

**Exit code 0 = success (treat as absolute).** On non-zero, read
`build.<config>.<platform>.errors.log` next to the project, fix or report. If the environment lacks
the toolchain (VS 2022 17.4+/2026, Windows SDK) and the build cannot complete, the verdict is
**BLOCKED** (environmental: `BLK-ENV`/INCONCLUSIVE) — never PASS on an unbuilt PR.

## Step 2 — Deploy / run your build (by module type)

Do **not** copy build output into `C:\Program Files\PowerToys\` or the Store package dir. Run the
built bits from the build output instead.

### Unpackaged module (most: FancyZones, PowerToys Run, ColorPicker, Peek, KBM, …)

The runner loads module DLLs from its own folder, so just run the **freshly built runner** from the
build output (this is the repo's own F5 dev path):

```powershell
Get-Process PowerToys -EA SilentlyContinue | Stop-Process -Force      # stop the installed runner
Start-Process "<PT_REPO>\x64\Release\PowerToys.exe"                   # your build's runner + module DLLs
# confirm you're on YOUR bits:
(Get-Process PowerToys | Select-Object -First 1).Path                 # must point under the build output
```

### Packaged module (Command Palette / CmdPal — MSIX)

Enable Developer Mode, then register your build's loose layout (or install the produced `.msix`).
Because the shipped package version may collide, remove it first and restore on cleanup:

```powershell
$shipped = Get-AppxPackage Microsoft.CommandPalette
$shipped | Remove-AppxPackage                                         # remove shipped (restore later)
Add-AppxPackage -Register "<build-output>\AppxManifest.xml"           # register your loose build
(Get-AppxPackage Microsoft.CommandPalette).InstallLocation            # must point under the build output
```

## Step 3 — Drive + classify

Same engine as A/B: pick the `SKILL.md` §2 bucket per item, drive, classify with `SKILL.md` §3.
The **live-drive floor** applies (≥4 `winapp ui …` rows + ≥1 post-state screenshot for any
user-visible surface). Because you control the build, you can also exercise the **failing** state
the PR fixes (e.g. revert-and-rebuild, or compare against the installed shipped build) to make a
PASS decisive rather than merely "the fix is present".

## Step 4 — Restore the machine to the shipped build (cleanup)

```powershell
Get-Process PowerToys -EA SilentlyContinue | Stop-Process -Force      # stop your built runner
Start-Process "$env:LOCALAPPDATA\PowerToys\PowerToys.exe"             # or Program Files install
# Packaged: re-add the shipped package
Add-AppxPackage -DisableDevelopmentMode -Register `
  "C:\Program Files\WindowsApps\<shipped>\AppxManifest.xml"           # or reinstall the shipped .msix
```

Leave the box on the shipped build; disclose any residue in the report.

## Report

`{Module}-PR{Number}/report.md` with the `BITS: local build …` header (incl. the sha/branch and
the proof-of-your-bits path), the per-item table (`../reporting-format.md`), the build summary
(exit code, project built), and a §G retrospective. A PR with a user-visible surface and zero
`winapp ui …` rows is not validated.
