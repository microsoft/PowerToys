# $WixToolsetMajorMinorVersion = "5.0"
# $MSBuildUserExtensionsPath = "$env:LOCALAPPDATA\Microsoft\MSBuild"
# $TargetPath = Join-Path $MSBuildUserExtensionsPath "WixToolset\$WixToolsetMajorMinorVersion\Imports\WixToolset.props\ImportBefore"

# if (Test-Path $TargetPath) {
#     Write-Host "✅ Path exists: $TargetPath"
# } else {
#     Write-Host "❌ Path does NOT exist: $TargetPath"
# }


Write-Host $(ImportUserLocationsByWildcardBeforeWixToolsetProps)' != 'false