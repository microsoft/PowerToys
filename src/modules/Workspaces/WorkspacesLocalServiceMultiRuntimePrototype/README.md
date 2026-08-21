# MSIX-delivered raw updater + virtual-account multi-runtime prototype

This native prototype validates a deliberately hybrid topology:

```text
PowerToys 0.101 bundle ─┐
                       ├─ byte-identical signed updater MSIX 5.0.0.0
PowerToys 0.110 bundle ─┘
                                  │
                      elevated bootstrap actor
                                  │
                                  ▼
       StagePackageAsync -> immutable WindowsApps payload
                                  │
              classic SCM CreateServiceW by exact path
                                  │
                   PtPuvrUpdater / LocalSystem
                   packageIdentityPresent=false
                                  │
             direct StagePackageAsync / RemovePackageAsync
                     no deployment helper process
                                  │
              ┌───────────────────┴───────────────────┐
              ▼                                       ▼
PtPuvrRuntime_<owner A hash>             PtPuvrRuntime_<owner B hash>
NT SERVICE virtual account               NT SERVICE virtual account
runtime track 1.0.0.0                     runtime track 2.0.0.0
WindowsApps package family 1              WindowsApps package family 2
packageIdentityPresent=false              packageIdentityPresent=false
```

Both updater and runtime packages are **delivery containers only**. Neither
manifest declares `desktop6:Service`. Classic SCM owns every service
registration, and SCM directly starts each exact WindowsApps executable.
Consequently, all service processes are ordinary unpackaged Win32 processes:

```text
GetCurrentPackageFullName -> APPMODEL_ERROR_NO_PACKAGE (15700)
```

"Raw" or "unpackaged" describes process identity and activation, not where the
signed payload is stored.

## Independent versions

| Artifact | Version |
|---|---:|
| Updater package/file, initial | `5.0.0.0` |
| Updater package/file, upgrade | `6.0.0.0` |
| Runtime track 1 package/file | `1.0.0.0` |
| Runtime track 2 package/file | `2.0.0.0` |
| Management protocol | `2` |

Updater v5 and v6 use one package family:

```text
Microsoft.PowerToys.WsPuvr.RawUpdater
```

The runtime tracks intentionally use different package families. This permits
two users to retain different runtime versions side by side without an update
in one family removing the other user's exact WindowsApps directory.

`Package.ps1` creates simulated `PowerToys-0.101` and `PowerToys-0.110`
bundles. Both carry a byte-identical updater v5 MSIX. The updater has a
servicing version independent of PowerToys and the runtimes.

## Why there is no `desktop6:Service`

Declaring a service extension would switch the prototype to platform-owned
packaged-service activation. That is the earlier topology in which the
packaged LocalSystem updater's AppX deployment call failed at
`SharedAppsRedirect` with `0x80070520`.

Here the manifest contains ordinary hidden full-trust application metadata
only. MSIX provides signed identity, versioning, staging, and an immutable
WindowsApps payload. Classic SCM provides the service account and lifetime.

The same split is used for the runtimes: MSIX delivery, classic SCM raw
activation, and no package identity in the resulting process.

## Helper elimination

The raw LocalSystem updater directly invokes `PackageManager` for each runtime.
The lifecycle records:

```text
operation=StagePackageAsync
callerProcessId=<updater SCM PID>
callerTokenUserSid=S-1-5-18
callerPackageIdentityPresent=false
hresult=0x0
win32=0
```

Validation compares the recorded caller PID with the updater service PID for
both runtime tracks. There is no `PtPuvrDeploymentHelper.exe` in the build,
packages, WindowsApps payloads, ProgramData evidence, or normal deployment
path.

This proves that the helper was a workaround for the tested **packaged caller
context**, not a requirement imposed by LocalSystem or by AppX deployment in
general.

## Updater bootstrap and servicing

The elevated controller performs this bootstrap:

1. validate the expected updater MSIX input;
2. stage the package;
3. derive the exact WindowsApps package directory from the expected identity;
4. create or validate one auto-start `PtPuvrUpdater` LocalSystem service;
5. set its `ImagePath` to the exact staged `PtPuvrUpdater.exe`;
6. start it and verify its raw process identity and physical package version.

For an existing service, account, type, and old `ImagePath` policy are
validated **before** staging a different updater version. The update sequence
is:

```text
validate old SCM configuration
  -> stage target updater package
  -> stop old process
  -> repoint SCM to target WindowsApps path
  -> start and verify target version
```

Normal v5 to v6 upgrade and v6 to v5 rollback are both exercised. Rollback
uses `ForceUpdateFromAnyVersion`.

### Important same-family update risk

Staging v6 makes v6 the preferred version and immediately moves the v5
directory into `WindowsApps\Deleted`, even while the raw v5 updater process is
still running. AppX deployment events observed:

- event 574: preferred package changed from v5 to v6;
- event 472: the old package directory moved to `WindowsApps\Deleted`;
- event 471: deletion of the still-running old updater EXE failed;
- event 400: v6 stage nevertheless completed successfully.

This is why old SCM policy must be checked before staging. More importantly,
there is a real production crash window after the platform retires the old
path but before SCM is repointed. If the external bootstrap actor crashes in
that interval, SCM may reference a path that no longer exists.

