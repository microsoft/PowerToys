# Prerequisites

Verify these on first run. If any is missing, guide the user through setup before proceeding. Run [scripts/Test-Prerequisites.ps1](../scripts/Test-Prerequisites.ps1) to check all four at once.

## 1. GitHub fork of PowerToys

The user needs a personal fork of `microsoft/PowerToys`.

**Check:**
```powershell
gh repo list --fork --json nameWithOwner | ConvertFrom-Json | Where-Object { $_.nameWithOwner -like "*/PowerToys" }
```

**If missing:**
```powershell
gh repo fork microsoft/PowerToys --clone=false
```

## 2. Local clone with a fork remote

A local git clone with a remote pointing at the user's fork.

**Check:**
```powershell
@("C:\PowerToys", "$env:USERPROFILE\source\repos\PowerToys", "$env:USERPROFILE\git\PowerToys") | Where-Object { Test-Path "$_\.git" }
```

**If missing:**
```powershell
git clone https://github.com/microsoft/PowerToys.git C:\PowerToys
cd C:\PowerToys
gh repo fork microsoft/PowerToys --remote-name fork
```

**If a clone exists but has no fork remote:**
```powershell
cd <CLONE_PATH>
git remote add fork https://github.com/<FORK_OWNER>/PowerToys.git
```

## 3. Copilot code review on the fork

GitHub Copilot code review must be enabled on the user's fork for the loop (Steps 4–6).

**Check:** after creating the first fork PR, request review and confirm `requested_reviewers` is non-empty:
```powershell
gh api repos/<FORK_REPO>/pulls/<N>/requested_reviewers -X POST -f "reviewers[]=copilot-pull-request-reviewer[bot]"
```

**If unavailable** (empty `requested_reviewers` or an error): the user must enable it themselves —

1. Open `https://github.com/<FORK_REPO>/settings` → Copilot → Code review → Enable.
2. Ensure their GitHub account has Copilot access (individual at `https://github.com/settings/copilot`, or org-level via an admin).
3. Re-request the review.

If the user cannot enable it (e.g., no Copilot license), fall back to a local code-review sub-agent as a last resort (see [copilot-review-loop.md](./copilot-review-loop.md#fallback-local-review)).

## 4. Visual Studio build tools

Required for building PowerToys locally.

**Check:**
```powershell
$vsPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath 2>$null
if ($vsPath) { "VS found at: $vsPath" } else { "NOT FOUND" }
```

**If missing:** offer to help the user acquire the tools before falling back. The build needs **Visual Studio 2022 (17.8+)** — or the standalone **Build Tools for Visual Studio 2022** — with the **Desktop development with C++** workload plus the **.NET desktop** workload and the matching **Windows 10/11 SDK**. Offer to install via winget:
```powershell
# Full IDE
winget install --id Microsoft.VisualStudio.2022.Community --override "--add Microsoft.VisualStudio.Workload.NativeDesktop --add Microsoft.VisualStudio.Workload.ManagedDesktop --includeRecommended --quiet"
# ...or just the build tools (no IDE)
winget install --id Microsoft.VisualStudio.2022.BuildTools --override "--add Microsoft.VisualStudio.Workload.VCTools --includeRecommended --quiet"
```
If they decline or it cannot be installed, you can still do Steps 1–6 (mirror, review, fix) and skip Steps 7/7b (build/test).
