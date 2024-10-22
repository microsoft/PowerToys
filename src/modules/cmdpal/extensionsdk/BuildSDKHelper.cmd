@echo off

powershell -ExecutionPolicy Unrestricted -NoLogo -NoProfile -File %~dp0\BuildSDKHelper.ps1 %*

exit /b %ERRORLEVEL%