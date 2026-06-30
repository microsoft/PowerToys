@echo off
rem Builds the PowerScripts Windows 11 context-menu handler DLL (self-contained, no PowerToys deps).
setlocal
set "VCVARS=C:\Program Files\Microsoft Visual Studio\18\Enterprise\VC\Auxiliary\Build\vcvars64.bat"
if not exist "%VCVARS%" (
    echo Could not find vcvars64.bat at "%VCVARS%". Edit build.cmd to point at your VS install.
    exit /b 1
)
call "%VCVARS%" >nul || exit /b 1
cd /d "%~dp0"
cl /nologo /std:c++17 /EHsc /O2 /MT /DUNICODE /D_UNICODE /LD dllmain.cpp ^
    /Fe:PowerToys.PowerScriptsContextMenu.dll ^
    /link /DEF:dll.def shlwapi.lib runtimeobject.lib ole32.lib || exit /b 1
echo Built PowerToys.PowerScriptsContextMenu.dll
endlocal
