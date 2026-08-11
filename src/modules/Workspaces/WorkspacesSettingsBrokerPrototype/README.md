# Workspaces machine-wide settings broker prototype

This isolated native prototype validates one machine-wide Windows service serving
multiple local users and multiple trusted module targets. It does not replace or
modify `WorkspacesSettingsService`.

**Decision status:** core feasibility is GO, but replacing the current per-user
virtual-account baseline is **not recommended under the current
security-isolation-first requirements**. The singleton reduces service count
and may simplify machine-wide servicing, but it moves cross-user isolation,
fairness, mixed-version compatibility, and machine-wide availability into one
privileged broker implementation.

## Architecture

- SCM service: `PTSettingsBrokerPrototype`.
- Identity: the unique virtual account
  `NT SERVICE\PTSettingsBrokerPrototype`; never LocalSystem or LocalService.
- One fixed local byte-mode pipe:
  `\\.\pipe\PTSettingsBrokerPrototype.v1`.
- The pipe grants Authenticated Users exactly read plus `FILE_WRITE_DATA`
  (`0x12008b`), excluding `FILE_APPEND_DATA` /
  `FILE_CREATE_PIPE_INSTANCE`; SYSTEM, Administrators, and the service retain
  full access. It uses `PIPE_REJECT_REMOTE_CLIENTS`; the first of eight
  persistent instances uses `FILE_FLAG_FIRST_PIPE_INSTANCE`.
- Eight fixed listener/worker threads provide bounded concurrency. Each I/O has a
  five-second timeout, cancellation completion is bounded, and each caller SID
  may occupy at most two authenticated connections.
- Clients open the pipe for generic read plus `FILE_WRITE_DATA`, use bounded
  overlapped request/response I/O, and fail closed unless
  `GetNamedPipeServerProcessId` exactly matches the RUNNING PID returned by
  `QueryServiceStatusEx` for `PTSettingsBrokerPrototype`.
- The service obtains the PID with `GetNamedPipeClientProcessId`, impersonates
  the pipe client, derives the SID only from `TokenUser`, rejects synthetic
  service/anonymous identities, and fail-fast terminates if `RevertToSelf`
  fails. Before opening a caller image, it canonicalizes the trusted local Bin
  and lexically rejects every process image path whose exact parent is not that
  Bin, including UNC/device, alias, and alternate-parent forms. It then opens
  only that admin-controlled local image while still impersonating and requires
  the handle-derived canonical path to exactly equal the gated path.
- A prototype-only trusted-path plus basename table binds:
  - `PTSettingsBrokerPrototype.WorkspacesClient.exe` to target 1 only.
  - `PTSettingsBrokerPrototype.KeyboardManagerClient.exe` to target 2 only.
  - The unknown client has no binding.
- Numeric target IDs are resolved by a separate trusted table:
  - 1: `Workspaces\workspaces.json`
  - 2: `KeyboardManager\default.json`
- No SID, namespace, filename, or path crosses the wire.

The local binaries are intentionally unsigned. The harness protects the install
root and `Bin` against standard-user modification, so this prototype uses the
canonical protected path plus a compile-time basename allowlist to exercise
binding and confinement. Product per-user clients still require the existing
signed-image, publisher, and version authentication pipeline in
`WorkspacesSettingsService\CallerAuth.cpp` and `CallerVerify.cpp`; protected
path plus basename is prototype-only and is not suitable for production.

## Protocol and storage

The packed request header carries magic, explicit protocol major/minor, opcode,
numeric target ID, and bounded payload length. The service supports major 1 and
minor 0 through 1:

- Minor 0: Ping/Get/Put, no advertised capabilities.
- Minor 1: advertises `multi-target` and `per-user-quota`.
- Minor 2 and other unsupported versions return an explicit rejection.

Payloads are capped at 1 MiB. There is one request per connection. After fully
reading a response, the client writes a one-byte response-consumed ACK. The
server waits for that ACK with bounded overlapped I/O and holds the per-SID quota
through response delivery and disconnect. Connections without an established
SID, and connections above the SID quota, are dropped promptly.

