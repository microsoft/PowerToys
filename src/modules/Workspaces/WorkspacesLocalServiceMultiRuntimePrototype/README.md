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

### 2. A packaged SYSTEM process cannot stage the runtime directly

Calling `PackageManager.StagePackageAsync` inside the packaged LocalSystem
service failed consistently:

```text
AppX deployment user: S-1-5-18
SharedAppsRedirect: 0x80070520 (ERROR_NO_SUCH_LOGON_SESSION)
```

The same API was already known to work from an ordinary LocalSystem service.
A child launched directly from the updater's WindowsApps directory inherited
the updater package identity and hit the same boundary.

The working design copies only a small updater-owned deployment helper to:

```text
%ProgramData%\Microsoft\PowerToys\
  WorkspacesPackagedUpdaterVirtualRuntimePrototype\
  DeploymentHelper\5.0.0.0\PtPuvrDeploymentHelper.exe
```

That directory is SYSTEM/Administrators-only. The transient helper is
unpackaged, runs as LocalSystem, stages/removes runtime MSIX packages, and then
exits. It is not another service. No runtime EXE is copied.

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
SID to that runtime package directory and `PtPuvrRuntime.exe` after staging and
before starting the service. With that ACE, both virtual-account services run
successfully. The updater must repeat this operation for every newly staged
package version.

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
- updater self-update still needs an external elevated actor, but can remain a
  rare `MinimumUpdaterVersion` event.

## Verdict

**Prototype GO:** different PowerToys versions can share one independently
versioned packaged LocalSystem updater while two simulated users concurrently
run different independently versioned virtual-account runtimes directly from
different WindowsApps package families.

**Conditional product GO:** the topology depends on a small unpackaged
deployment helper copy and dynamic service-SID RX ACEs on each runtime package.
Without approval for those two conditions, use an ordinary machine updater
service, LocalSystem/LocalService runtimes, or a protected runtime payload
outside WindowsApps instead.
