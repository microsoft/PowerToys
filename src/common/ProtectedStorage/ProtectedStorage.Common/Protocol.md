# Native protected-storage integration contract

## Build and test

From the `src\common\ProtectedStorage` directory:

```powershell
& ..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Release -Path $PWD
& ..\..\..\x64\Release\ProtectedStorage\ProtectedStorage.UnitTests.exe
```

`ProtectedStorage.Native.slnx` includes Common, Client, Bootstrap, Runtime and the
native executable test target. `/p:ProtectedStorageVersion=a.b.c.d` controls both
service PE versions; absent an override, the repository release version is used.
Packaging may instead pass `ProtectedStorageVersionMajor`,
`ProtectedStorageVersionMinor`, `ProtectedStorageVersionBuild` and
`ProtectedStorageVersionRevision=0`; these map directly to the four
`PTPS_VERSION_*` resource definitions. The effective PE version is the MSI's
three-part version plus `.0`. Invalid components, a nonzero revision, or
conflicting full/component overrides fail the project build.
Output is `<repo>\<platform>\<configuration>\ProtectedStorage`. Projects inherit
the repository toolset, SDK, `/W4`, warnings-as-errors and native analysis.

The test target also implements `msbuild /t:Test`. Tests use isolated directories
under their working directory and remove them, including failure paths. No test
creates services, accounts, profiles, persistent signing keys or certificate-store
entries. The CMS fixture contains public certificate/signature bytes only.
Common carries its required Windows SDK import libraries in object-file linker
directives. Version APIs are resolved from the exact System32 `version.dll`,
avoiding the repository's unrelated `version.lib` filename collision. Static
Client/Common consumers need only the project references; they do not need
module-local version/crypto workarounds or the service property sheet.

## Identities and deployment

* Application/root: `PowerToysProtectedStorage`.
* Service: `PowerToysProtectedStorage_<owner SID>`.
* Account: `NT SERVICE\PowerToysProtectedStorage_<owner SID>`.
* Code: `ProgramFiles\PowerToysProtectedStorage\Owners\<SID>\Code`.
* Data: `ProgramData\PowerToysProtectedStorage\Data\<SID>`.
* Policy: `ProgramData\PowerToysProtectedStorage\Policy\<SID>\policy.txt`.
* Installer: `ProgramData\PowerToysProtectedStorage\Installer\<SID>`.
* Fixed SCM command: `"…\Code\Bootstrap.exe" --service "<SID>"`.

The service accepts only its own exact VA token. Bootstrap starts Runtime with
explicit inherited readiness/stop/recovery handles, suspended, then assigns its private
kill-on-close job before resuming. The update coordinator is the same Bootstrap
image copied into a transaction directory, runs under the same VA outside the
worker job, and never starts MSI.

The lifecycle provisioner creates SYSTEM-owned protected roots, policy and
`maintenance.lock`. Code/Data grant writes only to SYSTEM and the exact VA.
Policy/Installer are SYSTEM-controlled; the VA can read the maintenance lock.
The service validates fixed paths, non-reparse ancestors, hardlink counts and
ACLs. It never makes private Code readable by the owner.

## Signed release unit

Code contains **all six** files:

```text
Bootstrap.exe
Runtime.exe
manifest.txt
manifest.p7s
ClientCatalog.json
ClientCatalog.p7s
```

`manifest.txt` is bounded ASCII key/value text, with each required key exactly once:

```text
format=1
app=PowerToysProtectedStorage
version=0.0.1.0
bootstrap_sha256=<lowercase SHA256>
runtime_sha256=<lowercase SHA256>
catalog_sha256=<lowercase SHA256 of ClientCatalog.json>
```

Policy has `format=1`, `app=PowerToysProtectedStorage`,
`signer_sha256=<SHA256 of DER signing certificate>`, and
`minimum_version=a.b.c.d`, one key per line. Detached CMS must have exactly one
signer, SHA256 digest, and the exact policy-pinned signer. This is explicit
certificate pinning, not an online revocation/expiration service. Trust-root
rotation remains a SYSTEM-authorized policy operation.

Catalog JSON:

```json
{"format":1,"app":"PowerToysProtectedStorage","version":"0.0.1.0","clients":[
  {"image":"PowerToys.WorkspacesEditor.exe","sha256":"<hash>","role":"workspaces.writer"},
  {"image":"PowerToys.WorkspacesLauncher.exe","sha256":"<hash>","role":"workspaces.launcher"},
  {"image":"PowerToys.ProtectedStorageMsiAction.exe","sha256":"<hash>","role":"maintenance.ca"}
]}
```

