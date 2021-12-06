cd /D "%~dp0"

nuget restore ../PowerToys.sln || exit /b 1

powershell.exe -Command "Invoke-WebRequest -OutFile %tmp%\wdksetup.exe https://go.microsoft.com/fwlink/p/?linkid=2085767"
%tmp%\wdksetup.exe /q

copy "C:\Program Files (x86)\Windows Kits\10\Vsix\VS2019\WDK.vsix" %tmp%\wdkvsix.zip
powershell Expand-Archive %tmp%\wdkvsix.zip -DestinationPath %tmp%\wdkvsix -Force

robocopy /e %tmp%\wdkvsix\$MSBuild\Microsoft\VC\v160  "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Microsoft\VC\v160"  || IF %ERRORLEVEL% LEQ 7  EXIT 0
robocopy /e %tmp%\wdkvsix\$VCTargets "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\IDE\VC\VCTargets" || IF %ERRORLEVEL% LEQ 7  EXIT 0
