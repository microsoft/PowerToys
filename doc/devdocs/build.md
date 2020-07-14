# Building PowerToys project

## Prerequisites
- Visual studio 2019
- Desktop development with C++
- .Net desktop development
- Universal Windows Platform development
- MSVC v142 - VS 2019 C++ x64/x86 build tools (v14.26)
- MSVC v142 - VS 2019 C++ x64/x86 Spectre-mitigated libs (v14.26)
- C++ ATL for latest v142 build tools (x86 & x64)
- C++ ATL for latest v142 build tools with Spectre Mitigations (x86 & x64)
- C++ MFC for latest v142 build tools with Spectre Mitigations (x86 & x64)
- C++ v14.24 ATL for v142 build tools with Spectre Mitigations (x86 & x64)
- C++ MFC ATL for v142 build tools with Spectre Mitigations (x86 & x64)
- Windows 10 SDK(10.0.19441.0 and 10.17134.0)

- WDK 1903(install with vs extension) https://docs.microsoft.com/en-us/windows-hardware/drivers/other-wdk-downloads
- WiX Toolset v3 Schemas for Visual Studio 
- WiX Toolset v4 Schemas for Visual Studio
- WiX Toolset Visual Studio 2019 extension https://wixtoolset.org/releases/


## Building 
- Build PowerToys.sln release
- Build installer/PowerToysSetup.sln as release

## Running
Some feature eg. VideoConference requires installation to runnable
- after building solution run installer/PowerToysSetup/x64/PowerToysSetup-x.xx.x-x64.msi

## Verification
- Check in device manager that under cameras there is "PowerToys VideoConference"
