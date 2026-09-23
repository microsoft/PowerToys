# PowerToys Protected Storage carrier

The carrier is a separate, per-owner `USERUNMANAGED (2)` MSI. The main PowerToys
installer must run owner maintenance **after** its own MSI transaction exits.
Never install the carrier from a main-MSI custom action, under SYSTEM, or under
the alternate administrator used for an over-the-shoulder UAC prompt.

## Build

Run from a PowerShell 7 developer terminal at the repository root:

```powershell
.\installer\PowerToysProtectedStorage\Build-Carrier.ps1 -Platform x64 -Configuration Debug -RunTests
.\installer\PowerToysProtectedStorage\Tests\Test-Authoring.ps1 -Compile
.\installer\PowerToysProtectedStorage\Tests\Test-ReleasePublication.ps1
```

The build invokes the repository `tools\build\build.ps1` wrapper. Native outputs:

```text
<Platform>\<Configuration>\ProtectedStorage\
  PowerToys.ProtectedStorageSetup.exe
  PowerToys.ProtectedStorageProvisionBroker.exe
  PowerToys.ProtectedStorageLifecycle.exe
  PowerToys.ProtectedStorageMsiAction.exe
  ProtectedStorage.InstallerTests.exe
```

Local compilation does not create certificates or change trust stores. These
unfinalized binaries deliberately reject privileged/package execution without
release resources. The authoring test builds and deletes a non-installable MSI
using invalid signature fixtures; it never installs anything.
The publication test uses a process-local signature mock and project-local
fixtures to verify exact public Setup bytes; it neither signs nor publishes a
production artifact.

Finalize a release with `Build-Carrier.ps1 -Package`, providing all of:

* `-Version <major.minor.build[.0]>` (MSI bounds; a nonzero fourth component is rejected);
* `-SigningCertificateThumbprint <existing CurrentUser\My certificate>`;
* `-ExpectedSignerSha256 <approved SHA-256 of that certificate's DER bytes>`;
* `-TimestampServer <release timestamp URL>`;
* `-ClientCatalogInput <JSON release-client inventory>`.

Native projects receive `ProtectedStorageVersion=a.b.c.0` and matching component
properties; the separate WiX carrier build receives the three-part MSI version.
For example, pipeline version `0.101.3000.0` produces carrier MSI version
`0.101.3000` and PE version `0.101.3000.0`, not a five-component version.

### External signing stages

`-ReleaseStage` runs one assembly stage without requiring a local private key.
Supply platform, configuration, and version on every invocation, plus
`-ExpectedSignerSha256` for every stage after `RuntimePayloads`. The stages are:

| Stage | Output requiring external signing before the next stage |
|---|---|
| `RuntimePayloads` | `Bootstrap.exe`, `Runtime.exe` (Authenticode; no policy embedded yet) |
| `Action` | `PowerToys.ProtectedStorageMsiAction.exe` (Authenticode; embeds the explicit carrier signer pin) |
| `Payloads` | Combined runtime/action stage for callers that already have the approved pin |
| `Documents` | `ClientCatalog.json`, `manifest.txt` (detached CMS, SHA256, one signer) |
| `Lifecycle` | `PowerToys.ProtectedStorageLifecycle.exe` (Authenticode) |
| `Carrier` | `PowerToys.ProtectedStorage.Carrier.msi` (Authenticode) |
| `Broker` | `PowerToys.ProtectedStorageProvisionBroker.exe` (Authenticode) |
| `Setup` | `PowerToys.ProtectedStorageSetup.exe` (Authenticode) |
| `Publish` | Verifies the completed release and atomically publishes Setup |

`Documents` also requires `-ClientCatalogInput`. Client hashes must be calculated
after their final signing operation; do not rebuild or re-sign those clients
afterward. CmdPal uses its own version train, so its executable is bound by
the catalog's exact signed-file hash, not by requiring its FileVersion to
equal the carrier version. `AdditionalBuildArguments` forwards approved
MSBuild configuration (for example the release NuGet config) to each assembly
build. Missing, incorrectly signed, or mismatched preceding-stage inputs fail.

