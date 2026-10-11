# Protected Storage implementation

This feature implements the MSI-carrier design for per-owner Workspaces storage.
It is developed in a separate `feature/workspaces-protected-storage-msi` worktree,
based on `44b9a329230252bbcaf2fca3474b683c78f4a14f`, so the previously validated
standalone demo and unrelated changes in the primary checkout remain untouched.

## Source map

| Area | Implementation |
|---|---|
| Framing, identities, signed catalogs, policy and update transactions | `src\common\ProtectedStorage\ProtectedStorage.Common` |
| Native data/control clients | `src\common\ProtectedStorage\ProtectedStorage.Client` |
| Managed transport, typed API and explicit setup/retry entry | `src\common\ProtectedStorage\ProtectedStorage.Client.Managed` |
| VA SCM host and update coordinator | `src\common\ProtectedStorage\ProtectedStorage.Bootstrap` |
| Generic durable Blob store, CAS/initialization, transient previews and drain | `src\common\ProtectedStorage\ProtectedStorage.Runtime` |
| Owner-bound setup, temporary broker, lifecycle and MSI action | Sibling `ProtectedStorage.Setup`, `.ProvisionBroker`, `.Lifecycle`, `.MsiAction` |
| Carrier authoring, signed build graph and publication | `installer\PowerToysProtectedStorage` |
| Module validation, migration, repository, editor/preview and arranger handoff | `src\modules\Workspaces` |
| Normal-owner update synchronization | `src\common\updating\protectedStorageUpdate.*`, `src\Update\PowerToys.Update.cpp`, `src\runner\main.cpp` |
| Main package integration | `installer\PowerToysSetupVNext\ProtectedStorage.wxs` and release prerequisite target |

The root solution includes the new projects. `ProtectedStorage.Integration.slnf`
selects the changed graph while preserving the main solution's existing
native/managed platform mappings.

## Local build and unit tests

From the repository root in PowerShell:

```powershell
.\tools\build\build-protected-storage.ps1 -Platform x64 -Configuration Debug -RunTests
.\tools\build\build-protected-storage.ps1 -Platform x64 -Configuration Release -RunTests
```

The script uses the repository build helpers, builds the affected production
and test projects, runs native and managed unit tests, then compiles carrier
and main-installer authoring fixtures. Fixtures are non-installable and removed
after validation. No command above installs a live service, imports a certificate
or generates a production signing key.

Native binaries are under `<Platform>\<Configuration>\ProtectedStorage`.
The managed test runner is under `ProtectedStorageTests`; Visual Studio test
results are written to `TestResults\ProtectedStorage`.

ARM64:

```powershell
.\tools\build\build-protected-storage.ps1 -Platform ARM64 -Configuration Release
```

An x64 cross-build does not execute ARM64 tests. Run them on an appropriate
acceptance machine before release.

## Isolated local deployment and cleanup

Do not use ADO as the first functional test loop. Real native/managed named-pipe
tests now run in the regular unit suite, including delayed/fragmented reads,
error replies, a stalled client and service-stop cancellation. They exercise the
same bounded response-completion primitive used by Runtime.

For a live development loop, use an elevated PowerShell terminal in the real
test user's desktop session:

```powershell
.\tools\build\Test-ProtectedStorageLocal.ps1 -Action Test
.\tools\build\Test-ProtectedStorageLocal.ps1 -Action Editor
.\tools\build\Test-ProtectedStorageLocal.ps1 -Action Inspect
```

`Test` builds, cleans the previous **local-test** deployment, deploys the real
Bootstrap/Runtime as a VA service, and launches the smoke client under the
same user's verified non-elevated Explorer token. It checks first migration,
matching legacy-file deletion, save/reopen, previews, cross-process persistence
and rejection of direct owner writes, then opens and verifies the real editor
main window before closing that test window. `Editor` launches the locally built
editor with an isolated mutex, logs and workspace directory. A visible
`[LOCAL TEST - isolated data]` title identifies this editor.
`-SkipBuild` repeats cleanup/deployment/testing with the existing bundle.
`-Action Build` only creates artifacts; `-Action Deploy` deploys an existing
bundle without resetting an existing instance.

These non-shipping projects live in
`src\common\ProtectedStorage\tests\LocalDeployment`, outside the shipping solution.
Artifacts and JSON results are in
`artifacts\ProtectedStorageLocalTest\<Platform>\<Configuration>`.
The namespace is fixed at build time to `PowerToysProtectedStorageLocalTest`,
including service name, Code/Data/Policy/Installer roots and pipe endpoints.
The managed test assembly also redirects legacy migration and editor data to
`%LocalAppData%\PowerToysProtectedStorageLocalTest\Workspaces`; it must never
migrate or remove the real user's legacy workspace file.

