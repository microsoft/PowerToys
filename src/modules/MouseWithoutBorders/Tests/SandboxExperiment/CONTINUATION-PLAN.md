# MWB autonomous Sandbox experiment: physical-devbox handoff

## Implementation checkpoint

Continuation was attempted on the existing cloud host after nested virtualization
was enabled for `PowerToysUiTest-Win10` (now 15 vCPUs, 24 GB static RAM). The new
`MouseWithoutBorders.UITests` project, legacy Sandbox fixture, privileged firewall
setup, Limited-user recovery, lean/native dependency packaging, and default-off
Debug CI wiring now exist. **The full unfiltered Win10 experimental suite has
completed once (2/2); a repeat was not green, and CI has not reached pairing.** This is
not yet two restored-baseline passes or Win11/module sign-off.

The first CI diagnostic and a retry of its original revision built the Debug
product successfully, then entered the unused VNext installer path. Its
local-publish fallback requested the unavailable Windows SDK 10.0.19041.0.
The pilot now sets `buildInstallers=false` in the shared build template, keeping
product/test artifact publication and both OS test stages. Normal Release
installer builds remain enabled by default. A new pipeline run must select the
updated branch; retrying the old job does not pick up source changes.

Nested bootstrap and receiver-only UIA execution succeeded. The full-runtime
15 vCPU/24 GB run `localvm-20260914-150422-1be2acf6` reached New key/Connect but
failed transport. Guest logs showed a connection to the outer NIC
`172.23.20.248`, not its intended gateway `172.21.16.1`.

The mapping cause is now reproduced and corrected: `SocketStuff` concatenated
the `Separator` string array rather than its CRLF element, inserting the literal
`System.String[]` into the first mapping. Eight production regression cases
failed before the correction and passed afterward. The fixture now records and
checks persisted peer mappings before Connect and captures all owned TCP states.
No policy gate, firewall scope, or timeout was relaxed.

Follow-up attempts on September 15 UTC (September 14 local time):

| Run | Result |
| --- | --- |
| `localvm-20260915-015535-6b4ed894` | 1 executed, 0 passed, 24m37s. Both mappings were seeded correctly and the host mapping survived New key. Guest UIA search exceeded 90s before Connect. Startup evidence showed an unresponsive `WinUI Desktop` placeholder and an empty UIA tree, not an initialized Settings surface. |
| `localvm-20260915-022752-71bdf3f1` | 1 executed, 0 passed, 15m38s. Added responsive final-title readiness with two native samples before any UIA. The run timed out earlier in the existing 15-minute guest bootstrap budget; its last guest marker was `ExtractingLocalRuntime`, with no readiness acknowledgement. |

Both runs used the corrected full runtime, exported evidence without errors,
restored host settings/clipboard, and removed their exact firewall rules. No
owned product/test/Sandbox processes remained afterward. The VM remains running
at 15 vCPUs/24 GB. Debug builds and 26 targeted C# regressions passed; 71 isolated
PowerShell preparation/readiness checks passed on PS5.1 and PS7.

### Root-cause investigation and verified progress

The mapping fix **has now been exercised through a live connection**. Run
`localvm-20260915-153859-93f78e69` (26m20s, 1 executed/0 passed) passed nested
bootstrap, both initialized Settings windows, real New key/Connect, owned
bidirectional TCP transport and the local-input negative control. It failed the
next assertion: the remote token appeared in the host receiver, not the guest.
No clipboard assertion was reached. The next iteration verifies both routing
slots and establishes pointer activity before keyboard switching.

| Confirmed problem | Evidence and correction |
| --- | --- |
| Incomplete lean WinUI payload | A private WER dump reported `Cannot create instance of type 'CommunityToolkit.WinUI.FontIconExtension'`, with inner `0x80040111`. Its constructor accesses `Microsoft.UI.Text.FontWeights.Normal`; the SDK manifest maps that class to `WinUIEdit.dll`, which the lean archive omitted. Added the dynamically loaded SDK servers, including DirectManipulation/Graphics.Display, loose XAML/XBF and localized PRI/MUI sidecars. Subsequent resource/interface failures disappeared with the audited SDK payload. |
| Bootstrap spent time doing unnecessary work | Replaying 378 obsolete lease files took 96s; cold NetTCPIP/CIM network discovery took over two minutes. Latest committed lease selection and direct .NET adapter enumeration each took about 5s after the fix. Bootstrap completed within the original 15-minute budget. |
| Oversized/redirected staging | The full runtime contained 5,182 files/2.1 GiB. The SDK-complete lean archive is about 288 MiB compressed. Buffered local copying validates SHA-256 before local extraction; native code is not executed from redirected storage. |
| Premature/overly narrow UI readiness | A `WinUI Desktop` placeholder and executable count were not proof of initialized Settings. Require two native observations of the one responsive final-title window owner, including when its launcher remains alive. |

