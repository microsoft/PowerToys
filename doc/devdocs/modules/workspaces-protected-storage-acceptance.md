# Workspaces protected storage: acceptance checklist

This checklist targets the production-oriented MSI-carrier implementation. A successful local build or a mocked unit test does not qualify an installed product or every failure-recovery scenario.

The implementation is isolated in `feature/workspaces-protected-storage-msi`, based on `44b9a329230252bbcaf2fca3474b683c78f4a14f`. The previously validated standalone demo remains separate.

## Decision baseline

- First release integrates Workspaces only; storage, identity and update infrastructure remain module-independent.
- Business validation and whole-unit migration belong to Workspaces. No partial migration after an error.
- Authenticated writers are trusted; this does not claim to detect compromise of an otherwise authorized process.
- Existing-owner code updates must not add UAC prompts or require an ordinary Windows restart.
- Other owners and unrelated modules are not updated or disabled as a side effect of one owner's failure.
- Failed operations have an explicit retry path; unknown outcomes are queried before replay.
- A successful durable migration is followed by deletion of the matching legacy source; no retention timer.
- Uninstall preserves business data by default; purge is explicit. Settings use ACL protection, not encryption.
- Machine uninstall is best effort across known owners, with visible residuals rather than fabricated complete success.
- **PM gate:** first-use authorization UX and exact fail-closed feature scope remain subject to Q02 approval. Do not enable an unprotected fallback while that decision is pending.

## Evidence required

For each executed installed scenario, retain the owner SID, installation scope, release/MSI identity, native exit codes, actual service/worker identity and versions, operation IDs, sanitized logs and observed UAC/restart behavior. Do not log workspace command lines or content by default.

Use separate actual interactive accounts A and B. A Session-0 synthetic token run is not a substitute for this matrix.

## Build and automated checks

| ID | Check | Pass criteria |
|---|---|---|
| BUILD-01 | Native Common/Client/Bootstrap/Runtime | x64 production configurations compile and link; no test-only authorization bypass |
| BUILD-02 | Setup/Broker/Lifecycle/MsiAction/carrier | Build order and resource embedding are deterministic; no production signing key is synthesized |
| BUILD-03 | Managed client and Workspaces integration | All changed native/managed consumers compile against the same contract; analyzer failures are resolved |
| BUILD-04 | Main updater/installer integration | Main product and carrier scopes remain separate; both per-user and per-machine authoring compile |
| BUILD-05 | ARM64 | Compile/link/package native ARM64 and matching managed payloads; execute on an ARM64 test machine before release |
| UNIT-01 | Wire framing | Invalid lengths, overflow, malformed UTF-8, unknown commands/versions, trailing data and truncated replies are rejected |
| UNIT-02 | Serialization consistency | Native and managed clients share golden vectors, including 64-bit values, Unicode, empty content and operation IDs |
| UNIT-03 | Store state | Uninitialized, initialized-empty and damaged/missing initialized targets remain distinct |
| UNIT-04 | Atomic/CAS writes | Stale revisions fail without overwriting; duplicate operation with changed content fails |
| UNIT-05 | Recovery | Interrupted intent/record/journal writes recover or return explicit recovery-required, never fabricated empty success |
| UNIT-06 | Release verification | Reject modified bytes, wrong signer/product/architecture, mismatched catalog, unsupported downgrade and mixed PE versions |
| UNIT-07 | Parser fuzzing | Bounded malformed/untrusted requests cannot crash, hang or allocate without limits |
| UNIT-08 | Updater outcomes | Success, reboot-required, retry-required and unknown outcomes are distinct; module failure does not undo main product success |

## Installation, authorization and isolation

| ID | Scenario | Pass criteria |
|---|---|---|
| INST-01 | A initial per-user install | Explicit first authorization; context-2 carrier belongs to A, not the administrator or VA |
| INST-02 | Standard B with another admin's credentials | Business owner remains B throughout provisioning and MSI execution |
| INST-03 | Authorization cancelled/unavailable | Only affected protected functionality is blocked under the tentative Q02 policy; legacy input remains intact |
| INST-04 | A/B same release | Same MSI/ProductCode, separate registrations, services, VAs and private stores; no MST |
| INST-05 | Already provisioned ordinary user | Normal access does not require repeated administrator credentials |
| INST-06 | Partial first installation | Retry identifies owned partial state; never deletes an established or peer instance |
| INST-07 | PowerToys already elevated | Editor/Launcher and maintenance execute in a verified same-owner ordinary process; a different-owner shell is never substituted |
| SEC-01 | Direct file mutation | Ordinary users cannot modify Code/Data/control files; peer VA cannot modify another owner's tree |
| SEC-02 | Data caller authorization | Wrong SID, unauthorized image/role/target and forged request owner fields fail |
| SEC-03 | Server impersonation | Client rejects an incorrect VA/server image or PID-reused server |
| SEC-04 | Filesystem redirection | Reparse points, hardlink substitution and source replacement do not redirect privileged writes or execution |
| SEC-05 | Maintenance boundary | VA cannot change its SCM configuration; SYSTEM never executes VA-writable live code |
| SEC-06 | Trust failure | Unsigned/missing/invalid release or caller catalog fails closed; no silent development pin or trust-store import |

## Workspaces migration and business paths

