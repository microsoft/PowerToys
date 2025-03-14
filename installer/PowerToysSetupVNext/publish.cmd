setlocal enableDelayedExpansion

IF NOT DEFINED PTRoot (SET PTRoot=..\..)

SET PlatformArg=%1
IF NOT DEFINED PlatformArg (SET PlatformArg=x64)
SET VCToolsVersion=!VCToolsVersion!
SET ClearDevCommandPromptEnvVars=false

rem In case of Release we should not use Debug CRT in VCRT forwarders
msbuild !PTRoot!\src\modules\previewpane\MonacoPreviewHandler\MonacoPreviewHandler.csproj -t:Publish -p:Configuration="Release" -p:Platform="!PlatformArg!" -p:AppxBundle=Never -p:PowerToysRoot=!PTRoot! -p:VCRTForwarders-IncludeDebugCRT=false -p:PublishProfile=InstallationPublishProfile.pubxml

msbuild !PTRoot!\src\modules\previewpane\MarkdownPreviewHandler\MarkdownPreviewHandler.csproj -t:Publish -p:Configuration="Release" -p:Platform="!PlatformArg!" -p:AppxBundle=Never -p:PowerToysRoot=!PTRoot! -p:VCRTForwarders-IncludeDebugCRT=false -p:PublishProfile=InstallationPublishProfile.pubxml

msbuild !PTRoot!\src\modules\previewpane\SvgPreviewHandler\SvgPreviewHandler.csproj -t:Publish -p:Configuration="Release" -p:Platform="!PlatformArg!" -p:AppxBundle=Never -p:PowerToysRoot=!PTRoot! -p:VCRTForwarders-IncludeDebugCRT=false -p:PublishProfile=InstallationPublishProfile.pubxml

msbuild !PTRoot!\src\modules\previewpane\SvgThumbnailProvider\SvgThumbnailProvider.csproj -t:Publish -p:Configuration="Release" -p:Platform="!PlatformArg!" -p:AppxBundle=Never -p:PowerToysRoot=!PTRoot! -p:VCRTForwarders-IncludeDebugCRT=false -p:PublishProfile=InstallationPublishProfile.pubxml
