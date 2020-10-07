setlocal enableDelayedExpansion

IF NOT DEFINED PTRoot (SET PTRoot=..\..)

rem Publish Settings
SET settingsProfileFolderName=!PTRoot!\src\core\Microsoft.PowerToys.Settings.UI.Runner\Properties\PublishProfiles\
rem Create the publish profile folder if it doesn't exist
IF NOT EXIST !settingsProfileFolderName! (mkdir !settingsProfileFolderName!)
SET settingsProfileFileName=SettingsProfile.pubxml
SET settingsPublishProfile=!settingsProfileFolderName!!settingsProfileFileName!

rem Create the publish profile pubxml
echo ^<?xml version="1.0" encoding="utf-8"?^> > !settingsPublishProfile!
echo ^<^^!-- >> !settingsPublishProfile!
echo https://go.microsoft.com/fwlink/?LinkID=208121.  >> !settingsPublishProfile!
echo --^> >> !settingsPublishProfile!
echo ^<Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003"^> >> !settingsPublishProfile!
echo   ^<PropertyGroup^> >> !settingsPublishProfile!
echo     ^<PublishProtocol^>FileSystem^</PublishProtocol^> >> !settingsPublishProfile!
echo     ^<Configuration^>Release^</Configuration^> >> !settingsPublishProfile!
echo     ^<Platform^>x64^</Platform^> >> !settingsPublishProfile!
echo     ^<TargetFramework^>netcoreapp3.1^</TargetFramework^> >> !settingsPublishProfile!
echo     ^<PublishDir^>..\..\..\x64\Release\SettingsUIRunner^</PublishDir^> >> !settingsPublishProfile!
echo     ^<RuntimeIdentifier^>win-x64^</RuntimeIdentifier^> >> !settingsPublishProfile!
echo     ^<SelfContained^>false^</SelfContained^> >> !settingsPublishProfile!
echo     ^<PublishSingleFile^>False^</PublishSingleFile^> >> !settingsPublishProfile!
echo     ^<PublishReadyToRun^>False^</PublishReadyToRun^> >> !settingsPublishProfile!
echo   ^</PropertyGroup^> >> !settingsPublishProfile!
echo ^</Project^> >> !settingsPublishProfile!

rem In case of Release we should not use Debug CRT in VCRT forwarders
msbuild !PTRoot!\src\core\Microsoft.PowerToys.Settings.UI.Runner\Microsoft.PowerToys.Settings.UI.Runner.csproj -t:Publish -p:Configuration="Release" -p:Platform="x64" -p:AppxBundle=Never -p:VCRTForwarders-IncludeDebugCRT=false -p:PublishProfile=!settingsProfileFileName!

rem Publish Launcher
SET launcherProfileFolderName=!PTRoot!\src\modules\launcher\PowerLauncher\Properties\PublishProfiles\

rem Create the publish profile folder if it doesn't exist
IF NOT EXIST !launcherProfileFolderName! (mkdir !launcherProfileFolderName!)
SET launcherProfileFileName=LauncherProfile.pubxml
SET launcherPublishProfile=!launcherProfileFolderName!!launcherProfileFileName!

rem Create the publish profile pubxml
echo ^<?xml version="1.0" encoding="utf-8"?^> > !launcherPublishProfile!
echo ^<^^!-- >> !launcherPublishProfile!
echo https://go.microsoft.com/fwlink/?LinkID=208121.  >> !launcherPublishProfile!
echo --^> >> !launcherPublishProfile!
echo ^<Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003"^> >> !launcherPublishProfile!
echo   ^<PropertyGroup^> >> !launcherPublishProfile!
echo     ^<PublishProtocol^>FileSystem^</PublishProtocol^> >> !launcherPublishProfile!
echo     ^<Configuration^>Release^</Configuration^> >> !launcherPublishProfile!
echo     ^<Platform^>x64^</Platform^> >> !launcherPublishProfile!
echo     ^<TargetFramework^>netcoreapp3.1^</TargetFramework^> >> !launcherPublishProfile!
echo     ^<PublishDir^>..\..\..\..\x64\Release\modules\launcher^</PublishDir^> >> !launcherPublishProfile!
echo     ^<RuntimeIdentifier^>win-x64^</RuntimeIdentifier^> >> !launcherPublishProfile!
echo     ^<SelfContained^>false^</SelfContained^> >> !launcherPublishProfile!
echo     ^<PublishSingleFile^>False^</PublishSingleFile^> >> !launcherPublishProfile!
echo     ^<PublishReadyToRun^>False^</PublishReadyToRun^> >> !launcherPublishProfile!
echo   ^</PropertyGroup^> >> !launcherPublishProfile!
echo ^</Project^> >> !launcherPublishProfile!

rem In case of Release we should not use Debug CRT in VCRT forwarders
msbuild !PTRoot!\src\modules\launcher\PowerLauncher\PowerLauncher.csproj -t:Publish -p:Configuration="Release" -p:Platform="x64" -p:AppxBundle=Never -p:VCRTForwarders-IncludeDebugCRT=false -p:PublishProfile=!launcherProfileFileName!
