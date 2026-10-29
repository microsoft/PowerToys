# Workspaces protected storage

Workspaces' live repository is the `workspaces.repository` target of the shared,
per-owner Protected Storage service. The module (not the service) owns JSON
validation, migration and launch semantics. An unavailable boundary fails closed;
other PowerToys modules remain usable.

## Data and user workflows

- Editor initializes the target with a complete, validated legacy document using
  `IfUninitialized`. Empty documents are valid. Invalid documents are never
  partially imported.
- Legacy cleanup uses a committed source receipt containing file identity,
  length, hash and migration ID. Cleanup verifies and deletes the opened object.
  A changed source is never deleted. **Retry cleanup** only retries cleanup; it
  does not re-import an initialized repository.
  Both snapshot reading and cleanup run under the ordinary same-owner token,
  even when Editor is elevated; an unavailable filtered owner token fails closed.
- After explicit purge, the SYSTEM-owned tombstone is exposed by the service as
  `autoImportSuppressed`. Initialization creates an empty protected collection
  without opening the legacy file; only an explicit Import can bring data back.
  A target-state response without this policy flag is incompatible
  and cannot authorize automatic migration.
- Normal saves use the loaded revision. Conflicts preserve the draft and require
  reloading before applying changes. An uncertain save queries its original
  operation rather than writing a fresh whole-document replacement.
  Transport, identity recheck, envelope, and committed-response validation
  failures after a mutation attempt remain `OutcomeUnknown` with the original
  operation ID. Only a fully validated server rejection is treated as known.
- **Import** explicitly replaces the collection in one validated CAS operation.
  **Export** writes a user-selected copy, which has no live storage authority.
- Capture creates a service-issued preview. The snapshot process
  returns only its reference over its inherited stdout pipe. Editing creates a
  revision-conditional update after GetTransient; uncertain retries retain the
  same operation ID, bytes and expected revision. Cancellation releases only
  the current preview. An expired object can be recreated from the validated
  in-memory draft, never from a legacy temporary file.
- LaunchAndEdit requires an explicit preview reference. Neither the launcher nor
  arranger reads `temp-workspaces.json`, and no live path falls back to the old
  `workspaces.json`.
- Launcher merges installation metadata by workspace and application identity
  into the latest collection with bounded CAS conflict retries. Last launch time
  is persisted explicitly after launch, not in a destructor. Retrying that
  metadata write does not launch applications again.

## Shared GUI/CLI execution

GUI and CLI use main's `WorkspacesSession::Start/Execute` core. Saved selections
and CLI listing read the authenticated repository; metadata and history updates
use its CAS operations rather than `WorkspaceStore` file replacement. Legacy
file helpers remain import/export utilities, not live storage fallback.
Launch-and-Edit still requires a protected preview reference.

An elevated entry sends a selector, not authoritative workspace bytes, through
main's same-user/session `SessionHandoff` to a verified Medium host. That host
loads the protected snapshot before executing. A caller-supplied snapshot on the
private host entry is rejected. CLI has its own catalog-authorized launcher role.

## Arranger session

The shared Medium executor sends its validated repository/preview snapshot over
the operation-scoped IPC from PR #51114. The arranger waits for both endpoints,
requests the snapshot, checks its protocol/operation/workspace identity, and
uses the immutable result for window placement. Malformed, oversized or missing
snapshots fail without falling back to a file.

The existing peer policy remains enabled in both directions. The arranger pins
the executor PID and accepts only the expected CLI/Launcher directory, basename,
version and Release signature in the same Windows session. The reverse direction
checks the Arranger module identity and session; it does not claim an exact
child-PID pin. The launcher separately retains the ShellExecute process handle
for operation lifetime and cleanup. Readiness, cancellation, final-result
acknowledgement and callback teardown remain the main implementation.

There is no second protected-plan pipe, `--plan` argument, cross-IL query grant
or query-handle transfer. Module IPC uses upstream limited-query authentication,
not the retired bidirectional mapped-image proof. This does not claim those
identity guarantees are equivalent: mapped-image/catalog/owner/role checks remain
mandatory at the data-service boundary and are unchanged.

Arranger has no current data-client catalog entry, never opens an owner's
repository or legacy file, and does not launch workspace applications. The
Medium executor retains application launch, confirmation and UAC behavior.
OTS remains a separate acceptance scenario; no credential user's profile may
replace the original owner's selected snapshot.

## Setup and failures

