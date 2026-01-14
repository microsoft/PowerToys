Param(
  # Using the default value of 1.7 for winAppSdkVersionNumber and useExperimentalVersion as false
  [Parameter(Mandatory=$False,Position=1)]
  [string]$winAppSdkVersionNumber = "1.8",

  # When the pipeline calls the PS1 file, the passed parameters are converted to string type
  [Parameter(Mandatory=$False,Position=2)]
  [boolean]$useExperimentalVersion = $False,

  # Root folder Path for processing
  [Parameter(Mandatory=$False,Position=3)]
  [string]$rootPath = $(Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)),

  # Root folder Path for processing
  [Parameter(Mandatory=$False,Position=4)]
  [string]$sourceLink = "https://microsoft.pkgs.visualstudio.com/ProjectReunion/_packaging/Project.Reunion.nuget.internal/nuget/v3/index.json"
)



function Read-FileWithEncoding {
    param (
        [string]$Path
    )

    $reader = New-Object System.IO.StreamReader($Path, $true)  # auto-detect encoding
    $content = $reader.ReadToEnd()
    $encoding = $reader.CurrentEncoding
    $reader.Close()

    return [PSCustomObject]@{
        Content  = $content
        Encoding = $encoding
    }
}

function Write-FileWithEncoding {
    param (
        [string]$Path,
        [string]$Content,
        [System.Text.Encoding]$Encoding
    )

    $writer = New-Object System.IO.StreamWriter($Path, $false, $Encoding)
    $writer.Write($Content)
    $writer.Close()
}


function Add-NuGetSourceAndMapping {
    param (
        [xml]$Xml,
        [string]$Key,
        [string]$Value,
        [string[]]$Patterns
    )

    # Ensure packageSources exists
    if (-not $Xml.configuration.packageSources) {
        $Xml.configuration.AppendChild($Xml.CreateElement("packageSources")) | Out-Null
    }
    $sources = $Xml.configuration.packageSources

    # Add/Update Source
    $sourceNode = $sources.SelectSingleNode("add[@key='$Key']")
    if (-not $sourceNode) {
        $sourceNode = $Xml.CreateElement("add")
        $sourceNode.SetAttribute("key", $Key)
        $sources.AppendChild($sourceNode) | Out-Null
    }
    $sourceNode.SetAttribute("value", $Value)

    # Ensure packageSourceMapping exists
    if (-not $Xml.configuration.packageSourceMapping) {
        $Xml.configuration.AppendChild($Xml.CreateElement("packageSourceMapping")) | Out-Null
    }
    $mapping = $Xml.configuration.packageSourceMapping

    # Remove invalid packageSource nodes (missing key or empty key)
    $invalidNodes = $mapping.SelectNodes("packageSource[not(@key) or @key='']")
    if ($invalidNodes) {
        foreach ($node in $invalidNodes) {
            $mapping.RemoveChild($node) | Out-Null
        }
    }

    # Add/Update Mapping Source
    $mappingSource = $mapping.SelectSingleNode("packageSource[@key='$Key']")
    if (-not $mappingSource) {
        $mappingSource = $Xml.CreateElement("packageSource")
        $mappingSource.SetAttribute("key", $Key)
        # Insert at top for priority
        if ($mapping.HasChildNodes) {
            $mapping.InsertBefore($mappingSource, $mapping.FirstChild) | Out-Null
        } else {
            $mapping.AppendChild($mappingSource) | Out-Null
        }
    }
    
    # Double check and force attribute
    if (-not $mappingSource.HasAttribute("key")) {
         $mappingSource.SetAttribute("key", $Key)
    }

    # Update Patterns
    # RemoveAll() removes all child nodes AND attributes, so we must re-set the key afterwards
    $mappingSource.RemoveAll()
    $mappingSource.SetAttribute("key", $Key)

    foreach ($pattern in $Patterns) {
        $pkg = $Xml.CreateElement("package")
        $pkg.SetAttribute("pattern", $pattern)
        $mappingSource.AppendChild($pkg) | Out-Null
    }
}