L0/L1 measurements did not show memory/CPU/disk exhaustion: L1 had about 20 GiB
available, read the 750 MB archive in 1.1-1.4s and hashed it in 1.7-2.2s. The nested
guest had about 3 GiB free of 4 GiB during extraction. These findings do not make
Windows 10 a compatibility blocker or justify broader firewall rules.

One later verification was invalidated by a documented guest-clock jump from
07:38:16 to 14:25:35 UTC on September 15. It is not a product failure. Recovery
completed, the owned VM was cold-booted without resizing, and subsequent runs
used the same product fix. Temporary private dump collection was removed and
both copies of the process-memory dump were deleted after extracting the
exception text.

### First complete unattended pass

`localvm-20260915-170002-e841cb1a` passed both tests. The full smoke took 25m59s
and completed actual pairing, peer transport, guest-only keyboard input, remote
cursor motion/clicks, clipboard-off isolation, clipboard transfer both ways and
cleanup. No operator input or manual firewall approval was needed.

After the startup repair, the final two input defects were in the harness:
the initial keyboard-only flow left MWB's simulated-input guard set (the next
mouse-hook event resets it), and the guest returned a focus acknowledgement while
Settings still owned foreground. The corrected run used a small pointer move
before routing and a bounded input-queue foreground handoff, then required the
receiver HWND and its edit control to own focus. The intermediate run with routing
fixed sent/received all 35 keyboard packets but still had Settings foreground,
confirming that the second problem was not a broken MWB network connection.

The first pass retained TRX but MSTest deleted its attached deployment-tree
artifacts. Run evidence now lives beside `TestContext.TestRunDirectory`, not
inside its disposable deployment tree. The fast receiver probe confirmed that
the success JSON survives and exports. Related builds and 27 C# regressions pass;
the PowerShell preparation suite has 86 checks.

The follow-up `localvm-20260915-173353-3804112e` executed both tests (1 passed,
1 failed). Its full artifacts survived in `TestResults\mwb-<RunId>`. The smoke
stopped at the unchanged eight-minute guest Settings readiness deadline: the
process was alive with the final `Administrator: PowerToys Settings` title but
unresponsive, and there were no application-crash events. This is an intermittent
initialization stall, not the previously identified missing-class/MUI crash, and
it must be investigated before claiming repeatable CI readiness. The run restored
settings/clipboard and removed its scoped rule.

The two restored-baseline passes, Win11 coverage and CI remain unfulfilled.

### Video diagnostics

The custom fixture now composes the shared screen recorder rather than relying
on `UITestBase` to start it. It retains continuous host-desktop video and separate
Sandbox-viewer segments, including the disposable pairing flow, with no redaction
gaps. Finalization precedes viewer destruction, desktop capture continues through
cleanup, and MP4s plus explicit recording status are attached to TRX on either
outcome. A focused recording probe verifies finalization after a simulated early
failure and captures an occluded window separately from the visible desktop.
Pre-MSTest provisioning failures still have no fixture video.

### CI startup detector correction

Later diagnostic runs passed the Debug build and booted the Win10 Sandbox.
Continuous desktop video showed the guest receiver open, then close before
MWB pairing. The controlling failure was the fixture's startup-error search:
it queried the entire normal Sandbox client through UIA for a generic native
dialog and timed out. Worker lease expiry and fixture cleanup then closed the
receiver and requested Sandbox shutdown. The receiver alone was not evidence
that MWB or Settings had started.

Discovery and close confirmation now use native owned-window APIs only.
Only positive startup-failure text in the launcher's native dialog is an error;
the normal viewer is never queried through UIA. A committed readiness marker
bypasses optional discovery, without bypassing RunId or worker-failure checks.
Separate viewer encoding begins after readiness acknowledgement, while desktop
recording remains continuous from preparation.

The lease publisher already had its own thread, so the UIA timeout alone does
not explain every observed heartbeat gap. Per-stage timing and sequence evidence
now distinguishes host writes, guest writes and completed cycles. The 45-second
watchdog and existing startup budgets are unchanged.

The native dialog regression passed inside the standard-user Win10 VM, and
37 focused C# regressions passed. Repeatable full runs and clean-baseline sign-off
remain separate gates. Win11's earlier privileged-provisioner exit must be
diagnosed from its captured output, not labeled a Sandbox compatibility failure.
The CI launcher now retains pre-marker initializer stdout/stderr in protected
run-local files and prints bounded, filtered excerpts on failure while preserving
the original exit code.

The corrected local smoke `localvm-20260917-201626-1d70b11a` no longer hit the
UIA startup-error probe. Guest bootstrap entered late and exceeded the unchanged
15-minute budget during runtime extraction. The lease publisher remained active
through 397 cycles, its final endpoint writes took about 7 ms, desktop video
finalized, and cleanup/recovery succeeded. This is still a failed full run, not
a replacement for the earlier complete feasibility pass.

The first explicitly authorized follow-up CI run passed native Sandbox readiness,
and its separate viewer video contained real guest pixels rather than black
frames. It then exposed the independent remaining failure: consecutive lease
generations 41 and 42 were about 80 seconds apart, exceeding the unchanged
45-second worker watchdog before endpoint startup. This proves a publication
gap, but does not identify which native/runtime operation stalled the managed
publisher.

