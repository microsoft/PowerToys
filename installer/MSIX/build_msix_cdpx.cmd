cd /D "%~dp0"

call "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64 -winsdk=10.0.19041.0

powershell -file update_appxmanifest_version.ps1 || exit /b 1

call makeappx build /v /overwrite /f PackagingLayout.xml /id "PowerToys-x64" /op bin\ || exit /b 1

setlocal EnableDelayedExpansion
for /f "tokens=3delims=<>" %%i in ('findstr "<Version>" "..\Version.props"') do (
  set MSIXVERSION=%%i
)
setlocal DisableDelayedExpansion
ren "bin\PowerToys-x64.msix" PowerToysSetup-%MSIXVERSION%-x64.msix