The mechanism prototype can recover by staging the intended package again and
repointing SCM, and it proves rollback works. A production design must still
choose and validate one of:

- an external installer/updater transaction with durable recovery;
- alternating updater package families (A/B);
- another design that never leaves the only repair actor dependent on the
  path being replaced.

Therefore packaging the raw updater removes the loose-Program-Files source and
preserves MSIX delivery benefits, but it does **not** make updater servicing
atomic.

## Why the updater remains a singleton

LocalSystem can run multiple service instances. Singleton is chosen because
the updater coordinates machine-wide AppX inventory, WindowsApps ACLs, and SCM
state. Multiple instances would share the same `S-1-5-18` identity and add
deployment/repath/removal races without creating an isolation boundary.

The singleton must obtain an explicit owner SID from authenticated product
state or IPC and constrain every operation to that owner's runtime. It must not
treat its LocalSystem token as a user identity.

The runtimes remain per owner because they need separate low-privilege writer
identities, store ACLs, versions, and compromise boundaries.

## Virtual-account runtime launch

Each runtime service uses:

```text
NT SERVICE\PtPuvrRuntime_<owner hash>
```

After staging, the updater grants that exact service SID read/execute access
to the exact runtime package-full-name/version directory and
`PtPuvrRuntime.exe`. It does not modify the WindowsApps root, unrelated
packages, or future versions.

Every version receives a new physical directory, so the scoped ACE must be
applied again before repointing and starting a runtime. This remains the
primary productization dependency: modifying ACLs beneath WindowsApps requires
explicit Windows platform support. Without that support, the exact combination
`virtual account + direct WindowsApps + no runtime copy` is not production
viable.

## Build and validate

Run from an elevated x64 PowerShell:

```powershell
.\Build.ps1 -Configuration Release -Clean
.\Package.ps1 -Configuration Release -SkipBuild -TrustMachine
.\Lifecycle.ps1 -Verb validate -Configuration Release
.\Teardown.ps1 -Configuration Release
```

`Lifecycle.ps1 -Verb validate` runs:

1. two byte-identical v5 bundle bootstraps, proving idempotence;
2. direct runtime track 1 stage by updater v5;
3. updater v5 to v6 upgrade;
4. updater v6 to v5 rollback;
5. final updater v5 to v6 upgrade;
6. direct runtime track 2 stage by updater v6;
7. concurrent validation of both virtual-account runtimes;
8. exact service/package/store cleanup in `finally`.

It temporarily trusts the exact bundled test certificate in
`LocalMachine\TrustedPeople` if needed and removes only that insertion on exit.
`Teardown.ps1` additionally removes test certificates created by local package
generation.

Machine-readable evidence is written to:

```text
artifacts\validation-result.json
```

The expected updater evidence includes:

```json
{
  "account": "LocalSystem",
  "standaloneVersion": "6.0.0.0",
  "artifactType": "msix-staged-raw-scm",
  "packageIdentityPresent": "false",
  "packageIdentityError": "15700",
  "deploymentMode": "direct-unpackaged-package-manager",
  "deploymentHelperPresent": false,
  "directStageResults": [
    { "runtimeTrack": 1, "hresult": "0x0" },
    { "runtimeTrack": 2, "hresult": "0x0" }
  ]
}
```

Package cleanup uses current-context `RemovePackageAsync` for the new staged
updater/runtime packages. Using `RemoveForAllUsers` for these `S-1-5-18`
staged entries can report successful de-stage events while leaving the
repository entry and package directory present.

## Portable cross-machine validation

After committing the worktree, create a minimal ZIP:

```powershell
.\Export-PortableArtifacts.ps1 -DestinationDirectory C:\Temp
```

On another x64 Windows 11 machine, extract it and run from an elevated
PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\Run-PortableValidation.ps1
```

The runner validates every file hash, temporarily trusts the exact certificate,
runs the complete v5/v6 and two-runtime lifecycle, guarantees teardown, and
removes the certificate if it was not already trusted.

## Product gaps

This is a topology/mechanism prototype, not production updater code:

- production IPC needs owner/install authorization, quotas, replay protection,
  auditing, and request serialization;
- package intake needs exact family, publisher, payload identity, version floor,
  anti-downgrade, and TOCTOU-safe validation;
- updater transitions need durable transaction state and crash recovery for
  the stage-to-repoint window;
- A/B package-family or equivalent external repair behavior needs validation;
- updater inventory needs durable leases and last-uninstall reconciliation;
- WindowsApps package-directory ACL changes need platform support approval;
- real Workspaces runtime logic, store migration, telemetry, installer
  ownership, repair, and enterprise policy remain to be integrated.

## Verdict

**Mechanism GO:** a classic raw LocalSystem updater can itself be delivered by
MSIX, launched from the exact WindowsApps path without package identity, and
directly stage/remove two independent runtime packages without a helper.
Updater v5/v6 repath, downgrade rollback, double-bundle idempotence, two
concurrent virtual-account runtimes, and exact cleanup pass end to end.

**Product conditional GO:** the topology still depends on per-version
WindowsApps runtime ACL changes, and same-family updater stage/repath is not
atomic. Those are production architecture decisions, not implementation polish.
