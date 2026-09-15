# Run permissions

Open **Run permissions…** on the configuration page before selecting **Run**.
Each setting displays its default, an explanation and a Reset button. Apply
updates only the configuration; Cancel discards the dialog draft. Windows and
Linux keep separate drafts for the lifetime of the window. Previous run reports
retain their own submitted policy snapshot.

The controls map the public policy surface of the pinned MXC .NET
SandboxPolicy, ProcessContainerContainment and WslcContainment types, with the
constraints described below, at
3eef7d60ce35d4d0ba568ddd0a9108beadb35b9a. This is the API used by Try Run's
Windows and Linux execution paths. It is not an editor for every native CLI
lifecycle/backend configuration (for example macOS Seatbelt or IsolationSession).
The obsolete SandboxPolicy.CaptureDenials alias is represented by the supported
ProcessContainerContainment.CaptureDenials controls.

See [the native coverage audit](POLICY-COVERAGE.md) for the complete inventory,
fixed values, missing native controls and unconnected backends. Field coverage
does not mean that every combination has been validated or can run on this host.

The form uses ordinary controls and nested network-rule editors, not executable
configuration or a raw JSON input box. Values are validated again in the worker.
Requested native settings that the host cannot support still fail at MXC launch.
Backend availability does not imply support for every policy combination.

## Options and defaults

| MXC surface | Try Run control | Default |
|---|---|---|
| policy.version | Policy version | 0.8.0-alpha; 0.9.0-alpha selectable |
| policy.timeoutMs | Time limit in milliseconds | 60,000; 0 selects no time limit |
| policy.telemetry.enabled | MXC telemetry | Off; requires schema 0.9 and MXC consent/admin policy |
| filesystem.readonlyPaths | Read-only paths | Windows runtime, installed app folder when applicable, fonts for EXEs; empty on Linux |
| filesystem.readwritePaths | Writable paths | This run's Work and Temp |
| filesystem.deniedPaths | Denied paths | Empty |
| filesystem.clearPolicyOnExit | Clear retained policy on exit | On for Windows; inverse of lifecycle.preservePolicy, including retained network policy; not submitted to WSLC |
| fallback.allowDaclMutation | Allow host file permission changes for fallback | Off, including requests without custom permissions; Windows only |
| network.enforcementMode | Network enforcement | Auto; Capabilities / Firewall / Both selectable in Windows Basic mode |
| network.allowOutbound | Allow outbound network | Off |
| network.allowLocalNetwork | Allow local network | Off; Windows only |
| network.allowedHosts | Allowed hosts | Empty; Windows only |
| network.blockedHosts | Blocked hosts | Empty; Windows only |
| network.proxy.localhost | Host-port proxy | Disabled; port field starts at 8080 |
| network.proxy.url | URL proxy | Disabled; URL empty |
| network.egress.default | Default outbound action | Deny |
| network.egress.allow | Outbound allow rules | Empty |
| network.egress.deny | Outbound deny rules | Empty |
| Each rule's to[].cidr | Destination IP/CIDR | No destinations (any); Add destination starts at 0.0.0.0/0 |
| Each destination's except[] | Excluded IP/CIDRs | Empty |
| Each rule's ports[].protocol | Protocol | No selectors (any); Add selector starts at TCP |
| Each selector's port / endPort | First / last port | Empty (any / single port); accepts 1–65535 |
| network.ingress.default | Default inbound action | Deny |
| network.ingress.hostLoopback | Host loopback access | Deny |
| network.runtimeConfig.networkProxy | Runtime loopback proxy URL | Empty |
| processContainer.network.allowedProxyPeer | Authorized proxy peer | Empty |
| ui.allowWindows | Allow application windows | On for Windows compatibility; not submitted to Linux |
| ui.clipboard | Clipboard access | None; Read / Write / All selectable |
| ui.allowInputInjection | Allow simulated keyboard/mouse input | Off |
| processContainer.ui.isolation | Desktop resource access | Desktop; Handles / Atoms / Container selectable |
| processContainer.ui.systemSettings | System settings access | None; Parameters / Display / All selectable |
| processContainer.ui.desktopSystemControl | Desktop and session control | Off |
| processContainer.ui.ime | Input method access | Off |
| processContainer.leastPrivilege | Least-privilege process mode | Off |
| processContainer.learningMode | Deny-and-record learning mode | Off |
| processContainer.capabilities | Additional capabilities | Empty; reserved learning-mode names rejected |
| captureDenials presence | Capture access checks | Off in permission presets; initial setup retains its existing capture checkbox choice when native capture is available |
| captureDenials.mode | Capture mode | Block; Allow explicitly selectable |
| captureDenials.outputPath | Capture output file | This run's diagnostics directory / denials.json |
| captureDenials.retainEtl | Retain native ETL trace | Off |
| wslc.image | Linux image on the setup page | alpine:3.22; Python profile initially suggests python:3.12-alpine |
| wslc.imageTarPath | Image archive on the setup page | Empty |
| wslc.cpuCount | Virtual CPUs | 2; 0 delegates to MXC |
| wslc.memoryMb | Memory in MiB | 2048; 0 delegates to MXC |
| wslc.gpu | GPU passthrough | Off |
| wslc.storagePath | Image storage directory | Try Run's dedicated cache; shared with Prepare image |
| wslc.portMappings[].windowsPort/containerPort | TCP port mappings | Empty; one host-port:container-port per line |
| request.environment | Additional environment variables | Empty overrides, layered on Try Run's existing environment |
| request.inheritDefaultEnv | Inherit backend default environment | Off; requires schema 0.9 |
| request.containerName | Container name | Empty; MXC generates it |