Publication now runs in its own hidden, non-elevated Windows PowerShell process,
outside MTP and the native recorders. The exact parent handle and original
40-minute deadline bound its lifetime; the workers' watchdog is not relaxed.
Run-correlated shutdown, identity-checked recovery, maximum cycle timing and
bounded stall history accompany the isolation change. Lifecycle regressions
cover immutable generations, owner exit, hard expiry and wrong-run stop requests.
The local follow-up `localvm-20260917-221917-1e192bd6` retained live leases through
279 generations (maximum cycle gap 20.033s) and stopped the publisher cleanly.
The full smoke still failed in the nested guest's receiver-window initialization,
before readiness; desktop video and standard-user recovery completed. This is
not full local sign-off, so the remaining CI attempt is diagnostic.

Win11's newly captured underlying error is definitive: **Windows Sandbox is
Disabled** on that CI image. Enabling it and rebooting belongs to image
preparation, not to the standard-user test fixture.

The second authorized CI attempt failed during the main build, before either
UI stage. The new publisher regression assumed four generations would exist
after a fixed five-second sleep; the concurrent build had only reached three.
It now waits for the same committed fourth generation on both endpoints to a
bounded deadline, rather than asserting incidental scheduler throughput.
Both authorized attempts are consumed. The isolated publisher still needs a
newly authorized CI run; the second attempt supplied no Sandbox video or
end-to-end verdict.

### Unit regression and transport follow-up

A renewed diagnostic run passed the main build with the corrected unit test.
The regression now checks the exact committed generation on both endpoints,
not a fixed scheduling delay or the mutable publisher diagnostic snapshot.
Keep the lifecycle coverage: parent exit, hard deadline and run correlation
remain safety requirements even during the experimental pilot.

The same CI run passed bootstrap, both endpoint startups and New key/Connect.
The isolated publisher stopped cleanly after 251 generations; its maximum cycle
gap was 19.733 seconds, below the unchanged watchdog. The next controlling
failure was `Transport #3` exceeding its 40-second command budget. Its delayed
response eventually contained a socket snapshot, so this was not a unit failure
or a missing response implementation.

Transport observation now reads native IPv4/IPv6 owner-PID tables instead of
cold-loading NetTCPIP/CIM. Loopback regressions verify listening/established
states, addresses, port byte order and exclusion of unowned sockets. Stage/timing
evidence remains available without increasing the command or pairing deadlines.
Full local execution is currently blocked by the saved VM control credential
being rejected; no credential reset or alternate control channel is attempted.

The next diagnostic build was stopped by an unrelated launcher rename-event
unit test. Retrying the unchanged revision passed the build, then distinguished
another liveness failure: the publisher's maximum gap was only 13.690 seconds,
but the host worker rejected a selected lease 55.8 seconds old. Publication
continued while the reader's observation aged.

Before rejecting an expired observation, the worker now re-reads the latest
committed generation once. It still requires the original publication timestamp
to be within 45 seconds and rejects a stopped publisher, uncommitted generation,
wrong RunId or still-expired replacement. Nine added file-fixture checks cover
these cases in both Windows PowerShell 5.1 and PowerShell 7.

### End of the five-run diagnostic cycle

The fourth run passed the build, recovered a stale lease observation, and
completed native transport snapshots in about 1.5 seconds. It reached the real
90-second connection assertion: both MWB processes listened on the expected
ports, but neither had an established peer connection. Name/address mappings
were correct, the guest routing slots included both names, and video showed
matching submitted keys. Do not relabel this as a unit-test failure or weaken
the transport assertion.

The fixture had collected sanitized endpoint logs without attaching the `logs`
subfolder to TRX. Evidence enumeration now includes only that explicit folder
alongside top-level JSON/PNG, with Host/Guest-prefixed filenames. Requests and
private recovery data remain excluded.

The fifth run stopped in the main build on the Settings MWB IPC certificate
fixture: an intermediate's separately sampled expiry exceeded its issuer's
encoded expiry by one second. Both intermediate and leaf fixtures now inherit
their issuer's validity bounds. A one-day root reproduces the old failure
deterministically; one-day and seven-day roots pass with the correction.
Production IPC verification and certificate trust policy are unchanged.

All five authorized runs are terminal; no sixth run was queued. The original
lease unit regression is cleared in CI, but the final certificate-fixture fix
has only local coverage. The last run produced no UI evidence, so the newly
attached sanitized logs still await a subsequent UI execution. Remaining gates:
diagnose the actual Win10 peer connection, restore the local VM control
credential, and enable/reboot Sandbox in the Win11 CI image.

### Win10-focused continuation

The first run of the renewed Win10-focused cycle passed the corrected
certificate fixtures and exported the previously missing sanitized logs.
This time New key did not persist, and Settings logged lost SettingsSync
JSON-RPC connections. The client contract declared state-changing methods as
`void`, which StreamJsonRpc sends as notifications, then immediately disposed
the per-request proxy after a stream flush.

