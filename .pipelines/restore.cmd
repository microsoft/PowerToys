cd /D "%~dp0"

powershell.exe -Command "Invoke-WebRequest -OutFile %tmp%\wdksetup.exe https://go.microsoft.com/fwlink/p/?linkid=2085767"
%tmp%\wdksetup.exe /q

nuget restore ../PowerToys.sln || exit /b 1