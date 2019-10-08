cd /D "%~dp0"

nuget restore ../installer/PowerToysSetup.sln || exit /b 1
