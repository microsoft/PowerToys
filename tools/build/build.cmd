@echo off
REM Wrapper to run the PowerShell build script from cmd.exe
set SCRIPT_DIR=%~dp0
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build.ps1" %*
exit /b %ERRORLEVEL%
