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
- Prefer Windows 11 or Windows 10 Enterprise LTSC for a maintained .NET 10 baseline.

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

Maintain separate volume names for distinct Windows/platform baselines. Restoring or cloning a
stopped volume is faster and more deterministic than accumulating repair scripts.

## Custom Windows media

Dockur accepts a custom ISO URL or a local `/custom.iso` mount. Use custom media only when licensing,
edition, language, servicing, or enterprise policy requires it. Keep OEM provisioning independent of
the media source where possible.

## Resource sizing

Start with 4 vCPUs, 8 GB RAM, and 128 GB disk for broad PowerToys UI-test work. Increase RAM for
WebView-heavy or multi-project suites. CPU affinity in the guest runner limits its descendant process
tree; it is separate from the VM's configured vCPU count.
