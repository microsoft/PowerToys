cd /D "%~dp0"

call "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64 -winsdk=10.0.18362.0
SET IsPipeline=1
call msbuild ../installer/PowerToysSetup.sln /target:PowerToysInstaller /p:Configuration=Release /p:Platform=x64 /p:CIBuild=true || exit /b 1
