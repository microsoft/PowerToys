# Packaged Host + ordinary protected Runtime integration prototype

> **Historical validation artifact, not the final process topology.**
>
> This worktree still contains a private ordinary
> `PtPuvrUpdater.exe` Engine behind the packaged Host. The accepted product
> architecture has since removed that split: the one packaged LocalSystem
> Updater EXE must own the control-plane implementation directly, alongside
> the ordinary per-SID Runtime services. There is no private Engine, helper,
> companion MSI, persistent bootstrapper, or proxy in the target design.
>
> The lifecycle results below remain useful evidence for AppX servicing,
> package identity, SCM repathing, protected-state preservation, and Runtime
> independence. They must not be cited as proof that the final two-role
> implementation already exists.

This prototype implements the earlier integration topology:

```text
machine-provisioned signed Updater MSIX
  -> manifest-declared PtPuvrHost / LocalSystem
  -> package identity and WindowsApps protection
  -> one stable public control-plane pipe

versioned ordinary protected PE Engine
  -> LocalSystem child of the Host
  -> no public pipe and no package identity

N dynamic ordinary protected PE Runtime services
  -> NT SERVICE\PtPuvrRuntime_<owner hash>
  -> unique virtual-account primary identities
  -> per-owner protected stores

existing PowerToys processes
  -> statically linked generic control client

production Proxy EXEs: 0
companion MSI/Burn processes: 0
```

In this historical prototype, the Updater role is split between the fixed
packaged Host and the versioned
`PtPuvrUpdater.exe` Engine. The historical Engine filename is retained only
because this worktree records the already-completed experiment; it is not
part of the accepted target architecture.

## Why the split is intentional

The fixed machine component is a good fit for a manifest-declared
`desktop6:Service`: the package has one stable service name, Windows validates
the MSIX signature, WindowsApps protects the installed Host bytes, and AppX
owns service registration and image-path transitions.

Dynamic per-SID Runtime services cannot be declared in a finite manifest.
They remain ordinary signed PE services in protected Program Files
directories. Each Runtime has a deterministic service name and unique
virtual-account SID, so its store ACL does not grant a shared built-in
service account.

The versioned Engine also remains ordinary PE. The packaged Host launches it
with `PROC_THREAD_ATTRIBUTE_DESKTOP_APP_POLICY` and
`PROCESS_CREATION_DESKTOP_APP_BREAKAWAY_ENABLE_PROCESS_TREE`, deliberately
preventing package-identity inheritance. Before accepting a request, the
Engine verifies:

1. its real parent PID is the Host PID passed on the protected command line;
2. the parent process has the expected package name, publisher-derived family,
   x64 architecture, and non-rollback package version;
3. the parent image is exactly
   `%ProgramFiles%\WindowsApps\<validated-full-name>\PtPuvrHost.exe`;
4. the parent PE has the pinned signer, product identity, architecture, and
   Host file version;
5. the Engine itself is the signed active version under the protected
   ordinary Engine root and has no package identity.

This boundary preserves AppX protection for the Host without turning the
ordinary Engine or Runtime into packaged processes.

## Signed package bootstrap

`Package.ps1` produces two signed test packages:

- `artifacts\updater-msix\PtPuvrHost-5.0.0.0.msix`
- `artifacts\updater-msix\PtPuvrHost-6.0.0.0.msix`

Each package contains:

```text
PtPuvrHost.exe
Bootstrap\
  code-signer-sha256.txt
  metadata-signer-sha256.txt
  Engines\5.0.0.0\PtPuvrUpdater.exe
  Policy\PtPuvrCodePolicy.exe
  Policy\PtPuvrMetadataPolicy.exe
```

On every Host start, the Host first validates its package identity and exact
package install path. It then seeds or repairs only this immutable bootstrap
set into protected ordinary locations:

| Location | Ownership |
|---|---|
| `%ProgramFiles%\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype\Engines\5.0.0.0` | Initial ordinary Engine |
| `%ProgramData%\Microsoft\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype` | Signer pins, policy PEs, mutable state, leases, journals, evidence, and per-owner stores |

Copy destinations are created with protected SYSTEM/Administrators-only
DACLs. Copy uses a fresh protected sibling, write-through atomic replacement,
and exact source/destination comparison. The source is not a caller path: it
is the current signed package's immutable WindowsApps payload.

Package repair does not reset mutable state. The Host creates the complete
initial state only when every state file is absent, marks initialization in a
protected registry value, and fails closed on partial state. A later package
version may repair the immutable initial Engine/policy/pins while preserving
the active Engine, version floors, accepted releases, SID leases, Runtime
inventory, and journals.

There is no companion MSI and no permanently installed bootstrapper. Initial
machine provisioning and rare Updater package upgrades are elevated AppX
deployment operations. Normal PowerToys/Engine/Runtime releases continue
through the already installed LocalSystem Host without UAC.

## Windows-owned install and update validation

The signed v5 and v6 test packages were also installed outside the lifecycle
script through Windows App Installer file activation. Directly invoking
`AppInstaller.exe <package.msix>` is not a supported command-line contract and
remained in its loading state; opening the `.msix` through the Windows file
association correctly used the protected `DelegateExecute` activation path.

The Windows-owned UI displayed the package publisher, version, and both
privileged service capabilities before deployment. The observed results were:

- fresh v5 install created the manifest-declared LocalSystem service and
  pointed SCM at the v5 WindowsApps payload;
- v5 -> v6 update changed the package registration and SCM image path to the
  v6 WindowsApps payload;
- the service was left stopped after servicing, consistent with the scripted
  AppX lifecycle;
- both operations created a registration for the initiating user and did not
  create an `Add-AppxProvisionedPackage -Online` machine-provisioning record;
- after UAC, the visible App Installer process had a High-integrity,
  full-elevation token;
- no PowerToys executable performed package signature or payload validation.

AppX accepted the valid signed packages and rejected a package whose
`PtPuvrHost.exe` entry had been rewritten after signing with `0x80073CF0`
before stage/register. A random edit to irrelevant ZIP container metadata did
not necessarily invalidate the package, so the security claim is specifically
about AppX signature, publisher, block-map, and declared-payload integrity,
not byte-for-byte equality of the outer ZIP container.

This machine validated an administrator in Admin Approval Mode. A true
standard-user, over-the-shoulder credential flow and multi-user
registration/uninstall ownership remain separate manual tests; this README
does not claim those results.

## AppX servicing contract

An elevated Updater package deployment:

1. validates the signed MSIX;
2. stops the old packaged Host;
3. atomically changes the package preference and SCM WindowsApps image path;
4. leaves the Host stopped.

The elevated caller that initiated deployment must then start the Host,
perform readiness checks, and force-update back to the prior signed package
if readiness fails. `Lifecycle.ps1` validates v5 -> v6 and a forced v6 -> v5
rollback. Both transitions preserve the two already-running ordinary Runtime
processes and the active ordinary Engine state.

AppX does not understand product leases for independent ordinary Runtime
services. Package removal must therefore first call the Host's
`--package-uninstall-check`; removal is refused while leases, Runtime
services, inventory records, or transaction journals remain. After every
owner releases its lease, `--package-uninstall-cleanup` removes the exact
ordinary protected roots and endpoint state before package deprovisioning.

## Public API and authorization

The package Host owns the only public control-plane endpoint. It exposes:

| API | Authorization and effect |
|---|---|
| `acquire` / `ensure` with bounded release ID | Derives owner SID and LocalAppData inbox from the kernel-reported caller token; accepts only bounded signed metadata and signed PE artifacts; creates or reconciles only that SID's Runtime service and lease. |
| `status` | Requires the caller's own existing lease and returns only its Runtime state. |
| `release` | Removes only the caller SID's lease and Runtime service. |