The store is:

`%ProgramData%\Microsoft\PowerToys\SettingsBrokerPrototype\Store\<token SID>\<trusted namespace>\<trusted file>`

The harness protects `Store` with a non-inheriting DACL granting Full Control
only to SYSTEM, Administrators, and the exact virtual service account. Runtime
children inherit it. Clients have no direct Store access and perform reads and
writes through the broker.

Writes are serialized per `(caller SID, target)`, written to unique temporary
files, flushed, and atomically replaced with write-through semantics. The
prototype intentionally uses last-writer-wins.

## Build and run

From a normal PowerShell:

```powershell
.\Harness.ps1 -Action Build
```

This builds the standalone solution as `Release|x64` and disables repository
vcpkg manifest integration because the prototype has no third-party
dependencies.

From an **elevated** PowerShell:

```powershell
.\Harness.ps1 -Action Install
.\Harness.ps1 -Action Test
.\Harness.ps1 -Action Cleanup
```

Or build, install, test, and always clean up:

```powershell
.\Harness.ps1 -Action All
```

Install fails rather than adopting an existing service, account, or install
root. It copies only the four prototype executables, protects `Bin`, `Store`,
`TestArtifacts`, and administrator state with explicit non-inheriting DACLs,
configures the service SID and restart actions, and creates two temporary
standard users with random high-entropy passwords. `TestArtifacts` grants
Authenticated Users read/execute only. The administrator pre-creates each
per-process stdout, stderr, and client output as a regular file and grants the
specific test user Modify only on that exact file; input data, scripts, and
other artifacts remain read-only. Credentials and the created account SIDs are
persisted as same-user DPAPI CLIXML under administrator-only state.

`All` tracks the root, service, and accounts created by that invocation and its
`finally` removes only those tracked resources; an install preflight collision
leaves preexisting same-name resources untouched. Explicit `Cleanup` first
validates the exact root and JSON sentinel, protected state, service StartName
and quoted prototype binPath, and recorded account SIDs before deleting
anything. No ordinary user can create, delete, or rename child entries or
junctions anywhere under the recursively removed install root, so cleanup does
not traverse a user-created tree. The matrix includes a non-destructive
cleanup-preflight self-test.

The test action prints a 13-case PASS/FAIL matrix and returns nonzero if any case
fails: authenticated singleton PID; SID isolation; executable/target and
user-writable non-Bin allowed-basename spoof rejection; protocol
framing/negotiation; denied standard-user pipe-instance creation; two large
slow response readers with cross-user availability and SID quota pressure;
concurrent atomic writes; prompt stop cancellation; restart persistence;
protected DACL/direct-write and TestArtifacts child-creation checks; and
non-destructive cleanup preflight.

## What 13/13 PASS means

`13/13` means thirteen named end-to-end integration cases passed in one
`Harness.ps1 -Action All` run. It does **not** mean thirteen Windows versions,
thirteen machines, fuzz coverage, or compatibility with thirteen released
PowerToys binaries.

`-Action All` performs the following real machine operations:

1. Builds all four executables as `Release|x64`.
2. Copies them into a protected ProgramData directory.
3. Creates and starts one real SCM service under
   `NT SERVICE\PTSettingsBrokerPrototype`.
4. Creates two real standard local accounts with random passwords.
5. Starts client processes under those two account credentials with their
   profiles loaded.
6. Executes each case below. A case is PASS only if every explicit assertion
   succeeds; any failed assertion makes the harness return nonzero.
7. In `finally`, removes only resources tracked as created by this invocation.

