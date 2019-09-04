# PowerToys Setup Project

## Build instructions
  * Install the [WiX Toolset Visual Studio 2019 Extension](https://marketplace.visualstudio.com/items?itemName=RobMensching.WiXToolset).
  * Install the [WiX Toolset build tools](https://wixtoolset.org/releases/) in the development machine.
  * Open `powertoys.sln`, select the "Release" and "x64" configurations and build the `PowerToysSetup` project.
  * The resulting installer will be built to `PowerToysSetup\bin\Release\PowerToysSetup.msi`.
