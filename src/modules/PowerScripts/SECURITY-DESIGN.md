# PowerScripts — Security Design

## Review decision

The security review selected a hybrid of the two proposals:

- Scripts remain in a user-selected folder.
- On Windows 11 24H2 or newer, execution uses Microsoft eXecution Container (MXC) by default when
  `wxc-exec --probe` reports support, with filesystem, network, UI, and least-privilege restrictions
  enabled. Unsupported hosts default MXC off and surface the reason to the user.
- The scripts folder and selected inputs are read-only inside MXC; writes are limited to a per-run
  workspace.
- A user may disable MXC or individual policies globally or per script only after explicitly
  accepting the reduced-isolation risk.
- Script descriptors may recommend MXC policies. Settings can use that recommendation as a draft,
  but it cannot weaken enforcement until the user applies it and explicitly accepts any added access.
- Per-script policy can also strengthen a globally relaxed policy, such as blocking network access
  for one script even when network access is globally allowed.

See the MXC configuration and E2E setup in [`README.md`](./README.md).

The original alternatives are retained as review history:

- [`SECURITY-PROPOSAL-A.md`](./SECURITY-PROPOSAL-A.md) — same-user execution with user-controlled installation.
- [`SECURITY-PROPOSAL-B.md`](./SECURITY-PROPOSAL-B.md) — WSL container or restricted Windows
  execution.
- [`SECURITY-THREAT-MODEL-TMT.md`](./SECURITY-THREAT-MODEL-TMT.md) — stencil, flow, trust-boundary,
  STRIDE, and reporting worksheet for creating the native Microsoft Threat Modeling Tool models.

The final design keeps Proposal A's user-selected storage and adopts MXC isolation from Proposal B
without adopting its administrator-managed secure script store.
