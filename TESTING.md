# Workspaces v6 (Approach 4) — testing the settings service

The tamper-resistant settings service (`PTSettingsSvc_<SID>`) and its protected
store are packaged by the build pipeline from:

```
src/modules/Workspaces/WorkspacesSettingsService/build-msix.ps1
```

which stages `package/AppxManifest.xml` + the built `PowerToys.PTSettingsSvc.exe`
into a signed MSIX (see `.pipelines/v2/templates/steps-build-installer-vnext.yml`
and Design-v6-Final.md §12.1).

The prototype developer/security-verification harness (service setup and the
end-to-end 9-step security suite: liveness, caller allow-list, path-prefix, DACL
hardness, round-trip, NotFound, per-user DACL, non-user-owner, and the core
Medium-IL tamper-rejection check) is preserved on the
`workspaces-eop-v6-devtools-archive` branch under
`src/modules/Workspaces/WorkspacesSettingsService/devtools/`.

For full installed-build (MSIX) lifecycle testing — fresh install, upgrade,
repair, uninstall — use the artifact inspector `Inspect-PtSettingsSvc.ps1` and
the lifecycle checklist kept with the design notes.
