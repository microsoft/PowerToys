cd /D "%~dp0"

REM Just in case CDPx (Legacy) doesn't do it
git submodule update --init --recursive

nuget restore ../PowerToys.sln || exit /b 1