The same stages back local `-Package` signing. `Compile` remains the default,
non-deployable local build. External signing does not create developer keys,
export keys, change trust stores, or waive the release verification gate.

The signed ADO pipeline runs these stages after final client signing and before
either main MSI. It reuses the existing ESRP signing identity and `CP-230012`.
Authenticode uses `SigntoolSign`; documents use `Pkcs7DetachedSign` with
`P7CE=/p7ce DetachedSignedData`, `P7EKU=1.2.840.113549.1.7.1`, and
`FileDigest=/fd SHA256`. This contract and the replacement behavior are
demonstrated by Microsoft's [MCP signing pipeline](https://github.com/microsoft/mcp/blob/c5c6ce286686d616528079af31c979c8fade84a8/eng/pipelines/templates/jobs/mcpb/pack-and-sign-mcpb.yml).
The pipeline submits copies named `manifest.p7s` and `ClientCatalog.p7s`;
ESRP replaces their contents with DER signatures, leaving the originals intact.

For `RuntimePayloads` only, no pin is required: neither runtime embeds the seed
policy. After signing, the pipeline derives the pin from Bootstrap and verifies
Runtime matches before building the pin-bearing MSI action.
The exact DER pin is taken from this job's final ESRP-signed Bootstrap, then
required for every carrier artifact and CMS signer. The signing identity must
be authorized for both operations and return the same certificate. Permission
denials, certificate changes, or unexpected output stop packaging. No permission
is granted automatically, and the task's Key Vault request-authentication
certificates are never used as product-signing keys. Certificate rotation still
requires the explicit protected-policy maintenance described in the design.
Before embedding signatures, the pipeline checks both `SignedCms` and the
native runtime's `CryptVerifyDetachedMessageSignature` API, plus one-signer,
SHA256, exact pin, version, and payload-hash requirements.
Client Authenticode certificates are not the carrier's update authority and need
not equal its pin. CI verifies the clients' final signatures and the pinned CMS
binds their exact hashes and roles. Reusing an older cached client signature must
not select the trust root for newly signed service/installer payloads. This does
not relax the native exact-pin checks for carrier files, manifests, or updates.
All four installer helpers carry PE version resources, including the embedded
MSI action, Lifecycle and Broker; the authoring compile test checks them.

`Tests\Test-ReleaseTools.ps1` exercises version normalization and real in-memory
CMS verification, including altered data, wrong pins, multiple signers, SHA1,
and malformed signatures. It never installs its short-lived test certificate.
`Tests\Test-ProjectEvaluation.ps1` checks native version properties without VC
imports, reproducing the SDK-only evaluation used by CI's `dotnet restore`.
Both native property sheets load the repository version explicitly when that
evaluation has not imported `Directory.Build.props`; they never invent a fallback
release version.
The standalone installer solution includes the bootstrapper's updater and
SettingsAPI dependencies, not just its direct DLL projects. The installer
contract test checks that complete project-reference closure so static-graph
builds retain the requested configuration and architecture.

### Release qualification blocker (2026-09-24)

