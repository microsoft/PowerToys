# Local VM customization

## What is actually customized

`dockur/windows` is a Linux container that runs QEMU. Installing software does not create a new
Windows Docker layer; it changes the persistent Windows disk in the `/storage` Docker volume.

Use three levels deliberately:

1. **Compose configuration** - Windows version, CPU, RAM, disk, networking, ports, and shares.
2. **OEM baseline** - deterministic software and policy applied during Windows installation.
3. **Retained disk** - tools, user profiles, caches, and ad hoc diagnostics installed later.

Pin the dockur image and Windows version for reproducibility. Keep OEM scripts in source control but
keep `.env`, credentials, Windows media, licensed installers, and the VM disk out of source control.

## OEM installation

Dockur copies the bound `oem` directory to `C:\OEM` and executes `install.bat` during the final
installation step. Extend `Provision-UiTestVm.ps1` or call additional scripts from `install.bat`.
Every addition must be silent, idempotent, checksum-verified, and return a meaningful exit code.

Prefer offline, signed installers staged beside the script. Avoid downloading floating `latest`
artifacts during baseline creation. Record versions and SHA-256 values in a provisioning manifest.

To update an existing VM, connect through administrator WinRM and run the same idempotent script, or
recreate the volume when installation-order/first-login behavior matters.

## .NET 10

Yes, .NET 10 can be part of the default baseline.

- Place `dotnet-sdk-10*-win-x64.exe` in `oem` to install the SDK and runtimes.
- If guest compilation is unnecessary, place `windowsdesktop-runtime-10*-win-x64.exe` instead.
- Pin the exact installer version and verify its Microsoft-published checksum.
- Use Windows 10 Enterprise LTSC 2021 for the default maintained baseline. Provision a separate
   Windows 11 baseline only for requirements that explicitly depend on Windows 11.

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
tools in the VM. The controller needs only HTTPS WinRM, Task Scheduler, the interactive desktop,
winappcli, the product, tests, and .NET runtime.

For interactive debugging:

1. Install the Remote Tools version compatible with the host Visual Studio, or copy the complete
   matching Remote Debugger folder from the host installation.
2. Use the x64 tools for an x64 guest. The x64 monitor can launch the 32-bit monitor when needed.
3. Uncomment loopback mappings for guest TCP 4026 and, only for WOW64 debugging, TCP 4025.
4. Add matching guest firewall rules.
5. Start `msvsmon` in the same `PTUser` desktop for normal user-process debugging.
6. Use `/nodiscovery` and connect directly to `127.0.0.1:14026`; UDP 3702 discovery does not need to
   be exposed.
7. Keep Windows authentication. Do not use no-auth mode outside an isolated disposable network.

Run an elevated monitor only when attaching to an elevated or different-user process. That changes
the integrity boundary and must not be confused with the normal user-only test scenario.

No additional Visual Studio components are required in the VM unless the guest must compile code.
Keep symbols on the host when possible; copy matching binaries/PDBs or point the debugger at the
staged build outputs.

## Optional debugger compose ports

```yaml
ports:
  - "127.0.0.1:14026:4026/tcp"
  - "127.0.0.1:14025:4025/tcp" # only for 32-bit targets
```

The standard controller does not start or stop `msvsmon`. Treat remote debugging as a developer
profile, not a dependency of unattended validation.

## Golden baselines and reset

Stop the VM before snapshotting or copying its Docker volume. A useful golden point has:

- Windows fully serviced and activated as appropriate.
- OEM provisioning complete.
- `PTUser` auto-login and HTTPS WinRM verified.
- Required display/scaling configured.
- Optional pinned runtimes installed.
- No PowerToys payload, test results, or module-specific cache in the guest work root.

Prefer cloning this stopped golden volume for every later clean-profile run. On virtualized Windows
hosts, rebuilding from ISO traverses WSL2 and nested KVM/QEMU and is substantially slower than the
available CPU, RAM, or physical-disk counters suggest.

Maintain separate volume names for distinct Windows/platform baselines. Restoring or cloning a
stopped volume is faster and more deterministic than accumulating repair scripts.

Keep the default Windows 10 and supplemental Windows 11 baselines in separate VM roots, containers,
and named volumes. Never upgrade one baseline in place to stand in for the other.

## Custom Windows media

Dockur accepts a custom ISO URL or a local `/custom.iso` mount. Use custom media only when licensing,
edition, language, servicing, or enterprise policy requires it. Keep OEM provisioning independent of
the media source where possible.

## Resource sizing

Use the `GreenFirst` profile until the target suite is fully green. It assigns half of host physical
RAM and approximately 60% of host physical CPU cores, rounding the CPU allocation up to an even
count. For example, a 64 GB / 8-core host runs a 32 GB / 6-vCPU guest. Establish correctness and
stable timings with this profile before investigating resource sensitivity.

Guest RAM is a physical host commitment, not a soft maximum. The dockur QEMU command does not attach
a memory-balloon device, and Windows touches most guest RAM during boot, so a 32 GB guest normally
appears as roughly 33-34 GB of resident QEMU/container memory even during installation. Size the WSL
ceiling and host workload with that commitment in mind.

For a disposable first-time baseline build, dockur supports `DISK_CACHE=writeback`; it automatically
uses threaded AIO because native AIO requires direct caching. This can improve Setup's synchronous
write workload, but it risks losing recent guest writes if QEMU, Docker, WSL, or the host fails. Do
not switch cache mode mid-install. Keep the default `none` for durable retained baselines unless the
faster installation tradeoff is intentional, then stop and snapshot the completed volume promptly.

Only after the suite is green, rerun with `-ResourceProfile Constrained`. Its defaults are 8 GB RAM
and 4 vCPUs and can be changed with `VM_CONSTRAINED_RAM_SIZE` and `VM_CONSTRAINED_CPU_CORES` in
`.env`. Treat failures introduced only by this second phase as resource-pressure findings; do not
weaken assertions to accommodate them.

```pwsh
# Default correctness pass.
pwsh .\Start-LocalVm.ps1 -ResourceProfile GreenFirst -WaitForWinRM

# Post-green pressure iteration.
pwsh .\Start-LocalVm.ps1 -ResourceProfile Constrained -WaitForWinRM
```

Run `Start-LocalVm.ps1 -PlanOnly` to inspect the resolved profile without starting Docker. Docker
Desktop runs inside WSL2, so its `.wslconfig` memory ceiling must exceed guest RAM by at least 4 GB;
otherwise QEMU and Docker have no host-side headroom. Changing `.wslconfig` requires `wsl --shutdown`
and restarting Docker Desktop. CPU affinity in the guest runner is separate from the VM's vCPU count.
