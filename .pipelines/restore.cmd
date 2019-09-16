cd /D "%~dp0"

dotnet restore ..\src\modules\fancyzones\editor\FancyZonesEditor\FancyZonesEditor.csproj || exit /b 1
