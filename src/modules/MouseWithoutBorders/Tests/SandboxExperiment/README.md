# Mouse Without Borders: prepared Debug Sandbox experiment

This is a **manual feasibility experiment**, not a UI-test suite, release sign-off,
or CI job. Preparation does not start PowerToys, Sandbox, or probes, and does not
touch host settings. Launch it only when everyone is ready to observe both desktops.

For the physical-devbox continuation, use [CONTINUATION-PLAN.md](CONTINUATION-PLAN.md):
physical host -> Windows 10 VM -> nested Sandbox, followed by an unattended MSTest
fixture and a scoped CI pilot. The current scripts are not yet the Win10 adapter.

## Requirements and limitations

- A successfully built, self-contained **x64 Debug** `x64\Debug` payload containing
  Runner, MWB, its companion `PowerToys.MouseWithoutBordersHelper`, and
  `WinUI3Apps\PowerToys.Settings.exe`. Rebuild the helper with the same GPO/interop
  dependencies as MWB; a stale generated WinRT projection can fail at startup.
  Rebuild Settings as well: preparation rejects differing root/WinUI3Apps copies
  of the settings library, which can otherwise leave the UI using an older MWB IPC protocol.
  The MWB binary must contain
  the new `POWERTOYS_MWB_ALLOW_NONCONSOLE` flag. Prepare **after** building; do not
  rebuild the mapped output while an experiment is running.
- An already installed **x64 winapp** MSIX package with `winapp.exe` and
  `libSkiaSharp.dll` at its root. Preparation stages these two portable files;
  it neither installs tools nor maps the inaccessible WindowsApps package directory.
