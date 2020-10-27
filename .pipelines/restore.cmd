cd /D "%~dp0"

nuget restore ../PowerToys.sln || exit /b 1