New key, Connect and Reconnect now return tasks on both private contracts, and
Settings awaits the remote operation before disposing its channel. RPC method
names, positional arguments, pipe identity checks and trust policy are unchanged.
The actual Settings proxy is exercised against a gated server: all three cases
failed before the correction and now remain incomplete until the server releases
its acknowledgement. Existing peer-identity and serialization regressions pass.
Shutdown remains a notification because it can terminate the responding process.

The next CI run passed the build but stopped before product startup on a real
62.203-second lease-publication gap. The external writer still serialized host
and mapped guest writes. Publication is now isolated per endpoint, with two
independently owned processes, role-specific control/status files and per-role
write timing in stall evidence. A failing guest publication cannot stop host
generations, as covered by a subprocess regression. Bootstrap detects publisher
exit, cleanup stops both, and recovery supports both the new two-publisher
journal and older one-publisher journals. Watchdog and hard deadlines are
unchanged. The acknowledged RPC revision still needs to reach pairing in CI.

On this host, the dedicated cold checkpoint is `mwb-nested-clean-20260912`.
Do not restore the older `provisioned-baseline`, which predates the nested setup.
Runtime evidence is under
`C:\PowerToysUiTestVm\shared\PowerToysUiTests\MouseWithoutBorders\LocalVmResults`.
Resume with the exact unattended command documented in the new test project's
README. Use the SDK-complete runtime from the corrected archive builder, and
continue with the startup-detector correction, intermittent Settings initialization
stall, persistent success evidence and clean-baseline confirmation. Diagnostic CI
runs beyond the normal local gate require explicit authorization. Do not
count a placeholder HWND or a persisted mapping as a successful connection.

## Goal and starting point

Continue on branch `gleb/ui-tests-mouse-borders`. Build an **MSTest-owned,
unattended** Mouse Without Borders feasibility test inside a Windows 10 VM on a
physical devbox with nested virtualization. The Windows 10 VM and a Sandbox
running inside it are the two MWB peers.

The devbox is only the build/control machine. Neither Copilot nor an operator may
be needed inside the test VM or Sandbox after the test command starts. Payload
deployment, firewall provisioning, pairing, input, clipboard, evidence, and
cleanup must all be automated.

This document is a plan, not evidence that nested Win10 or CI already works.
Do not launch CI until the new MSTest command passes unattended locally.

### Work already in this branch

| Area | Existing implementation |
| --- | --- |
| Product override | `App\Core\SessionPolicy.cs`, initialized in `App\Class\Program.cs` |
| Override configuration | `POWERTOYS_MWB_ALLOW_NONCONSOLE=1`, process-scoped, Debug only, off by default |
| Guards | Socket construction, lifecycle timer, desktop transitions, and original settings UI use the session policy |
| Unchanged boundaries | Active desktop, Release console requirement, service/LocalSystem/logon/screensaver restrictions, IPC authentication, encryption, and GPO gates |
| Mapping correction | `SocketStuff.GetName2IpMappingLines` separates policy/user rules with CRLF, not `System.String[]`; `SocketMappingTests.cs` covers first-line integrity and rule order |
| Prototype orchestration | The scripts in this directory; currently require the modern Win11 `wsb` CLI for lifecycle |
| Settings baseline | `MwbSettings.template.json`, with production-compatible boolean wrappers |
| Regressions | `MouseWithoutBorders.UnitTests\Core\SessionPolicyTests.cs`, `ExperimentSettingsTests.cs`, and `Test-Preparation.ps1` |

The remainder of this document preserves the original implementation plan. The
project, legacy adapter, firewall provisioner, and opt-in Debug CI wiring have now
been added; their validation status is recorded in the checkpoint above. The
green experimental smoke is not a completed full-module migration or CI sign-off.

### What was actually proved on September 12, 2026

With matching Debug binaries and the override, an active RDP host session and a
Windows 11 Sandbox successfully performed:

- Settings New key, real guest Connect, a green connected peer tile, and an
  established MWB transport.
- Host-to-Sandbox keyboard input and mouse movement/clicks into an independent
  receiver, while the Sandbox viewer was not the input target.
- Text clipboard sharing in both directions.
- Negative controls: local-mode typing stayed local, and clipboard-sharing-off
  prevented transfer while preserving the receiver's own clipboard baseline.
- Restoration of original host settings and clipboard; shutdown of the owned
  Sandbox and experiment processes.

Firewall prompts were approved by the operator. Several launcher and mixed-build
problems were fixed interactively. Thus this was a **feasibility pass, not an
unattended CI pass or full completion of #40678**.

The old screenshots, run directories, generated keys, and ad-hoc input/clipboard
receiver scripts were machine-local session artifacts. They are not required
inputs to this plan and must not become dependencies of the new suite. Recreate
and package the receiver as test-owned code. Do not copy credentials or private
profile backups from the old machine.

