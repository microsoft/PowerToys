# Packaged updater + virtual-account multi-runtime prototype

This native prototype validates the following topology:

```text
PowerToys 0.101 bundle ─┐
                       ├─ byte-identical updater 5.0.0.0 MSIX
PowerToys 0.110 bundle ─┘
                                  │
                                  ▼
                manifest-owned PtPuvrUpdater service
                    LocalSystem, packaged process
                                  │
                    transient deployment helper
                 LocalSystem, ordinary process
                                  │
              ┌───────────────────┴───────────────────┐
              ▼                                       ▼
PtPuvrRuntime_<owner A hash>             PtPuvrRuntime_<owner B hash>
NT SERVICE virtual account               NT SERVICE virtual account
runtime track 1.0.0.0                     runtime track 2.0.0.0
WindowsApps package family 1              WindowsApps package family 2
ordinary unpackaged process               ordinary unpackaged process
```

The two owner SIDs represent two users with different PowerToys installations.
The runtime services are dynamically named and do not appear in a signed
manifest.

## Independent versions

| Artifact | Package/File version |
|---|---:|
| Singleton updater | `5.0.0.0` |
| Runtime track 1 | `1.0.0.0` |
| Runtime track 2 | `2.0.0.0` |
| Management protocol | `2` |

`Package.ps1` creates simulated `PowerToys-0.101` and `PowerToys-0.110`
bundles. Both contain the exact same signed updater MSIX. The successful
validation asserts that both copies have the same SHA-256; the generated value
is recorded in `artifacts\validation-result.json`.

The runtime packages intentionally use different package families. Exact
side-by-side versions cannot safely share one package family because staging a
new version can remove the old WindowsApps directory.

## Important findings

### 1. The packaged LocalSystem updater works

The updater is declared by:

```xml
<desktop6:Service
  Name="PtPuvrUpdater"
  StartupType="manual"
  StartAccount="localSystem" />
```

SCM launches it directly from its updater package. Evidence confirmed:

- primary SID `S-1-5-18`;
- package identity present;
- package and PE file version `5.0.0.0`;
- one singleton SCM service/process.

### 2. The tested packaged SYSTEM call path cannot stage the runtime

Calling `PackageManager.StagePackageAsync` inside the packaged LocalSystem
service failed consistently on the validated Windows build:

```text
AppX deployment user: S-1-5-18
SharedAppsRedirect: 0x80070520 (ERROR_NO_SUCH_LOGON_SESSION)
```

`0x80070520` is the specific deployment error
`HRESULT_FROM_WIN32(ERROR_NO_SUCH_LOGON_SESSION)`. The outer AppX deployment
event also reported `0x80073CF9`, and identified `SharedAppsRedirect` as the
failing internal state handler. `SharedAppsRedirect` has no public contract
that explains which caller property caused the failure, so this result does
not prove that all packaged LocalSystem processes are categorically unable to
stage packages.

The prototype now runs a controlled child-process comparison:

1. A default child launched from the updater package retains the updater
   package identity. This control does not call `StagePackageAsync`.
2. A bridge child is created with
   `PROC_THREAD_ATTRIBUTE_DESKTOP_APP_POLICY` and
   `PROCESS_CREATION_DESKTOP_APP_BREAKAWAY_ENABLE_PROCESS_TREE`.
3. The bridge launches a descendant from the same WindowsApps helper path.
   On Windows `10.0.26200.0`, the descendant still retains package identity and
   a real `StagePackageAsync` call still returns `0x80070520`.
4. The same helper bytes copied to a SYSTEM/Administrators-only ProgramData
   cache run without package identity, and the same stage operation succeeds.

This shows that desktop-app breakaway is not a mitigation for this call path
on the tested build. It also corrects the earlier child experiment: the old
`5023` result came from the prototype's own package-identity guard before
Stage was called; it was not an AppX deployment result.

Calling `AddPackageAsync` instead of `StagePackageAsync` also does not avoid
the boundary. The prototype calls `AddPackageAsync` directly inside the
packaged LocalSystem updater, once for each independent runtime package. Both
calls return the same:

```text
0x80070520 (ERROR_NO_SUCH_LOGON_SESSION)
```

AppX event 607 classifies the request as a Deployment Add operation running for
user SID `S-1-5-18`. Events 648/401/404 report the same specific error and
`SharedAppsRedirect`. Event 613 reports `Failed to reach state
SharedAppsRedirect`; `Stage required`, `Machine register`, and `Registration`
costs are all zero. Therefore Add fails in the shared early deployment path
before its stage/register work, rather than bypassing the Stage failure.

This narrows the boundary but still does not reveal the undocumented internal
root cause.

The working design therefore uses one validated mitigation: copy only the
small updater-owned deployment helper to:

```text
%ProgramData%\Microsoft\PowerToys\
  WorkspacesPackagedUpdaterVirtualRuntimePrototype\
  DeploymentHelper\5.0.0.0\PtPuvrDeploymentHelper.exe
```

