# MXC policy coverage audit

This matrix replaces any broad claim that Try Run exposes all MXC configuration.
It audits the pinned revision **4a941b0b913c39d91a9fb1ad1824c537a896eef5**.
The machine-readable inventory is [policy-coverage.json](policy-coverage.json).
Control defaults are in [POLICY-OPTIONS.md](POLICY-OPTIONS.md).

## What is being counted

The inventory contains **162 definition/property entries** from MXC's generated
0.9.0-alpha native schema, including union branches, aliases, structural sections,
metadata and state-aware requests. These are not 162 independent user permissions.
Shared types can be referenced by multiple backends and phases.

| Classification | Entries | Meaning |
|---|---:|---|
| mapped | 57 | A Try Run control or setup field authors this value, sometimes through an inverse or translation |
| fixed | 5 | The current workflow or SDK fixes/derives the value; it is not freely configurable |
| gap | 2 | A native one-shot control is not exposed by the current .NET request path |
| backend-gap | 65 | Its backend or multi-call phase is not connected to Try Run |
| structural | 14 | Parent/alias sections; child entries determine actual coverage |
| metadata | 16 | Schema/comment annotations, not execution permissions |
| testing-only | 3 | Deliberately permissive native test features, absent from the normal UI |

The inventory also classifies all **13 one-shot containment spellings**, including
aliases. Only Windows ProcessContainer and WSLC are connected to Try Run.

Evidence is deliberately separated from availability:

- **Control/mapping tests** prove that values survive the UI and reach SDK types.
- **Native parser tests** prove that representative native configurations are accepted
  or rejected as intended, without launching a workload.
- **Runtime tests** prove the specific observed behavior of a tested workload/host.
  They do not prove all permission combinations, OS tiers, or application behavior.
- A mapped parent section or passing test count is not an enforcement guarantee.

## Coverage by area

| Area | Current implementation | Evidence / remaining boundary |
|---|---|---|
| Filesystem grants and denies | Read-only, writable and denied path controls | Windows/Linux custom read-only runtime tests; copied-workspace tests. Explicit writable host paths affect originals |
| Policy retention | Clear retained policy maps to the inverse of lifecycle.preservePolicy | Mapped; applies to retained filesystem/network policy, not just files |
| Legacy network | Outbound, LAN, host rules and URL/host-port proxies | Mapped; backend-dependent enforcement; host rules require outbound access on Windows |
| Network enforcement mechanism | Native network.enforcementMode is missing | capabilities/firewall/both cannot be selected through the current one-shot .NET type |
| Directional network | Egress allow/deny, CIDRs/exclusions, protocols/port ranges, ingress and host loopback | Native parser comparisons in schemas 0.8/0.9; a wildcard-deny runtime smoke test. Arbitrary Windows filtering still needs a compatible PSEC contract |
| Runtime proxy and peer identity | Configured in Directional mode | Native parser checks for loopback, port, egress/ingress and peer constraints; not a claim that all proxy applications work |
| Windows UI and capabilities | Window, clipboard, input injection, handles/atoms, system settings, IME, least privilege and capabilities | Mapping coverage plus representative GUI runs; no exhaustive behavioral test of every UI permission |
| Denial capture | Block/allow, output path and ETL retention | Real block/allow runs and report-label tests. No elevated capture fallback; incompatible combinations are rejected |
| WSLC image and resources | Image/archive, CPU, memory, GPU, storage and TCP port mappings | Mapping and representative Linux runtime tests. Numeric UI inputs are bounded; resource settings are not all independently measured |
| Process metadata | Time limit, environment overrides/inheritance, container name | Mapping and timeout/environment runtime tests. Command/cwd are derived from the selected workload and workspace |
| Host ACL fallback consent | Native fallback.allowDaclMutation is missing | Important gap: the pinned native parser defaults it to true. Try Run has not yet authored a false override |
| Container lifetime | destroyOnExit is fixed true; the worker also cleans up | Persistent/reusable sessions and phase-specific settings are not implemented |
| Other backends | Windows Sandbox, IsolationSession, LXC, Bubblewrap, Seatbelt and micro-VM choices remain unconnected | Not implemented or not applicable to this Windows workflow; they are not counted as completed policy controls |
| Native test proxy | builtinTestServer is absent | Deliberately test-only; native SDK rejects it without its separate testing route |

## Corrections in this stage

1. An empty destination or port-selector list now omits the corresponding native
   field. MXC requires a present list to be nonempty. Omission means any destination
   or any protocol/port. The regression checks both allow/deny lists and schemas
   0.8.0-alpha and 0.9.0-alpha.
2. CIDRs must use a network base address. Exclusions must use the same IP family,
   lie inside their parent network and have an equal or longer prefix.
3. Proxy URLs need an explicit non-default port in the pinned MXC parser. Its URL
   normalization removes HTTP 80 / HTTPS 443 before the port-presence check.
4. Runtime proxies must be loopback endpoints with egress Deny and no direct rules.
   Windows requires ingress Allow. A named proxy peer requires host-loopback Deny;
   an unnamed peer requires host-loopback Allow. Reserved/standalone peer names are
   rejected.
5. A previous mapping test combined direct egress rules with a runtime proxy. That
   combination was not valid native policy. It is now tested separately as valid
   direct-rule and proxy configurations, with invalid combinations rejected.

## Automated audit checks

PolicyCoverageTests checks the matrix against the actual schema copied from
MxcRoot during the test build. It checks the revision and schema SHA-256, requires
every native field/backend to have an explicit classification, rejects duplicate
entries, resolves evidence test names, and connects every policy control to a
native destination. Additional setup controls are checked against the WPF window.

Changing the pinned schema or adding a control therefore requires reviewing the
matrix; a missing field cannot silently disappear from the audit.

PolicyNetworkTests invokes the matching wxc-exec helper with **--dry-run** to test
native parsing without executing its deliberately nonexistent command. This is
separate from the actual contained wildcard-deny smoke run.

## Next implementation milestones

1. Extend the native request path for **fallback.allowDaclMutation** and
   **network.enforcementMode**. Proposed Try Run default: no host DACL mutation;
   retain explicit, visible network mechanism selection. These must reach MXC before
   backend selection. Do not add UI-only placeholders or infer safety after spawn.
2. Add a typed multi-call session workflow with owned session identity and verified
   stop/deprovision behavior, then expose supported lifecycle choices.
3. Add other backend-specific configuration only with its actual backend adapter,
   capability checks and runtime tests.
4. Treat application-internal document import, copy-on-write behavior and Word
   handoff compatibility as separate file/workflow capabilities, not policy toggles.
