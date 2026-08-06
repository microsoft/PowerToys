# Workspaces machine-wide settings broker prototype

This isolated native prototype validates one machine-wide Windows service serving
multiple local users and multiple trusted module targets. It does not replace or
modify `WorkspacesSettingsService`.

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
