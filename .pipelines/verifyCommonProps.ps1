[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$sourceDir
)

# scan all csharp project in the source directory
function Get-CSharpProjects {
    param (
        [string]$path
    )

    # Get all .csproj files under the specified path
    return Get-ChildItem -Path $path -Recurse -Filter *.csproj | Select-Object -ExpandProperty FullName
}

# Check if the project file imports 'Common.Dotnet.CsWinRT.props'
function Test-ImportSharedCsWinRTProps {
    param (
        [string]$filePath
    )

    # Load the XML content of the .csproj file
    [xml]$csprojContent = Get-Content -Path $filePath

    
    # Check if the Import element with Project attribute containing 'Common.Dotnet.CsWinRT.props' exists
    return $csprojContent.Project.Import | Where-Object { $null -ne $_.Project -and $_.Project.EndsWith('Common.Dotnet.CsWinRT.props') }
}

# Call the function with the provided source directory
$csprojFilesArray = Get-CSharpProjects -path $sourceDir

$hasInvalidCsProj = $false

# Enumerate the array of file paths and call Validate-ImportSharedCsWinRTProps for each file
foreach ($csprojFile in $csprojFilesArray) {
    # Skip if the file ends with 'TemplateCmdPalExtension.csproj'
    if ($csprojFile -like '*TemplateCmdPalExtension.csproj') {
        continue
    }
    
    # The CmdPal.Core projects use a common shared props file, so skip them
    if ($csprojFile -like '*Microsoft.CmdPal.Core.*.csproj') {
        continue
    }
    if ($csprojFile -like '*Microsoft.CmdPal.Ext.Shell.csproj') {
        continue
    }

    $importExists = Test-ImportSharedCsWinRTProps -filePath $csprojFile
    if (!$importExists) {
        Write-Output "$csprojFile need to import 'Common.Dotnet.CsWinRT.props'."
        $hasInvalidCsProj = $true
    }
}

if ($hasInvalidCsProj) {
    exit 1
}

exit 0