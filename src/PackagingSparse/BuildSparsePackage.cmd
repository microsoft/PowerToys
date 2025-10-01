@echo off
REM Wrapper to invoke PowerOCR sparse package build script.
REM Pass through all arguments (e.g. Platform=arm64 Configuration=Debug -Clean)

powershell -ExecutionPolicy Bypass -NoLogo -NoProfile -File "%~dp0\BuildSparsePackage.ps1" %*
exit /b %ERRORLEVEL%
