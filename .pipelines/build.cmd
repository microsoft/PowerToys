cd /D "%~dp0"
REM set MSBUILD_EXE="C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
call "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\Tools\VsDevCmd.bat -arch=amd64 -host_arch=amd64 -winsdk=10.0.16299.0"
call msbuild ../PowerToys.sln /p:Configuration=Release /p:Platform=x64 || exit /b 1
