# Protected ordinary-PE updater and multi-runtime prototype

This native mechanism prototype uses only ordinary signed PE files, SCM
services, Program Files, and ProgramData:

```text
PowerToys 0.101 bundle --- signed PtPuvrUpdater.exe 5.0.0.0 ---+
                                                              |
PowerToys 0.110 bundle --- byte-identical updater PE ---------+
                                                              v
%ProgramFiles%\PowerToys\WorkspacesProtectedRuntimeUpdaterPrototype
  Updater\5.0.0.0\PtPuvrUpdater.exe
  Runtimes\TrackN\<version>\PtPuvrRuntime.exe
                                                              |
                             one LocalSystem SCM updater -----+
                                      |
                 +--------------------+--------------------+
                 v                                         v
PtPuvrRuntime_<owner-A hash>                 PtPuvrRuntime_<owner-B hash>
NT SERVICE virtual account                    NT SERVICE virtual account
exact owner-A ProgramData store               exact owner-B ProgramData store
```

The updater is the sole machine-wide coordinator. Each owner gets a
dynamically derived SCM service name and exact virtual-account SID. No service
accepts caller-controlled destination directories, service names, accounts, or
command lines. The updater derives those values from fixed code, the canonical
owner SID, the validated track, and the validated PE version.
The durable inventory is capped at 32 owners. The cap is enforced before
staging, before an inventory append, and again before every inventory write.

## Trust boundary

The elevated controller is deliberately a **simulation of a trusted installer
bootstrap**. It is not a production trust anchor if a user can modify it or
its invocation. Production must place the same bootstrap behavior in a
Microsoft-controlled installer/update trust boundary.

For both updater and runtime intake, the actor:

1. creates a fresh SYSTEM/Administrators-only staging directory beneath the
   protected Program Files root;
2. copies the potentially user-writable source into that directory;
3. validates the protected copy with native `WinVerifyTrust`, takes the leaf
   certificate from the successful provider state, and compares its SHA-256
   certificate fingerprint to the protected policy pin;
4. verifies an x64 PE, a LocalMachine-trusted Authenticode chain, the pinned
   WinVerifyTrust leaf, `CompanyName`, `ProductName`, `OriginalFilename`, and
   fixed file/resource version;
5. rejects version collisions, wrong tracks, unsigned or altered files, wrong
   products, and inventory-based downgrades;
6. writes and flushes a journal containing the validated staging path and
   derived versioned destination before atomically moving that protected file.

`Package.ps1` creates fresh primary and foreign test certificates on every
package run and emits both exact thumbprints, certificate files, and SHA-256
certificate fingerprints in `artifacts.json`. Before replacing a release root,
it validates that run's ownership manifest and restores only exact certificate
thumbprints which that run introduced. The new ownership manifest records both
signers and every relevant certificate store, flushes before any
`TrustedPeople` import, and records each import as owned before performing it.
A caught packaging failure restores only the exact new certificates introduced
by that run; pre-existing trust is never removed. A second package run
therefore proves prior-run certificate convergence while the new release root
tracks only its current primary and foreign certificates. The
trusted-bootstrap controller accepts only the primary fingerprint as trusted
artifact metadata, validates the staged updater against it, and writes the exact pin to
`ProgramData\...\trusted-signer-sha256.txt` under SYSTEM/Administrators-only
ACLs. The updater validates its own installed image and every staged runtime
against that same protected pin. It never reopens the source path to identify
the signer.

Multiple signatures are deliberately fail-closed: the prototype requires
exactly one primary signer in the WinVerifyTrust provider state and requires
that selected, successfully verified leaf to match the pin. A second embedded
or nested signature is not an authorization fallback.

Production must pin Microsoft-owned signing public-key material or leaf
fingerprints in installer-controlled policy, never an X.500 subject string.
Signer rotation must be an authenticated, atomic installer/update transaction:
the currently pinned key authorizes a policy containing the next key, both
keys remain allowed only for a bounded overlap, and the previous key is then
removed after revocation and rollback windows close. This fixed-pin prototype
does not implement online rotation; a different pin is rejected.

