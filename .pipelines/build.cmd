cd /D "%~dp0"

call msbuild ../PowerToys.sln || exit /b 1