That directory is SYSTEM/Administrators-only. The transient helper is
unpackaged, runs as LocalSystem, stages/removes runtime MSIX packages, and then
exits. It is not another service. No runtime EXE is copied. This experiment
does not establish that a protected helper copy is the only possible
mitigation; an ordinary unpackaged machine updater service or an external
elevated installer/updater actor would also avoid this tested packaged caller
context.

### 2.1 Unpackaged updater alternative

The earlier `D:\PowerToys-Workspaces-SystemMulti` prototype uses an ordinary
LocalSystem updater service installed under a protected Program Files path.
That updater has no package identity and successfully calls
`StagePackageAsync` directly. Its LocalSystem runtimes are unrelated to the
deployment result; the determining variable is the updater caller context.

This gives three distinct choices:

| Updater | Runtime | Deployment helper | WindowsApps runtime ACL |
|---|---|---|---|
| Packaged LocalSystem | Virtual account | Required by current evidence | Exact package-version service-SID ACE |
| Unpackaged LocalSystem | Virtual account | Not required | Exact package-version service-SID ACE |
| Unpackaged LocalSystem | LocalSystem | Not required | No extra runtime ACE observed |

The unpackaged-updater variants still provide silent runtime updates after the
one-time elevated service installation. Their tradeoff is updater servicing:
the updater binary must live in a protected ordinary directory and needs its
own signature, anti-downgrade, atomic replacement, rollback, and recovery
design. Updating the updater itself still requires an external elevated/SYSTEM
actor or an occasional UAC event.

### 3. Virtual accounts cannot initially execute arbitrary WindowsApps payloads

`CreateService` succeeds with:

```text
NT SERVICE\PtPuvrRuntime_<owner hash>
```

The same virtual account can launch an EXE from a normal filesystem path.
However, direct WindowsApps launch initially failed before process creation:

```text
StartServiceW -> ERROR_ACCESS_DENIED
SCM event 7000 -> Access is denied
```

The package ACL explicitly grants LocalSystem, LocalService, NetworkService,
and package capability SIDs, but not future dynamic service SIDs.

The updater therefore adds an exact read/execute ACE for the runtime's service
SID to one package-full-name/version directory and its exact
`PtPuvrRuntime.exe` after staging and before starting the service. For example:

```text
C:\Program Files\WindowsApps\
  Microsoft.PowerToys.WsPuvr.Runtime1_1.0.0.0_x64__...\
```

It does not modify the WindowsApps root, all versions of a package family, or
unrelated packages. The directory ACE is inheritable within that one package
directory, and the EXE also receives an explicit ACE. A new package version
creates a new full-name directory, so the updater must repeat the operation
for every staged version.

This is the largest productization risk. The prototype proves the mechanism,
but changing ACLs beneath WindowsApps needs an explicit Windows platform/support
review before shipping. If such ACL changes are rejected, the exact
`virtual account + direct WindowsApps + no runtime copy` combination is NO-GO.

## Successful validation

Validated on Windows `10.0.26200.0` on 2026-08-19.

- Release x64 build completed with compiler warnings treated as errors.
- Runtime 1/2 and updater MSIX packages were packed, signed, and verified.
- Simulated PowerToys 0.101/0.110 bundles carried byte-identical updater MSIX.
- One manifest-owned packaged LocalSystem updater ran from WindowsApps.
- Two owner SIDs produced two dynamic runtime services.
- Both SCM accounts were their exact `NT SERVICE\<service>` virtual accounts.
- Both ran concurrently in Session 0 with distinct PIDs and primary SIDs.
- Each primary token SID exactly equaled its service SID.
- Owner A ran runtime/file/package version `1.0.0.0`.
- Owner B ran runtime/file/package version `2.0.0.0`.
- Runtime package families and WindowsApps directories were different.
- Both runtime processes reported `APPMODEL_ERROR_NO_PACKAGE`.
- The default updater child retained package identity.
- The desktop-app-breakaway bridge and descendant both retained package
  identity.
- The breakaway descendant's real Stage call returned exact
  `0x80070520`.
- Direct packaged-updater `AddPackageAsync` calls for both runtime families
  returned exact `0x80070520` before stage or registration work.
- The protected-cache helper had no package identity and staged the same
  runtime source successfully.
- No runtime EXE existed in the ProgramData store.
- Exact teardown removed all services, packages, stores, helper cache, and test
  certificates.

Machine-readable evidence is written to:

```text
artifacts\validation-result.json
```

## Build and run

Run from an elevated PowerShell:

```powershell
.\Build.ps1 -Configuration Release -Clean
.\Package.ps1 -Configuration Release -SkipBuild -TrustMachine
.\Lifecycle.ps1 -Verb validate -Configuration Release
.\Teardown.ps1 -Configuration Release
```

