<#
.SYNOPSIS
    Validates that all C# projects in the repository import a required shared props file.

.DESCRIPTION
    Recursively searches for .csproj files under the given root directory and checks that
    each one imports either Common.Dotnet.CsWinRT.props or Common.Dotnet.props. These
    shared MSBuild props files enforce consistent build settings across all C# projects.

.PARAMETER sourceDir
    Root directory to recursively search for .csproj files.

.OUTPUTS
    Writes the path of any non-conforming or malformed .csproj file to the output stream.
    Exits with code 1 if any such files are found, and with code 0 otherwise.
#>

[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$sourceDir
)

$hasInvalidCsProj = $false

$csprojFiles = [System.IO.Directory]::EnumerateFiles($sourceDir, '*.csproj', [System.IO.SearchOption]::AllDirectories)

foreach ($csprojFile in $csprojFiles) {
    $filename = [System.IO.Path]::GetFileName($csprojFile)

    # Skip the CmdPal extension template project, which doesn't require the shared props.
    if ($filename -eq 'TemplateCmdPalExtension.csproj') {
        continue
    }

    $importExists = $false

    try {
        $xml = New-Object System.Xml.XmlDocument

        $xml.Load($csprojFile)

        # The '*' wildcard matches Import elements regardless of XML namespace.
        foreach ($importNode in $xml.GetElementsByTagName('Import', '*')) {
            if ($null -ne $importNode.Project) {
                $importFilename = [System.IO.Path]::GetFileName($importNode.Project)

                if ($importFilename -eq 'Common.Dotnet.CsWinRT.props' -or $importFilename -eq 'Common.Dotnet.props') {
                    $importExists = $true
                    break
                }
            }
        }
    }
    catch {
        Write-Output "Error parsing ${csprojFile}: $_"
        $hasInvalidCsProj = $true
        continue
    }

    if (-not $importExists) {
        Write-Output "$csprojFile needs to import 'Common.Dotnet.CsWinRT.props' or 'Common.Dotnet.props'."
        $hasInvalidCsProj = $true
    }
}

if ($hasInvalidCsProj) {
    exit 1
}

exit 0
