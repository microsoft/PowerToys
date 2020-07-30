cd /D "%~dp0"

call "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64 -winsdk=10.0.18362.0
pushd .
cd ..
set SolutionDir=%cd%
popd
SET IsPipeline=1
call msbuild ../src/bootstrapper/bootstrapper.vcxproj /p:Configuration=Release /p:Platform=x64 /p:CIBuild=true /p:BuildProjectReferences=false /p:SolutionDir=%SolutionDir%\ || exit /b 1