`Lifecycle.ps1 -Verb validate` always performs managed cleanup in `finally`.

## Portable cross-machine validation

Create a minimal ZIP containing the signed MSIX artifacts, controller, metadata,
hash manifest, temporary test certificate, and one-command runner:

```powershell
.\Export-PortableArtifacts.ps1 -DestinationDirectory C:\Temp
```

On the other x64 Windows 11 machine, extract the ZIP and run from an elevated
PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\Run-PortableValidation.ps1
```

The runner resolves artifacts relative to the extraction directory, validates
their SHA-256 hashes, temporarily trusts only the bundled test certificate,
runs the full two-runtime validation, and guarantees teardown. It preserves an
identical certificate if that certificate was already trusted before the run.
The result is written to `artifacts\validation-result.json`; retain it and the
console output when comparing different Windows builds.

## Machine-wide lifecycle validation

Additional validation on Windows `10.0.26200.0` on 2026-08-24 tested whether
the updater MSIX could replace the external trusted bootstrap and servicing
actor, rather than only protecting the updater bytes.

### Passed

- `Add-AppxProvisionedPackage -Online` performed a real machine provisioning
  operation and created the fixed `PtPuvrUpdater` LocalSystem service.
- Two temporary standard users independently registered the same provisioned
  updater package. Removing user A's registration preserved user B's
  registration, the machine provisioning record, and the running singleton
  service.
- An elevated machine deployment operation upgraded updater package
  `5.0.0.0` to `6.0.0.0` while v5 was running. Windows stopped v5, changed the
  SCM image path to v6, and left the service stopped for the external actor to
  health-check and restart.
- The v6 test package intentionally contained v5 bytes. The updater's
  exact-version self-check rejected it with service exit 13. An elevated
  `ForceUpdateFromAnyVersion` operation then restored the provisioned v5
  package, the v5 SCM path, and a healthy running updater.
- Removing the updater package did not remove or stop the two independently
  created runtime services. Re-provisioning v5 rebuilt the package-owned
  updater service asynchronously in about 2.8 seconds, and the repaired updater
  reconciled both still-running runtimes.

`MachineMultiUser.ps1` is the repeatable two-standard-user registration and
one-user-removal harness. Machine-readable results are written under
`artifacts\machine-multi-user-result.json` and
`artifacts\machine-servicing\`.

### Blocking result

A standard user could not update v5 to v6 while another registered user was
logged in. Deployment returned outer error `0x80073D19` with the specific
error:

```text
0x80073D25
Packages with singleton components will fail if other users are logged in
and have the package installed.
```

The same package update succeeded when invoked by an elevated machine actor.
Therefore AppX supplies signature validation, protected bytes, transactional
package replacement, and SCM path ownership, but it does not create a
non-privileged cross-user servicing authority for a package containing this
singleton service.

Machine provisioning also requires administrator authority, and updater
replacement terminates the updater before health-check/restart. Consequently
the updater cannot be its own only bootstrap, update, rollback, or repair
actor. A machine installer, enterprise deployment system, or occasional
explicit elevation is still required.

## Product gaps

This is a topology/mechanism prototype, not production updater code:

- the named-pipe client is currently restricted to administrators;
- production requests need owner/install authorization, quotas, and replay
  protection;
- runtime source MSIX paths need signature, exact identity, anti-downgrade, and
  TOCTOU-safe protected caching;
- updater inventory needs durable install leases, transaction recovery, and
  last-uninstall reconciliation;
- WindowsApps ACL mutation needs platform support confirmation;
- the `SharedAppsRedirect` failure's internal root cause remains undocumented;
- `AddPackageAsync` shares the same failing early deployment path and is not a
  mitigation on the validated build;
- machine-wide updater install, cross-user update, rollback, and last-removal
  still need an external elevated actor;
- package removal does not understand independently created runtime leases, so
  product uninstall must refuse updater retirement until protected inventory
  proves that no installations or runtimes still depend on it.

## Verdict

**Prototype GO:** different PowerToys versions can share one independently
versioned packaged LocalSystem updater while two simulated users concurrently
run different independently versioned virtual-account runtimes directly from
different WindowsApps package families.

**Conditional product GO:** the topology depends on a small unpackaged
deployment helper copy and dynamic service-SID RX ACEs on each exact runtime
package-version directory. Desktop-app breakaway did not remove the helper
requirement on the tested build, and replacing Stage with Add produces the same
error. Without approval for those two conditions, the cleanest equivalent is
an ordinary unpackaged LocalSystem updater with virtual-account runtimes; use
LocalSystem runtimes only if machine-compromise blast radius is acceptable.

**Bootstrap/self-update NO-GO under the no-external-actor requirement:** a
machine-wide MSIX packaged updater cannot by itself replace a trusted machine
bootstrap and servicing actor. The existing packaged-updater mechanism remains
usable only when the product accepts an elevated installer/enterprise actor for
first provisioning and rare updater-version changes.
