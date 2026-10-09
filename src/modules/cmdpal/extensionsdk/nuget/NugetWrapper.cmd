@echo OFF
setlocal

if "%VisualStudioVersion%" == "" set VisualStudioVersion=15.0

if not exist %TEMP%\nuget.6.4.0.exe (
    echo Nuget.exe not found in the temp dir, downloading.
    powershell -Command "& { Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/v6.4.0/nuget.exe -outfile $env:TEMP\nuget.6.4.0.exe }"
)

%TEMP%\nuget.6.4.0.exe %*

exit /B %ERRORLEVEL%
