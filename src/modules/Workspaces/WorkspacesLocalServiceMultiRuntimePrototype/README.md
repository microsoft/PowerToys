# Workspaces protected runtime control-plane prototype

This prototype proves a machine-protected, ordinary-PE control plane for
per-user Workspaces runtimes. It uses no MSIX, AppX package identity,
WindowsApps path, package alias, package ACL, or user-writable elevated
bootstrap. The validated result is a signed WiX v5 companion MSI, one stable
LocalSystem SCM host, versioned on-demand updater engines, and dynamically
created per-owner runtime services using `NT SERVICE\<derived-name>` virtual
accounts.

The prototype is intentionally isolated to this directory. It does not
modify the PowerToys installer.

## Topology and MSI ownership

The WiX v5 project in `Installer\PtPuvrControlPlane.wixproj` produces a
per-machine companion MSI. The successful lifecycle installs it with elevated
`msiexec`; it does **not** use the legacy controller bootstrap command.

| Location | Immutable MSI content and protected runtime role |
|---|---|
| `%ProgramFiles%\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype` | MSI-owned stable `PtPuvrHost.exe`, machine-protected `PtPuvrUserClient.exe`, and initial `Engines\5.0.0.0\PtPuvrUpdater.exe`; host-created protected directories hold later engines and runtimes. |
| `%ProgramData%\Microsoft\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype` | MSI-owned immutable policy PEs and signer pins; host-owned active-engine state, activation/runtime/acquisition journals, accepted-release state, version floors, SID leases, inventory, request/reply files, and evidence. |
| SCM | The fixed-image-path `PtPuvrHost` service installed as `LocalSystem` through declarative WiX `ServiceInstall` and controlled through `ServiceControl`. |

The host, user proxy, initial engine, policy files, and signer pins are MSI
payloads. Mutable state is deliberately absent from the MSI `File` table.
After validating the immutable signed policy, the host creates the complete
initial mutable-state set once and marks it initialized in the protected
registry key. It fails closed if only part of that set exists; it never
silently resets security state. A forced MSI repair therefore repairs only
immutable content and preserves evolved engine state, floors, accepted
releases, leases, and inventory. The host writes evidence identifying the
bootstrap as `companion-msi`, and its image path remains fixed for the entire
lifecycle. The installer and host apply protected ACLs; neither executable
identity nor protected state is established by copying a normal-user file
into Program Files or ProgramData.

The old `Controller` source is quarantined legacy experiment material. Its
project is absent from `PtLsmr.sln`, the build, MSI, lifecycle, and portable
bundle; its trusted-bootstrap simulation is not part of the current topology.

A future PowerToys Burn chain should retain a per-user PowerToys package if
needed, while chaining this companion MSI for the first machine-component
installation that needs one UAC elevation. This prototype does **not** claim
that a current per-user Burn cache is protected.

## Stable host and versioned engine boundary

`PtPuvrHost.exe` is deliberately small and stable. It is the only SCM service
and owns the public named pipe. It authenticates the caller, creates
protected request/reply files, selects the active engine, and starts that
engine as a LocalSystem child with fixed arguments. It validates the child
engine's protected path, x64/code-signing identity, product/version, active
state, and parent-host relationship.

`PtPuvrUpdater.exe` is not an SCM service and exposes no public pipe. It is
an on-demand child in a protected versioned engine directory. The package
builds these versions:

| Engine | Purpose |
|---|---|
| `5.0.0.0` | Initial healthy MSI-installed engine. |
| `5.1.0.0` | Healthy self-service upgrade. |
| `5.2.0.0` | Signed candidate that deterministically fails qualification/readiness. |
| `5.3.0.0` | Healthy candidate used for activation-journal crash recovery. |
| `5.4.0.0` | Test-only candidate whose first qualification blocks for stop/recovery validation. |

