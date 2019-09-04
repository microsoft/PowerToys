cd /D "%~dp0"

dotnet restore ..\PowerToys.sln || exit /b 1