Multiple entries allow compatible older client hashes and multiple roles. Allowed
roles are `workspaces.writer`, `workspaces.reader`, `workspaces.launcher`,
`workspaces.preview`, `workspaces.arranger`, `maintenance`, and `maintenance.ca`. Only the writer may
initialize the repository or acknowledge source cleanup. Launcher may read and CAS
replace it. Preview permissions never permit repository initialization.
Preview access requires the explicit `workspaces.preview` role, rather than being
implied by writer/launcher. Editor therefore receives writer+preview, Launcher
launcher+preview, and SnapshotTool preview. The Arranger's `workspaces.arranger`
role exists only for signed module-handoff peer verification and grants no
repository or transient API access. Actual ModuleServices hosts receive reader.
Expected peer-role matching is exact; there are no alternate labels or aliases.

The CA must be built **before** the catalog. Neither the catalog nor a manifest
containing its hash may be embedded in that CA. Store payload/manifest/catalog
in separate MSI Binary entries to avoid a transitive self-hash cycle.
The implemented production CA is an executable that reads these separate Binary
streams from the matching signed MSI. The build graph is therefore:

```text
data clients + Bootstrap/Runtime + MsiAction (version/signing pin only)
  -> signed ClientCatalog containing final MsiAction hash
  -> signed manifest containing catalog hash
  -> Lifecycle / carrier MSI
  -> Broker
  -> Setup
```

Setup, Broker and Lifecycle are not catalog-authorized blob writers or maintenance
callers. Setup is deliberately absent from the catalog. Its owner-bound read-only
Status/Capabilities calls do not need its final self-containing executable hash.
Owner staging must grant the exact VA read/traverse on payload files/ancestors.
No unverified allowlist is accepted.

Prepare authenticates the new maintenance caller using the independently verified
complete candidate bundle. Matching transaction queries/decisions first use
protected signed OLD/NEW catalog snapshots with their recorded exact hashes,
even when the request includes an owner-stage path. A missing/corrupt optional
owner stage cannot block an original authorized caller's prepared decision.
Catalog snapshots are persisted for same-version `unchanged` operations too.

Incoming proof is only needed for Prepare, absent/pre-transaction Query/Rollback,
or a NEW release caller querying a previous TERMINAL operation before beginning
its own upgrade. Such a historical Query first tries the protected authority;
incoming proof is a fallback only when that caller is not in the old snapshots.
Pending decisions/queries never replace protected authority with owner material,
and damaged protected proof fails closed. Incoming proof must satisfy SYSTEM and
running-version floors. This extension grants maintenance roles only and cannot
extend data authorization; only Prepare stages or publishes payload.

## Process and pipe authentication

The client grants the exact service VA query-only access to its process
(`PROCESS_QUERY_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE`,
mask `0x101400`) and token (`TOKEN_QUERY`), preserving
the existing DACL. No `VM_READ`, handle duplication or mutation rights are granted.
Native `ConnectOwnerPipe` does this automatically; managed clients must match.

Connections use static `SECURITY_IMPERSONATION` under the **filtered owner token**.
An elevated same-owner caller uses its `TokenLinkedToken` while opening the pipe,
then reverts immediately. An unfilterable elevated token is rejected, never
silently handed to the VA. The server checks actual pipe owner, process owner,
PID, birth time and live process handle. It impersonates only that non-elevated
owner while opening pinned client images, then reverts before storage operations.
This permits per-user installations without loosening executable-directory ACLs.
Some elevated contexts expose a linked token with TokenImpersonation/
SecurityIdentification rather than a usable primary or SecurityImpersonation
token. Native preflight explicitly checks type, level, non-elevation and same
TokenUser and returns `OwnerContextRequired` (native 1346 for unusable level)
before service lookup/pipe waiting. It does not attempt to promote the token,
fall back to the elevated token or misreport absent storage. Pipe connection
restores any previous thread token. A real ordinary same-owner context is
required when the linked token is unusable.
Maintenance additionally requires the caller's actual process primary token to
be non-elevated, in both MsiCall preflight and server authentication. A filtered
pipe impersonation token is not authority to run MSI from an elevated process.
Thread `RunImpersonated` around Process.Start/CreateProcess does not change the
child's primary token. Elevated/SYSTEM maintenance must defer to the correctly
bound normal-owner worker, never relax Setup's normal-owner guard.