The request cannot contain an owner SID, source path, destination path,
service/account name, URL, Runtime track, or raw command line. The caller
binary is not a capability. The client binds the pipe server PID to the
SCM-owned Host; the Host binds pipe PID, process token, pipe impersonation
token, and session before deriving the caller SID and inbox.

The Host uses a fresh random endpoint on every start, anchors the first pipe
instance, rejects remote clients, gives Authenticated Users data rights but
not pipe-instance creation, enforces a one-active-connection-per-SID quota,
uses bounded stop-aware I/O, and serializes protected mutations.

Normal release intake retains the existing defenses:

- no-follow local source handles and exact final-path containment;
- reparse, hard-link, UNC, remote-drive, traversal, and size rejection;
- distinct pinned code and metadata signers;
- exact product, architecture, length, SHA-256, version, and release-ID checks;
- security-epoch, Engine-version, and per-track Runtime-version floors;
- durable Engine activation, acquisition, Runtime, and cleanup journals;
- readiness rollback and crash convergence;
- exact per-SID Runtime inventory and store ACLs.

## Build and validate

Run from an elevated PowerShell 7 terminal:

```powershell
.\Build.ps1 -Configuration Release -Clean
.\Package.ps1 -Configuration Release -SkipBuild -TrustMachine
.\Lifecycle.ps1
```

`Build.ps1` compiles all Host, Engine, Runtime, policy, manifest, and client
test artifacts as x64 Release with warnings treated as errors.

`Package.ps1` creates fresh test code/metadata/foreign certificates, signs all
PEs and MSIX packages, records exact certificate-store ownership, creates
positive and negative signed release sets, and writes
`artifacts\release\artifacts.json`.

`Lifecycle.ps1` requires no pre-existing `PtPuvr*` artifacts. It:

1. machine-provisions signed package v5 and starts the LocalSystem Host;
2. proves package identity and first-start ordinary bootstrap;
3. creates two standard local users;
4. has both users acquire signed release 102, activating ordinary Engine
   5.1 and two distinct ordinary Runtime services;
5. proves Runtime virtual accounts, per-owner evidence, ordinary Program
   Files image paths, and absence of Runtime package identity;
6. updates the Updater package to v6 while both Runtime processes remain
   alive with unchanged PIDs;
7. restarts and validates the v6 Host and preserved leases/Engine state;
8. force-rolls back to v5 and repeats the preservation checks;
9. releases both leases, performs guarded Host cleanup, removes provisioning
   and registrations, users/profiles, and exactly owned test certificates;
10. asserts zero service, package, provisioning, root, registry, user, and
    certificate residue.

The validated result is written to
`artifacts\packaged-lifecycle-result.json`.

This development machine also retains a separate frozen alias experiment
whose unsupported custom-account packaged worker runs in session 0. That
worker must be stopped and AppModel allowed to quiesce before this packaged
service lifecycle runs, then restored afterward. This is test-environment
isolation, not a dependency of this architecture.

If an interrupted run leaves artifacts after all users have released their
leases, run:

```powershell
.\Teardown.ps1
```

## Validated result

The integration lifecycle passed on Windows 11 build 26200:

- signed machine-provisioned `desktop6:Service` Host runs as LocalSystem with
  the expected package identity;
- package bootstrap seeds the ordinary protected Engine and policy roots;
- two standard users concurrently own separate ordinary Runtime services;
- both Runtime processes retain the same PIDs through Updater v5 -> v6 and
  forced v6 -> v5 rollback;
- Runtime evidence reports `packageIdentityPresent=false`;
- active ordinary Engine 5.1, two protected leases, Runtime inventory, and
  per-owner stores survive both package transitions;
- guarded release and uninstall remove every integration artifact;
- no persistent production executable exists beyond the packaged Host,
  ordinary Engine, and ordinary Runtime payloads.
