# PowerToys cleanup

`Uninstall-PowerToys.ps1` removes the current user's per-user PowerToys
installation and the machine-wide installation. Use it to recover from a
mixed-scope state where each PowerToys bootstrapper tells you to uninstall the
other installation first.

The script:

1. Finds PowerToys MSI products by their known upgrade codes.
1. Uninstalls the MSI products directly, bypassing the conflicting bootstrapper
   conditions.
1. Runs the cached WiX bootstrappers to remove their registrations and caches.
1. Removes known installation-directory and install-scope registry remnants.

PowerToys settings, logs, and update downloads are preserved unless
`-RemoveSettings` is specified. The shared WebView2 runtime and administrator
policies are never removed.

## Usage

Open 64-bit PowerShell as administrator, change to this directory, and preview
the detected installations:

```powershell
.\Uninstall-PowerToys.ps1 -WhatIf
```

Remove all detected PowerToys installations:

```powershell
.\Uninstall-PowerToys.ps1
```

Also remove the current user's settings, logs, and update cache:

```powershell
.\Uninstall-PowerToys.ps1 -RemoveSettings
```

The script displays one confirmation prompt. For unattended support scenarios,
append `-Confirm:$false`.

Per-user MSI products can only be removed from the Windows profile that owns
them. If other profiles have per-user PowerToys installations, sign in to each
affected profile and run the script again.

MSI logs are written to a timestamped `PowerToys-Cleanup-*` directory under
`%TEMP%`. A failed run preserves those logs and reports any bootstrapper whose
cache is missing or invalid. For safety, the script only executes cached MSI
packages and bootstrappers with a valid Microsoft signature and PowerToys
identity.
