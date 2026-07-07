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

> **Partial build ⇒ missing-module dialogs are EXPECTED (READ THIS before Step 2).** A
> module-specific validation should build **only the affected project** (`build-essentials` builds
> only runner + settings; `build.ps1` builds only the current project — per
> `tools\build\BUILD-GUIDELINES.md` and `tools\build\Worktree-Guidelines.md`). **Building the whole
> solution just to populate every module is NOT preferred** (slow, and unrelated to the PR). The
> consequence is that the build-output runner loads **every** module from its own folder and pops a
> modal **"Failed to load PowerToys.<Module>ModuleInterface.dll"** dialog for each un-built one —
> **this is normal and fair for a partial build, not a build failure.** Just handle the dialogs per
> Step 2 — **dismiss them** (option A); only if that's blocked, fall back to a full solution build
> (option B). Do **not** overlay your built bits onto the install.

## Step 2 — Deploy / run your build (by module type)

Do **not** copy build output into `C:\Program Files\PowerToys\` or the Store package dir. Run the
built bits from the build output instead.

### Unpackaged module (most: FancyZones, PowerToys Run, ColorPicker, Peek, KBM, …)

The runner loads module DLLs from its own folder, so run the **freshly built runner** from the
build output (this is the repo's own F5 dev path). A module-specific build only produced *your*
module, so the runner will raise the expected "Failed to load …ModuleInterface.dll" dialogs for the
un-built modules (see the callout above). Handle them with **one** of these — in preference order:

- **(A) Dismiss the expected dialogs (preferred — keep the build partial, 100 % your bits).** Run
  the build-output runner as-is and close each "Failed to load …ModuleInterface.dll" dialog. They
  are benign — the module you actually built still loads and works. This is the normal outcome of a
  targeted module build; don't try to eliminate them. **These are native Win32 `#32770` message
  boxes — `winapp ui invoke OK` does NOT reliably close them**; instead `PostMessage`/`SendMessage`
  `WM_CLOSE (0x0010)` to each `#32770` window (enumerate via `EnumWindows`+`GetClassName`), or send
  Enter (OK is the default button) to the focused dialog. Loop until none remain, then drive.
- **(B) Full solution build (fallback — only if (A) is blocked).** If the dialogs cannot be
  dismissed or keep re-appearing and racing your module's activation, build the whole solution so
  every `*ModuleInterface.dll` lands in `x64\Release` and the runner starts clean. This is slower
  and unrelated to the PR, so use it **only when (A) fails** — never as the default.

```powershell
Get-Process PowerToys -EA SilentlyContinue | Stop-Process -Force      # stop the installed runner
Start-Process "<PT_REPO>\x64\Release\PowerToys.exe"                   # your build's runner + module DLLs
# confirm you're on YOUR bits:
(Get-Process PowerToys | Select-Object -First 1).Path                 # must point under the build output
```

> **ANTI-PATTERN — do NOT overlay a partial build onto the installed layout.** The tempting shortcut
> "copy just my built `Foo.dll` into `%LOCALAPPDATA%\PowerToys` (or Program Files) and run the
> *installed* runner" **violates the rule above and silently corrupts the test**:
> 1. It mixes your `0.0.1.0` files with the shipped `0.100.x` install (you're no longer testing a
>    coherent build), and
> 2. For **WinUI modules you must copy the module's *complete, consistent* fileset** — the managed
>    `.dll` **plus its `.pri`** (compiled-XAML/resource index) **plus dependent `*.UI.Controls.dll`/`.pri`
>    and `*.UI.Lib.dll`**. Copying the `.dll` alone means a `0.0.1.0` assembly resolves XAML from the
>    stale `0.100.x` `.pri` → the page **renders blank / the new control never appears** (observed:
>    the Advanced Paste settings page went empty because `PowerToys.Settings.pri` +
>    `PowerToys.Settings.UI.Controls.dll` were left at the installed version).
> Prefer option **(A)** (dismiss the expected dialogs); if it's blocked, fall back to option **(B)**
> (full solution build) — never overlay your bits onto the install.

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
