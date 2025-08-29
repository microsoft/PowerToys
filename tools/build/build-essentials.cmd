@echo off
REM Wrapper to run build-essentials.ps1 from cmd.exe
setlocal enableextensions enabledelayedexpansion
set SCRIPT_DIR=%~dp0
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build-essentials.ps1" %*
exit /b %ERRORLEVEL%
