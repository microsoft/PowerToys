cd /D "%~dp0"

call "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64 -winsdk=10.0.18362.0
pushd .
cd ..
set SolutionDir=%cd%
popd
SET IsPipeline=1
call msbuild ../tools/BugReportTool/BugReportTool.sln /p:Configuration=Release /p:Platform=x64 /p:CIBuild=true || exit /b 1
call msbuild ../tools/WebcamReportTool/WebcamReportTool.sln /p:Configuration=Release /p:Platform=x64 /p:CIBuild=true || exit /b 1

