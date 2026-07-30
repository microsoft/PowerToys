@echo off
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File C:\OEM\Provision-UiTestVm.ps1 > C:\OEM\Provision-UiTestVm.log 2>&1
exit /b %ERRORLEVEL%
