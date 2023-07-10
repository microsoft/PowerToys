[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$targetDir
)

# This script runs some simple checks to avoid conflicts between assets from different applications during build time.
$totalFailures = 0

# Verify if the Assets folder contains only sub-folders.
# The purpose is to avoid applications having assets files that might conflict with other applications.
# Applications should be setting their own directory for assets.

$targetAssetsDir = $targetDir + "/Assets"

$nonDirectoryAssetsItems = Get-ChildItem $targetAssetsDir -Attributes !Directory
$directoryAssetsItems = Get-ChildItem $targetAssetsDir -Attributes Directory

if ($directoryAssetsItems -le 0) {
    Write-Host -ForegroundColor Red "No directories detected in " $nonDirectoryAssetsItems ". Are you sure this is the right path?`r`n"
    $totalFailures++;
} elseif ($nonDirectoryAssetsItems -gt 0) {
    Write-Host -ForegroundColor Red "Detected " $nonDirectoryAssetsItems " files in " $targetAssetsDir "`r`n"
    $totalFailures++;
} else {
    Write-Host -ForegroundColor Green "Only directories detected in " $targetAssetsDir "`r`n"
}

# Make sure there's no resources.pri file. Each application should use a different name for their own resources file path.
$resourcesPriFiles = Get-ChildItem $targetDir -Filter resources.pri
if ($resourcesPriFiles -gt 0) {
    Write-Host -ForegroundColor Red "Detected a resources.pri file in " $targetDir "`r`n"
    $totalFailures++;
} else {
    Write-Host -ForegroundColor Green "No resources.pri file detected in " $targetDir "`r`n"
}

# TODO: Verify there are no xbf files in the root, after isolating PowerRename as well. These can cause build racing conditions when packaging pri files.
# Each application should have their XAML files in their own paths to avoid these conflicts.

if ($totalFailures -gt 0) {
    Write-Host -ForegroundColor Red "Found some errors when verifying " $targetDir "`r`n"
    exit 1
}

exit 0