## 1. Establish the correct topology

```text
L0: physical x64 devbox
    - Git checkout, Visual Studio build tools, Hyper-V control
    - PowerShell Direct / Copy-VMFile / console capture
    - NOT an MWB peer; personal clipboard and desktop stay out of the test
    |
    +-- L1: Windows 10 x64 VM, representing the future CI agent
        - interactive standard-user test desktop
        - MSTest / UITestAutomation.Next
        - Debug Runner + Settings + MWB + companion helper: peer A
        - narrowly scoped privileged provisioning helper
        |
        +-- L2: Windows Sandbox inside L1
            - packaged bootstrap / test worker / input receiver
            - matching Debug product payload: peer B
```

L0-to-L1 control uses VMBus, not a network listener. L1-to-L2 control uses a `.wsb`
LogonCommand and run-correlated mapped requests/results. **MWB data must travel
over the L1/L2 virtual network**, not through the control channel.

Use Sandbox's internal/default-switch NAT first. No bridged access to the
physical LAN, MAC-spoofing change, or inbound WinRM/SSH opening is needed merely
to connect peer A to peer B. Discover the actual L2 address and matching L1
virtual-switch address on every run; never reuse an address from the cloud run.

## 2. Provision L1 on the physical devbox

Read the applicable skills before executing their commands:

- `.github\skills\ui-tests-local-vm\SKILL.md`
- `.github\skills\ui-tests-local-vm\references\setup.md`
- `.github\skills\ui-tests-local-vm\references\agentic-loop.md`
- `.github\skills\ui-tests-migration\SKILL.md`

The local-VM skill normally assumes **no nesting**. Reuse its L0/L1 provisioning,
payload transfer, standard-user desktop, checkpoints, and evidence export, but
explicitly add and verify nested virtualization for this experiment.

### Host and VM prerequisites

1. Fetch this branch and rebuild on the devbox; old build outputs and staging
   directories are not portable. Verify x64 architecture and the CPU/host/VM
   configuration requirements in Microsoft's nested-virtualization documentation.
2. Use a dedicated VM name, for example `PowerToysUiTest-MWB-Win10`. Keep the VHDX,
   VM configuration, media, and writable exchange outside the repository, on NTFS
   for the VM storage paths.
3. Prefer Windows 10 Enterprise LTSC 2021, updated to the repository's .NET 10 CET
   floor, **1904x.5007 or newer**. Record edition, full build/UBR, and ISO hash.
   Use a supported Pro/Enterprise/Education image with Sandbox, not Home.
4. Start with approximately **4 vCPUs, 12 GB static RAM, and a 128 GB disk** for L1.
   These are proposed nested-test resources, not a measured minimum. Allow room
   for L1, the product, and L2; check against the eventual CI agent's resources.
5. Create new DPAPI-protected administrator credentials on the physical host
   through the existing human-only setup flow. Old DPAPI files are machine/user
   bound. Never put passwords in this plan, source, arguments, or request JSON.

Example scaffold and readiness commands, executed on L0 only:

```powershell
pwsh .github\skills\ui-tests-local-vm\scripts\Initialize-LocalVm.ps1 `
    -DestinationRoot C:\PowerToysUiTestVm\MwbWin10

# Edit the scaffold's untracked vm.config.psd1 for the dedicated Win10 VM,
# amd64 media, NTFS paths, ProcessorCount=4, and MemoryStartupGB=12.

pwsh .github\skills\ui-tests-local-vm\scripts\Initialize-LocalVmHost.ps1 `
    -VmRoot C:\PowerToysUiTestVm\MwbWin10 -CheckOnly
```

If the readiness check fails, the human runs its prescribed elevated setup/media
command. An agent must not bypass the setup gate or obtain credentials through
chat.

### Enable nesting before the Sandbox baseline

After L1 exists, an authorized elevated L0 shell configures the **named, stopped**
VM. Do not stop or reconfigure an unrelated VM:

```powershell
$vm = 'PowerToysUiTest-MWB-Win10'
if ((Get-VM -Name $vm).State -ne 'Off') {
    throw 'Stop this dedicated VM cleanly before changing its processor configuration.'
}
Set-VMProcessor -VMName $vm -Count 4 -ExposeVirtualizationExtensions $true
Set-VMMemory -VMName $vm -DynamicMemoryEnabled $false -StartupBytes 12GB
Get-VMProcessor -VMName $vm | Select-Object Count, ExposeVirtualizationExtensions
```

Keep the same resource values in `vm.config.psd1`; the normal VM starter reapplies
its configured profile. Check the Hyper-V VM configuration version rather than
blindly upgrading it.

Inside L1, use the approved provisioning context to enable
`Containers-DisposableClientVM` and its dependencies, reboot if required, and
confirm the feature is Enabled with no pending restart. This is **image setup**,
not something an in-flight MSTest test should repair by rebooting its agent.