`NtQueryInformationProcess(ProcessImageFileMapping=44)` verifies that the pinned
`FILE_EXECUTE` image handle is the actual process image section, not a same-named
replacement. Unsupported/failing image-binding queries deny authorization.
Data-client image SHA256 and filename must match the signed catalog. For an
approved maintenance executable only, the filename is not an authorization
input because Windows Installer assigns extraction names. The actual mapped
executable, exact catalog hash and maintenance role are still required. Loading
a trusted DLL inside arbitrary `msiexec.exe` is not an authentication path.

Only control Status and data GetCapabilities allow catalog-free owner-bound
read-only inspection. These still bind the pipe token to the exact owner process,
PID and birth. They grant no roles, never read blobs, and cannot write, initialize,
acknowledge cleanup or mutate maintenance state. Every other command retains
approved-image/catalog authorization.

Clients check the actual server VA SID, fixed Runtime/Bootstrap image, live
PID/birth and fixed SCM account/ImagePath. Bootstrap PID must equal SCM PID.
Runtime must have that exact live Bootstrap parent generation. No client opens
private Code files to authenticate the server. OTS credentials never confer
another owner's access.

### Reusing catalog trust for module-owned handoffs

Native `ProtectedStoreClient::GetSignedClientCatalog()` requests command 1 with
`{"includeClientCatalog":true}`. The `signed-peer-catalog` capability advertises
this optional response: `catalogLength` in metadata and opaque bytes consisting
of exactly that many UTF-8 catalog bytes followed by the detached CMS signature.
The service revalidates the live proof before returning it. This exposes signed
public release metadata only, never Code executables or owner blobs.

`Common\Authentication.h` exposes:

```cpp
SignedClientCatalog { std::vector<BYTE> document, signature; };
CallerIdentity VerifyPeerImage(
    HANDLE process, uint64_t expectedBirth, const std::wstring& originalOwnerSid,
    const SignedClientCatalog&, const std::string& expectedRole,
    const std::wstring& expectedImagePath);
void AllowPeerIdentityQuery(const std::wstring& peerSid);
```

`VerifyPeerImage` opens only the fixed SYSTEM-owned Policy for the original
owner, validates its protected ancestors/ACLs, checks the pinned CMS signer and
minimum release, then verifies the actual mapped process image, expected path,
exact catalog hash and role. Its result retains process/image/ancestor handles.
The original owner must be an actual endpoint's TokenUser, not an unrelated
caller-selected account. The module must derive it from the authenticated
**original launcher**, and must separately bind the exact process handle/PID,
creation time, Windows session, fixed sibling image and immutable handoff.
Launcher uses `workspaces.launcher`; Arranger uses `workspaces.arranger` (not reader).

The signed proof can travel within the module's read-only handoff so an OTS
arranger does not need access to the owner's service endpoint or private Code.
Policy is public-read/SYSTEM-write metadata; no profile-wide or Code read ACL is
added. For cross-owner process observation, the target can call
`AllowPeerIdentityQuery` for the already bound peer SID: only process query/
synchronize and token-query access is added, never VM reads, handle duplication,
impersonation or mutation. This verification API grants no data-store authority.

## Data protocol v1

Endpoint: `\\.\pipe\PowerToysProtectedStorage.Data.<SID>`, local-only byte pipe,
one bounded connection at a time. Each connection carries one request/response.

| Offset | Field |
|---|---|
| 0 | ASCII `PTPS` |
| 4, 6 | little-endian uint16 major=1, minor=0 |
| 8 | little-endian uint32 command |
| 12 | little-endian uint32 headerLength=36 |
| 16 | little-endian uint32 bodyLength |
| 20 | 16 UUID bytes in RFC/network order |
| 36 | little-endian uint32 metadataLength, UTF-8 JSON, opaque remaining bytes |

Metadata is at most 64 KiB, a blob at most 16 MiB. Integer revisions are decimal
strings. Duplicate keys, invalid UTF-8/surrogates, excessive depth/node count,
overflow, unsupported versions and malformed/trailing encoded frame bytes fail.
Request ID is transport correlation; operation ID is durable write identity.
Network request IDs must be non-nil. The shared storage/digest codec also supports
nil IDs for internal records; pipe ReadFrame/WriteFrame enforce the stricter
network rule.

