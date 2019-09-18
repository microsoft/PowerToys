cd /D "%~dp0"
set MSBUILD_EXE="C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
REM commented out until new container is finished
REM call %MSBUILD_EXE% ../installer/PowerToysSetup.sln /p:Configuration=Release /p:Platform=x64 || exit /b 1
