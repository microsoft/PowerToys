# PowerToys Setup Project

## Build instructions
  * Install the [WiX Toolset Visual Studio 2019 Extension](https://marketplace.visualstudio.com/items?itemName=RobMensching.WiXToolset).
  * Install the [WiX Toolset build tools](https://wixtoolset.org/releases/) in the development machine.
  * Open `powertoys.sln`, select the "Release" and "x64" configurations and build the `PowerToysSetup` project.
  * The resulting installer will be built to `PowerToysSetup\bin\Release\PowerToysSetup.msi`.

## Building and installing self-signed PowerToys MSIX package
* Make sure you've built correct `powertoys.sln` configuration
* Add `PowerToysTestKey.pfx` to the [TRCA store](https://docs.microsoft.com/en-us/windows-hardware/drivers/install/trusted-root-certification-authorities-certificate-store)
* Execute `reinstall.ps1` from the devenv powershell:
## Removing all current PowerToys installations
```ps
$name='PowerToys'
Get-AppxPackage -Name $name | select -ExpandProperty "PackageFullName" | Remove-AppxPackage
gwmi win32_product -filter "Name = '$name'" -namespace root/cimv2 | foreach {
  if ($_.uninstall().returnvalue -eq 0) { write-host "Successfully uninstalled $name " }
  else { write-warning "Failed to uninstall $name." }
}
```
