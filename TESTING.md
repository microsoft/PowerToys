# Workspaces v6 (Approach 4) — testing the settings service

The tamper-resistant settings service (`PTSettingsSvc_<SID>`) and its protected
store are validated with the developer tooling under:

```
src/modules/Workspaces/WorkspacesSettingsService/devtools/
```

See [`devtools/README.md`](src/modules/Workspaces/WorkspacesSettingsService/devtools/README.md)
for the authoritative, up-to-date steps. In short:

1. Build the service + smoke test (`Debug|x64`).
2. Elevated PowerShell 7+: `pwsh -File .\setup-ptsettingssvc.ps1`
   — registers the per-user virtual-account service `NT SERVICE\PTSettingsSvc_<SID>`
   and creates the PROTECTED store at
   `%ProgramData%\Microsoft\PowerToys\Settings\<SID>\`.
3. Non-elevated PowerShell 7+: `pwsh -File .\verify-prototype.ps1`
   — runs the 9-step end-to-end security suite (liveness, caller allow-list,
   path-prefix, DACL hardness, round-trip, NotFound, per-user DACL, non-user
   owner, and the core check: a Medium-IL non-elevated write/delete is rejected).

For full installed-build (MSIX) lifecycle testing — fresh install, upgrade,
repair, uninstall — use the artifact inspector `Inspect-PtSettingsSvc.ps1` and
the lifecycle checklist kept with the design notes.