Each build creates an in-memory seven-day signing certificate. Its private key
is disposed after signing; no key or root is installed in a certificate store.
Only the separately linked test verifier accepts that exact public certificate
for detached CMS. It verifies the signature and still uses the actual manifest,
catalog hashes, process identity, VA, ACL and storage code. It explicitly refuses
Authenticode installer authorization. Production keeps
`microsoft-production-v1`, and the local build verifies that the production
verifier rejects its test signature. There is no runtime production bypass flag.

This is a storage/editor development deployment, **not** qualification of the
production MSI, UAC bootstrap, app launching/arranger integration, ESRP,
timestamps or revocation. Those still require the real release acceptance
matrix. Native module launchers are not deployed by this local editor harness.

Cleanup:

```powershell
# Preview, then remove this owner's disposable local-test service/code/data.
.\tools\build\Clear-ProtectedStorageTestEnvironment.ps1 -WhatIf
.\tools\build\Clear-ProtectedStorageTestEnvironment.ps1

# Separately, from the ORIGINAL USER'S NON-ELEVATED terminal:
# explicitly remove the installed release's instance via signed Setup, keeping data.
.\tools\build\Clear-ProtectedStorageTestEnvironment.ps1 -InstalledRelease
```

For a machine installation, pass its actual `-InstallRoot`. Installed-release
cleanup does not uninstall the main PowerToys product and does not purge business
data. No cleanup command searches for and deletes all `PowerToys*` services.
Historical demo/prototype services remain untouched.

The local lifecycle checks the exact service image/account and fixed owner
paths, rejects reparse trees, waits for the exact host to exit, and reports
residuals. A still-loaded VA profile is recorded as `profileCleanupPending`,
not force-unloaded or falsely reported removed. Test Code/Data are reset
independently, so the next test can have fresh storage while reusing that
same isolated identity. Temporary SYSTEM lifecycle services run only an
admin-protected copy of the test helper and are deleted after completion.
Unknown outcomes retain their evidence; they must not be blindly replayed.

## Release packaging is a separate, authenticated step