| ID | Command | Request metadata | Response |
|---|---|---|---|
| 1 | GetCapabilities | `{}` | protocolMajor/minor, release, maxima, features, maintenance, recoveryRequired |
| 2 | GetState | target | state, autoImportSuppressed, optional revision/migrationSource/initializationOperationId, cleanupAcknowledged |
| 3 | GetBlob | target | target, revision, contentSchema + opaque bytes |
| 4 | PutBlob | target, operationId, contentSchema, condition, optional migrationSource + opaque bytes | operationId, outcome=`Committed`, revision |
| 5 | QueryWrite | target, operationId | operationId, outcome=`Committed`/`NotCommitted`/`Unknown`, optional revision |
| 6 | AcknowledgeSourceCleanup | target, initialization operationId | operationId, cleanupAcknowledged |
| 7 | CreateTransient | target, operationId, contentSchema + opaque bytes | server-generated id, operationId |
| 8 | GetTransient | target, id | target, id, revision, contentSchema + opaque bytes |
| 9 | DeleteTransient | target, id | id; already absent is idempotent success |
| 10 | UpdateTransient | target, id, operationId, contentSchema, IfRevision condition + opaque bytes | id, operationId, outcome=`Committed`, revision |

Revision: `{"epoch":"<canonical UUID>","sequence":"1"}`.
Condition: `{"kind":"IfUninitialized"}` or
`{"kind":"IfRevision","expected":{...revision...}}`.
Successful metadata includes `errorCode:"None"`. Errors include string errorCode,
uint32 nativeCode, retryClass, messageKey and operationId. A transport timeout is
not proof a write failed. Query the original operation; do not change its bytes,
condition or ID to force replay.
The native client validates response framing, correlation, error envelopes and
mutating success DTOs before interpreting an explicit server error. Any transport/
parsing/identity failure after a mutation write attempt becomes OutcomeUnknown
with the original operation ID. Only a validated server rejection (for example
RevisionConflict) retains its known error. Local pre-write validation stays known.
A validated server `retryClass:"QueryOutcome"` is still OutcomeUnknown, including
a server Timeout response; it preserves the request's original operation ID.

The public native API is `ProtectedStorage.Client\ProtectedStoreClient.h`,
namespace `PowerToys::ProtectedStorage`, including `ProtectedStoreClient`,
`MaintenanceClient`, `StorageError`, `Revision`, `WriteRequest` and `TargetInfo`.

## Durable state and migration receipts

The lifecycle provisioner seeds `storage.index` **once for brand-new data only**:

```json
{"format":1,"epoch":"<new canonical UUID>","targets":{"workspaces.repository":{
  "sequence":"0","generation":"","receipts":[],"cleanupAcknowledged":false
}}}
```

Retained data must never be reseeded because the index is missing. An absent,
invalid or inconsistent index/initialized generation returns RecoveryRequired.
There is no empty-data, plaintext, old-file or preview fallback.

Explicit purge retains a SYSTEM-owned `Installer\<SID>\purged` tombstone containing
exactly `format=1`, `owner=<SID>` and `mode=purge`, one key per line. Runtime validates
its directory/file ownership, DACL, non-reparse/single-link identity and content,
then exposes `autoImportSuppressed:true` on GetState. Malformed/inaccessible policy
fails closed as RecoveryRequired. Missing wire suppression metadata is not silently
treated as false; clients require this field (`purge-suppression` capability).

After a proven purge, Lifecycle creates a new index/epoch only for the newly
created empty Data tree, and never removes the suppression tombstone. The module
initializes a freshly validated empty document without `migrationSource`, rather
than auto-importing leftover legacy files. Runtime rejects migration-source writes
while suppression is active. Ordinary KeepData retains the complete existing Data
tree and initialization/receipt state unchanged.
Because the purge marker is historical and permanent, it must not override a
later `retained` (KeepData) receipt or authorize reseeding a subsequently missing
index. Lifecycle must establish that the latest authorized removal was a purge
before treating absent Data as a new store.

An immutable `blob.<UUID>` generation combines record metadata and opaque payload
with a payload hash. It is flushed before the single atomic index replacement.
The index simultaneously publishes the generation, revision, operation digest
and receipt. Generation creation and index publication are **not** represented
as a multi-file OS transaction. Before publication an orphan is uncommitted;
after publication the same operation is queryable even when its response was lost.
Recovery validates the full index and referenced records before reclaiming orphans.
The runtime supplies mandatory private-file ACL validation to the store.

The last 128 write receipts per target are retained. Missing historical receipts
return Unknown once any write exists; NotCommitted is used only for a logically
never-initialized target. Original stale CAS/initialization requests cannot replay.
Migration source metadata and its initialization operation ID survive later CAS
writes and receipt pruning. Source cleanup acknowledgment is durable and idempotent.
The service does not interpret source identity or workspace content.

