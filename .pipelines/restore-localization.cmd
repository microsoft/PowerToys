@echo off

cd /D "%~dp0"

echo Installing nuget packages

setlocal

rem In this sample, the repo root is identical to the script directory path. Adjust the value of the RepoRoot variable accordingly based on your environment.
rem Again, ensure the RepoRoot variable is set to the real repo root location, otherwise the localization toolset wouldn't work as intended.
rem Note that the resolved %~dp0 ends with \.
set RepoRoot=%~dp0..\
set OutDir=%RepoRoot%out
set NUGET_PACKAGES=%RepoRoot%packages
set LocalizationXLocPkgVer=2.0.0

nuget install Localization.XLoc -Version %LocalizationXLocPkgVer% -OutputDirectory "%NUGET_PACKAGES%" -NonInteractive -Verbosity detailed
if "%errorlevel%" neq "0" (
    exit /b %errorlevel%
)

nuget install LSBuild.XLoc -OutputDirectory "%NUGET_PACKAGES%" -NonInteractive -Verbosity detailed
if "%errorlevel%" neq "0" (
    exit /b %errorlevel%
)

nuget install Localization.Languages -OutputDirectory "%NUGET_PACKAGES%" -NonInteractive -Verbosity detailed
if "%errorlevel%" neq "0" (
    exit /b %errorlevel%
)

exit /b %errorlevel%
