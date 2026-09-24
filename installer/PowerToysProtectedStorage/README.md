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
.\installer\PowerToysProtectedStorage\Tests\Test-PipelineSigning.ps1
.\installer\PowerToysProtectedStorage\Tests\Test-ReleaseTools.ps1 -LiveTimestamp
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
* `-TimestampServer <approved Microsoft RFC3161 timestamp URL>`;
* `-ClientCatalogInput <JSON release-client inventory>`.

The certificate parameter selects a local signing key only, never a trust
authority. All resulting signatures must independently pass the fixed
`microsoft-production-v1` native policy. No developer certificate is generated
or accepted. Use the same compatible Microsoft timestamp server for PE and CMS
signing: `http://timestamp.acs.microsoft.com` or the approved corporate endpoint
`http://rfc3161.gtm.corp.microsoft.com/TSS/HttpTspServer`.
Local PE/MSI signing uses the Windows SDK SignTool with `/fd SHA256 /tr ... /td SHA256`,
then `verify /pa /all /tw` and the shared native verifier. It does not use
PowerShell's legacy Authenticode timestamp request. Missing timestamps and
SignTool warnings/errors stop packaging; the native policy also requires a
verified Authenticode timestamp before accepting release files.

Native projects receive `ProtectedStorageVersion=a.b.c.0` and matching component
properties; the separate WiX carrier build receives the three-part MSI version.
For example, pipeline version `0.101.3000.0` produces carrier MSI version
`0.101.3000` and PE version `0.101.3000.0`, not a five-component version.

### External signing stages

`-ReleaseStage` runs one assembly stage without requiring a local private key.
Supply platform, configuration, and version on every invocation. The stages are:

| Stage | Output requiring external signing before the next stage |
|---|---|
| `Payloads` | `Bootstrap.exe`, `Runtime.exe`, `PowerToys.ProtectedStorageMsiAction.exe` (Authenticode) |
| `Documents` | `ClientCatalog.json`, `manifest.txt` (detached CMS, SHA256, one signer, bound RFC3161 timestamp) |
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

CI uses `CarrierInputs` to validate the signed inputs and derive the same
version/ProductCode, then compiles the carrier with `VSBuild@1`, matching the
main MSI execution path. Local `Carrier`/`-Package` builds still use the repository
build wrapper. Carrier intermediates are project-local under
`obj\Carrier\<Platform>\<Configuration>`; only the completed carrier is written
to the release stage. ICE validation remains enabled. Windows Installer service
state and recent installer events are retained to distinguish an agent service
failure from an actual ICE authoring finding; neither is reported as success.

The signed ADO pipeline runs these stages after final client signing and before
either main MSI. It reuses the existing ESRP signing identity and `CP-230012`.
Authenticode uses `SigntoolSign`; documents use `Pkcs7DetachedSign` with
`P7CE=/p7ce DetachedSignedData`, `P7EKU=1.2.840.113549.1.7.1`, and
`FileDigest=/fd SHA256`. This contract and the replacement behavior are
demonstrated by Microsoft's [MCP signing pipeline](https://github.com/microsoft/mcp/blob/c5c6ce286686d616528079af31c979c8fade84a8/eng/pipelines/templates/jobs/mcpb/pack-and-sign-mcpb.yml).
The pipeline submits copies named `manifest.p7s` and `ClientCatalog.p7s`;
ESRP replaces their contents with DER signatures, leaving the originals intact.

No timestamp parameters are invented for ESRP. After detached signing,
`Add-DetachedTimestamp.ps1` uses .NET
`Rfc3161TimestampRequest.CreateFromSignerInfo`, `ProcessResponse`, and
`SignerInfo.AddUnsignedAttribute`. The request hashes the **actual signer
signature value**, includes a random nonce, and sends only the timestamp request,
not document contents. The response must match the request and cryptographically
bind the signer. There is one RFC3161 unsigned attribute/value: an existing valid
one is preserved; malformed, copied, or duplicate timestamps are rejected.
HTTP requests have a 30-second timeout, a 1-MiB response bound, at most three
attempts for transient failures, and no redirects or arbitrary provider scripts.
Missing/untrusted timestamps fail before any signed resources are embedded.
The original `.p7s` is replaced atomically only after native production
verification accepts the timestamped candidate.

### Fixed production trust

Every PE, helper, MSI, client, and detached document is independently verified
by the shared native Common trust engine: verified Microsoft publisher,
machine certificate chain, and Windows Microsoft application-root policy.
There are no leaf pins, certificate hash allowlists, certificate enrollment from
build outputs, or same-certificate requirements. Different valid Microsoft
certificates can sign different artifacts. Certificate hashes in diagnostics
are observations only, not trust inputs.

The verification-only CLI links that same Common library:

