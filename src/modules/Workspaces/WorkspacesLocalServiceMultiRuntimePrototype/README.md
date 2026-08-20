# Unpackaged updater + virtual-account multi-runtime prototype

This native prototype validates the following topology:

```text
PowerToys 0.101 bundle ─┐
                       ├─ byte-identical signed updater PE 5.0.0.0
PowerToys 0.110 bundle ─┘
                                  │
                     one elevated bootstrap
                                  │
                                  ▼
       %ProgramFiles%\PowerToys\WorkspacesUnpackagedUpdater...
                       PtPuvrUpdater.exe
                                  │
               ordinary LocalSystem SCM service
               packageIdentityPresent = false
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
packageIdentityPresent = false            packageIdentityPresent = false
```

The two owner SIDs represent users with different PowerToys installations.
The updater is a machine-wide singleton with an independent servicing version.
The runtime services are dynamically named and do not appear in a signed
manifest.

## What "unpackaged runtime" means

The runtime EXE is still delivered by a signed MSIX and staged into an exact
versioned WindowsApps directory. SCM directly starts that EXE as an ordinary
classic service. The process therefore has no package identity:

```text
GetCurrentPackageFullName -> APPMODEL_ERROR_NO_PACKAGE (15700)
```

There is no runtime EXE copy. "Unpackaged runtime" describes the process
identity and activation model, not the payload delivery format.

## Independent versions

| Artifact | Version |
|---|---:|
| Singleton updater PE | `5.0.0.0` |
| Runtime track 1 package/file | `1.0.0.0` |
| Runtime track 2 package/file | `2.0.0.0` |
| Management protocol | `2` |

`Package.ps1` creates simulated `PowerToys-0.101` and `PowerToys-0.110`
bundles. Both contain byte-identical, Authenticode-signed
`PtPuvrUpdater.exe` files. The validation asserts that both hashes equal the
canonical standalone updater hash.

The runtime tracks intentionally use different package families. That permits
two users to retain different runtime versions side by side without one
package-family update removing the other's exact WindowsApps directory.

## Helper elimination

The earlier packaged-updater prototype observed:

```text
packaged LocalSystem updater
  StagePackageAsync / AddPackageAsync
  -> 0x80070520 at SharedAppsRedirect
```

It used a transient unpackaged helper as a measured workaround. This variant
makes the updater service itself an ordinary LocalSystem process. It calls
`PackageManager.StagePackageAsync` and `RemovePackageAsync` directly.

For each runtime track, the updater writes evidence containing:

```text
operation=StagePackageAsync
callerProcessId=<updater service PID>
callerTokenUserSid=S-1-5-18
callerPackageIdentityPresent=false
hresult=0x0
win32=0
```

The lifecycle test compares `callerProcessId` with the SCM updater PID. The
solution, package layout, Program Files install root, and ProgramData store
contain no deployment-helper executable.

This proves that the helper was required by the tested packaged caller context,
not by LocalSystem or AppX deployment in general.

## Updater installation and servicing

The elevated controller bootstrap:

1. takes the signed updater PE from a simulated PowerToys bundle;
2. copies it to:

   ```text
   %ProgramFiles%\PowerToys\
     WorkspacesUnpackagedUpdaterVirtualRuntimePrototype\
     PtPuvrUpdater.exe
   ```

3. applies a protected SYSTEM/Administrators-only DACL;
4. creates one auto-start `PtPuvrUpdater` LocalSystem service;
5. verifies the SCM account and exact protected ImagePath before starting it.

The updater evidence must report:

```text
tokenUserSid=S-1-5-18
packageIdentityPresent=false
packageIdentityError=15700
updaterVersion=5.0.0.0
deploymentMode=direct-unpackaged-package-manager
```

The tradeoff is explicit: the updater no longer receives MSIX servicing.
Updating it requires a trusted external SYSTEM/elevated actor to perform a
stop, signature and anti-downgrade validation, atomic replacement, rollback,
and restart. Because the updater version is independent of PowerToys, that can
remain an occasional `MinimumUpdaterVersion` event rather than every product
update.

## Why the updater is a singleton

LocalSystem can run multiple service instances; that is not the reason for the
singleton. Package deployment, WindowsApps inventory and ACLs, and SCM
create/repath/remove operations are machine-wide state that require one
coordinator.

Multiple per-user updater services would all have the same `S-1-5-18` primary
identity. They would provide no isolation while adding deployment and cleanup
races. The singleton derives an explicit owner SID from an authenticated
request and manages the corresponding per-user runtime.

The runtimes remain per-user because they require distinct low-privilege
writer identities, store ACLs, versions, and compromise boundaries.

## Virtual-account runtime launch

Each runtime service uses:

```text
NT SERVICE\PtPuvrRuntime_<owner hash>
```

After staging, the updater grants that exact service SID read/execute access
to the exact runtime package-full-name/version directory and its
`PtPuvrRuntime.exe`. It does not modify the WindowsApps root, unrelated
packages, or future versions in the same family.

Every new version creates a new physical directory, so the updater must apply
the scoped ACE again before repointing and starting the service.

This remains the primary productization risk. Modifying ACLs beneath
WindowsApps requires an explicit Windows platform/support decision. If it is
not supported, the exact combination
`virtual account + direct WindowsApps + no runtime copy` remains NO-GO.

## Build and validate

Run from an elevated x64 PowerShell:

```powershell
.\Build.ps1 -Configuration Release -Clean
.\Package.ps1 -Configuration Release -SkipBuild -TrustMachine
.\Lifecycle.ps1 -Verb validate -Configuration Release
.\Teardown.ps1 -Configuration Release
```

`Lifecycle.ps1 -Verb validate` always performs service, package, install-root,
and store cleanup in `finally`. Machine-readable evidence is written to:

```text
artifacts\validation-result.json
```

If the updater service is unavailable during teardown, the controller runs its
own existing executable once as a transient LocalSystem cleanup service and
calls `RemovePackageAsync` by exact package full name. The temporary SCM entry
is deleted immediately; this is validation/teardown recovery, not a shipped
deployment helper or part of the updater deployment path.

The expected updater section contains:

```json
{
  "account": "LocalSystem",
  "standaloneVersion": "5.0.0.0",
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

The runner validates every file hash, temporarily trusts the exact test
certificate, runs the complete two-runtime lifecycle, guarantees teardown,
and removes the certificate if it was not already trusted.

## Product gaps

This is a topology/mechanism prototype, not production updater code:

- the named-pipe client is intentionally restricted to administrators;
- production requests need owner/install authorization, quotas, and replay
  protection;
- the privileged bootstrap/installer must verify the updater signer, exact
  product identity, version floor, and anti-downgrade policy before copying;
- runtime package sources need exact identity, signer, anti-downgrade, and
  TOCTOU-safe intake rules;
- updater inventory needs durable leases, crash recovery, and last-uninstall
  reconciliation;
- updater self-update needs an external trusted SYSTEM/elevated actor;
- WindowsApps package-directory ACL changes need platform support approval.

## Verdict

**Prototype GO:** an ordinary protected LocalSystem updater directly stages
and removes two independent runtime packages without a deployment helper.
Two dynamic virtual-account runtime services run concurrently from their exact
WindowsApps payloads with different versions and no package identity.

**Product conditional GO:** removing the helper simplifies the deployment
chain, but the design still depends on per-version WindowsApps service-SID RX
ACLs and on a separate secure servicing design for the updater PE.
