cd "%ProgramFiles(x86)%\Microsoft Visual Studio\2019"
ECHO %cd%

SET targetFolder="\"
IF EXIST Preview\NUL (SET targetFolder=Preview)
IF EXIST Enterprise\NUL (SET targetFolder=Enterprise)
IF EXIST Professional\NUL (SET targetFolder=Professional)
IF EXIST Community\NUL (SET targetFolder=Community)

ECHO %targetFolder%

cd "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\"
ECHO %cd%

call vs_installer.exe ^
modify --installpath "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\%targetFolder%" ^
--add Microsoft.VisualStudio.Workload.NativeDesktop ^
--add Microsoft.VisualStudio.Workload.ManagedDesktop ^
--add Microsoft.VisualStudio.Workload.Universal ^
--add Microsoft.VisualStudio.Component.Windows10SDK.17134 ^
--add Microsoft.VisualStudio.ComponentGroup.UWP.VC ^
--add Microsoft.VisualStudio.Component.VC.Runtimes.x86.x64.Spectre ^
--add Microsoft.VisualStudio.Component.VC.ATL.Spectre ^
--quiet ^
--norestart > nul 2> nul


echo COMPLETE