An engine update is streamed from an opened, no-follow hostile-inbox handle
into a fresh protected stage. The host validates its exact signed length,
signer pin, product, x64 architecture, hash, version, and fixed qualification
command before atomically changing active-engine state. The durable activation
journal records the prior and candidate versions. Injected crashes before the
active switch roll back; crashes after the switch but before journal clear
converge to the active candidate on host startup or the next request. The
stable host is **not** self-serviced: updating it requires the MSI or an
external repair/update mechanism. The host never loads a versioned engine DLL.

## Per-start pipe and normal-user authorization

Each host start generates a new 128-bit random endpoint
`\\.\pipe\PtPuvrHost-<32-lowercase-hex>`. The host creates the first instance
with `FILE_FLAG_FIRST_PIPE_INSTANCE` before publishing the endpoint and before
reporting `SERVICE_RUNNING`; it retains that handle as a lifetime anchor.
While running as SYSTEM it creates three additional instances for a bounded
four-listener pool. Authenticated Users still lack
`FILE_CREATE_PIPE_INSTANCE`, so they cannot add server instances.
Only after creation succeeds does it publish the name under the protected,
user-readable
`HKLM\SOFTWARE\Microsoft\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype`
key. Startup and stop clear stale publication. Precreating an endpoint from a
prior host instance cannot block a later random endpoint.

The pipe grants local `Authenticated Users` only the data, attribute,
read-control, and synchronize rights needed to use it; it does not grant
`FILE_CREATE_PIPE_INSTANCE`. The client independently reads the protected
endpoint pointer, requests only
`FILE_READ_DATA | FILE_WRITE_DATA | SYNCHRONIZE`, and binds the connected
server process to the current SCM host PID. The host rejects remote clients with `PIPE_REJECT_REMOTE_CLIENTS`. Immediately
after each `ConnectNamedPipe`, before reading request bytes, the worker binds
the retained client process to both its primary token and the impersonated
pipe token. Because Windows named-pipe impersonation is unavailable until a
server read has completed, the protected proxy first emits a fixed eight-byte
`AUTH`/protocol-version preface containing no operation, path, SID, or other
caller-controlled request data. A purely lexical image-string filter rejects
non-proxy processes before waiting for that preface. The host derives the
canonical process-token SID and immediately reserves that SID's single
active-connection slot before waiting for the preface. After consuming it,
the host retains the impersonated pipe token and requires its canonical user
SID to match the process-token SID. Only while that RAII quota guard is alive
does it validate the protected proxy path and signature, resolve LocalAppData,
read the request, and serve the connection.
Excess same-SID connections and direct/non-proxy clients are closed
immediately, while other SIDs retain listener capacity. Request reads and
response writes each have an exact five-second, stop-aware deadline. A global
stop-aware dispatch mutex serializes protected state mutation across the
listener pool. Caller-controlled Win32 and filesystem/path failures reject
only that connection; listener/connect/disconnect synchronization failures
remain worker-wide infrastructure failures. Stop cancels all pending
connects/I/O before joining all workers. WiX
configures three five-second SCM restart actions, including non-crash service
failures.

For every request the host:

1. obtains the actual client PID and session using
   `GetNamedPipeClientProcessId` and `GetNamedPipeClientSessionId`;
2. opens and retains that process with `PROCESS_QUERY_LIMITED_INFORMATION`,
   `SYNCHRONIZE`, queries its image as a raw DOS string, and lexically rejects
   UNC, device, remote-drive, non-drive-root, DOS-device-component, and
   root-escaping forms against the local fixed-drive mask captured during
   trusted host startup, without canonicalizing or probing the caller path.
   It compares that string to the trusted expected proxy string so non-proxy
   clients are closed before any authentication-preface wait;
3. re-queries the pipe client PID, requires the process session to equal the
   pipe client session, and requires the retained process to remain live;
4. opens the process token, derives its canonical user SID, and acquires the
   per-SID connection quota before waiting for the fixed authentication
   preface, known-folder resolution, filesystem access, signature validation,
   or request reading;
5. consumes only the fixed authentication preface, briefly impersonates the
   pipe client to retain its thread token, reverts to SYSTEM, and requires the
   pipe-token canonical user SID to match the process-token SID;
