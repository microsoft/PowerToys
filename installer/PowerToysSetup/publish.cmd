setlocal enableDelayedExpansion

IF NOT DEFINED PTRoot (SET PTRoot=..\..)

SET PlatformArg=%1
IF NOT DEFINED PlatformArg (SET PlatformArg=x64)

rem In case of Release we should not use Debug CRT in VCRT forwarders
msbuild !PTRoot!\src\settings-ui\Settings.UI\PowerToys.Settings.csproj -t:Publish -p:Configuration="Release" -p:Platform="!PlatformArg!" -p:PowerToysRoot=!PTRoot! -p:AppxBundle=Never -p:VCRTForwarders-IncludeDebugCRT=false -p:PublishProfile=InstallationPublishProfile.pubxml

rem In case of Release we should not use Debug CRT in VCRT forwarders
msbuild !PTRoot!\src\modules\launcher\PowerLauncher\PowerLauncher.csproj -t:Publish -p:Configuration="Release" -p:Platform="!PlatformArg!" -p:AppxBundle=Never -p:PowerToysRoot=!PTRoot! -p:VCRTForwarders-IncludeDebugCRT=false -p:PublishProfile=InstallationPublishProfile.pubxml

msbuild !PTRoot!\src\modules\previewpane\MonacoPreviewHandler\MonacoPreviewHandler.csproj -t:Publish -p:Configuration="Release" -p:Platform="!PlatformArg!" -p:AppxBundle=Never -p:PowerToysRoot=!PTRoot! -p:VCRTForwarders-IncludeDebugCRT=false -p:PublishProfile=InstallationPublishProfile.pubxml

msbuild !PTRoot!\src\modules\previewpane\MarkdownPreviewHandler\MarkdownPreviewHandler.csproj -t:Publish -p:Configuration="Release" -p:Platform="!PlatformArg!" -p:AppxBundle=Never -p:PowerToysRoot=!PTRoot! -p:VCRTForwarders-IncludeDebugCRT=false -p:PublishProfile=InstallationPublishProfile.pubxml

msbuild !PTRoot!\src\modules\previewpane\SvgPreviewHandler\SvgPreviewHandler.csproj -t:Publish -p:Configuration="Release" -p:Platform="!PlatformArg!" -p:AppxBundle=Never -p:PowerToysRoot=!PTRoot! -p:VCRTForwarders-IncludeDebugCRT=false -p:PublishProfile=InstallationPublishProfile.pubxml

msbuild !PTRoot!\src\modules\previewpane\SvgThumbnailProvider\SvgThumbnailProvider.csproj -t:Publish -p:Configuration="Release" -p:Platform="!PlatformArg!" -p:AppxBundle=Never -p:PowerToysRoot=!PTRoot! -p:VCRTForwarders-IncludeDebugCRT=false -p:PublishProfile=InstallationPublishProfile.pubxml

msbuild !PTRoot!\src\modules\MeasureTool\MeasureToolUI\MeasureToolUI.csproj -t:Publish -p:Configuration="Release" -p:Platform="!PlatformArg!" -p:AppxBundle=Never -p:PowerToysRoot=!PTRoot! -p:VCRTForwarders-IncludeDebugCRT=false -p:PublishProfile=InstallationPublishProfile.pubxml

msbuild !PTRoot!\src\modules\FileLocksmith\FileLocksmithUI\FileLocksmithUI.csproj -t:Publish -p:Configuration="Release" -p:Platform="!PlatformArg!" -p:AppxBundle=Never -p:PowerToysRoot=!PTRoot! -p:VCRTForwarders-IncludeDebugCRT=false -p:PublishProfile=InstallationPublishProfile.pubxml
