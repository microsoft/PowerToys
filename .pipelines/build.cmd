cd /D "%~dp0"
set MSBUILD_EXE="C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
call %MSBUILD_EXE% ../PowerToys.sln /p:Configuration=Release /p:Platform=x64 || exit /b 1

dir
cd ..
dir
cd x64
dir
cd Release
dir
