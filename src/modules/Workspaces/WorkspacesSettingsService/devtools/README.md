# WorkspacesSettingsService — dev validation tooling

These scripts stand up and exercise `PTSettingsSvc` locally to validate the v6
tamper-resistant settings design. They are **developer tooling, not product
code** and are not part of the shipping build or the installer.

| File | Purpose |
| --- | --- |
| `setup-ptsettingssvc.ps1` | Registers the service, creates the PROTECTED `%ProgramData%` store, and a fake admin-locked install folder. Run elevated. |
| `verify-prototype.ps1` | Runs the 9-step end-to-end security suite (liveness, caller allow-list, path-prefix, DACL hardness, round-trip, NotFound, per-user DACL, non-user owner, non-elevated write/delete rejection). Does not need elevation. |
| `SaferModify.cs` | Helper compiled on demand by step 9 to obtain a Medium-IL (non-elevated) SAFER token and attempt a tamper write/delete. |

## Usage

```powershell
# 1. Build the service + smoke test (Debug|x64) first.
# 2. Elevated:
pwsh -File .\setup-ptsettingssvc.ps1
# 3. Non-elevated:
pwsh -File .\verify-prototype.ps1
```

`RepoRoot` is derived automatically from the script location; pass `-RepoRoot`
to override. Requires PowerShell 7+ (the suite uses the ternary operator).