6. compares the lexically normalized raw image string to the fixed
   MSI-installed
   `%ProgramFiles%\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype\PtPuvrUserClient.exe`
   path computed once during trusted host setup, using case-insensitive
   ordinal Windows comparison with no caller-filesystem traversal;
7. validates that protected file's pinned WinVerifyTrust-selected leaf,
   company, product, original filename, architecture, and version;
8. resolves the retained process token's LocalAppData with
   `KF_FLAG_DONT_VERIFY`, lexically requires a local fixed-drive DOS path, and
   derives but does not touch the release inbox before request dispatch. The
   later no-follow source-handle intake remains the authoritative
   containment, reparse, hard-link, and final-path check.

The proxy is machine protected, but it is not a secret or an authorization
capability: a same-user attacker can invoke it. Security comes from binding
the kernel-reported pipe client identity to a retained live process and its
token, deriving the owner and release inbox from that token, exposing only
self-scoped operations, and accepting only signed, bounded release metadata.
A copied signed proxy outside Program Files is rejected even for `status` on
an existing lease. The lifecycle also connects from the local `pwsh.exe`
process as a deliberate non-proxy, proves pre-read rejection without any UNC
or device/network path access, observes the same host PID and listener count,
then proves the original SID recovers and another SID reaches normal request
dispatch. The proxy exposes only
`acquire`/`ensure` for a bounded release ID, `status` for the caller, and
`release` for the caller. It cannot send an owner SID, candidate path,
destination, service/account name, raw command line, URL, runtime track, or
arbitrary update choice.

## Owner identity and leases

The owner comes from the proven caller SID, never from the wire. There is
exactly one deterministic lease per canonical owner SID; no path, install
root, or caller-controlled string participates in lease identity. The
canonical SID-only file is bounded to 32 unique records and 16 KiB before an
atomic write. Missing, malformed, duplicate, noncanonical, oversized, or
over-limit lease state fails closed.

`acquire` inserts the caller's lease durably after artifact validation but
before engine activation or runtime provisioning. A failed first acquisition
can therefore leave a lease that blocks uninstall; `release` removes that
lease safely even if no runtime or inventory record exists. `status`,
`acquire`, and `release` always use the caller SID's single lease, so one user
cannot address another SID's lease or service. The lifecycle creates two
standard local users, gives each only its token-derived LocalAppData release
inbox, and has both invoke the same Program Files proxy. It proves distinct
SIDs, leases, services, service SIDs, and stores, plus one-record-per-SID
behavior and lease-only release.

## Signed release manifests and protected floors

Normal users supply only a bounded release ID such as `release-101`. The host
derives that user's `ReleaseInbox\<release-id>` from the caller token. It
opens each manifest and artifact without following reparse points, permits
only read sharing, and streams it to a newly created SYSTEM/Administrators
stage before validation or use. Intake rejects directories, reparse points,
multiple hard links, UNC/remote paths, path-containment changes, zero-length
artifacts, and over-limit input. It rechecks source size after copying,
flushes the destination, and removes a partial destination on failure.
Manifest PEs are limited to 1 MiB; runtime and engine artifacts are each
limited to 64 MiB. Abandoned release stages are removed on host startup and
the next request.

A release manifest is a data-only PE with `PTPUVR_MANIFEST` RCDATA and a
separate Authenticode metadata-signing leaf. Its exact leaf pin is distinct
from the code-artifact signer pin. The signed data includes:

- schema version and bounded release ID;
- security epoch and minimum stable-host version;
- runtime track, version, basename, signed exact byte length, and SHA-256;
- optional engine version, basename, signed exact byte length, and SHA-256,
  or explicit no-engine update;
- test-only deterministic runtime/engine crash phases.

The parser rejects an unknown schema or field, duplicate field, malformed
value, mismatched release ID, basename containing a separator, traversal,
UNC/remote source, zero or oversized file, signed/source length mismatch,
tampering, wrong metadata signer, wrong code signer, wrong
product/track/architecture/version, hash mismatch, and host-version mismatch.
It binds the signer pin to the exact leaf selected by successful
`WinVerifyTrust`, rather than using a subject-name comparison or reopening the
user-writable source.

