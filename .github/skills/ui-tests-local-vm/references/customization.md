# Local VM customization

## What is actually customized

The guest is an ordinary Hyper-V virtual machine backed by one VHDX. Installing software does not
produce a new layer or image; it changes that persistent disk. Checkpoints are the only rollback
mechanism, so take one whenever you reach a state later runs should inherit.

Use three levels deliberately:

1. **VM configuration** (`vm.config.psd1`) - name, storage paths, disk size, CPU, RAM, switch,
   accounts, architecture, locale, and the baseline checkpoint name.
2. **OEM baseline** - deterministic software and policy applied during Windows installation.
3. **Retained disk** - tools, user profiles, caches, and ad hoc diagnostics installed later.

Pin the Windows media and every installer version for reproducibility. Keep OEM scripts in source
control, and keep `vm.config.psd1`, credentials, Windows media, licensed installers, checkpoints, and
the VHDX out of source control. The scaffold's `.gitignore` already excludes them.

## OEM installation

`New-UiTestVm.ps1` builds an answer-file ISO whose `FirstLogonCommands` copy the scaffold's `oem`
folder into `C:\OEM` and run `Provision-UiTestVm.ps1`. Extend that script or call additional scripts
from it. Every addition must be silent, idempotent, checksum-verified, and return a meaningful exit
code.

Prefer offline, signed installers staged beside the script. Avoid downloading floating `latest`
artifacts during baseline creation. Record versions and SHA-256 values in a provisioning manifest.

To update an existing VM, run the same idempotent script over PowerShell Direct with
`scripts/Invoke-GuestScript.ps1`, then re-take the baseline checkpoint. Recreate the guest instead
when installation-order or first-logon behavior matters.

## .NET 10

Yes, .NET 10 can be part of the default baseline.

- Place `dotnet-sdk-10*-win-*.exe` in `oem` to install the SDK and runtimes.
- If guest compilation is unnecessary, place `windowsdesktop-runtime-10*-win-*.exe` instead.
- Match the guest architecture: an ARM64 guest needs `-win-arm64` installers.
- Pin the exact installer version and verify its Microsoft-published checksum.
- Use Windows 10 Enterprise LTSC 2021 for the first maintained baseline on x64, and keep an equally
  maintained Windows 11 baseline: both carry a full suite run.

The UI-test controller still stages a private pinned `dotnet-runtime.zip` by default. This is
intentional: revision runs remain independent of servicing changes in the VM. The baseline runtime is
useful for diagnostics and manually launched tools. Rely on the system runtime only after extending
the guest contract to record and enforce its exact version.

## WebView2 and other runtime prerequisites

Place `MicrosoftEdgeWebView2RuntimeInstaller*.exe` in `oem` for WebView/Monaco-heavy suites. The
guest runner also supports a run-specific installer when the baseline lacks it.

VC++ redistributables, Windows App SDK runtimes, certificates, fonts, media codecs, and test data can
be provisioned the same way. Install only what CI or the target user machine actually has; an overly
rich baseline can hide deployment defects.

## Visual Studio Remote Debugger (`msvsmon`)

`msvsmon` is optional. Normal UITest.Next execution requires no Visual Studio installation or remote
tools in the VM. The controller needs only PowerShell Direct, Task Scheduler, the interactive
desktop, winappcli, the product, tests, and the .NET runtime.

For interactive debugging:

1. Install the Remote Tools version compatible with the host Visual Studio, or copy the complete
   matching Remote Debugger folder from the host installation.
2. Match the guest architecture. The x64 monitor can launch the 32-bit monitor when needed.
3. Attach the guest to a switch the host can reach - the `Default Switch` gives it a host-only NAT
   address - and note that address; the control channel itself needs no network.
4. Add guest firewall rules for TCP 4026, and TCP 4025 only for WOW64 debugging.
5. Start `msvsmon` in the same `PTUser` desktop for normal user-process debugging.
6. Use `/nodiscovery` and connect directly to `<guest-address>:4026`; UDP 3702 discovery does not
   need to be exposed.
7. Keep Windows authentication. Do not use no-auth mode outside an isolated disposable network.

Run an elevated monitor only when attaching to an elevated or different-user process. That changes
the integrity boundary and must not be confused with the normal user-only test scenario.

No additional Visual Studio components are required in the VM unless the guest must compile code.
Keep symbols on the host when possible; copy matching binaries/PDBs or point the debugger at the
staged build outputs.

The standard controller does not start or stop `msvsmon`. Treat remote debugging as a developer
profile, not a dependency of unattended validation.

## Golden baselines and reset

A useful golden checkpoint has:

- Windows fully serviced and activated as appropriate.
- OEM provisioning complete.
- `PTUser` auto-logon verified and the resolution task applied.
- Optional pinned runtimes installed.
- No PowerToys payload, test results, or module-specific cache in the guest work root.

```pwsh
pwsh .\Reset-LocalVm.ps1 -List                                   # show checkpoints
pwsh .\Reset-LocalVm.ps1 -CreateBaseline -CheckpointName 'webview2-installed'
pwsh .\Reset-LocalVm.ps1 -Restore -StartAfterRestore             # back to the clean baseline
```

Standard checkpoints include memory, so restoring returns to the captured logged-on desktop in
seconds rather than a cold boot - far cheaper than reinstalling Windows for every clean-profile run.
Restoring a checkpoint is also more deterministic than accumulating repair scripts.

Keep the Windows 10 and Windows 11 baselines as separate VMs with their own
configuration files and VHDX paths. Never upgrade one baseline in place to stand in for the other.

Checkpoints consume disk. Budget at least twice `DiskSizeGB`, plus `MemoryStartupGB` per standard
checkpoint, and prune obsolete ones.

## Custom Windows media

`New-UiTestVm.ps1` accepts any Windows ISO through `-InstallMedia`, and
`scripts/Get-WindowsMedia.ps1` validates local media or resolves an official Microsoft retail link.
Host and guest architecture must match: Hyper-V does not emulate a foreign architecture, so an ARM64
host builds ARM64 guests only. Use custom media when licensing, edition, language, servicing, or
enterprise policy requires it, and keep OEM provisioning independent of the media source.

## Resource sizing

Use the default profile until the target suite is fully green: 4 vCPUs and 8 GB RAM, from
`ProcessorCount` and `MemoryStartupGB` in `vm.config.psd1`. Establish correctness and stable timings
with this profile before investigating resource sensitivity.

Only after the suite is green, restart with the constrained profile. Its defaults are 4 GB RAM and
1 vCPU, overridable with `ConstrainedMemoryStartupGB` and `ConstrainedProcessorCount`. Restricting
the VM to a single core is how this workflow reproduces slow-agent and CI-like timing pressure. Treat
failures introduced only by this second phase as resource-pressure findings; do not weaken assertions
to accommodate them.

```pwsh
# Default correctness pass.
pwsh .\Start-LocalVm.ps1 -ResourceProfile Default -Wait

# Post-green pressure iteration.
pwsh .\Start-LocalVm.ps1 -ResourceProfile Constrained -Wait
```

Run `Start-LocalVm.ps1 -PlanOnly` to inspect the resolved profile without touching the VM. The guest
runs directly on the platform hypervisor, so its memory comes straight from the host: leave several
gigabytes of headroom for the host and the build. Apply CPU pressure by lowering the VM's vCPU count,
not process affinity.
