# Local UI-test VM on Hyper-V

Create a persistent, interactive Windows guest on the platform hypervisor and drive it from the host
over PowerShell Direct. This is the only supported VM backend for this skill; everything after the
guest exists is described in [agentic-loop.md](agentic-loop.md).

What you get:

- No nested virtualization, so the scaffold works on x64 and on Windows on ARM alike.
- No guest listener, port, certificate, or firewall rule. A guest with no network adapter at all
  still works.
- Fast, repeatable clean baselines: a standard checkpoint restores a logged-on desktop in seconds.
- An agent-readable console: `Get-VmConsoleImage.ps1` writes a PNG of the framebuffer, and
  `vmconnect.exe` is available for interaction.

| Concern | Mechanism |
|---|---|
| Control channel | PowerShell Direct over VMBus |
| Exchange | Guest-local `C:\PowerToysUiTestExchange\<name>`, mirrored by the controller |
| Payload transfer | `Copy-VMFile` over the Guest Service Interface, skipping archives whose SHA-256 already matches; the session copy is a fallback |
| Clean baseline | `Reset-LocalVm.ps1 -Restore` |
| Host shell | **Elevated**, or an account in the local Hyper-V Administrators group |

## Host requirements

- Windows 10/11 Pro, Enterprise, or Education with the Hyper-V feature enabled, or Windows Server.
- Permission to manage Hyper-V, granted once through [step 0](#0-grant-hyper-v-access-one-time-elevated).
  The scripts probe the capability rather than the token shape, and stop with `BLOCKED` rather than
  degrading the channel when neither route is available.
- **Guest storage on an NTFS volume.** On the host this skill was built against, keeping the VHDX on
  a Dev Drive wedged the Hyper-V management service mid-operation: `vmms` and `vmwp` sat at 0% CPU,
  later management calls never returned - including read-only ones such as `Get-VM` - and recovery
  needed a `vmms` restart or a host reboot. Moving the guest to NTFS fixed it.

  Treat that as an observation on one host rather than a property of ReFS. Hyper-V on ReFS is a
  supported and in places preferred configuration, and ReFS block cloning accelerates checkpoint
  merges. A Dev Drive differs from plain ReFS mainly in that it attaches only an allow-listed set of
  filesystem filters, which is a plausible - but unproven - way to strand a storage operation.

  `New-UiTestVm.ps1` therefore refuses ReFS by default and accepts `-AllowReFsVolume` as the
  override; ReFS is a cheap proxy for "Dev Drive", since telling the two apart needs elevation.
  Check with `(Get-Volume -DriveLetter D).FileSystemType`.

  This applies only to `VhdPath` and `VmPath`. The scaffold, the `shared` exchange, and the staged
  archives are ordinary file I/O and run fine on a Dev Drive.
- Free disk for the guest disk plus checkpoints. Budget at least twice `DiskSizeGB`. A standard
  checkpoint also stores the guest's memory, so add `MemoryStartupGB` on top.
- Host and guest architecture must match. Hyper-V does not emulate a foreign architecture, so an
  ARM64 host builds ARM64 guests only.
- **The Visual C++ redistributable, for MP4 capture.** The harness records each test with
  ScreenRecorderLib, a mixed-mode assembly importing `VCRUNTIME140`/`MSVCP140`; a clean Windows image
  has neither, so video is skipped (the harness now prints why). `Initialize-LocalVmHost.ps1`
  downloads the architecture-matched Microsoft-signed redistributable, verifies its Authenticode
  signer, and stages it under `oem`; provisioning installs it automatically and
  `ProvisioningReady.json` reports `ScreenRecordingSupported`. The controller also repairs an
  existing guest from that verified payload before a run.
- **PowerShell 7 for guest-side orchestration.** The setup helper downloads the pinned official MSI,
  verifies both its published release SHA-256 and Microsoft Authenticode signer, and stages it under
  `oem`. Provisioning installs it with PS remoting disabled; the controller repairs existing guests
  and runs desktop-probe/test scheduled tasks under `pwsh.exe`.

  PowerShell Direct and OEM bootstrap still use inbox Windows PowerShell 5.1. That is intentional:
  registering a PS7 remoting endpoint would weaken the no-remoting posture, and provisioning must
  work before PS7 exists. Keep every `Invoke-Command -VMName` scriptblock PS5.1-compatible; PS7
  removes that constraint from the much larger interactive runner.

> **ARM64 guests need ARM64 payloads.** An ARM64 guest needs ARM64 PowerToys, test, winappcli, .NET,
> and WebView2 payloads, and it must be run with `-Platform ARM64` so visual baselines resolve.

## 0. Human-only host setup (one command)

Three prerequisites gate every agent-driven run, and **an agent can perform none of them**: two need
elevation, which no tool call can approve, and one needs a password, which must never be routed
through a model. `Initialize-LocalVmHost.ps1` does all three, reports what is already in place,
performs only what is missing, and is safe to re-run. It also refreshes copied scaffold scripts from
the current skill templates before mutating anything, stages VC++ and PowerShell 7 prerequisites,
and verifies Windows 10's .NET 10 CET floor before recapturing the baseline.

| # | Prerequisite | Why a human |
|---|---|---|
| 1 | Membership in the local **Hyper-V Administrators** group | Elevation; takes effect only after signing out and back in |
| 2 | DPAPI **guest administrator credential** | A password typed straight into the prompt |
| 3 | The **guest** itself (`New-UiTestVm.ps1`) | Elevation, to read the media and create the virtual disk |

Scaffold ([step 1](#1-scaffold)) and obtain media ([step 3](#3-get-windows-media)) first, then run
this once from an **elevated PowerShell 7** terminal:

```pwsh
pwsh .github\skills\ui-tests-local-vm\scripts\Initialize-LocalVmHost.ps1 `
  -VmRoot C:\PowerToysUiTestVm `
  -InstallMedia C:\PowerToysUiTestVm\media\Win11_25H2_English_x64.iso
```

If the account was not yet in Hyper-V Administrators, the script adds it and stops: group membership
is baked into the logon token, so it **signs out and back in, then re-runs** to finish steps 2 and 3.
Everything after that is unattended.

### Agents: check, then stop

Run it with `-CheckOnly`. It needs no elevation, changes nothing, prints a status table, and exits
non-zero when something is missing:

```pwsh
pwsh .github\skills\ui-tests-local-vm\scripts\Initialize-LocalVmHost.ps1 -VmRoot C:\PowerToysUiTestVm -CheckOnly
```

```text
Local UI-test VM host setup - PowerToysUiTest-Win11
  [ok]      Hyper-V access    ok
  [missing] Guest credential  missing: ...\admin.credential.xml
  [ok]      Guest             ok (State=Running)
```

When it reports `IsReady=false`, **stop and ask the user to run the elevated command it prints.** Do
not autopilot around it, do not ask for a password, and do not substitute a weaker channel.
`Invoke-LocalVmUiTest.ps1` enforces the same check and throws `BLOCKED` with the same instruction.

Useful switches: `-CheckOnly`, `-SkipScaffoldRefresh`, `-SkipVcRedist`, `-SkipPowerShell`, `-SkipWindowsUpdate`,
`-SkipGroupMembership`, `-SkipCredential`, `-SkipGuestCreation`, `-Account` (defaults to the current
user), `-Force`, `-AllowReFsVolume`.

### Verifying by hand

```pwsh
whoami /groups | Select-String 'Hyper-V Administrators'   # must print the group
Get-VM                                                    # must not throw a permission error
```

`Get-VM: You do not have the required permission to complete this task` means the membership is
missing or the session predates it. Membership alone covers the whole run loop - scaffold, start and
stop, checkpoint reset, PowerShell Direct, `Copy-VMFile`; only **creating** a guest additionally needs
an elevated terminal.

## 1. Scaffold

```pwsh
pwsh .github\skills\ui-tests-local-vm\scripts\Initialize-LocalVm.ps1 `
  -DestinationRoot X:\PowerToysUiTestVm
```

```text
X:\PowerToysUiTestVm\
|-- vm.config.example.psd1
|-- New-UiTestVm.ps1
|-- Start-LocalVm.ps1
|-- Stop-LocalVm.ps1
|-- Reset-LocalVm.ps1
|-- .gitignore
|-- unattend\unattend.xml.template
|-- oem\Provision-UiTestVm.ps1
`-- shared\
```

Copy `vm.config.example.psd1` to `vm.config.psd1` and set the VM name, storage paths, resources, and
`ProcessorArchitecture`. The configuration never contains a password.

## 2. Save the administrator credential first

[Step 0](#0-human-only-host-setup-one-command) does this for you. The equivalent by hand, when you
want to rotate the password or stage it separately:

`New-UiTestVm.ps1` reads the guest administrator password from a DPAPI-protected file so it never
appears in a command line, a configuration file, or a chat prompt. Type it directly into the prompt.

```pwsh
$credentialRoot = Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm'
New-Item $credentialRoot -ItemType Directory -Force | Out-Null
Get-Credential -UserName PTAdmin -Message 'Local UI-test VM administrator' |
  Export-Clixml (Join-Path $credentialRoot 'admin.credential.xml')
```

The file decrypts only for the same Windows user on the same host.

## 3. Get Windows media

```pwsh
# Validate media you already have.
pwsh .github\skills\ui-tests-local-vm\scripts\Get-WindowsMedia.ps1 `
  -Source Local -Path D:\media\Win11_25H2_English_x64.iso

# Windows 11 (x64 or arm64).
pwsh .github\skills\ui-tests-local-vm\scripts\Get-WindowsMedia.ps1 `
  -Source Fido -Windows 11 -Architecture arm64 -DestinationRoot D:\media

# Windows 10 for the second guest. Fido automates Microsoft's mobile-user-agent download page;
# the resulting file is hosted by software.download.prss.microsoft.com.
pwsh .github\skills\ui-tests-local-vm\scripts\Get-WindowsMedia.ps1 `
  -Source Fido -Windows 10 -Architecture x64 -DestinationRoot D:\media
```

| Source | Use it for |
|---|---|
| `Local` | An ISO you already downloaded. Reports the SHA-256 so a team can pin one baseline. |
| `Url` | A pinned Microsoft Evaluation Center link. Enterprise/LTSC evaluations live here. |
| `Fido` | Official retail links resolved through the GPL-3.0 helper used by Rufus. Covers **Windows 10 and 11**, and is the only public route that also resolves arm64 Windows 11. |

### Which Windows 10 image

The two public Microsoft routes are both valid but neither is current enough for this repository:

| Official route | Image currently produced | Notes |
|---|---|---|
| Microsoft ISO page (mobile user agent), automated by Fido | `Win10_22H2_English_x64v1.iso`, **19045.2965** (May 2023) | Smallest automation surface; direct Microsoft CDN |
| Microsoft Media Creation Tool | **19045.3803** (December 2023 service refresh) | Tool requires UAC and interactive choices |

.NET 10 needs Windows 10 **1904x.5007 or newer** for complete CET support. Either public image by
itself aborts with `0x80131506` / `Your Windows doesn't fully support CET`. The primary path is
therefore Microsoft media plus the answer file's supported Windows Setup `DynamicUpdate=true`, which
fetches the current cumulative update during installation before the first baseline. The VM needs a
network switch for this (the default `Default Switch` does).

`Initialize-LocalVmHost.ps1` verifies the installed UBR before accepting the baseline. If Dynamic
Update could not reach 5007, it runs `Update-LocalVmGuest.ps1` as a fallback, handles reboots, and
recreates the baseline. Do not capture a Win10 baseline below that floor.

Windows 10 **Enterprise LTSC 2021** (build 19044) remains the stricter baseline named in the guest-OS
policy. Its old Evaluation Center page is no longer a dependable public download route; use licensed
Microsoft 365 / Visual Studio subscription media when available, then let Dynamic Update apply the
same CET-floor validation. Record the edition, ISO hash, and installed full build in every run.

The `Fido` source downloads the helper from a pinned tag and refuses to run it unless its SHA-256
matches the value pinned in the script. Upstream publishes no Authenticode-signed script, so review
the upstream diff and update the tag and hash together when raising the pin. Fido is never vendored
into this repository.

A prepared, generalized VHDX is not supported by this script: it always installs from media so that
Setup owns the disk layout and the boot configuration.

## 4. Create the guest

[Step 0](#0-human-only-host-setup-one-command) invokes this script for you. Call it directly when you
want `-ListImages`, `-PlanOnly`, or to rebuild an existing guest with `-Force`.

The scaffold files are copies and can become stale when the skill is updated. Prefer the step-0
helper, which refreshes them and executes the **source template** directly. If you intentionally run
the copied `X:\PowerToysUiTestVm\New-UiTestVm.ps1`, refresh first with
`Initialize-LocalVm.ps1 -DestinationRoot X:\PowerToysUiTestVm -Force`.

```pwsh
# Inspect what the media contains.
pwsh .\New-UiTestVm.ps1 -InstallMedia D:\media\Win11_25H2_English_Arm64_v2.iso -ListImages

# Build the guest.
pwsh .\New-UiTestVm.ps1 -InstallMedia D:\media\Win11_25H2_English_Arm64_v2.iso -ImageName 'Windows 11 Pro'
```

The script creates an empty virtual disk, generates an answer file, packs it with the OEM payload
into a small ISO, attaches both that and the installation media, and lets **Windows Setup install
from inside the guest**. Run `-PlanOnly` first to check the resolved configuration and confirm the
answer file renders, without touching Hyper-V.

Do not be tempted to speed this up by applying the image with DISM and running `bcdboot` on the host.
That is what `Convert-WindowsImage` does, but its goal is *native-VHD boot*, so `bcdboot` records
`vhd=[X:]\path\to.vhdx` device references. Inside a virtual machine that file does not exist - the
VHDX is the disk - and the guest dies with `0xc000000e` before writing a single log line. It cannot
be repaired from the host either: `bcdedit` resolves drive letters through the host's view and
rewrites them straight back into `vhd=` references.

Several answer-file details are derived from [Rufus](https://github.com/pbatard/rufus) (GPL-3.0,
`src/wue.c`), which solves the same problem for USB media:

| Setting | Why it matters |
|---|---|
| `<ProductKey><Key /></ProductKey>` | Setup rejects the answer file without a product key element, even an empty one. |
| `HideOnlineAccountScreens` + a local account | Skips the Microsoft-account wall. Preferred over the deprecated `SkipMachineOOBE`/`SkipUserOOBE`, which Microsoft warns can leave OOBE in an unexpected state. |
| Base64-obfuscated passwords | Windows appends the element name to the password before base64-encoding UTF-16LE, so no plaintext password reaches the media. |
| `PreventDeviceEncryption`, `TCGSecurityActivationDisabled` | The guest has a virtual TPM, so Windows 11 would otherwise silently BitLocker-encrypt the disk and make it unreadable offline. |
| `BypassNRO` in specialize | Removes the online-account requirement during OOBE. |

The script also presses Enter on the guest's virtual keyboard through `Msvm_Keyboard` while Setup
starts, because installation media waits for "Press any key to boot from CD or DVD" and nothing
types it in an automated VM.

When provisioning finishes the script detaches both optical drives, deletes the generated answer ISO,
and takes the `provisioned-baseline` checkpoint. Provisioning creates `PTUser`, removes it from
Administrators, grants it the work root, configures console auto-logon, and disables sleep. It does
**not** enable remoting or open any port: the control channel needs neither.

`Set-UiTestAutoLogon.ps1` validates each generated PTUser credential with `LogonUser`, stores it as
the protected LSA `DefaultPassword` secret, and removes the readable Winlogon `DefaultPassword` and
finite `AutoLogonCount` values. The count in the unattended answer is only a bootstrap until OEM
provisioning replaces the administrator login with persistent PTUser `ForceAutoLogon`.
`Stop-LocalVm.ps1` revalidates and preserves that credential before a cold shutdown, so PTUser's
DPAPI-protected profile data remains decryptable. `Start-LocalVm.ps1` uses the same helper for a
one-time repair when no PTUser Explorer session appears; a new credential is generated only when
the account has no valid protected secret. Updating only the Winlogon registry identity while
leaving an older LSA password produces a bad-password console logon.

Watch progress at any time without VMConnect:

```pwsh
pwsh ..\..\scripts\Get-VmConsoleImage.ps1 -VmName PowerToysUiTest-Win11 -Path X:\evidence\console.png
```

## 5. Confirm the desktop baseline

Provisioning registers a logon task that sets the interactive desktop to 1920x1080 through
`ChangeDisplaySettings`, because display settings belong to the interactive session and cannot be
applied from the PowerShell Direct session. No manual step is needed; verify it instead:

```pwsh
pwsh ..\..\scripts\Get-VmConsoleImage.ps1 -VmName PowerToysUiTest-Win11 -Path X:\evidence\desktop.png
```

The controller's own probe is the authoritative check - it fails the run unless the interactive user
is the configured standard user, is not an administrator, has a session ID above zero, has Explorer
running, can reach the guest exchange, and matches the requested resolution.

If you open the console interactively with `vmconnect.exe localhost "PowerToysUiTest-Win11"`, turn
**off** enhanced session mode in the View menu: it opens a second session and can displace the
console session where the standard user is logged on.

Re-take the baseline whenever you change the guest in a way later runs should inherit:

```pwsh
pwsh .\Reset-LocalVm.ps1 -CreateBaseline
```

## 6. Run tests

```pwsh
pwsh .github\skills\ui-tests-local-vm\scripts\Invoke-LocalVmUiTest.ps1 `
  -VmName PowerToysUiTest-Win11 `
  -VmRoot X:\PowerToysUiTestVm `
  -ExchangeRoot X:\PowerToysUiTestVm\shared\PowerToysUiTests\MyModule `
  -TestExecutable MyModule.UITests.Next.exe `
  -Filter 'Name=MyModule.FocusedTest' `
  -Platform ARM64 `
  -BuildLabel (git rev-parse HEAD) `
  -ReuseStagedPayload
```

Use `-Platform ARM64` for an ARM64 guest. The value reaches the tests as the `platform` environment
variable, where `VisualAssert` builds baseline filenames from it (`<Class>_<Test>_ARM64.png`) and any
non-empty value makes the framework consider itself in a pipeline. It is restricted to the names CI
uses - `x64Win10`, `x64Win11`, `ARM64` - because an unrecognised value resolves no baseline and fails
silently rather than loudly.

The controller creates the guest exchange, grants `PTUser` access to it, copies only the archives
whose hash changed, writes the request, probes the interactive desktop, dispatches the shared guest
runner as a limited interactive scheduled task, streams progress, copies the evidence back to
`<ExchangeRoot>\LocalVmResults\<runId>`, and removes the guest copy of that run folder.

Payloads move with `Copy-VMFile` over the Guest Service Interface, measured at ~82 MB/s. The
PowerShell Direct session copy is the fallback only: it manages ~17 MB/s and stalls outright on
archives approaching a gigabyte.

## 6a. Shell-extension modules: sign the payload before packaging

Modules with a modern Windows 11 context menu (Image Resizer, PowerRename, File Locksmith, New+)
register a sparse MSIX at module-enable time, which requires a signature chaining to a trusted root.
An unsigned package fails `0x800B0100` and the menu never appears - the tests then fail with
messages like "Explorer did not show the 'Resize with Image Resizer' command".

Sign at **packaging** time, not after deployment. The guest runner extracts the product and runs the
tests in one step, so there is no point in between where the extracted `.msix` could be signed; a
post-deployment signing step costs an extra full run every time the product archive changes.

```pwsh
# 1. Host: sign the staged product tree without trusting anything on the build machine.
.\.pipelines\signSparsePackages.ps1 `
  -PackageRoot X:\PowerToysUiTestPayload\product `
  -SkipLocalTrust -ExportCertificatePath X:\PowerToysUiTestPayload\pt-test-signer.cer

# 2. Guest, once per VM: trust the exported public certificate.
& .github\skills\ui-tests-local-vm\scripts\Invoke-GuestScript.ps1 `
  -VmName PowerToysUiTest-Win11 `
  -ScriptBlock {
      foreach ($store in 'Cert:\LocalMachine\Root', 'Cert:\LocalMachine\TrustedPeople') {
          Import-Certificate -FilePath C:\PowerToysUiTestTools\pt-test-signer.cer -CertStoreLocation $store
      }
  }
```

Then zip the product tree as usual. Every later re-stage carries signed packages, and the trust
anchor lives only inside the disposable guest. The certificate is valid for a year, so step 2 is not
repeated. See [shell-extensions-and-signing.md](shell-extensions-and-signing.md) for why signing -
rather than driving the classic menu - is the faithful fix.

Inspect guest state at any time over the same channel:

```pwsh
& .github\skills\ui-tests-local-vm\scripts\Invoke-GuestScript.ps1 `
  -VmName PowerToysUiTest-Win11 `
  -ScriptBlock { Get-Process explorer | Select-Object Id, SessionId }
```

## 7. Baselines and resets

```pwsh
pwsh .\Reset-LocalVm.ps1 -List                                   # show checkpoints
pwsh .\Reset-LocalVm.ps1 -Restore -StartAfterRestore             # back to the clean baseline
pwsh .\Reset-LocalVm.ps1 -CreateBaseline -CheckpointName 'webview2-installed'
```

Standard checkpoints include memory, so restoring returns to the captured desktop rather than a cold
boot. Use a restored checkpoint, not a long-lived mutated guest, for any clean-profile claim.

`Stop-LocalVm.ps1 -Save` saves state instead of shutting down when you want the next run to resume
instantly.

## Security notes

- No inbound listener, no published port, and no certificate exist. The control channel is VMBus and
  is reachable only by a local administrator on this host.
- Auto-logon necessarily stores a recoverable password inside the guest. Treat the guest as an
  isolated test machine and never reuse either account elsewhere.
- Keep `vm.config.psd1`, the guest disk, checkpoints, and the credential file out of source control.
  The scaffold's `.gitignore` already excludes them.
