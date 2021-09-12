cd /D "%~dp0"

call nuget.exe restore -PackagesDirectory . packages.config || exit /b 1

move /Y "Microsoft.PowerToys.Telemetry.2.0.0\build\include\TraceLoggingDefines.h" "..\src\common\Telemetry\TraceLoggingDefines.h" || exit /b 1
move /Y "Microsoft.PowerToys.Telemetry.2.0.0\build\include\TelemetryBase.cs" "..\src\common\Telemetry\TelemetryBase.cs" || exit /b 1