On a genuine first start, the host creates
`accepted-release-state.txt` containing the initial security epoch and
accepted release-ID/epoch/manifest-hash records. The strict canonical format
is bounded to 32 KiB and 128 releases and is replaced atomically. All replay
and collision checks happen before durable advancement: a lower epoch fails,
the same epoch is accepted only for the same recorded release and hash, and
reuse of a release ID with a different epoch or hash fails as a collision.
Thus there is no split write between accepted epoch and release identity.

Engine and per-track runtime version floors remain protected state independent
of caller metadata. A correctly signed old manifest still fails after its
epoch, engine, or runtime is below the protected floor.

When a manifest names the currently active engine version, acquisition
compares the fully validated staged engine with the exact protected active
engine executable before lease insertion or any journal, floor, accepted
release, or active-engine advancement. Exact-byte equality remains an
idempotent retry; byte-different reuse of the active version fails with
`ERROR_FILE_EXISTS` and `engine version collision policy`.

## Runtime transactions and protection

The engine retains the ordinary-PE multi-runtime model:

```text
protected stage -> strict validation -> transaction journal flush
-> versioned final directory -> stop/repath/start/readiness
-> protected hash/generation inventory commit
-> cleanup/sibling reconciliation -> journal clear
```

Runtime files live in protected versioned Program Files directories and are
never overwritten in place. Each owner receives a derived dynamic
`PtPuvrRuntime_<hash>` service with an `NT SERVICE\<derived-name>` virtual
account and an exact service-SID ProgramData store ACL. The runtime records
denied attempts to write its own binary and a sibling owner store. Startup and
request-time reconciliation handle provisioning, cleanup, stale protected
stage, and crash journals. The durable inventory has a 32-owner guard and
binds each owner/track/version record to the artifact SHA-256 and transaction
generation.

An outer `acquisition-transaction.txt` journal binds owner, release, manifest
hash, previous runtime version/hash/generation, target runtime
version/hash/generation, and exact before/target runtime-floor and
accepted-state hashes. Recovery first converges the inner runtime transaction.
If the target did not predate the acquisition, recovery requires the exact
target version and hash plus a committed or recovered inner transaction. If
the target version and hash already existed, only the explicit post-readiness
outer `runtime-committed` phase can prove success; an inventory version match
alone cannot. Exact same-version/same-hash retries reuse the existing
generation and preserve byte-for-byte durable state. A signed
same-version/different-hash payload is rejected without advancing the
accepted epoch, release state, or runtime floor. Normal readiness failures
likewise roll back floor/security advancement while retaining the already
durable lease for explicit release. Crashing between runtime commit and floor
advancement therefore cannot bypass rollback protection.

Atomic writes remove their own `.new` file on ordinary failure. Before the
host evaluates partial-state initialization, it now validates and recovers
replacement remnants for active-engine state, engine floor, both runtime
floors, accepted-release state, leases, and inventory. A valid primary is
authoritative and a validated stale `.new` is deleted unless the exact
activation/acquisition/runtime/cleanup journal phase proves that the
replacement is the committed target. If only `.new` exists, its exact format,
signature/engine identity where applicable, and owning journal ordering are
validated before a write-through promotion. Lease and journal-free state
replacements can be promoted only after their bounded canonical validation.
Malformed or journal-incoherent replacements fail closed with
`ERROR_INVALID_DATA`; they are never generically promoted. The existing
transaction recovery then converges service, inventory, floor, and accepted
state in the original order.

The lifecycle injects a one-shot crash after inventory commit but before
synchronization. It independently observes the inner and outer journals,
durable SID lease, SCM image path, and inventory target; proves MSI uninstall
is blocked; and then proves retry convergence. Its protected record consumes
the signed crash phase so the retry does not repeatedly crash.

