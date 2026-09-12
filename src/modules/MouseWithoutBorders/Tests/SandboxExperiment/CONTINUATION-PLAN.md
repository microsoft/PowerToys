# MWB autonomous Sandbox experiment: physical-devbox handoff

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
| Unchanged boundaries | Active desktop, Release behavior, service/LocalSystem/logon/screensaver restrictions, IPC authentication, encryption, and GPO |
| Prototype orchestration | The scripts in this directory; currently require the modern Win11 `wsb` CLI for lifecycle |
| Settings baseline | `MwbSettings.template.json`, with production-compatible boolean wrappers |
| Regressions | `MouseWithoutBorders.UnitTests\Core\SessionPolicyTests.cs`, `ExperimentSettingsTests.cs`, and `Test-Preparation.ps1` |

There is **no `MouseWithoutBorders.UITests` project yet**, no legacy Win10 Sandbox
lifecycle adapter, no unattended firewall provisioner, and no Debug CI wiring.
The existing scripts are a reference implementation, not the final MSTest suite.

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
