cd /D "%~dp0"
dotnet build --no-restore ..\PowerToys.sln || exit /b 1
