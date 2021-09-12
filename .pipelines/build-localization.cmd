@echo off
rem This script will fail to run unless you have the appropriate permissions

cd /D "%~dp0"

echo Preparing localization build...

setlocal

rem In this sample, the repo root is identical to the script directory path. Adjust the value of the RepoRoot variable accordingly based on your environment.
rem Again, ensure the RepoRoot variable is set to the real repo root location, otherwise the localization toolset wouldn't work as intended.
rem Note that the resolved %~dp0 ends with \.
set RepoRootWithoutBackslash=%~dp0..
set RepoRoot=%RepoRootWithoutBackslash%\
set OutDir=%RepoRoot%out
set NUGET_PACKAGES=%RepoRoot%packages
set LocalizationXLocPkgVer=2.0.0

echo Running localization build...

set XLocPath=%NUGET_PACKAGES%\Localization.XLoc.%LocalizationXLocPkgVer%
set LocProjectDirectory=%RepoRootWithoutBackslash%

rem Run the localization tool on all LocProject.json files in the src directory and it's subdirectories (directory format must not end with \)
dotnet "%XLocPath%\tools\netcore\Microsoft.Localization.XLoc.dll" /f "%LocProjectDirectory%"

echo Localization build finished with exit code '%errorlevel%'.

rem Move UWP resource files to correct file paths as per file path expected by UWP project
cd %RepoRootWithoutBackslash%
powershell -NonInteractive -executionpolicy Unrestricted ".\tools\localization\move_uwp_resources.ps1"

exit /b %errorlevel%