```text
x64\<Configuration>\ProtectedStorage\ProtectedStorage.TrustVerifier.exe verify-file <absolute path>
x64\<Configuration>\ProtectedStorage\ProtectedStorage.TrustVerifier.exe verify-detached <absolute document> <absolute p7s>
```

It has no development or alternate-policy mode and returns nonzero on failure.
CI explicitly builds it for the **x64 host before verification**, including ARM64
release jobs; local `-Package` builds it too. External stage callers must build
the x64 tool first. It is never signed/published/harvested as a product helper,
embedded, or authorized in the client catalog. Its raw output stays under
`x64\<Configuration>\ProtectedStorage\`, not the public BinDir.

Detached verification binds the exact original document bytes, requires one
SHA256 signer, and validates a genuine trusted RFC3161 timestamp over that
signer's signature. A `signingTime` attribute is not timestamp evidence and
there is no uncertified expiration bypass. Timestamp mechanics alone do not
grant publisher trust; the native verifier is the final authority.
The catalog and manifest still bind exact final signed-file hashes, release
identity, roles, and normalized version. Resource 110 contains exactly the ASCII
bytes `microsoft-production-v1`, with no newline, from `Generated\signer.policy`.
The seed `policy.txt` has these exact ASCII/LF bytes, including the final LF:

```text
format=2
app=PowerToysProtectedStorage
signer_policy=microsoft-production-v1
minimum_version=a.b.c.0
```

All four installer helpers carry matching PE version resources, including the
embedded MSI action, Lifecycle and Broker; the authoring compile test checks them.

`Tests\Test-ReleaseTools.ps1` exercises version normalization, fail-closed policy,
real in-memory CMS/RFC3161 mechanics, zero/multiple signers, SHA1, altered data,
malformed responses, nonce and actual-signature binding, wrong timestamp EKU,
response bounds, and missing/copied/duplicate timestamps. It never installs
its short-lived certificates. `-LiveTimestamp` checks the public Microsoft
endpoint with only an ephemeral signature digest and preserves original bytes.
`-TrustVerifierPath <absolute x64 verifier>` additionally requires native
production rejection of the self-signed fixture, even when Microsoft timestamped.
`Tests\Test-PipelineSigning.ps1` checks the stage order, host architecture,
unchanged ESRP contract, timestamp/native gates, and both installer scopes.
The process-scoped publication mock tests exact final Setup identity and bytes,
every native gate, final payload hashes, policy/version, and BOM/line-ending
rejection. It never supplies a product trust bypass.
`Tests\Test-ProjectEvaluation.ps1` checks native version properties without VC
imports, reproducing the SDK-only evaluation used by CI's `dotnet restore`.
Both native property sheets load the repository version explicitly when that
evaluation has not imported `Directory.Build.props`; they never invent a fallback
release version.
The standalone installer solution includes the bootstrapper's updater and
SettingsAPI dependencies, not just its direct DLL projects. The installer
contract test checks that complete project-reference closure so static-graph
builds retain the requested configuration and architecture.

### Release qualification status (2026-09-24)

The earlier [ADO run 158436898](https://microsoft.visualstudio.com/Dart/_build/results?buildId=158436898)
failed because ESRP returned different valid leaf certificates for different
artifacts. The approved publisher/root policy replaces that single-leaf design;
it does not replace chain validation with publisher-name string matching.
The public Microsoft RFC3161 endpoint has been exercised successfully using an
in-memory ephemeral CMS, with verified response/signature binding. That isolated
test is not proof of release publisher trust or an installable signed package.
The integrated native verifier/builds and a newly signed pipeline release still
require qualification; no new ADO run or installable release is claimed here.
Failure artifacts preserve diagnostic signature/CMS metadata and build logs.
The raw-MSI alternative (Q10) remains future work, not this signing topology.

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
`release.json` records ProductCode, architecture, version, `trustPolicy`, MSI and Setup
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
`ProtectedStorageTrustPolicy=microsoft-production-v1` is the required explicit
release gate property. It has no default that silently enables packaging.
The validation target uses the x64 native verifier to check every staged signed
helper/MSI/document, recorded hashes, version, architecture, and exact equality
of the public Setup bytes; it never substitutes
an unsigned native compilation output.
The updater's dedicated Setup identity gate may require exact version-resource
values `CompanyName=Microsoft Corporation`,
`ProductName=PowerToys (Preview) Protected Storage`, and
`OriginalFilename=PowerToys.ProtectedStorageSetup.exe`, in addition to the
existing release-signature checks. `FileVersion`/`ProductVersion` are the
four-part carrier PE release version; resource strings alone are not trust.

Build order deliberately avoids self-hash cycles:

1. Build/sign Bootstrap and Runtime.
2. Build/sign MsiAction (identity and fixed trust policy only; combined with step 1).
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
unsigned native output. `RequireProtectedStorageRelease` verifies its production
publisher/root signature policy and `release.json` hash alongside Setup/MSI.

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