- **Host control currently requires Windows 11 24H2 or later with the modern
  `wsb.exe` CLI**, an already enabled Windows Sandbox feature, and virtualization.
  It uses `wsb start --id --config`, `list --raw`, `connect --id`, and `stop --id`
  to own a specific Sandbox GUID. See [Microsoft's CLI documentation](https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-cli).
  The guest bootstrap uses `.wsb` **LogonCommand and mapped request/results files**,
  not `wsb exec`, and is Windows PowerShell 5.1 compatible. This does **not** make
  the host controller Windows 10 compatible. Legacy Sandbox lifecycle ownership
  needs a separate implementation and validation before any Win10 CI claim.
- Start from a **non-elevated**, active, unlocked default desktop. Active RDP is
  permitted only with the explicit experiment switch. Disconnected, locked,
  Session 0, SYSTEM, and unavailable input desktops are refused. Keep the host
  input desktop active; a transient null foreground HWND during window creation
  is diagnostic, not a disconnect. Cursor access, WTS state, and the input-desktop
  name determine eligibility, matching MWB's desktop-activity check. Keep the host
  session and Sandbox viewer connected. The default guest account may report
  elevated; the scripts do not change its token, UAC, services, or policy. An
  ordinary-user guest configuration is a separate, unvalidated scenario.
- Close existing PowerToys and Sandbox sessions yourself. A shared idle
  `WindowsSandboxServer` is allowed and never killed. Firewall restrictions,
  Settings IPC/test-signing restrictions, rendering failures, or missing guest
  runtime dependencies may still block the experiment. No connection or remote
  input success is claimed by merely starting endpoints.

The flag is off by default, **Debug only**, and inherited from Runner by MWB.
Launchers set it only in Runner's `ProcessStartInfo.EnvironmentVariables`; they
never set a User/Machine environment variable or change the caller's environment.
Release ignores it. It relaxes console-session eligibility only; active/unlocked
default desktop and non-service restrictions remain. No settings schema/UI switch
is added.

## 1. Prepare only (safe to do before disconnecting)

From the repository root, choose a **new private directory outside the checkout**:

```powershell
.\src\modules\MouseWithoutBorders\Tests\SandboxExperiment\Prepare-Experiment.ps1 `
    -Destination C:\MwbExperiments\DebugCandidate1
```

Preparation refuses an existing directory, network share, source-tree destination,
or destination ancestor junction. It writes:

| Path | Purpose |
| --- | --- |
| `prepared.json` | Original payload paths, Debug/x64 configuration, timestamps, SHA-256 hashes of key binaries, staged scripts/tools, and source-derived module names |
| `input\` | Reusable scripts/documentation and portable `winapp\` tools; mapped read-only |
| `input\Sandbox.wsb.template` | Network enabled; clipboard redirection, audio input, video input, and printer redirection disabled |
| `runs\` | Initially empty; each later launch creates a fresh GUID directory |

The product build is mapped read-only, not copied. Hash checks run before host
mutation and again inside the guest; changed/missing recorded files require a new
preparation. Launch from the staged `input` directory and do not relocate the
preparation. The manifest fingerprints **key files**, not every dependency in the
multi-gigabyte build. The fixed `x64\Debug` source path and flag-presence check
assume a successfully completed Debug build; they do not prove that a mixed build
is coherent. `FileVersionInfo.IsDebug` is not used: managed Debug and Release
binaries can both report false.

## 2. Start later, together

Keep this command in a **foreground shell**; it is the lifecycle supervisor:

```powershell
& C:\MwbExperiments\DebugCandidate1\input\Start-Experiment.ps1 `
    -Destination C:\MwbExperiments\DebugCandidate1 -AllowNonConsole
```

It prints a unique `RunRoot` and the corresponding stop command. `active-run.json`
also records that path. Startup creates a run-specific `.wsb`, read-only request
mapping, and writable **guest-evidence-only** mapping. Host settings backups are
never mapped to the guest. The Sandbox GUID equals the run GUID, assigned and
journaled **before** requesting creation.

Guest readiness must carry that run ID. DNS host names use `Dns.GetHostName()`
(not the truncated guest `COMPUTERNAME`); connection names and name-to-IP mappings
then use MWB's 32-character limit from `Common.GetMachineName`. Full DNS names are
retained separately in network evidence. IPv4 addresses are rediscovered from
guest bootstrap and matched to the host's current virtual-switch gateway; an
ambiguous network is refused rather than guessed.

Only MWB is enabled in the seeded configuration. Both endpoints start with a blank
key, service mode off, and MWB clipboard/file sharing **off**. No pairing, UI probe,
clipboard write, input injection, service test, secure-desktop test, firewall
change, installation, or CI run occurs automatically.

## 3. Optional explicit probes during the joint experiment

Use a second host shell, substituting the **printed** run directory:

```powershell
$probe = 'C:\MwbExperiments\DebugCandidate1\input\Invoke-ExperimentProbe.ps1'
$run = 'C:\MwbExperiments\DebugCandidate1\runs\<printed-guid>'
& $probe -RunRoot $run -Target Host  -Action SessionEvidence
& $probe -RunRoot $run -Target Guest -Action SessionEvidence
& $probe -RunRoot $run -Target Host  -Action Navigate
& $probe -RunRoot $run -Target Guest -Action Navigate

# These are real Settings UI actions, only when explicitly requested:
& $probe -RunRoot $run -Target Host  -Action GenerateKey
& $probe -RunRoot $run -Target Guest -Action Connect -KeyPath "$run\host\generated-key.txt"
& $probe -RunRoot $run -Target Host  -Action Status
& $probe -RunRoot $run -Target Guest -Action Status
```

`Refresh` is also available. Probes target recorded Settings process IDs, not any
arbitrary Settings window. The navigation/button labels currently assume English
Settings. They use UIA invocation/set-value, not SendInput. A submitted Connect
request or device-list entry **does not prove a connected MWB transport**. Compare
both endpoints' session/process/TCP evidence and observed UI state. Actual remote
mouse/keyboard/clipboard tests need a separately approved plan, including negative
controls against Sandbox viewer/native redirection.

Generated keys and pending Connect requests are sensitive local experiment data;
do not commit or upload the run directory. Successful stop removes generated-key
files and pending requests. Original settings backups can also contain secrets.

## 4. Stop and restore, including failed bootstrap

Ctrl+C in the supervisor requests cleanup through `finally`. Alternatively:

```powershell
& C:\MwbExperiments\DebugCandidate1\input\Stop-Experiment.ps1 `
    -RunRoot 'C:\MwbExperiments\DebugCandidate1\runs\<printed-guid>'
```

The second-shell command asks the live supervisor to stop, or performs recovery
itself if the supervisor is gone. Run it as the original host account. It verifies
owned **PID + executable path + start time + session**, stops Runner before its
recorded descendants, and stops only the recorded Sandbox GUID. There is no
process-name kill and no takeover of existing sessions.

Before any future host settings mutation, the original global `settings.json`,
`oobe_settings.json`, and `MouseWithoutBorders\settings.json` are backed up as raw
bytes, with hashes and prior absence recorded. Cleanup restores and verifies exact
bytes or deletes files originally absent, even if guest bootstrap/startup failed.
All newly written native settings JSON is UTF-8 **without BOM**, including under
Windows PowerShell 5.1. MWB booleans use its `{"value": false}` property wrapper;
bare JSON booleans are rejected by its converter and can cause fallback to defaults.
Logs/other runtime-created caches are not rolled back.

The supervisor continually checks host desktop eligibility. Guest and host
exchange leases/heartbeats; disconnect/lock or expired heartbeat initiates cleanup.
Guest bootstrap first waits, with a bounded deadline, for the viewer to establish
an input desktop. Mapped-channel JSON is written in place because Sandbox can hold
files without delete sharing; readers retry incomplete publication briefly and fail
if a complete document does not appear.
An abruptly terminated host PowerShell cannot restore its own settings: the guest
times out, and **you must run Stop** for host recovery before starting another run.
Unproven process ownership or an active unrelated PowerToys process fails cleanup
closed; inspect the evidence and close that process yourself, then retry Stop.
Do not delete a failed run's backups. A successful `Stopped` status and
`host\restored.json` are the cleanup evidence; backups remain until you remove the
private experiment directory.

**No firewall/elevation/service changes are implemented.** If connectivity is
blocked by firewall or privilege boundaries, collect evidence and request separate
explicit approval before modifying them.

## Validation status

Run the non-launch checks from the source checkout:

```powershell
powershell.exe -NoProfile -File .\src\modules\MouseWithoutBorders\Tests\SandboxExperiment\Test-Preparation.ps1
```

These check parsing, preparation/opt-in refusal, BOM-free writes, exact byte/absence
restoration, corrupted-backup refusal, stale payload rejection, process identity,
MWB machine-name normalization, startup readiness, and mocked desktop gates.
Fixtures are created beside the scripts and removed;
no live product settings, processes, UI, or Sandbox are mutated.

Script parsing and non-launch preparation/helper checks are suitable now. The
initial Windows 11 host/Sandbox feasibility run exercised supervised startup and
cleanup, real Settings key generation/connection, host-to-guest keyboard and mouse
input, and text clipboard transfer in both directions. A local-mode input control
and clipboard-sharing-off control ruled out native viewer redirection. Clipboard
copies were spaced beyond MWB's one-second event throttle.

`MwbSettings.template.json` is also deserialized by the production settings model
in `ExperimentSettingsTests`, so malformed property wrappers cannot silently turn
the intended baseline into product defaults.

This is still not a full module suite, service/secure-desktop sign-off, or Windows 10
CI result. No automatic build hook or UITest project invokes these scripts.