| # | Case | Operation and PASS condition | What it establishes |
|---:|---|---|---|
| 1 | Both users ping one singleton | Both accounts Ping; each client verifies the pipe server PID equals the one RUNNING SCM service PID | Both users reached the same genuine singleton, not a squatted pipe |
| 2 | User A roundtrip | A Put/Get target 1 and receives byte-identical data | Basic authenticated storage works |
| 3 | Per-user isolation | B initially gets NotFound, writes its own value, and A's value remains unchanged | Store partition key comes from the caller token SID |
| 4 | Executable-to-target confinement | Cross-module target requests, unknown client, and an allowed basename copied into a user-writable non-Bin directory are rejected | Caller binding and trusted-path checks prevent namespace selection and basename spoofing |
| 5 | Protocol window | 1.0 and 1.1 Ping succeed; 1.0 gets no capabilities; 1.1 gets the expected bits; 1.2 receives UnsupportedMinor | Explicit protocol negotiation works without inferring compatibility from product version |
| 6 | Malformed/oversized frame | Bad magic receives BadRequest; declared payload over 1 MiB receives PayloadTooLarge | Invalid framing and memory-amplification input are rejected before dispatch/allocation |
| 7 | Pipe-instance denial | A standard user calls CreateNamedPipe for the fixed name and receives ERROR_ACCESS_DENIED | Client data rights do not accidentally include FILE_CREATE_PIPE_INSTANCE |
| 8 | Slow response readers | A stores 900 KB; two A clients delay response reads and retain A's two quota slots; B Ping stays under 3 seconds; third A fails quickly | Slow readers cannot release quota early and consume every global worker |
| 9 | Concurrent atomic writes | Two 256 KiB patterned payloads write concurrently; final SHA-256 exactly equals one complete input | Readers never observe a mixed/truncated whole-file result; semantics are last-writer-wins |
| 10 | Stalled request and stop | Two A clients send only a partial header; Stop-Service completes under 3 seconds (observed about 8 ms) | Pending overlapped reads observe stop, cancel, and do not trap SCM in STOP_PENDING |
| 11 | Restart persistence | After the stop/start cycle, A and B still read their own expected blobs | SID-partitioned data is persistent, not process-memory state |
| 12 | Identity and DACLs | Service account/SID type and protected ACLs match expectations; ordinary user cannot write Store or create file/directory/junction children under TestArtifacts | Sole-writer ACL and reparse-safe cleanup precondition hold |
| 13 | Cleanup preflight | Exact root/sentinel, invocation ID, protected state, service StartName/binPath, and recorded account SIDs validate; no resource is deleted by preflight | Cleanup refuses unowned or changed resources before destructive work |

Case 13 validates the non-destructive ownership gate. The actual deletion occurs
after the matrix in `All`'s `finally`; the completed run additionally verified
that the service, both accounts, and the exact ProgramData prototype root were
absent afterward.

### Protocol compatibility: major, minor, and capabilities

The PowerToys product version is not the wire contract. An arithmetic rule such
as "accept callers within N releases" cannot tell whether two binaries encode
the same request semantics, and it can accidentally keep a known-vulnerable
caller inside the allowed window.

The prototype separates three decisions:

- **Major** identifies a breaking wire/semantic contract. A service speaking
  major 1 does not guess how to process major 2.
- **Minor** identifies explicitly supported backward-compatible additions.
  This service advertises a concrete supported range, 0 through 1.
- **Capabilities** should be response bits for concrete optional client-visible
  behavior. A production client would use an optional operation only when the
  server reports the corresponding bit.

For the prototype:

```text
request 1.0 -> accepted, capabilities = 0
request 1.1 -> accepted, capabilities = multi-target | per-user-quota
request 1.2 -> UnsupportedMinor
```

The current two bits are illustrative server traits. `multi-target` describes
the target table and `per-user-quota` describes admission behavior; the
prototype client only prints/asserts them and does not select a different
operation based on either bit. Therefore case 5 validates version-window
handling and capability-field advertisement, not a complete capability-gated
fallback workflow.

A product capability should correspond to behavior the client can actually
choose, for example:

```text
compare-and-swap
batch-get
transactional-migration
compression
```