Then prove an actual L2 boot and correlated guest heartbeat. Feature presence alone
does not prove that nested Hyper-V, HNS/default-switch networking, or Sandbox
services are usable. Preserve HRESULTs and relevant event logs on failure.

### Interactive desktop and clean checkpoint

- Use the local-VM skill's standard-user interactive task dispatch. PowerShell
  Direct's administrative/session-0 context is for provisioning, not UI execution.
- Verify L1 user, session, token integrity, Explorer, display geometry, input
  desktop, and cursor access. A transient zero foreground HWND is not itself a
  disconnected desktop; require the exact foreground target for physical input.
- Observe with basic VMConnect or console capture. Do not rely on an enhanced/RDP
  session staying connected to keep L1's test desktop alive.
- Establish the minimum rights needed for a standard user to launch legacy Sandbox.
  If privileged launch is necessary, design an explicit helper; do not silently
  make the test user an administrator or disable UAC.
- Capture a clean checkpoint **after** nested virtualization, Windows updates,
  Sandbox provisioning/reboot, and test prerequisites are ready, but **before**
  MWB launch, pairing, firewall approvals, or synthetic clipboard activity.
  Stop L2 before capturing it. Verify checkpoint/save/restore support for the chosen
  nested configuration; if memory checkpoints are unsupported, use a powered-off
  clean disk/checkpoint baseline rather than forcing the skill's usual saved desktop.

Do not apply the skill's normal **1-vCPU/4-GB Constrained profile** to this nested
scenario: it is below Sandbox's documented CPU requirement and leaves insufficient
room for both layers. Define and label a nested-specific reduced profile only
after a green baseline and capability verification. Do not silently reinterpret
the normal Constrained gate as passed.

## 3. Implement an MSTest-owned test project

Create `src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests\MouseWithoutBorders.UITests.csproj`,
matching the module's sibling unit-test project layout, with `UITestAutomation.Next`, MSTest/MTP,
PerMonitorV2 manifest, shared C# props import, standard test output staging, and
`PowerToys.slnx` registration. Use the greenfield naming rules; there is no legacy
MWB UI-test project to preserve alongside a `.Next` project.

Start with one ordered smoke scenario, not the entire 28-item checklist. Keep the
fixture nonparallel and give it explicit ownership of both endpoints. Reuse the
new harness's UIA, keyboard/mouse, clipboard, and wait helpers, but do not allow
generic `UITestBase` hygiene to kill a Runner that the two-endpoint fixture owns.

Suggested components, not existing APIs:

| Component | Responsibility |
| --- | --- |
| Two-endpoint fixture | Run ID, setup phases, lifetimes, timeouts, settings snapshots, cleanup journal |
| Legacy Sandbox controller | Win10 launch, guest-ready correlation, owned window/process discovery, termination/confirmation |
| Guest worker | Packaged LogonCommand entry point and typed bounded commands; no AI/interactive shell dependency |
| Input/clipboard receiver | Independent observable text and mouse counters, synthetic clipboard source/check actions, in-memory preservation where needed |
| Privileged provisioner | Only approved firewall/prerequisite operations, with narrowly validated paths and peer addresses |
| MSTest assertions | Transport and real behavior, negative controls, artifacts, and truthful failure classification |

### Win10 lifecycle adapter

- Launch a generated `.wsb` through the installed legacy Sandbox entry point.
  Do not call `wsb start/list/exec/stop/share/ip`; those are 24H2+ APIs.
- Put all required mappings into the `.wsb` before launch. The guest bootstrap and
  request/results protocol must be sufficient without live `wsb exec` repairs.
- Refuse pre-existing Sandbox instances. Identify ownership using process/window
  identity plus the unique guest bootstrap acknowledgement; do not assume a launcher
  PID is the lasting client or infer success from its exit code.
- Verify legacy close behavior, including any deletion-confirmation dialog.
  Implement graceful stop and a bounded fallback for **proved owned** resources.
  Never kill every Sandbox/Hyper-V process or stop shared virtualization services.
- Hide helper consoles at creation, but explicitly show receiver windows when
  needed. A hidden startup flag can also suppress a form's first ShowWindow.
- Keep any modern Win11 implementation behind a capability-selected adapter.
  Merely selecting a Windows 11 image does not guarantee the modern CLI is installed
  and available to the test user.

### Payload and configuration

- Build Runner, Settings, MWB, and its companion helper coherently in Debug.
  Stage from one revision/configuration; include runtime, native GPO/interop,
  Settings resources, and test-worker dependencies. Do not overlay an old helper
  or Settings client onto a freshly built MWB.
- Record hashes. Reject incompatible root/WinUI3Apps settings-library copies.
  `FileVersionInfo.IsDebug` is not a reliable managed-build check.
- L0-to-L1 uses the local-VM archive contract and `Copy-VMFile`; execute from
  L1-local storage. L1-to-L2 maps only the needed payload read-only and a separate
  writable guest-evidence directory. Never map host profile backups or the whole
  developer checkout into L2.
