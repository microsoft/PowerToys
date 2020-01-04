# PowerToys Setup Project

## Build instructions
  * Install the [WiX Toolset Visual Studio 2019 Extension](https://marketplace.visualstudio.com/items?itemName=RobMensching.WiXToolset).
  * Install the [WiX Toolset build tools](https://wixtoolset.org/releases/) in the development machine.
  * Open `powertoys.sln`, select the "Release" and "x64" configurations and build the `PowerToysSetup` project.
  * The resulting installer will be built to `PowerToysSetup\bin\Release\PowerToysSetup.msi`.

## Building and installing self-signed PowerToys MSIX package
For the first-time installation, you should generate a self-signed certificate and add it to the [TRCA store](https://docs.microsoft.com/en-us/windows-hardware/drivers/install/trusted-root-certification-authorities-certificate-store). That could be done by simply running ` 
generate_self_sign_cert.ps1` from a powershell admin. After that:

* Make sure you've built the `Release` configuration of `powertoys.sln`
* Launch `msix_reinstall.ps1` from the devenv powershell

`msix_reinstall.ps1` removes the current PowerToys installation, restarts explorer.exe (to update PowerRename shell extension), builds `PowerToys-x64.msix` package, signs it with a PowerToys_TemporaryKey.pfx, and finally installs it.
## Removing all .msi/.msix PowerToys installations
```ps
$name='PowerToys'
Get-AppxPackage -Name $name | select -ExpandProperty "PackageFullName" | Remove-AppxPackage
gwmi win32_product -filter "Name = '$name'" -namespace root/cimv2 | foreach {
  if ($_.uninstall().returnvalue -eq 0) { write-host "Successfully uninstalled $name " }
  else { write-warning "Failed to uninstall $name." }
}
```