| ID | Scenario | Pass criteria |
|---|---|---|
| DATA-01 | Valid legacy collection | Module validates the complete unit; durable target and initialization state precede old-source deletion |
| DATA-02 | Corrupt/unsupported item | Entire migration fails; no partial publication or success marker; source preserved |
| DATA-03 | Sensitive launch fields | Module's declared business validation decides whether/how to migrate; generic service does not interpret Workspaces JSON |
| DATA-04 | Source replaced during migration | Submitted bytes are the validated snapshot; source deletion does not remove a subsequently replaced object |
| DATA-05 | Lost commit response | Retry queries the original operation and does not overwrite a committed migration |
| DATA-06 | Old-source deletion fails | Target stays authoritative; retry only cleanup; no repeated import or plaintext fallback |
| DATA-07 | Restart/update/reinstall after initialization | No automatic re-import of a stale or modified legacy source |
| DATA-08 | Initialized record missing/corrupt | Explicit recovery state, not fresh initialization |
| DATA-09 | Concurrent editors/writers | CAS conflict is visible; launcher metadata updates do not overwrite unrelated user edits |
| DATA-10 | Editor create/edit/delete | Repository persists complete valid data; failed save preserves editable UI state and offers retry |
| DATA-11 | Snapshot/capture/cancel | Preview objects are owner-bound, bounded and reclaimed; no uncontrolled temp JSON path |
| DATA-12 | Launch-and-edit | Launcher consumes the exact intended preview; it cannot substitute an arbitrary file |
| DATA-13 | Normal workspace launch | Uses a consistent protected snapshot across launch branches; existing launch semantics are preserved |
| DATA-14 | Elevated/OTS window arranger | Original owner/session and exact layout plan survive elevation; no reads from the credential user's unrelated profile |
| DATA-15 | Launcher write-back | App metadata and last-launched time persist through observable CAS writes, not silent destructor I/O |
| DATA-16 | ModuleServices / external clients | Enumeration and explicit launch paths use the same repository; no legacy read escape |
| DATA-17 | Import/export | Import performs whole-unit validation and conditional commit; export is not an authoritative live store |
| DATA-18 | Restart/maintenance during accepted save | Accepted writes reach an unambiguous durable outcome before drain; rejected requests can retry |

## Upgrade, compatibility and failure handling

| ID | Scenario | Pass criteria |
|---|---|---|
| UPD-01 | A v1 → v2 | Real MSI major upgrade and both live PE versions change; service account and fixed ImagePath stay unchanged |
| UPD-02 | B upgrades while A remains v1 | A's versions/data are unchanged |
| UPD-03 | A upgrades after B | B remains healthy at its own version |
| UPD-04 | Repair current v2 from a v1 entry | Repairs the registered release, not the embedded v1 seed; no data reseeding |
| UPD-05 | Bad candidate readiness | Known pre-commit failure restores the previous release; no mixed registration/live-code success claim |
| UPD-06 | Commit acknowledgment loss | Exact operation is queried; no rollback of a confirmed committed operation |
| UPD-07 | Windows Installer busy / service busy | Bounded handling and visible Retry; no nested MSI or indefinite polling |
| UPD-08 | Broken Bootstrap | Normal maintenance does not fake success; explicit authorized recovery is available |
| UPD-09 | Main per-user update | Owner instance is coordinated in its real normal context after the main transaction |
| UPD-10 | Main per-machine interactive update | Main installation can elevate; owner carrier remains associated with the original ordinary user |
| UPD-11 | SYSTEM/unattended main deployment | No SYSTEM-owned per-user carrier; unavailable owner work is deferred explicitly |
| UPD-12 | Other owner offline | No forced update deadline; newer shared PowerToys checks required compatibility when that user returns |
| UPD-13 | Failed owner maintenance | Other normal modules keep working; failure details and retry are visible |
| UPD-14 | Reboot/kill at transaction boundaries | Recover correctly or report recovery-required; no false atomicity guarantee across MSI and VA |

## Removal and retained data

| ID | Scenario | Pass criteria |
|---|---|---|
| REM-01 | Remove B, retain A | B's registration/service is removed; A remains usable with unchanged data/release |
| REM-02 | KeepData uninstall/reinstall | Retained data, epoch and initialization state are recovered without legacy re-import or reset |
| REM-03 | Explicit purge | Only authorized owner data is deleted; no real user profile/account or other module/user data is removed |
| REM-04 | Machine uninstall | Attempts all attributable product instances; reports each residual owner/resource/error |
| REM-05 | Offline carrier registration | Does not hand-delete Windows Installer internals or block all successful uninstall work |
| REM-06 | VA profile in use | Accurately reports pending cleanup/reboot; no forced hive unload or clean claim |
| REM-07 | Completed helper stages | Deletes only verified current-owner terminal stages; peer stages are retained without false failure |
| REM-08 | Unknown outcome / inaccessible resource | Preserves evidence and reports the actual error; access denied is not treated as absence |
| REM-09 | Idempotent cleanup | Re-running after successful removal is safe and does not recreate deleted resources |
| REM-10 | Failed remove/rollback | Retains recoverable code/data and does not strand successful peer installations |
| REM-11 | Per-user main PowerToys uninstall | Explicit owner-bound outer coordination removes or clearly reports the retained carrier/service; no nested MSI or silent orphan |

## Release gates

Before broad release, obtain PM confirmation for Q02, complete installed x64/ARM64 and OTS matrices, perform fault/power-loss testing and a focused security review. Capture actual UAC observations; a headless or pre-authorized harness cannot establish prompt count or interactive consent behavior.

The final implementation handoff must distinguish automated PASS, manually confirmed PASS, not-run scenarios and genuine blockers. Do not mark the whole matrix green based solely on compilation.