Before uninstall, the elevated non-impersonated MSI check requires canonical
readable protected state, zero leases, zero inventory entries, no
`PtPuvrRuntime_*` SCM services, and no pending runtime, cleanup, acquisition,
or engine-activation journal. The check is non-destructive and runs after
`StopServices`, so a rejected uninstall leaves the MSI files, roots, and host
registration intact. Both full uninstall and feature-level transition of the
host component to absent invoke the same guard, so maintenance mode cannot
bypass an active lease. Only the embedded commit custom action performs irreversible protected-root
cleanup after the successful installer script commit. It retries exact prior
tombstones, validates that roots are non-reparse directories, renames only
the exact roots to exact sibling tombstones, removes and rechecks them, and
deletes the endpoint publication/key. Every rename, enumeration, deletion,
and registry failure contributes an exact internal Win32 result. The commit
action remains nonfatal to Windows Installer (`Return="ignore"`) so cleanup
cannot roll back an already committed uninstall, but it writes a protected
outcome under the separate
`HKLM\SOFTWARE\Microsoft\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototypeValidation`
key with a fresh 128-bit run nonce, UTC FILETIME, stage, and Win32 status.
Lifecycle clears prior outcome state, runs raw MSI uninstall, and before any
fallback asserts that this exact run succeeded and that both roots,
tombstones, and endpoint registry key are absent. A failed assertion keeps
the verdict `FAIL`; the finally path and standalone `Teardown.ps1` may still
perform exact fallback recovery, but that recovery is not reported as MSI
commit success. Final teardown removes the outcome key.

Every active engine and qualification process is created suspended, assigned
to a kill-on-close job, and only then resumed. The host waits on both service
stop and child completion with a 120-second bound. Stop terminates and reaps
the job with `ERROR_OPERATION_ABORTED`, preserves durable transaction and
activation journals for startup recovery, and suppresses activation retry
after stop. The engine diagnostic uses an explicit inherited-handle list so
unrelated host handles cannot leak into the child.

## Build, package, validation, and portable export

Run from an elevated x64 PowerShell 7 (`pwsh.exe`):

```powershell
.\Build.ps1 -Configuration Release -Clean
.\Package.ps1 -Configuration Release -TrustMachine
.\Lifecycle.ps1 -Verb validate -Configuration Release
```

`Build.ps1` builds x64 Release `/WX` host, user client, policy, manifest,
engine, and runtime artifacts. `Package.ps1` dynamically creates test-only
code, metadata, and foreign signing leaves; signs the PEs and companion MSI;
creates all release sets; and records exact certificate ownership. It does
not commit private keys or credentials. `Lifecycle.ps1` always attempts its
exact teardown and writes `artifacts\validation-result.json`.

`Teardown.ps1` is a standalone recovery tool. Owners must release through the
MSI-installed Program Files proxy. The script performs an early zero-lease
check, then relies on the MSI's stricter protected-state/inventory/service/
journal pre-remove check rather than deleting the host or protected roots
directly. A blocked or failed teardown deliberately leaves the test
certificates trusted while MSI, service, or protected state remains, so the
same owner can still use the signed proxy to release its lease. Only after
successful product, service, root, and test-user removal does teardown restore
the exact pre-run certificate state. It removes only the two explicitly
identified prototype users/profiles and only exact certificate entries
recorded as owned by the run.

`Export-PortableArtifacts.ps1` packages the MSI, signed release material,
validation/teardown scripts, and a hash manifest. It requires a clean
worktree, rebuilds and packages from `HEAD`, verifies package metadata names
that same clean source commit, and records build provenance plus every file's
length and SHA-256. It prints the source commit and portable-manifest SHA-256
for out-of-band conveyance. `Run-PortableValidation.ps1` requires those two
external expected values and checks them before trusting bundled certificate
metadata or executing bundled scripts; it then validates every contained
file, provenance record, and reparse-point-free path before running lifecycle
and teardown. The bundle is validation evidence, not a production trust
channel, and values copied from inside the bundle are not an external anchor.

## Release and negative matrix

