cd /D "%~dp0"

set PROJECT="..\src\modules\fancyzones\editor\FancyZonesEditor\FancyZonesEditor.csproj"
set TELEMETRY_PKG="Microsoft.PowerToys.Telemetry"

dotnet add %PROJECT% package %TELEMETRY_PKG%