function Resolve-WinAppSdkSplitDependencies {
    Write-Host "Version $WinAppSDKVersion detected. Resolving split dependencies..."
    $installDir = Join-Path $rootPath "localpackages\output"
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null

    # Create a temporary nuget.config to avoid interference from the repo's config
    $tempConfig = Join-Path $env:TEMP "nuget_$(Get-Random).config"
    Set-Content -Path $tempConfig -Value "<?xml version='1.0' encoding='utf-8'?><configuration><packageSources><clear /><add key='TempSource' value='$sourceLink' /></packageSources></configuration>"

    try {
        # Extract BuildTools version from Directory.Packages.props to ensure we have the required version
        $dirPackagesProps = Join-Path $rootPath "Directory.Packages.props"
        if (Test-Path $dirPackagesProps) {
            $propsContent = Get-Content $dirPackagesProps -Raw
            if ($propsContent -match '<PackageVersion Include="Microsoft.Windows.SDK.BuildTools" Version="([^"]+)"') {
                $buildToolsVersion = $Matches[1]
                Write-Host "Downloading Microsoft.Windows.SDK.BuildTools version $buildToolsVersion..."
                $nugetArgsBuildTools = "install Microsoft.Windows.SDK.BuildTools -Version $buildToolsVersion -ConfigFile $tempConfig -OutputDirectory $installDir -NonInteractive -NoCache"
                Invoke-Expression "nuget $nugetArgsBuildTools" | Out-Null
            }
        }

        # Download package to inspect nuspec and keep it for the build
        $nugetArgs = "install Microsoft.WindowsAppSDK -Version $WinAppSDKVersion -ConfigFile $tempConfig -OutputDirectory $installDir -NonInteractive -NoCache"
        Invoke-Expression "nuget $nugetArgs" | Out-Null

        # Parse dependencies from the installed folders
        # Folder structure is typically {PackageId}.{Version}
        $directories = Get-ChildItem -Path $installDir -Directory
        $allLocalPackages = @()
        foreach ($dir in $directories) {
            # Match any package pattern: PackageId.Version
            if ($dir.Name -match "^(.+?)\.(\d+\..*)$") {
                $pkgId = $Matches[1]
                $pkgVer = $Matches[2]
                $allLocalPackages += $pkgId

                $packageVersions[$pkgId] = $pkgVer
                Write-Host "Found dependency: $pkgId = $pkgVer"
            }
        }

        # Update repo's nuget.config to use localpackages
        $nugetConfig = Join-Path $rootPath "nuget.config"
        $configData = Read-FileWithEncoding -Path $nugetConfig
        [xml]$xml = $configData.Content

        Add-NuGetSourceAndMapping -Xml $xml -Key "localpackages" -Value $installDir -Patterns $allLocalPackages

        $xml.Save($nugetConfig)
        Write-Host "Updated nuget.config with localpackages mapping."
    } catch {
        Write-Warning "Failed to resolve dependencies: $_"
    } finally {
        Remove-Item $tempConfig -Force -ErrorAction SilentlyContinue
    }
}

# Execute nuget list and capture the output
if ($useExperimentalVersion) {
    # The nuget list for experimental versions will cost more time
    # So, we will not use -AllVersions to wast time
    # But it can only get the latest experimental version
    Write-Host "Fetching WindowsAppSDK with experimental versions"
    $nugetOutput = nuget list Microsoft.WindowsAppSDK `
        -Source  $sourceLink `
        -Prerelease
    # Filter versions based on the specified version prefix
    $escapedVersionNumber = [regex]::Escape($winAppSdkVersionNumber)
    $filteredVersions = $nugetOutput | Where-Object { $_ -match "Microsoft.WindowsAppSDK $escapedVersionNumber\." }
    $latestVersions = $filteredVersions
} else {
    Write-Host "Fetching stable WindowsAppSDK versions for $winAppSdkVersionNumber"
    $nugetOutput = nuget list Microsoft.WindowsAppSDK `
        -Source $sourceLink `
        -AllVersions
    # Filter versions based on the specified version prefix
    $escapedVersionNumber = [regex]::Escape($winAppSdkVersionNumber)
    $filteredVersions = $nugetOutput | Where-Object { $_ -match "Microsoft.WindowsAppSDK $escapedVersionNumber\." }
    $latestVersions = $filteredVersions | Sort-Object { [version]($_ -split ' ')[1] } -Descending | Select-Object -First 1
}

Write-Host "Latest versions found: $latestVersions"
# Extract the latest version number from the output
$latestVersion = $latestVersions -split "`n" | `
    Select-String -Pattern 'Microsoft.WindowsAppSDK\s*([0-9]+\.[0-9]+\.[0-9]+-*[a-zA-Z0-9]*)' | `
    ForEach-Object { $_.Matches[0].Groups[1].Value } | `
    Sort-Object -Descending | `
    Select-Object -First 1

if ($latestVersion) {
    $WinAppSDKVersion = $latestVersion
    Write-Host "Extracted version: $WinAppSDKVersion"
    Write-Host "##vso[task.setvariable variable=WinAppSDKVersion]$WinAppSDKVersion"
} else {
    Write-Host "Failed to extract version number from nuget list output"
    exit 1
}

# Resolve dependencies for 1.8+
$packageVersions = @{ "Microsoft.WindowsAppSDK" = $WinAppSDKVersion }

Resolve-WinAppSdkSplitDependencies

# Update Directory.Packages.props file
Get-ChildItem -Path $rootPath -Recurse "Directory.Packages.props" | ForEach-Object {
    $file = Read-FileWithEncoding -Path $_.FullName
    $content = $file.Content
    $isModified = $false
    
    foreach ($pkgId in $packageVersions.Keys) {
        $ver = $packageVersions[$pkgId]
        # Escape dots in package ID for regex
        $pkgIdRegex = $pkgId -replace '\.', '\.'
        
        $newVersionString = "<PackageVersion Include=""$pkgId"" Version=""$ver"" />"
        $oldVersionString = "<PackageVersion Include=""$pkgIdRegex"" Version=""[-.0-9a-zA-Z]*"" />"

        if ($content -match "<PackageVersion Include=""$pkgIdRegex""") {
            # Update existing package
            if ($content -notmatch [regex]::Escape($newVersionString)) {
                $content = $content -replace $oldVersionString, $newVersionString
                $isModified = $true
            }
        }
    }

    if ($isModified) {
        Write-FileWithEncoding -Path $_.FullName -Content $content -Encoding $file.encoding
        Write-Host "Modified " $_.FullName
    }
}