| Release | Expected outcome |
|---|---|
| `release-101` | Initial runtime `1.0.0.0` on engine `5.0.0.0`. |
| `release-101-collision` | Same release ID with different signed content is rejected before durable advancement. |
| `release-102` | Runtime `1.1.0.0` plus healthy engine `5.1.0.0` activation. |
| `release-103-readiness` | Signed runtime fails readiness and rolls back. |
| `release-104-engine-fail` | Signed `5.2.0.0` engine fails qualification. |
| `release-105-engine-before` | `5.3.0.0` crash before active switch; recovery retains `5.1.0.0`. |
| `release-106-engine-after` | `5.3.0.0` crash after active switch; recovery converges at `5.3.0.0`. |
| `release-107-runtime-crash` | One-shot runtime transaction crash, followed by successful retry at `1.3.0.0`. |
| `release-108-engine-stop` | Test-only `5.4.0.0` qualification blocks once; host stop kills/reaps it promptly, restart rolls back coherently, and retry activates it. |
| `release-109-same-version-collision` | Signed `1.1.0.0` payload with different bytes/hash is rejected without durable state or floor advancement. |
| `release-110-engine-version-collision` | Valid code-signed byte-different `5.4.0.0` engine is rejected with `ERROR_FILE_EXISTS` / `engine version collision policy` while the active `5.4.0.0` bytes, runtime, lease, floors, accepted state, and inventory remain unchanged. |
| `release-201` through `release-208` | Wrong metadata signer, tampered manifest, hash mismatch, traversal basename, stale epoch, host minimum mismatch, code-signer mismatch, and runtime downgrade rejection. |
| `release-209-size-mismatch` | Signed expected length differs from the source and fails exact-size intake before durable advancement. |

The current `validation-result.json` records `PASS` with exact Win32/detail
assertions and explicit events for independently queried MSI/SCM/file/pipe
bootstrap evidence, caller authorization, random endpoint publication and
DACL queried from an authenticated signed-client handle, immediate raw-client
and outside-path rejection with no network path, stable host PID/listener
count and cross-SID dispatch, externally measured four-instance capacity and per-SID quota,
five-second timeout with stable host PID,
old-endpoint squatting,
abandoned-stage cleanup, normal-user leases, signed exact-size and metadata
negatives with unchanged protected-state snapshots, live zero-byte and
max-plus-one manifest/runtime/engine intake, `.new` recovery for every mutable
state file and journal recovery, engine self-servicing, engine activation
crash recovery, stop-aware qualification recovery, exact-byte same-version
engine retry, byte-different active-version engine collision rejection before
durable advancement, same-version runtime acquisition,
runtime transactions/floors, forced repair, blocked feature removal, blocked
teardown with retained trust, blocked uninstall, raw MSI commit-cleanup
outcome/root assertions, and last-uninstall. Final
evidence reports zero prototype services, local test users, Program Files
root, ProgramData root, MSI product registration, and introduced test
certificate entries.

## Proven scope and remaining production work

**Mechanism GO:** the prototype validates a real MSI-owned protected host and
proxy, process/pipe-token-bound normal-user admission, SID-only lease
isolation, an unsquattable per-start endpoint, bounded no-follow signed-length
intake, atomic signed-release state and anti-downgrade floors, versioned
child-engine servicing, hash/generation-aware signed PE runtime transactions,
deterministic journal replacement and outer-acquisition recovery,
repair-preserved host-owned mutable state, guarded maintenance removal, and
exact teardown with trust preservation.

It is not a production-ready installer or update channel. Production still
needs Microsoft-owned signer/key rotation and revocation policy, real release
distribution and telemetry, formal PowerToys/Burn integration, a product UX
for first machine install and last-user removal, production account/tenant
policy, operational observability, and review of diagnostic exposure and the
temporary inherited diagnostic-handle implementation. The dynamically
generated test certificates are validation-only. The WiX build can emit
`WIX1105` when local machine policy blocks ICE validation; that environmental
policy is not suppressed by this prototype. WiX can also emit advisory
`WIX1149` for the Windows Installer `ServiceConfig` non-crash failure flag;
the lifecycle independently queries SCM and proves that the flag and restart
actions are active.