**Prior CI qualification:** ADO run `158436898` for `0.101.3000.0` failed
under the former single-leaf policy and produced no main installers. The user
approved replacing that policy on 2026-09-24, without a manual leaf list.
The replacement is `microsoft-production-v1`: machine-context code-signing
trust, verified Microsoft publisher/application-root policy and authenticated
timestamps. [ADO run 158510076](https://microsoft.visualstudio.com/Dart/_build/results?buildId=158510076)
subsequently succeeded for `0.101.3000.0` on source
`331f6d8ca5e45ec45453f5e01d21679b3e4f7a24`, producing signed per-user and
per-machine installers for x64 and ARM64. Both carrier releases and all four
installer hashes/signatures were independently reverified after download.
Full carrier ICE ran in CI with zero warnings/errors. Local WIX1105 means ICE
was not executed, not a pass; CI treats that condition as an error.
Installed acceptance remains separate and has not been established by this build.

The local Setup/Broker/Lifecycle/MsiAction compilation outputs deliberately do
not contain finalized release resources and must not be treated as installers.
They reject privileged execution when the required signed resources are absent.

After signing the final Workspaces consumers and the actual ModuleServices host,
create the inventory without authorizing arbitrary executables:

```powershell
.\tools\build\New-ProtectedStorageClientInventory.ps1 `
    -Platform x64 -Configuration Release `
    -ModuleServicesHostPath '<signed output>\Microsoft.CmdPal.Ext.PowerToys.exe' `
    -OutputPath '<new release inventory>.json'
```

Then follow `installer\PowerToysProtectedStorage\README.md` to run
`Build-Carrier.ps1 -Package` with an approved existing signing certificate,
timestamp URL, release version and client inventory.
The release build verifies and publishes only the finalized self-contained
`PowerToys.ProtectedStorageSetup.exe` beside the main PowerToys executable.
It does not publish loose Bootstrap/Runtime update payloads.

The signed ADO release pipeline instead uses the same builder's external
`ReleaseStage` sequence, interleaving ESRP Authenticode and `Pkcs7DetachedSign`
operations. It needs no local product private key and does not derive trust
from whichever certificate happened to sign Bootstrap. Every returned signature
must satisfy the fixed production policy. Detached CMS also carries a verified
RFC3161 timestamp, not a self-asserted signing time. The host-native, non-shipping
TrustVerifier uses the same implementation as the runtime before resources are
embedded. The complete contract and stage ordering are in the carrier README.

Main installer packaging requires `ProtectedStorageTrustPolicy` and
the verified published release. There is no unsigned bypass or automatic test
certificate fallback. The main MSI embeds the signed Lifecycle resource for
machine-only final-uninstall cleanup; it never runs a helper selected from a
user-writable installation as SYSTEM or launches nested carrier MSI.

`workspaces.arranger` is the sole identity-only arranger catalog role.
It has no Blob/transient permissions. All server, installer inventory and
consumer references are checked together by
`Test-ProtectedStorageMainInstaller.ps1`.

## Behavior and limits

- Workspaces owns schema/business validation; the service handles authorization,
  initialization/CAS, persistence and bounded resource usage.
- Runtime `GetCapabilities` and control `Status` expose only owner-bound health
  and signed public metadata. They do not authorize a Blob read or mutation.
- Migration validates the whole input, commits durably, then deletes only the
  matching legacy source. Initialized data never silently falls back to old
  `workspaces.json` or `temp-workspaces.json`.
- Ordinary saves and preview updates use revision checks and retain operation
  IDs across uncertain outcomes. Retry does not repeat application launches.
- The elevated arranger receives an immutable, signed-peer-bound placement
  handoff rather than reading another user's store or profile.
- Routine update synchronization never elevates or creates a first service.
  Main product update success is independent of owner-storage failure.
- Default removal preserves data/initialization state. Explicit purge records
  import suppression, so old legacy files cannot silently restore purged data.
- Machine cleanup is best effort. Residual per-user MSI registrations/profiles
  are reported rather than force-removed or claimed clean.
- Identified-but-unusable linked tokens fail with `OwnerContextRequired`;
  the service never weakens impersonation or accepts an elevated token as a
  substitute for the required normal owner context.
- Runner-to-Editor, elevated direct Launcher, and maintenance synchronization
  use a separate explicit same-owner process launch boundary. It validates the
  real shell SID/session/medium primary token, starts the child suspended with
  its owner environment, verifies that child's identity, and only then resumes.
  It never selects another user's shell or promotes an identification-only token.
- Q02's fail-closed first-use policy is implemented as the current design
  direction, but final scope/UX still requires PM approval.

The accepted trusted-writer model does not detect compromise of an already
authorized PowerToys process. File ACLs, mapped-image/catalog checks and UAC
must not be presented as proof of human intent for such a caller.

## Acceptance

The complete functional matrix is
[workspaces-protected-storage-acceptance.md](../workspaces-protected-storage-acceptance.md).
Builds, unit fixtures and authoring checks do not establish real A/B provisioning,
OTS consent UX, signed package deployment, profile cleanup, physical power-loss
recovery, or all supported machine policies. Record these separately.

The approved design and decision history are retained alongside this README.
The executable wire/API contract is maintained in
`src\common\ProtectedStorage\ProtectedStorage.Common\Protocol.md`; where class
names or message details evolved during implementation, that contract and its
cross-language tests take precedence over illustrative draft signatures.

### Recorded local validation

The focused x64 Debug and Release runs completed with zero build errors.
The expanded Debug graph also builds the real CmdPal consumer, whose existing
native extension project reports LNK4075 (`/INCREMENTAL` ignored with `/PROFILE`);
this warning is not suppressed or treated as a new protected-storage failure.
Each configuration passed:

- 3,010 native assertions and 20,000 deterministic framing mutations.
- 4,128 installer contract checks.
- 38 managed tests.
- 100 Workspaces/updater tests through Visual Studio's test runner after adding
  same-owner launch-boundary and outer-uninstall policy checks.
- Carrier WiX authoring, both main-installer scope fragments, catalog-role
  consistency, outer-bundle payload authoring, and process-local mocked
  publication/tamper checks.

Logs are under `artifacts\ProtectedStorageVerification\final-debug.log` and
`final-release.log`; test results are separated by platform/configuration under
`TestResults\ProtectedStorage`. Native ARM64 component builds also passed, but
ARM64 execution and the full ARM64 consumer graph are not qualified by those
component cross-builds.

**Not a release-completion claim:** no approved signing inputs were used to
finalize a deployable package, no production-named service was installed, and
live A/B/OTS scenarios were not run. Unknown commit outcomes remain explicit
recovery states rather than a claim of complete automatic reconciliation.
Final VA-profile/orphan-stage cleanup and offline-user registration removal may
remain visible residuals under the best-effort policy.

A standalone diagnostic child also verified the actual elevated-to-medium
same-owner/same-session launch on the local interactive desktop. This did not
run or install product code and does not substitute for signed GUI/OTS
acceptance. The per-user Burn callback runs owner removal after its main MSI
execution finishes, using the bundle's verified retained Setup payload.
It excludes related-bundle upgrades, preserves data, and logs cancellation or
residual failures without rolling back the main uninstall. The per-machine
main-MSI cleanup uses the separate SYSTEM-only Lifecycle resource.
Directly uninstalling the inner per-user MSI without its outer bundle does not
run that callback; explicit owner Setup removal remains necessary in that path.

### Remaining implementation work versus unexecuted acceptance

These are actual implementation gaps, not tests that can simply be marked
pending on otherwise complete behavior:

- Ambiguous post-commit MSI/VA outcomes are retained and fail closed. Complete
  automatic reconciliation across every such outcome is not implemented.

Separate from those gaps, signed release creation and deployed A/B/OTS,
power-loss, retention/reinstall and policy qualification require the approved
signing inputs and an acceptance machine. Do not merge/release this feature
branch as production-ready based only on its successful local builds.
