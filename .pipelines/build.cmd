cd /D "%~dp0"
set MSBUILD_EXE="C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
call %MSBUILD_EXE% ../PowerToys.sln || exit /b 1