- Package the repository-pinned standalone winappcli, receiver, and required
  runtimes. CI must not depend on the old host's MSIX registration, winget,
  manually installed Notepad, or session-local scripts.
- Set `POWERTOYS_MWB_ALLOW_NONCONSOLE=1` only in experimental child-process
  environments. Leave normal/Release behavior and authentication unchanged.
- Use `MwbSettings.template.json` and production-model validation. MWB booleans
  require `{"value": false}` wrappers. Write native settings as UTF-8 without BOM.
- Derive all enabled-module keys from source; enable only MWB. Suppress first-run
  welcome/update UI. Observe actual settings states before making assertions.
- Use `Dns.GetHostName()` normalized to MWB's 32-character limit for connection
  names and name-to-IP mappings, not the shorter `COMPUTERNAME` value. Discover
  current peer IPs from the inner virtual network.

## 4. Eliminate operator-dependent firewall prompts

Create temporary firewall rules **before either MWB endpoint starts**. Use the
separate administrative provisioning context in L1 and an explicitly supported
privileged bootstrap in L2; keep the UI test/MWB execution context intentional.

Rules must be scoped to the actual MWB executable, TCP 15100/15101, and the
specific peer address or smallest required inner subnet. Account for the active
network profile. Use unique run-owned rule names and remove only those names.
Do not disable the firewall, alter GPO, or delete unrelated program rules.

Use fresh per-run product paths or detect conflicting pre-existing block/allow
rules so an earlier manual approval cannot make the unattended test falsely pass.
The current product's Add firewall rule button is not a suitable provisioner:
it uses `runas`, deletes existing inbound program rules, and ends with `pause`.

Missing privileges or policy restrictions must fail the infrastructure preflight
with evidence. Do not wait for an operator to approve UAC/network dialogs and do
not turn that condition into an ignored/inconclusive test.

The privileged helper must expose bounded, narrow operations, not arbitrary
command execution supplied through an unauthenticated request file.

## 5. First autonomous smoke scenario

The complete sequence runs from one MSTest invocation:

| Step | Required observation |
| --- | --- |
| Preflight/bootstrap | Real L2 boot, matching run ID, known user/session/desktop, payload hashes, dynamic peer network |
| Firewall setup | Run-owned rules installed; neither endpoint requires human permission prompts |
| Endpoint startup | Exactly the owned Runner/MWB/helper/Settings processes; experimental flag active where needed |
| Pairing | New key changes, guest Connect is submitted through Settings, actual handshake/transport and connected state observed |
| Local input control | A local-mode token reaches only the L1 receiver |
| Remote keyboard | Ctrl+Alt+F2 followed by a different token reaches only the L2 receiver while the L1 test window, not the Sandbox viewer, owns input |
| Remote mouse | Movement and click register in L2; L1 click counter does not increment |
| Clipboard off | Explicitly observed off toggles prevent transfer; receiver baseline stays intact |
| Clipboard on | Explicitly observed on toggles allow new synthetic text in both directions |
| Cleanup | Owned processes/Sandbox/rules gone, settings/clipboard restored, evidence exported |

Use deterministic test-owned windows, not an installed editor that may be absent
in Sandbox. The receiver must not have a control-channel command that fakes the
keyboard/mouse assertion by setting the expected text or invoking its click target.

Space clipboard changes beyond the existing 1000-ms MWB event throttle, including
after receiving the opposite direction's update; 1.3 seconds worked in the local
probe. Keep Sandbox native clipboard redirection disabled and do not use clipboard
or shared result files as the data transport under test.

No operator may resize/focus a window, approve a prompt, copy a key, execute a guest
command, repair a payload, or restart a process after this invocation begins.

## 6. Failure handling and evidence

- Bound every phase and the whole suite. Use owned, authoritative ready signals;
  do not assume a successful process launch or two local device names mean pairing.
- Use `QueryFullProcessImageName` for early process identity, and compare PID,
  executable path, session, and UTC-normalized start times. `MainModule` can be
  missing or misleading while a new process loader is still initializing.
- Mapped-folder servers can hold files without delete sharing. Preserve the
  in-place publication/bounded incomplete-read handling rather than reintroducing
  rename/replace assumptions. Validate request/response run IDs and command IDs.
- Capture failure screenshots, logs, status, and video where supported **before**
  teardown. Keep guest output separate from host restoration data.
- Restore settings byte-for-byte or restore prior absence. Restore any saved
  clipboard only after MWB has stopped so restoration cannot leak it to the peer.
- Cover setup failures as well as test failures. Add an always-running pipeline
  cleanup/recovery path for an aborted MSTest host; `TestCleanup` alone is not enough.
- Distinguish `BLOCKED_INFRASTRUCTURE`, product/assertion failure, and cleanup
  failure in evidence. Required missing capabilities must not yield a green run.
- Require TRX `total > 0`, all intended tests executed and passed, no ignored/
  inconclusive/not-executed cases, and no cleanup or export errors.

Never publish credentials, original clipboard content, pairing keys, private
profile backups, or internal-only artifacts in source control or public reports.

