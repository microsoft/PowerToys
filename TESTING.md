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

## Local developer experience (F5 / non-installer builds)

A local build is **not signed** and does **not** produce the service MSIX, so the
production provisioning path (signature-verified `Add-AppxPackage` of a signed
package staged by the installer) does not apply. Two `#if DEBUG`-gated
accommodations — physically absent from Release — make Workspaces "just work"
from a local **Debug** build:

1. **Provisioning** (`ServiceProvisioner.EnsureProvisioned`, C#): when the signed
   MSIX is missing, a Debug build registers the service **directly** from the
   just-built `PowerToys.PTSettingsSvc.exe` via its own elevated `--register <SID>`
   (no MSIX, no self-signed cert, no manual deploy script). The exe still
   self-copies into the admin-only `%ProgramData%\...\SettingsSvcBin` and hardens
   the store, so the protected boundary is identical to production. Just build,
   open the Workspaces editor, and accept the one-time UAC prompt.

2. **Caller authorization** (`CallerAuth.cpp`, C++): production requires every
   caller to be Microsoft-signed *and* version-matched to the service. In a Debug
   build the signature requirement is waived (`#ifdef _DEBUG`); the exact
   **version match still applies**, which a locally built caller + service satisfy
   because they share the same version.

Implications:

- **Build Debug** for local Workspaces work — both accommodations are Debug-only.
- A local **Release** build behaves like production: the unsigned caller is
  rejected and no MSIX exists to provision from, so the protected store is
  unreachable. To exercise the protected path in Release, use a real signed
  installer build.
- To tear down a locally registered service, run the built exe elevated with
  `--unregister <SID>`.