The signed pipeline is wired, but release `0.101.3000.0` has **not** produced
an installable main package. [ADO run 158436898](https://microsoft.visualstudio.com/Dart/_build/results?buildId=158436898)
on commit `eebf8c1e8e961188041e5087ac4ba75a94a8af5f` completed with failures:

* ARM64: Bootstrap and Runtime returned different valid Authenticode signer
  certificates from the same ESRP signing task (failure log 205).
* x64: the manifest's detached CMS signer differed from the pinned
  Authenticode signer (failure log 298).

The observed DER SHA256 identities were
`c30b441672c82883d92eddac6d24cb57e9960bda4486c7fb5865e74157f35850` and
`d33927e4dda9b91def9f8ed282549a49217ed8cacf54577a690963cbc5eff3ed`.
These are diagnostic observations, **not an approved trust allowlist**.
Matching publisher and issuer names do not make the certificates identical.
The selection mechanism (including any rotation or caching) is unconfirmed.

Therefore the single-DER-certificate requirement is not established by merely
using the same ESRP key code. An ESRP-supported immutable-certificate contract,
or an explicitly approved bounded signer-policy redesign, is required before
claiming release readiness. No exact-pin check has been disabled, no unknown
certificate is automatically enrolled, and no unsupported signing parameter is
used. Blind retries or restricting the build to x64 do not resolve this issue.
Failure artifacts preserve signature/CMS metadata and native build logs.

The inventory has a `clients` array of `{ "path": "...", "role": "..." }`.
Supported client roles are `workspaces.writer`, `workspaces.reader`,
`workspaces.launcher`, `workspaces.preview`, and the handoff-only
`workspaces.arranger`. Paths select already-signed
release build outputs; no path is embedded as caller authority. The signed
catalog contains filename, exact hash, role, and release.
Setup, Broker, Lifecycle, Bootstrap, Runtime, and MsiAction cannot be supplied
as data-client inventory entries. The builder adds the signed MsiAction
maintenance entry itself, after compiling/signing that executable.
Preview is explicit: Editor needs writer+preview, Launcher launcher+preview,
SnapshotTool preview, and actual ModuleServices hosts reader. Arranger's
`workspaces.arranger` entry authorizes only module-owned peer verification,
not repository or transient APIs.
The confirmed ModuleServices process is `Microsoft.CmdPal.Ext.PowerToys.exe`
(`workspaces.reader`); do not blanket-authorize `PowerToys.exe` or other hosts.
Arranger still needs its peer-only catalog entry despite
having no direct data-service calls; LauncherUI needs no data-service entry.
The current source inventory must map `PowerToys.WorkspacesWindowArranger.exe`
to `workspaces.arranger`, matching the module's exact `VerifyPeerImage` role.

Release output is
`artifacts\ProtectedStorage\<Platform>\<Configuration>\Package\`.
Only that directory's finalized, signed **PowerToys.ProtectedStorageSetup.exe**
is the public distributable; the unsigned compilation output is not.
After release verification (and requested tests), `-Package` atomically publishes
only this self-contained Setup to `<Platform>\<Configuration>\` for the main
installer's root-file harvest. `-PublishDirectory` selects a different main
BinDir. Bootstrap, Runtime, Lifecycle, MsiAction, Broker, and their raw build
outputs stay under the controlled `ProtectedStorage\` subdirectory or release
staging; no loose update payload is published to the main output root.
`PowerToys.ProtectedStorage.Client.Managed.dll` is an application-client
dependency, not a service/update payload. The outer installer must harvest it
beside `PowerToys.WorkspacesCsharpLibrary.dll`; do not exclude every file merely
because its name begins with `PowerToys.ProtectedStorage`. Caller catalogs name
the actual consumer executable, not this shared library.
`release.json` records ProductCode, architecture, version, signer, MSI and Setup
hashes. ProductCode is deterministic for release and architecture and is
identical for all owners. Never republish different bytes under one release.

Main WiX integration may import `ProtectedStorage.Release.props` and
`ProtectedStorage.Release.targets`, run `RequireProtectedStorageRelease` before
compiling, and package `$(ProtectedStorageSetupPath)` next to `PowerToys.exe`.
`ProtectedStoragePackageRoot` overrides the staged artifact location;
`ProtectedStoragePublishDirectory` selects the main BinDir (default `$(BinDir)`
when defined, otherwise the platform/configuration root). The public
`ProtectedStorageSetupPath` points there; `ProtectedStorageStagedSetupPath`
identifies its verified staged source.
`ProtectedStorageExpectedSignerSha256` is a required explicit release trust input.
The validation target checks signed Setup/MSI, recorded hashes, version, and
architecture, and exact equality of the public Setup bytes; it never substitutes
an unsigned native compilation output.
The updater's dedicated Setup identity gate may require exact version-resource
values `CompanyName=Microsoft Corporation`,
`ProductName=PowerToys (Preview) Protected Storage`, and
`OriginalFilename=PowerToys.ProtectedStorageSetup.exe`, in addition to the
existing release-signature checks. `FileVersion`/`ProductVersion` are the
four-part carrier PE release version; resource strings alone are not trust.

Build order deliberately avoids self-hash cycles:

1. Build/sign Bootstrap and Runtime.
2. Build/sign MsiAction (identity and signer pin only).
3. Finalize/sign caller catalog, including MsiAction's hash; sign manifest.
4. Build/sign Lifecycle with those exact seed resources.
5. Build/sign MSI containing separate Binary streams for every payload.
6. Build/sign Broker with the MSI; build/sign Setup with Broker and the same MSI.

MSI action extracts its payload and incoming caller proof from its matching
signed MSI; it does not embed a catalog containing its own hash. Bootstrap must
authenticate an incoming signed catalog for each control request, including
query/commit/rollback, so a new-release MSI action can update an old instance.
The request's bundle is only an authenticated source, never a destination.

## Public maintenance contract

```text
PowerToys.ProtectedStorageSetup.exe inspect [--json]
PowerToys.ProtectedStorageSetup.exe sync [--json]
PowerToys.ProtectedStorageSetup.exe ensure [--json]
PowerToys.ProtectedStorageSetup.exe upgrade [--json]
PowerToys.ProtectedStorageSetup.exe repair [--json]
PowerToys.ProtectedStorageSetup.exe repair --authorize-repair [--json]
PowerToys.ProtectedStorageSetup.exe remove [--keep-data|--purge-data] [--json]
PowerToys.ProtectedStorageSetup.exe retry <32-hex-operation-id> [--json]
```

Setup is a Windows-subsystem executable; background execution creates no console.
It writes one UTF-8 JSON object to inherited stdout:
`operationId`, `state`, `nativeCode`, `retryKind`, `cleanupPending`, `dataRetained`.
It also reports the observed `productCode` and `installedVersion`; these are
re-read after a successful transaction rather than copied from the requested
release.
The process exit code is the native result, including `1602` cancellation,
`1618` MSI busy, `1641` reboot initiated, and `3010` reboot/cleanup pending.
There is no public arbitrary-owner, executable, MSI, or destination-path option.

`ensure` may request initial explicit authorization; `upgrade` and ordinary
`repair` do not. Repair requires this exact registered release. The explicit
`--authorize-repair` option repairs fixed Bootstrap resources when ordinary
maintenance cannot run. Public commands require the original non-elevated
business owner's token and loaded profile.

`sync` is the fixed no-UI/no-UAC updater entry point. No carrier and no service
returns `0` / `NotProvisioned`; a registered matching release is inspected using
the exact signed MSI action without starting an MSI transaction, returning
`0` / `Unchanged` only when both live PE hashes/versions, service/worker identity,
and terminal update phase match, and read-only Runtime capabilities confirm
the store is neither recovering nor under maintenance. The live signed catalog
retrieved through read-only capabilities must also match the MSI's catalog hash;
matching PE versions alone is insufficient. An older existing instance uses an ordinary
owner MSI upgrade. Partial inventory, missing service with registration,
unresolved decisions, newer releases, or same-version different ProductCodes
fail explicitly. `1618` and `3010` are preserved. This command never calls the
authorization path and never creates a first protected instance.
The authoritative control Status must explicitly report `healthy=true`,
`workerReady=true`, no data recovery/drain/unresolved transaction, matching live
version, and a settled phase. A missing health field fails closed.
`MsiQuery` of an unrelated fresh operation ID is never used as readiness proof.

Retry records are user-owned hints, **not authorization**. Only known busy MSI
failures are replayed. Unknown decisions return `RecoveryRequired` /
`InspectUnknownOutcome`, retaining the original material. They never silently
roll back a committed VA generation or substitute a new transaction.
After a matching commit is confirmed, diagnostic logging or owner-stage cleanup
failure cannot turn that known commit into an MSI rollback. Cleanup is recorded
as pending and retried separately; the caller proof remains available until the
decision is known.
An owner-gate busy rejection records the safe original maintenance verb before
returning its retry ID; it never overwrites an existing retry receipt.
`inspect` queries authoritative native Status against the actually registered
release rather than treating an SCM-running process as proof of readiness.
Payload cleanup pins the exact non-reparse directory, deletes only the six
known payload leaves, and retries sharing/access-denied failures for at most
five seconds. Exhaustion after a known commit remains `cleanupPending`, never
an installer rollback request.

Authorization binds a live process handle, PID, creation time, canonical owner,
operation-specific nonce/event names, and exact signed Setup image bytes.
Both the elevated rendezvous and SYSTEM broker bind the requester's actual
kernel image-file mapping to a pinned execute-capable file handle; replacing a
path or presenting an identical-byte copy is not proof of the loaded image.
Installer helpers restrict static dependency loading through their PE load
configuration and dynamic DLL searches at entry to System32. They do not search
the writable per-user installation directory for Windows SDK dependencies.
The alternate administrator creates a one-time protected SYSTEM broker, which
executes only the fixed Lifecycle Binary extracted from its signed embedded MSI.
Installed VA-writable Bootstrap/Runtime are never executed as SYSTEM.
Lifecycle leaves the standard VA service-token privileges intact. Any future
`SERVICE_CONFIG_REQUIRED_PRIVILEGES_INFO` restriction must retain
`SeImpersonatePrivilege` and `SeChangeNotifyPrivilege`: the server needs bounded
impersonation of the validated **filtered** owner to inspect private client
images. This does not permit elevated-owner impersonation or adding owner read
access to private Code.

## Retention and cleanup

KeepData is the default. It preserves the entire Data tree, including records,
epoch/revision, initialization state, and migration-cleanup receipts. Service
and Code removal leaves a protected `Installer\<SID>\retained` inventory record.
Reinstall validates that record and reattaches data without reseeding it.

For a genuinely new Data directory, Lifecycle flushes and publishes
`storage.index` once, without replacement. It contains format `1`, a canonical
new GUID epoch, and the uninitialized `workspaces.repository` target
(`sequence="0"`, empty generation/receipts, `cleanupAcknowledged=false`).
Existing indexes are read-only during provisioning and checked against the
generic storage-index invariants. A missing retained Data directory, missing
index, or corrupt index returns `RecoveryRequired`; no epoch, revision,
initialization, or migration receipt is regenerated. An explicitly purged,
absent Data directory may receive a new empty index, while its protected purge
tombstone continues to suppress automatic legacy import.
A separate SYSTEM-only `purge-recreation.ticket` is created only after a
successful authorized Data purge and consumed before recreating Data. KeepData
invalidates this ticket. The permanent historical `purged` marker by itself
cannot authorize a new epoch if a later retained receipt or index is lost.

PurgeData must be bound to the explicit authorization and protected removal
receipt. It removes product data but retains a content-free `purged` tombstone.
Runtime/Workspaces must suppress automatic legacy import while that tombstone
exists; explicit Import remains a separate user action.

The internal SYSTEM-only `Setup --machine-remove` entry point extracts its
signed broker/MSI/Lifecycle and invokes fixed Lifecycle
`machine-remove --keep-data`. It enumerates protected
inventory and emits per-owner structured residuals. It does not impersonate
offline users to uninstall their MSI registrations. It never deletes accounts,
real-user profiles, MSI registry records, or Windows Installer cache files.
The main installer must extract and validate the finalized Setup from its own
trusted package into protected staging before invoking this SYSTEM entry point.
Do not execute a Setup selected from a writable per-user installation as SYSTEM;
the child checking its own signature is not a pre-execution trust boundary.
The parent may embed finalized Setup itself as a main-MSI Binary and invoke
`--machine-remove` as a commit, no-impersonate, nonfatal EXE custom action after
`RemoveFiles`, only on final per-machine removal and not during major upgrade.
MSI-generated extraction basenames are supported: this internal entry validates
the actual mapped image and approved signer, not its on-disk basename, and does
not require an ordinary-owner profile. Broker/MSI/Lifecycle are all self-contained
Setup resources, so removal does not depend on the deleted installation path.
VA-profile review and uncertain orphan-stage outcomes are reported as pending,
not force-unloaded or falsely reported as fully clean.
Authorized operations retain their protected broker receipts/package stage for
diagnosis and report `cleanupPending`; an ordinary successful upgrade does not
create such a stage or acquire authorization. Stage retention by itself is not
a claim that a Windows restart is required.

### Final per-machine main-MSI uninstall

The parent installer may embed **`$(ProtectedStorageLifecyclePath)`** directly
as an MSI Binary. This is the signed
`Package\PowerToys.ProtectedStorageLifecycle.exe`, not a VA Code file or an
unsigned native output. `RequireProtectedStorageRelease` verifies its pinned
signature and `release.json` hash alongside Setup/MSI.

The exact fixed backend command is:

```text
PowerToys.ProtectedStorageLifecycle.exe machine-remove --keep-data
```

Schedule it deferred, no-impersonate, **only in the per-machine main package**,
on final `REMOVE=ALL AND NOT UPGRADINGPRODUCTCODE`. Use best-effort return
handling (`Return="ignore"` for an EXE CA, or a native wrapper that logs failures
without rolling back the main uninstall). Do not invoke carrier MSI from this
action. No main-installer source files are owned by this component.

The backend requires SYSTEM and its approved release signature. It enumerates
only protected owner inventory, holds per-owner maintenance leases, verifies
fixed service identity, stops/removes services and Code, and preserves Data,
initialization state, policy, and retention records. A three-minute admission
budget prevents unbounded all-owner work; already-started bounded native waits
finish, while subsequent owners are reported as unverified and requiring retry.
It never modifies MSI registration/cache directly, deletes accounts or profiles,
or executes VA-writable code as SYSTEM.

JSON is written durably, with flush and atomic latest-report publication:

```text
%ProgramData%\PowerToysProtectedStorage\Installer\MachineRemovalReports\<operationId>.json
%ProgramData%\PowerToysProtectedStorage\Installer\MachineRemovalReports\latest.json
```

It is also written to stdout when a caller supplies a pipe; a missing deferred
CA stdout handle is not an error. Top-level fields include `operationId`,
`mode`, `reportPath`, `mainProductRemoved`, `perOwner`, and `completeCleanup`.
Each owner records independently observed `serviceStopped`, `serviceRemoved`,
`codeRemoved`, `msiRegistrationRemoved`, `dataRetained`, `profileRemoved`,
`pendingItems`, `nativeErrors`, and `retryClass`.

A completed best-effort pass returns **0 even when residuals remain**; callers
must inspect `completeCleanup`/`pendingItems` instead of treating exit 0 as
“everything erased.” Offline registrations and profile review do not force a
reboot. Global trust/inventory/reporting failures return the native failure and
emit event-log diagnostics; they must not roll back the main uninstall.

Per-user main **bundle** uninstall invokes owner-bound removal from its outer
BA callback after successful main-MSI execution, using the signed Setup payload
retained in the bundle's BA container. It excludes related-bundle upgrades and
never runs this SYSTEM-only all-owner backend. The owner operation preserves
data and can request explicit removal authorization. Failure/cancellation is
logged as residual work without rolling back main removal. Direct inner-MSI
uninstall does not execute the outer callback and requires explicit owner
carrier maintenance afterward.
After a machine removal leaves an offline owner's registration, a later explicit
`ensure` or `repair --authorize-repair` validates the retained inventory and
reattaches the instance through authorized provisioning before reconciling that
owner's MSI. It does not reseed Data or require deleting MSI registry/cache
records. Background `sync` continues to defer this partial state without UAC.

Live A/B, OTS, signed-release upgrade/repair, power-loss, and uninstall acceptance
must run separately on a disposable acceptance machine with approved signing
inputs. The local contract tests intentionally do not modify services, accounts,
profiles, trust stores, or installed MSI products.