`ProtectedStoreClient.GetCapabilitiesAsync()` reports the authenticated owner's
runtime release, protocol, limits, maintenance/recovery flags, and supported
storage features. Workspaces verifies these for native and managed repository
operations. Editor's `WorkspacesRepository.EnsureReadyAsync()` first tries the
actual compatible, catalog-authorized boundary. Only an incompatible/unauthorized
older existing runtime triggers `ProtectedStorageSetupClient.SyncAsync()`, followed
by fresh capability and data-authorization checks before migration. Background
enumeration never performs setup or assumes the main product update updated the
owner runtime.
Client executable versions are not required to equal the runtime release:
supported protocol/features and the signed catalog's actual approved client
hashes govern compatibility. Capabilities never authorize a data read or mutation;
GetState/GetBlob must still pass mapped-image/catalog authorization.

Shared managed maintenance entrypoints are `InspectAsync()`, `SyncAsync()`, `EnsureReadyAsync()`,
`RepairAsync()`, and `RetryFailedOperationAsync(Guid)`. They invoke only fixed
verbs on the adjacent Setup executable, using the current non-elevated owner.
Maintenance checks the actual process primary token, not an impersonated thread
identity; it never treats filtered pipe impersonation as permission to run MSI.
`RepairAsync()` is ordinary matching-release repair without UAC.
`RepairBootstrapWithAuthorizationAsync()` is the separately named, explicit-user
bootstrap-recovery operation that may request authorization.
Setup is resolved beside the main `PowerToys.exe`, including when Editor is in
a subfolder. Their `MaintenanceResult` is a presentation observation, not storage authority.
The distributable Setup originates from
`artifacts\ProtectedStorage\<arch>\<config>\Package\PowerToys.ProtectedStorageSetup.exe`.
`Build-Carrier -Package` verifies and atomically publishes it into the main
BinDir root. Outer packaging uses `$(ProtectedStorageSetupPath)` together with
`RequireProtectedStorageRelease`, which verifies equality with the signed staged
artifact. The raw `<arch>\<config>\ProtectedStorage\` compilation output is not
a production Setup and must not be shipped or substituted.
`SyncAsync()` never provisions an absent instance or requests UAC. A maintenance
retry passes its original operation ID; it does not construct installation
commands from a UI failure record.
Only known busy maintenance failures use `retry`; unknown outcomes use read-only
`inspect`. Native reboot results `3010`/`1641` remain successful observations but
require explicit readiness/restart handling rather than being reported as rollback.
Setup's operation reference is 32 hexadecimal characters (`Guid` format `N`),
distinct from the PTPS data protocol's format-`D` operation IDs. Observed
ProductCode and installedVersion are retained only for presentation.

Only an explicit Editor **Set up / repair** button starts an authorization-capable
Setup operation. Automatic synchronization and inspection cannot request UAC.
Ordinary operation retries do not reinstall
the carrier or create background UAC loops. ModuleServices background enumeration
returns an error when storage is unavailable instead of returning an empty list.
Startup migration and setup errors block Workspaces, not the runner.

## Ordinary-owner launch boundary

Runner's Workspaces module starts Editor through `common\utils\owner_process.h`.
An elevated same-owner Runner uses the actual interactive shell's primary token,
after checking the exact owner SID, Windows session, medium integrity,
non-elevation and fixed Windows Explorer image. The child is created suspended
with the correct owner environment, its actual token is rechecked, then it is
resumed. No identification-only linked token is promoted and no other desktop
user is selected.

Elevated direct GUI/CLI launch entries use main's shared `SessionHandoff`, with
same-user/session and actual Medium-token validation before execution. They wait
for the typed host result instead of reporting success after an untracked launch.
A different-owner shell, missing normal context or Session 0 fails explicitly.
This launch boundary does not weaken the data client's authentication checks.

Normal Editor children (Launcher and SnapshotTool) therefore inherit the
ordinary owner token. The arranger remains the separately bound, possibly
OTS-elevated placement consumer. Directly starting an otherwise unsupported
high-token data client outside these owner-launch entrypoints still fails closed.

## Validation

Build from the respective project directories using `tools\build\build.ps1`
with the normal platform/configuration. Managed tests are in
`src\common\ProtectedStorage\tests\ProtectedStorage.Client.Managed.UnitTests`
and run with the repository's MSTest executable harness. Native tests use
`WorkspacesLib.UnitTests` and Visual Studio's test runner.

Unit tests use mock transports and project-local input files, never install a
service or trust a certificate. Shared validation vectors cover both codecs;
malformed protocol and workspace byte corpora exercise input boundaries.
Packaged signed multi-user/UAC workflows require the separate installation
integration environment and are not simulated by these unit tests.
