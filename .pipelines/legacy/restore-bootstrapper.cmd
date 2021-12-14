cd /D "%~dp0"

nuget restore ../installer/PowerToysBootstrapper/PowerToysBootstrapper.sln || exit /b 1
