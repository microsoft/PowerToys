# PowerToys installer instructions

## MSI installer instructions

1. Install the [WiX Toolset Visual Studio 2019 Extension](https://marketplace.visualstudio.com/items?itemName=RobMensching.WiXToolset).
2. Install the [WiX Toolset build tools](https://wixtoolset.org/releases/) in the development machine.
3. Open `powertoys.sln`, select the "Release" and "x64" configurations and build the `PowerToysSetup` project.
4. The resulting installer will be built to `PowerToysSetup\bin\Release\PowerToysSetup.msi`.

## MSIX installer instructions

### One-time tasks

#### Create and install the self-sign certificate
For the first-time installation, you'll need to generate a self-signed certificate.  The script below will generate and add a cert to your [TRCA store](https://docs.microsoft.com/en-us/windows-hardware/drivers/install/trusted-root-certification-authorities-certificate-store). 
1. Open `Developer PowerShell for VS` as an Admin
2. Navigate to your repo's `installer\MSIX`
3. Run `.\generate_self_sign_cert.ps1`

**Note:** if you delete the folder, you will have to regenerate the key

#### Elevate `Developer PowerShell for VS` permissions due to unsigned file
`msix_reinstall.ps1` is unsigned, you'll need to elevate your prompt.
1. Open `Developer PowerShell for VS` as admin
2. Run `Set-ExecutionPolicy -executionPolicy Unrestricted`

#### Allow Sideloaded apps
In order to install the MSIX package without using the Microsoft Store, sideloading apps needs to be enabled. This can be done by enabling `Developer Options > Sideload apps` or `Developer Options > Developer mode`. 

### Building the MSIX package
1. Make sure you've built the `Release` configuration of `powertoys.sln`
2. Open `Developer PowerShell for VS`
3. Navigate to your repo's `installer\MSIX`
4. Run `.\msix_reinstall.ps1` from the devenv powershell

### What msix_reinstall.ps1 does
`msix_reinstall.ps1` removes the current PowerToys installation, restarts explorer.exe (to update PowerRename shell extension), builds `PowerToys-x64.msix` package, signs it with a PowerToys_TemporaryKey.pfx, and finally installs it.

## Cleanup - Removing all .msi/.msix PowerToys installations
```ps
$name='PowerToys'
Get-AppxPackage -Name $name | select -ExpandProperty "PackageFullName" | Remove-AppxPackage
gwmi win32_product -filter "Name = '$name'" -namespace root/cimv2 | foreach {
  if ($_.uninstall().returnvalue -eq 0) { write-host "Successfully uninstalled $name " }
  else { write-warning "Failed to uninstall $name." }
}
```