## 7. Local validation gate on the physical machine

On the new checkout, initialize submodules and build a coherent Debug payload.
These commands are for the physical devbox, not an instruction to run another
experiment on the old machine:

```powershell
git submodule update --init --recursive
tools\build\build-essentials.cmd -Platform x64 -Configuration Debug
tools\build\build.cmd -Path src\modules\MouseWithoutBorders\App -Platform x64 -Configuration Debug
tools\build\build.cmd -Path src\modules\MouseWithoutBorders\App\Helper -Platform x64 -Configuration Debug
tools\build\build.cmd -Path src\settings-ui\Settings.UI -Platform x64 -Configuration Debug

# After creating the new test project:
tools\build\build.cmd -Path src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests `
    -Platform x64 -Configuration Debug
```

1. Build with the repository scripts to exit code 0. First restore only when the
   new project/dependency change or a missing-package failure requires it.
2. Package the coherent Debug product, test executable/worker, winappcli, and .NET
   payloads using the local-VM skill. Keep media/configuration outside the checkout.
3. Run `Invoke-LocalVmUiTest.ps1 -PlanOnly` with the dedicated Win10 VM and
   `MouseWithoutBorders.UITests.exe`. Inspect the actual request before execution.
4. Run the focused smoke via the provisioned standard-user desktop with
   `-Platform x64Win10`. L0 waits for durable status/TRX; it does not drive test
   steps interactively.
5. Restore the clean pre-experiment checkpoint and repeat the same invocation
   without assistance. Require at least two clean-baseline passes to expose stale
   firewall rules, clipboard state, pairing data, and incomplete cleanup.
6. Save the invocation, commit SHA, hashes, L0/L1/L2 capabilities, elapsed phases,
   TRX counters, and cleanup evidence. Check the result without relying on the
   operator's observations.

Windows 10 is the first **pilot**, not full module sign-off. Ordinary module
validation still needs the applicable Windows 11/full-suite matrix and appropriate
resource profiles. If a narrower infrastructure-only CI probe is desired before
that matrix, obtain an explicit scoped approval rather than silently weakening the
normal verification gates.

## 8. CI integration after the local unattended gate

Consult `.github\skills\ui-tests-pipeline-ci\SKILL.md` and its agentic loop before
Azure operations. Use its readiness preflight, pushed-SHA check, branch/run
serialization, module scoping, attempt ledger, and terminal completion waiter.

Relevant current files:

- `.pipelines\v2\templates\pipeline-ui-tests-automation.yml`
- `.pipelines\v2\templates\pipeline-ui-tests-full-build.yml`
- `.pipelines\v2\templates\job-build-project.yml`
- `.pipelines\v2\templates\job-test-project.yml`
- `.pipelines\runUiTestAsUser.ps1`
- `.pipelines\InstallWinAppCli.ps1`

The full-build template currently hard-codes Release. Add a **narrow experimental
Debug path** for the MWB Sandbox suite, while leaving default builds and all
unrelated modules unchanged. Route build artifacts and test configuration
consistently; setting the environment variable on Release binaries cannot work.
Keep real Settings IPC and existing companion-signing rules intact.

Use a privileged setup/cleanup step where needed and standard-user test dispatch.
No Copilot process, remote operator, downloaded session-state script, or manual
firewall approval may be part of the CI runtime.

Queue one scoped x64 diagnostic attempt initially. Report Win10 and Win11 stages
independently; the x64 path currently expands to both. An expected lack of Sandbox
support on Win11 is an infrastructure result, not permission to claim a green
matrix. A deliberately Win10-only pilot must be explicit.

Do not assume the image team's experimental Win10 enablement is sufficient: inspect
feature state, nested virtualization, services, desktop, privileges, and an actual
Sandbox boot from the MSTest results. Record a terminal result before stopping.
Any later stabilization remains subject to the skill's three-run ceiling.

## Acceptance checklist

- [ ] L0/L1/L2 topology is real and recorded; the physical desktop is not a peer.
- [ ] Win10 Sandbox starts and stops through the legacy adapter with proved ownership.
- [ ] The new MTP/MSTest executable owns every test-time action without assistance.
- [ ] Firewall provisioning suppresses prompts without broad rules or disabled protection.
- [ ] Matching Debug product, helper, Settings, and test payloads are reproducible.
- [ ] Pairing, input, clipboard, and negative controls pass from clean baselines.
- [ ] Failure/abort cleanup and secret-safe evidence export are demonstrated.
- [ ] Two unattended clean Win10 passes are recorded before the CI pilot.
- [ ] CI Debug wiring is opt-in; default Release behavior is unchanged.
- [ ] The first approved CI shot has a terminal, evidence-backed report.

References:
[nested virtualization](https://learn.microsoft.com/windows-server/virtualization/hyper-v/enable-nested-virtualization),
[Sandbox installation](https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-install),
[shared `.wsb` configuration](https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file),
[24H2+ CLI](https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-cli).