## Protection and ownership

The updater directory and its service configuration are restricted to
SYSTEM/Administrators. Runtime version directories use protected ACLs:

| Principal | Runtime directory access |
|---|---|
| SYSTEM | Full control |
| BUILTIN\Administrators | Full control |
| BUILTIN\Users | Read/execute |
| Exact runtime service SID | Read/execute |

The exact per-owner ProgramData store grants full control only to SYSTEM,
Administrators, and that runtime service SID. The runtime records evidence
that its virtual-account token cannot write its executing binary or a sibling
owner’s store.

The updater pipe is restricted to administrators for this mechanism
prototype. The client binds its pipe connection to the current updater SCM
PID. Production caller authorization, ownership binding, quotas, replay
protection, and policy evaluation remain open work.

## Inventory, transactions, and recovery

Protected ProgramData holds runtime inventory, a provisioning journal, and a
cleanup journal. The provisioning journal records the owner, derived service,
previous path and version, validated staging path, derived final path,
running state, and phase. Each journal write is atomic and flushed before the
following durable transition.

The successful update path is:

```text
protected intake -> validation in SYSTEM-only staging -> journal flush
-> atomic final install -> stop -> SCM repath -> start/readiness
-> inventory commit -> sibling synchronization -> unreferenced cleanup
-> journal clear
```

Runtimes are never overwritten in place. A readiness failure restores the
previous derived ImagePath and prior running state without advancing inventory.
On service startup and before every pipe request, the updater converges every
provisioning and cleanup journal before processing the request. Pre-inventory
crashes roll back the service state and discard the staged/final unreferenced
candidate. Within the fixed, validated `TrackN\<version>` root, an
unreferenced version directory is removed as a whole even if a crash left it
empty or incomplete; executable, signature, and version/path identity remain
mandatory for inventory-referenced versions. Once inventory names the
candidate, recovery finalizes by
reconciling sibling command lines and cleanup before clearing the journal, so
it never reverts a committed runtime. Validation is phase and inventory aware:
after a committed candidate's unreferenced cleanup, its prior runtime bytes may
legitimately be absent; a pre-commit rollback still requires and verifies them.

Cleanup journals service deletion before inventory removal and keeps the
journal through store deletion, sibling synchronization, and runtime cleanup.
Recovery compares its owner, track, and version with current protected
inventory: a matching or absent entry rolls forward, while a different current
entry is a later committed provision and is reconciled rather than deleted.
Startup reconciliation is idempotent: it recreates/repairs inventory-backed
prototype services and their derived commands, removes unreferenced
`PtPuvrRuntime_*` services, owner stores, staging directories, and runtime
versions. Thus cleanup crashes, incomplete recursive-delete remnants, and
ordinary cleanup failures converge without a stale journal deleting a later
provision or a referenced runtime root.

The updater pipe is opened for overlapped I/O. Every connect, read, and write
uses an operation-owned event and waits on both the operation and the service
stop event. Stop cancellation calls `CancelIoEx` and reaps the pending
operation before either the pipe handle or event is destroyed. Fixed request
and reply sizes are still enforced; a stalled or disconnected client is
discarded without blocking service stop.

## Artifacts and validation

`Build.ps1` produces Release `/WX` x64 PEs. `Package.ps1` Authenticode-signs
the following ordinary artifacts:

| Track | Version | Readiness |
|---|---:|---|
| Updater | `5.0.0.0` | healthy |
| 1 | `1.0.0.0` | healthy |
| 1 | `1.1.0.0` | healthy upgrade |
| 1 | `1.2.0.0` | intentional readiness failure |
| 1 | `1.3.0.0` | healthy journal-crash retry |
| 1 | `1.4.0.0` | healthy final-install crash retry |
| 1 | `1.5.0.0` | healthy SCM-repath-crash retry |
| 1 | `1.6.0.0` | healthy inventory-commit crash recovery |
| 1 | `1.7.0.0` | healthy post-obsolete-delete crash recovery |
| 1 | `1.8.0.0` | healthy target-directory crash retry |
| 2 | `2.0.0.0` | healthy |

