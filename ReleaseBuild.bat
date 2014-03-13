@echo off

set DOTNET_HOME=%windir%\Microsoft.NET\Framework64\v4.0.30319
set ReleaseFolder=Release

if exist %ReleaseFolder% RMDIR %ReleaseFolder% /S /Q

%DOTNET_HOME%\MSBUILD.exe wox.sln /p:Configuration=Release

REM default plugins
%DOTNET_HOME%\MSBUILD.exe Plugins\Wox.Plugin.PluginManagement\Wox.Plugin.PluginManagement.Release.csproj

copy wox\config.json %ReleaseFolder%\cofnig.json
for %%f in (%ReleaseFolder%\*.xml) do (
  del %%f
)
for %%f in (%ReleaseFolder%\*.pdb) do (
  del %%f
)