For example, a client seeing `compare-and-swap` could send
`Put(expectedGeneration, bytes)`; without the bit it must use unconditional Put
or disable concurrent editing. If no such optional operations are needed,
removing capabilities is better than advertising server-internal
implementation details.

The harness changes the protocol fields emitted by the same prototype client.
It is not a substitute for later testing with independently built old/new
product binaries. Production still needs a separate signed-binary identity
check, minimum-secure-build policy, and anti-rollback policy.

### Why quota, bounded I/O, and response ACK are all needed

The broker has eight fixed workers in this prototype. This means at most eight
connections/requests can be active at the same instant; it does **not** mean the
broker supports only eight SIDs. Connections are short-lived and workers return
to the pool after one request, so arbitrarily many SIDs can be served over time.
The product value `8` is not proposed as a final capacity.

- **Per-SID quota:** after obtaining the real token SID, the server allows at
  most two active connections for that SID. One user can therefore consume two
  workers at one instant, not all eight. No workers are permanently reserved
  for that SID; the quota is only an admission counter keyed by the token SID.
- **Bounded I/O:** every pipe read/write has one five-second deadline. It uses
  overlapped I/O and waits on both completion and the service stop event.
  Timeout/stop calls `CancelIoEx`; cancellation itself has a one-second bound.
  The client uses bounded overlapped I/O too, so a fake or hung server cannot
  block it indefinitely.
- **Response ACK:** after reading the complete response, the client sends
  `0xA5`. The server holds the SID quota until that ACK arrives or times out.
  Therefore a client that sends a valid request but refuses to consume a large
  response remains charged to its SID. This also avoids an unbounded
  `FlushFileBuffers` wait on a named pipe.

These controls bound the tested slow-request and slow-response cases. They do
not prove immunity to every local DoS strategy: many distinct local SIDs,
expensive future authentication work, CPU exhaustion, handle pressure, or
implementation bugs still require a broader product threat model.

This fairness machinery is a real cost of the singleton topology. A per-user
service pipe admits only its owning SID, so one user can stall only that user's
service; it does not need per-SID fairness to protect other users. It still
needs bounded I/O and connection limits for reliability and prompt stop, but a
self-DoS does not become a machine-wide cross-user outage.

A production singleton need not use one thread per pipe instance. IOCP/thread
pool I/O could support many more concurrent connections with fewer threads, but
it would not eliminate admission accounting: one SID must still be prevented
from consuming all pending requests, memory, handles, or execution slots.

## Security properties validated by this prototype

- Machine-wide singleton and intentionally machine-wide service blast radius.
- Token-derived user isolation with no caller-supplied identity or path.
- Local-only pipe, client-authenticated server PID, and authorization after
  connection.
- Cheap local trusted-Bin image gating before any caller-image open, followed
  by handle-canonical equality and executable-to-target confinement.
- Explicit protocol support window and capability negotiation.
- Bounded payload, worker count, client/server I/O time, cancellation, response
  ACK, and per-SID active connections.
- Protected service-owned store and atomic complete-file replacement.
- No user-writable directory beneath the elevated cleanup root; only exact
  administrator-created output files are writable by their test user.
- Prompt cancellation on service stop.

## Remaining product work

- Decide whether the machine-wide blast radius is acceptable and complete threat
  modeling for compromise of the broker identity.
- Define the protocol support window, minimum security floor, deprecation
  policy, and anti-downgrade mechanism across independently updated callers.
- Define service ownership and repair rules across per-user and per-machine
  PowerToys installations.
- Replace the prototype basename table with governed, signed-image, version, and
  publisher binding; define who can add module/target mappings.
- Add durable generations/CAS if callers need conflict detection rather than
  last-writer-wins.
- Define migration, rollback, and coexistence with current per-user services and
  legacy files.
- Define privacy-safe logging, diagnostics, telemetry, retention, and redaction.
- Define user/SID data cleanup and orphan detection.
- Validate packaged/MSIX deployment, ordinary unpackaged deployment, upgrades,
  repair, uninstall, enterprise policy, and external multi-machine matrices.
