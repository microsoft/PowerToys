cd $PSScriptRoot
cd ..\..
$cwd = Get-Location
$SolutionDir = $cwd,"" -join "\"
cd $SolutionDir
$BuildArgs = "/p:Configuration=Release /p:Platform=x64 /p:BuildProjectReferences=false /p:SolutionDir=$SolutionDir"

$ProjectsToBuild = 
  ".\src\runner\runner.vcxproj",
  ".\src\modules\shortcut_guide\shortcut_guide.vcxproj",
  ".\src\modules\fancyzones\lib\FancyZonesLib.vcxproj",
  ".\src\modules\fancyzones\dll\FancyZonesModule.vcxproj"

$ProjectsToBuild | % {
  Invoke-Expression "msbuild $_ $BuildArgs"
}