Transient blobs are bounded to 64 objects/64 MiB per runtime, with a 15-minute
lifetime, operation deduplication while live, and explicit deletion. They are
deliberately lost across a runtime restart and never initialize durable storage.
The `transient-cas` capability advertises command 10. A new transient revision is
`{epoch:server-issued object UUID,sequence:"1"}`; updates require the revision
returned by GetTransient, increment the sequence, and retain 128 update receipts
for same-operation retries. They do not extend the original expiry. An Editor
can create a slot and pass its UUID to SnapshotTool, which reads its revision and
CAS-updates the same owner/role-authorized slot. The UUID is not bearer authority.
No service-side cross-owner handoff grant exists: Workspaces launch-plan transfer
to an elevated/OTS arranger remains module-owned, authenticated and read-only.

## Maintenance compatibility endpoint

`\\.\pipe\PowerToysProtectedStorage.Control.<SID>` retains the proven message-pipe
MSI protocol, separately from PTPS data framing. `Request` is exactly 8,280 bytes:
uint32 magic=`0x3143534d`, uint32 command, 40 UTF-16 operation characters, 4,096
UTF-16 bundle-path characters. All unused string elements must be zero.
Commands: Status=1, Prepare=3, Commit=4, Rollback=5, Query=6; value 2 is retired.
Prepare requires the bundle field. Query/Commit/Rollback may carry it for the
limited caller-proof cases above; matching protected transaction authority takes
precedence and Status rejects it. Used proof handles remain pinned through
dispatch and response, including the Prepare copy.
Each response is UTF-8; send a uint32 magic acknowledgment after reading it.
The typed compatibility helpers are `MsiCall` and `MaintenanceClient`.

MSI response keys: format=1, operation, phase, version. Phases are preparing,
prepared, committed, rolled_back, unchanged, recovery_required or absent.
Status/error responses retain the compatibility JSON shape.

### Read-only inspection for Setup sync

Use `Command::Status` through `Call(paths, Request{})` (or
`MaintenanceClient::GetStatus()`), not `MsiCall`/a newly generated query operation.
Status is owner/PID/birth authenticated and deliberately does not require a Setup
catalog hash or a bundle, preventing resource/signature build cycles.

Successful Status JSON includes these top-level fields:

```json
{"ok":true,"version":"0.0.1.0","protocolMajor":1,"protocolMinor":0,
 "workerReady":true,"dataRecoveryRequired":false,
 "hasUnresolvedTransaction":false,"maintenance":false,"healthy":true,
 "currentOperation":"","phase":"none"}
```

The existing owner/service, nested bootstrap/worker process identities/versions,
and `update.operation`/`update.phase` diagnostics remain present. `version` is the
verified live pair's version, not MSI inventory. `workerReady` requires the actual
SCM Bootstrap generation, live validated worker and inherited readiness signals.
Runtime reports startup/observed storage recovery through an inherited event;
recovery is sticky for that worker generation. Status revalidates the signed
release and treats a busy maintenance lease conservatively as maintenance.

`healthy` is true only with a ready worker, no reported data recovery, no pending
transaction, no drain and an idle maintenance gate. Settled states are `none`
(no current operation), `committed`, `rolled_back`, and `unchanged`; every other
current phase is unresolved. In particular, Query of a new/random operation can
return `absent` while a **different** current operation is still prepared. That
result is never proof that the service is healthy or idle.

Setup sync must first reconcile real-owner inventory/instance presence. Absent is
a no-op, equal installed/live/package version with `healthy:true` is a no-op,
and an older healthy owner instance can use the normal owner MSI upgrade.
Partial, newer, recovery or unresolved state fails rather than provisioning,
showing UAC or guessing an installer decision.

The same SYSTEM-controlled maintenance lock serializes accepted writes and update/
repair/remove. Preparing an update denies new data operations, drains accepted
writes, replaces the six-file release with the old worker stopped, and verifies
both processes before reporting prepared. Writes stay blocked until a terminal
decision. A durable MSI commit is never automatically rolled back because later
readiness fails; such failures require recovery. Interrupted or unknown actors
fail closed rather than guessing an installer outcome.

## Acceptance still requiring packaged/isolated-machine execution

Native unit tests do not claim deployed-service acceptance. Verify signed MSI
provisioning, both install scopes, ordinary/elevated same-owner clients, denial of
another owner and an unsigned same-owner image, real randomized CA loading, the
supported Windows image-binding API matrix, catalog rollover across real releases,
power interruption at publication boundaries, busy maintenance during saves,
prepared/commit/rollback crashes, keep-data reinstall, and no UAC/reboot on ordinary
updates. Certificate pin rotation/revocation policy and release signing must be
validated by the production packaging/release infrastructure.