Basic networking is the initial mode. Directional mode exposes the separate
outbound/inbound fields. A non-default value in an inactive network mode is
rejected until reset; switching modes never silently discards a grant or deny.

Network enforcement **Auto** keeps MXC's existing choice: capabilities without
host rules, both with host rules. Explicit Capabilities cannot filter host lists
and is rejected with either list. Explicit mechanisms cannot be combined with
Directional mode or WSLC. Firewall/Both depend on host privileges and support;
Try Run does not elevate to enable them.

Host DACL fallback is **off** by default, including older requests without a
policy envelope. If MXC needs a tier that changes host file permissions, it must
refuse before starting the workload. Enabling the option permits that fallback;
it does not grant file-content access by itself or suppress backend warnings.
This switch controls fallback DACL changes, not every possible host effect.

Empty destination/port lists are submitted by omitting those fields, meaning any.
CIDRs must be network base addresses and exclusions must belong to the parent
network. Proxy URLs require an explicit non-default port. Runtime loopback proxies
require egress Deny with no direct rules, ingress Allow, and host-loopback Deny
with a named peer (Allow without a peer). Invalid combinations fail before launch.

## Files, diagnostics and backend differences

- Path lists accept one existing local file or directory per line for read/write
  grants. Denied paths may name absent local paths. The form also accepts $work,
  $temp, $runtime, $app and $fonts as whole-entry tokens; Windows runtime tokens
  are not valid for Linux. Empty/inapplicable runtime tokens resolve to no grant.
  Application/runtime grants are editable, not automatically re-added.
- $work contains copies. Adding a writable host path grants access to originals;
  file review/export still covers the run workspace, not arbitrary host folders.
  The summary and editor state this distinction.
- WSLC networking is all-or-nothing and may reach local networks when enabled.
  Host filters and Windows UI policies are unavailable, not emulated. URL proxies
  are cooperative. Denying a child of a mounted folder is rejected.
- Native Windows capture is required. Try Run rejects capture combined with
  least-privilege mode, a legacy proxy or denied paths because those combinations
  can fall back to an elevated capture provider. It does not invoke that fallback.
- Allow-mode capture is explicitly labeled permissive before execution and in the
  report. Captured events are labeled **Allowed (recorded)**, never **Blocked**.
  Only MXC's exact expected permissive warning is accepted for this opt-in mode;
  other policy warnings still stop the run.
- Capture output may be placed in an existing local directory. MXC adds a run ID;
  reports read only the returned file within the selected directory using bounded
  file validation. Retained trace paths are shown in the report. Files under the
  temporary session are deleted when that session is discarded; external outputs
  persist.
- Preparing an image uses its own bounded preparation operation. Its storage
  directory follows the selected policy, but workload permissions and telemetry
  are not applied to the image-download helper.

## Presets and implementation

Restore defaults selects offline permissions. File processing uses those
permissions with a 300-second limit. Network task enables outbound networking
with a 300-second limit. Presets modify a dialog draft and do not launch code.

The policy catalog is TryRun.Core/PolicySettings.cs; nested rules have typed
models. PolicyWindow and NetworkRulesEditor render the catalog. PolicyMapper
translates it to the pinned SDK types. RunDiagnostics includes the full submitted
policy/backend snapshot with resolved paths in each report. The snapshot describes
the submitted configuration, not proof of every individual enforcement outcome.

Custom-policy requests use the PolicyProtocol: 1 envelope. Older workers see a
missing execution request and reject it, instead of silently ignoring the new
permissions. Legacy requests without policy settings remain supported.

Policy tests cover all form/default entries, bounded protocol and fuzz inputs,
inactive and incompatible settings, nested network rules, native-request mapping,
permissive report labels, backend draft retention, previous-result snapshots,
and actual Windows/Linux execution with customized permissions.
