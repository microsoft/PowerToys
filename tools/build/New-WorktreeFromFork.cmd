@echo off
setlocal
set SCRIPT_DIR=%~dp0
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%New-WorktreeFromFork.ps1" %*
