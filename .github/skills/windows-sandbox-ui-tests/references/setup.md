# Windows Sandbox setup

Use this reference before the first Sandbox run on a machine. Enabling Windows optional features
requires an elevated terminal and can require a reboot; never try to route a UAC prompt through an
agent tool.

## Requirements

Check the current Microsoft documentation before changing host configuration:

- [Install Windows Sandbox](https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-install)
- [Configure Windows Sandbox](https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file)
- [Sample configuration files](https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-sample-configuration)

The host needs a supported Windows edition/build, virtualization enabled in firmware, enough memory
and disk, and the `Containers-DisposableClientVM` optional feature.

## Inspect the host

Run from PowerShell 7:

```pwsh
Get-ComputerInfo -Property HyperVisorPresent,OsName,OsVersion,WindowsProductName
Get-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM
Get-AppxPackage MicrosoftWindows.WindowsSandbox | Select-Object Name,Version,InstallLocation
Get-Command wsb.exe -ErrorAction SilentlyContinue
Get-StartApps | Where-Object AppID -eq 'Microsoft.Windows.Containers.Sandbox'
```

Expected state:

- Optional feature: `Enabled`.
- Store-delivered Sandbox package is registered.
- `wsb.exe` resolves.
- Start AppID `Microsoft.Windows.Containers.Sandbox` exists.

## Enable the feature

Run this yourself from an **elevated** PowerShell window:

```pwsh
Enable-WindowsOptionalFeature `
  -Online `
  -FeatureName Containers-DisposableClientVM `
  -All `
  -NoRestart
```

Reboot when Windows requests it. After reboot, launch **Windows Sandbox** once from Start and verify
that the `WDAGUtilityAccount` desktop appears.

If PowerShell feature cmdlets are unavailable, use the equivalent elevated DISM command:

```cmd
dism.exe /online /Enable-Feature /FeatureName:Containers-DisposableClientVM /All
```

## Store client and CLI

Current Windows builds can provide a Store-delivered client with these commands:

```pwsh
wsb --help
wsb list --raw
wsb share --help
wsb exec --help
wsb stop --help
```

The skill's default controller deliberately launches the registered Start-menu app, waits for the
interactive login, and only then calls `wsb share` and `wsb exec`. This is more reliable than splitting
startup into `wsb start` + `wsb connect`, or mounting a large folder before guest login.

## Security defaults

- Sandbox is disposable, but a writable mapped folder persists guest changes on the host. Map a
  dedicated exchange, never a repository root or user profile.
- Keep the exchange lean and inspect everything copied back from it.
- Disable clipboard/audio/video/printer redirection when using a `.wsb` configuration.
- Networking is enabled in the Start-menu default. Use it only for prerequisites that cannot be
  staged offline, such as the WebView2 bootstrapper; do not pass host credentials into the guest.
- Never store tokens, passwords, certificates, or source credentials in the exchange.

## Optional `.wsb` configuration

For a simple human-driven run, mapped folders and `LogonCommand` are supported:

```xml
<Configuration>
  <VGpu>Enable</VGpu>
  <Networking>Disable</Networking>
  <ClipboardRedirection>Disable</ClipboardRedirection>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>C:\Temp\SandboxExchange</HostFolder>
      <SandboxFolder>C:\SandboxExchange</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell.exe -NoProfile -File C:\SandboxExchange\run-ui-tests.ps1</Command>
  </LogonCommand>
</Configuration>
```

For the unattended agentic loop, prefer Start-menu activation plus dynamic sharing as documented in
[agentic-loop.md](agentic-loop.md). It separates desktop creation from payload attachment and gives
the host an exact Sandbox ID for teardown.