The release set also contains a signed wrong-product PE, a trusted-chain PE
signed by a distinct test certificate, and an altered signature-negative PE
for rejection tests. Simulated PowerToys bundles are directories containing
only signed PEs and JSON metadata.

From an elevated x64 PowerShell:

```powershell
.\Build.ps1 -Configuration Release -Clean
.\Package.ps1 -Configuration Release -SkipBuild -TrustMachine
.\Package.ps1 -Configuration Release -SkipBuild -TrustMachine
.\Lifecycle.ps1 -Verb validate -Configuration Release
```

`artifacts\validation-result.json` proves:

| Validation | Expected result |
|---|---|
| Bootstrap | LocalSystem updater, fixed Program Files ImagePath, protected evidence |
| Concurrent owners | Two distinct virtual-account services, PIDs, service SIDs, and stores |
| Runtime protection | Ordinary Program Files paths plus denied binary and sibling-store writes |
| Identity evidence | Every updater/runtime records `GetCurrentPackageFullName == 15700` |
| Signer binding | Bootstrap metadata pin, protected policy pin, and WinVerifyTrust selected leaf match |
| Candidate rejection | Altered, wrong-product, trusted-chain foreign-signer, and wrong-track input rejected while `1.0.0.0` keeps running |
| Downgrade | `1.1.0.0` rejects `1.0.0.0` from protected inventory |
| Upgrade | `1.0.0.0` to `1.1.0.0` succeeds |
| Readiness rollback | Valid `1.2.0.0` fails readiness and restores `1.1.0.0` |
| Provisioning recovery | Crashes after journal preparation, target-directory creation before the atomic move, final install, SCM repath, inventory commit, and obsolete-runtime deletion converge with exact SCM commands, sibling evidence, empty staging, cleared journals, and exact version directories |
| Cleanup recovery | Crashes after service deletion and inventory removal, plus a deterministic non-crash cleanup failure, converge before a later provision can mutate state |
| Pipe lifecycle | Service stop and restart complete while an administrator pipe client is connected but has sent no request |
| Owner limit | 32 virtual-account owners are admitted; the 33rd request creates neither service nor owner store |
| Artifact paths | Installed SCM ImagePaths and artifacts contain neither `WindowsApps` nor `.msix` |
| Certificate ownership | Two consecutive package runs leave only the current exact primary and foreign certificates tracked; final teardown restores both certificates to each store's pre-run state without subject-based deletion |
| Cleanup | Staging empties; final teardown restores pre-run state for both exact primary and foreign certificate thumbprints without subject-based deletion |

`Lifecycle.ps1 -Verb validate` always executes final teardown and writes a
PASS or FAIL result after that teardown. `Teardown.ps1` is available for an
explicit cleanup run.

## Remaining product gaps

- Replace the controller simulation with a genuine Microsoft installer/update
  trust boundary and production signer/product identity/rotation policy.
- Define production caller authorization and the relationship between a
  requesting user, a PowerToys installation, and an owner SID.
- Add production telemetry, policy, lease/reconciliation, installer
  integration, and recovery observability.
- Define updater self-servicing policy beyond this fixed `5.0.0.0` prototype.
- Implement the documented production signer-rotation, revocation, network
  retrieval, and version-floor operations in the real installer policy path.

## Verdict

**Mechanism GO:** one protected ordinary LocalSystem updater can securely
install and coordinate multiple dynamically named virtual-account runtime
services from versioned ordinary PE directories, with verified staged intake,
rollback, crash recovery, and teardown evidence.
