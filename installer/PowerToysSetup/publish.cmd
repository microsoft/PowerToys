setlocal enableDelayedExpansion

IF NOT DEFINED PTRoot (SET PTRoot=..\..)

rem In case of Release we should not use Debug CRT in VCRT forwarders
msbuild !PTRoot!\src\settings-ui\Settings.UI\PowerToys.Settings.csproj -t:Publish -p:Configuration="Release" -p:Platform="x64" -p:PowerToysRoot=!PTRoot! -p:AppxBundle=Never -p:VCRTForwarders-IncludeDebugCRT=false -p:PublishProfile=InstallationPublishProfile.pubxml

rem In case of Release we should not use Debug CRT in VCRT forwarders
msbuild !PTRoot!\src\modules\launcher\PowerLauncher\PowerLauncher.csproj -t:Publish -p:Configuration="Release" -p:Platform="x64" -p:AppxBundle=Never -p:PowerToysRoot=!PTRoot! -p:VCRTForwarders-IncludeDebugCRT=false -p:PublishProfile=InstallationPublishProfile.pubxml
