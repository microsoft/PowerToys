# PowerScripts — Security Design

## Review decision

The security review selected a hybrid of the two proposals:

- Scripts remain in a user-selected folder.
- On Windows 11 24H2 or newer, execution uses Microsoft eXecution Container (MXC) by default when
  `wxc-exec --probe` reports support, with filesystem, network, UI, and least-privilege restrictions
  enabled. Unsupported hosts default MXC off and surface the reason to the user.
- The scripts folder and selected inputs are read-only inside MXC. Windows execution limits writes
  to a per-run workspace. Managed Linux uses a shared writable workspace root with a distinct
  working directory for each invocation.
- A user may disable MXC or individual policies globally only after explicitly
  accepting the reduced-isolation risk.
- Descriptors cannot configure or recommend security policy. Every script uses the global policy;
  there are no per-script overrides or local policy records.
- Managed Linux requires MXC and filesystem protection. Its unsupported Windows UI and
  least-privilege policies require explicitly acknowledged global exceptions; Python runs as guest root.
- Managed Linux scripts share a warm WSLC container and its mutable guest state, background
  processes, and workspace mount. This is a host boundary, not a boundary between scripts.
  Only selected external files are staged read-only; no whole-drive mounts are added for reuse.
  Trust and input validation remain per invocation. Changes to the image, catalogue, effective
  policy, or bundled runtime replace the container, as do failed executions.

See the MXC configuration and E2E setup in [`README.md`](./README.md).

The original alternatives are retained as review history:

- [`SECURITY-PROPOSAL-A.md`](./SECURITY-PROPOSAL-A.md) — same-user execution with user-controlled installation.
- [`SECURITY-PROPOSAL-B.md`](./SECURITY-PROPOSAL-B.md) — WSL container or restricted Windows
  execution.
- [`SECURITY-THREAT-MODEL-TMT.md`](./SECURITY-THREAT-MODEL-TMT.md) — stencil, flow, trust-boundary,
  STRIDE, and reporting worksheet for creating the native Microsoft Threat Modeling Tool models.

The final design keeps Proposal A's user-selected storage and adopts MXC isolation from Proposal B
without adopting its administrator-managed secure script store.
