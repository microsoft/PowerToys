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
    $csprojFiles = Get-ChildItem -Path $path -Recurse -Filter *.csproj

    # Initialize an array to hold the file paths
    $csprojArray = @()

    foreach ($file in $csprojFiles) {
        # Add the full path of each .csproj file to the array
        $csprojArray += $file.FullName
    }

    # Return the array
    return $csprojArray
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