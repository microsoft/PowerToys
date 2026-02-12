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
  [string]$sourceLink = "https://microsoft.pkgs.visualstudio.com/ProjectReunion/_packaging/Project.Reunion.nuget.internal/nuget/v3/index.json",

  # Use Azure Pipeline artifact as source for metapackage
  [Parameter(Mandatory=$False,Position=5)]
  [boolean]$useArtifactSource = $False,

  # Azure DevOps organization URL
  [Parameter(Mandatory=$False,Position=6)]
  [string]$azureDevOpsOrg = "https://dev.azure.com/microsoft",

  # Azure DevOps project name
  [Parameter(Mandatory=$False,Position=7)]
  [string]$azureDevOpsProject = "ProjectReunion",

  # Pipeline build ID (or "latest" for latest build)
  [Parameter(Mandatory=$False,Position=8)]
  [string]$buildId = "",

  # Artifact name containing the NuGet packages
  [Parameter(Mandatory=$False,Position=9)]
  [string]$artifactName = "WindowsAppSDK_Nuget_And_MSIX",

  # Metapackage name to look for in artifact
  [Parameter(Mandatory=$False,Position=10)]
  [string]$metaPackageName = "Microsoft.WindowsAppSDK"
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

function Download-ArtifactFromPipeline {
    param (
        [string]$Organization,
        [string]$Project,
        [string]$BuildId,
        [string]$ArtifactName,
        [string]$OutputDir
    )

    Write-Host "Downloading artifact '$ArtifactName' from build $BuildId..."
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

    try {
        # Authenticate with Azure DevOps using System Access Token (if available)
        if ($env:SYSTEM_ACCESSTOKEN) {
            Write-Host "Authenticating with Azure DevOps using System Access Token..."
            $env:AZURE_DEVOPS_EXT_PAT = $env:SYSTEM_ACCESSTOKEN
        } else {
            Write-Host "No SYSTEM_ACCESSTOKEN found, assuming az CLI is already authenticated..."
        }

        # Use az CLI to download artifact
        $azArgs = "pipelines runs artifact download --organization $Organization --project $Project --run-id $BuildId --artifact-name `"$ArtifactName`" --path `"$OutputDir`""
        Invoke-Expression "az $azArgs"

        if ($LASTEXITCODE -eq 0) {
            Write-Host "Successfully downloaded artifact to $OutputDir"
            return $true
        } else {
            Write-Warning "Failed to download artifact. Exit code: $LASTEXITCODE"
            return $false
        }
    } catch {
        Write-Warning "Error downloading artifact: $_"
        return $false
    }
}

function Get-NuspecDependencies {
    param (
        [string]$NupkgPath,
        [string]$TargetFramework = ""
    )

    $tempDir = Join-Path $env:TEMP "nuspec_parse_$(Get-Random)"

    try {
        # Extract .nupkg (it's a zip file)
        # Workaround: Expand-Archive may not recognize .nupkg extension, so copy to .zip first
        $tempZip = Join-Path $env:TEMP "temp_$(Get-Random).zip"
        Copy-Item $NupkgPath -Destination $tempZip -Force
        Expand-Archive -Path $tempZip -DestinationPath $tempDir -Force
        Remove-Item $tempZip -Force -ErrorAction SilentlyContinue

        # Find .nuspec file
        $nuspecFile = Get-ChildItem -Path $tempDir -Filter "*.nuspec" -Recurse | Select-Object -First 1

        if (-not $nuspecFile) {
            Write-Warning "No .nuspec file found in $NupkgPath"
            return @{}
        }

        [xml]$nuspec = Get-Content $nuspecFile.FullName

        # Extract package info
        $packageId = $nuspec.package.metadata.id
        $version = $nuspec.package.metadata.version
        Write-Host "Parsing $packageId version $version"

        # Parse dependencies
        $dependencies = @{}
        $depGroups = $nuspec.package.metadata.dependencies.group

        if ($depGroups) {
            # Dependencies are grouped by target framework
            foreach ($group in $depGroups) {
                $fx = $group.targetFramework
                Write-Host "  Target Framework: $fx"

                foreach ($dep in $group.dependency) {
                    $depId = $dep.id
                    $depVer = $dep.version
                    # Remove version range brackets if present (e.g., "[2.0.0]" -> "2.0.0")
                    $depVer = $depVer -replace '[\[\]]', ''
                    $dependencies[$depId] = $depVer
                    Write-Host "    - $depId : $depVer"
                }
            }
        } else {
            # No grouping, direct dependencies
            $deps = $nuspec.package.metadata.dependencies.dependency
            if ($deps) {
                foreach ($dep in $deps) {
                    $depId = $dep.id
                    $depVer = $dep.version
                    $depVer = $depVer -replace '[\[\]]', ''
                    $dependencies[$depId] = $depVer
                    Write-Host "  - $depId : $depVer"
                }
            }
        }

        return $dependencies
    }
    catch {
        Write-Warning "Failed to parse nuspec: $_"
        return @{}
    }
    finally {
        Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Resolve-ArtifactBasedDependencies {
    param (
        [string]$ArtifactDir,
        [string]$MetaPackageName,
        [string]$SourceUrl,
        [string]$OutputDir
    )

    Write-Host "Resolving dependencies from artifact-based metapackage..."
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

    # Find the metapackage in artifact
    $metaNupkg = Get-ChildItem -Path $ArtifactDir -Recurse -Filter "$MetaPackageName.*.nupkg" |
                 Where-Object { $_.Name -notmatch "Runtime" } |
                 Select-Object -First 1

    if (-not $metaNupkg) {
        Write-Warning "Metapackage $MetaPackageName not found in artifact"
        return @{}
    }

    # Extract version from filename
    if ($metaNupkg.Name -match "$MetaPackageName\.(.+)\.nupkg") {
        $metaVersion = $Matches[1]
        Write-Host "Found metapackage: $MetaPackageName version $metaVersion"
    } else {
        Write-Warning "Could not extract version from $($metaNupkg.Name)"
        return @{}
    }

    # Parse dependencies from metapackage
    $dependencies = Get-NuspecDependencies -NupkgPath $metaNupkg.FullName

    # Copy metapackage to output directory
    Copy-Item $metaNupkg.FullName -Destination $OutputDir -Force
    Write-Host "Copied metapackage to $OutputDir"

    # Copy Runtime package from artifact (it's not in feed)
    $runtimeNupkg = Get-ChildItem -Path $ArtifactDir -Recurse -Filter "$MetaPackageName.Runtime.*.nupkg" | Select-Object -First 1
    if ($runtimeNupkg) {
        Copy-Item $runtimeNupkg.FullName -Destination $OutputDir -Force
        Write-Host "Copied Runtime package to $OutputDir"
    }

    # Prepare package versions hashtable
    $packageVersions = @{ $MetaPackageName = $metaVersion }

    # Download other dependencies from feed (excluding Runtime as it's already copied)
    # Create temp nuget.config that includes both local packages and remote feed
    # This allows NuGet to find packages already copied from artifact
    $tempConfig = Join-Path $env:TEMP "nuget_artifact_$(Get-Random).config"
    $tempConfigContent = @"
<?xml version='1.0' encoding='utf-8'?>
<configuration>
  <packageSources>
    <clear />
    <add key='LocalPackages' value='$OutputDir' />
    <add key='RemoteFeed' value='$SourceUrl' />
  </packageSources>
</configuration>
"@
    Set-Content -Path $tempConfig -Value $tempConfigContent

    try {
        foreach ($depId in $dependencies.Keys) {
            # Skip Runtime as it's already copied from artifact
            if ($depId -like "*Runtime*") {
                $packageVersions[$depId] = $dependencies[$depId]
                Write-Host "Skipping $depId (already in artifact)"
                continue
            }

            $depVersion = $dependencies[$depId]
            Write-Host "Downloading dependency: $depId version $depVersion from feed..."

            $nugetArgs = "install $depId -Version $depVersion -ConfigFile `"$tempConfig`" -OutputDirectory `"$OutputDir`" -NonInteractive -NoCache"
            Invoke-Expression "nuget $nugetArgs"

            if ($LASTEXITCODE -eq 0) {
                $packageVersions[$depId] = $depVersion
                Write-Host "  Successfully downloaded $depId"
            } else {
                Write-Warning "  Failed to download $depId version $depVersion"
            }
        }
    }
    finally {
        Remove-Item $tempConfig -Force -ErrorAction SilentlyContinue
    }

    # Parse all downloaded packages to get actual versions
    $directories = Get-ChildItem -Path $OutputDir -Directory
    $allLocalPackages = @()

    # Add metapackage and runtime to the list (they are .nupkg files, not directories)
    $allLocalPackages += $MetaPackageName
    if ($packageVersions.ContainsKey("$MetaPackageName.Runtime")) {
        $allLocalPackages += "$MetaPackageName.Runtime"
    }

    foreach ($dir in $directories) {
        if ($dir.Name -match "^(.+?)\.(\d+\..*)$") {
            $pkgId = $Matches[1]
            $pkgVer = $Matches[2]
            $allLocalPackages += $pkgId
            # Don't overwrite metapackage version that was set earlier
            if (-not $packageVersions.ContainsKey($pkgId)) {
                $packageVersions[$pkgId] = $pkgVer
            }
        }
    }

    # Update nuget.config dynamically during pipeline execution
    # This modification is temporary and won't be committed back to the repo
    $nugetConfig = Join-Path $rootPath "nuget.config"
    $configData = Read-FileWithEncoding -Path $nugetConfig
    [xml]$xml = $configData.Content

    Add-NuGetSourceAndMapping -Xml $xml -Key "localpackages" -Value $OutputDir -Patterns $allLocalPackages

    $xml.Save($nugetConfig)
    Write-Host "Updated nuget.config with localpackages mapping (temporary, for pipeline execution only)."

    return $packageVersions
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

# Main logic: choose between artifact-based or feed-based approach
if ($useArtifactSource) {
    Write-Host "=== Using Artifact-Based Source ===" -ForegroundColor Cyan
    Write-Host "Organization: $azureDevOpsOrg"
    Write-Host "Project: $azureDevOpsProject"
    Write-Host "Build ID: $buildId"
    Write-Host "Artifact: $artifactName"

    if ([string]::IsNullOrEmpty($buildId)) {
        Write-Host "Error: buildId parameter is required when using artifact source"
        exit 1
    }

    # Download artifact
    $artifactDir = Join-Path $rootPath "localpackages\artifact"
    $downloadSuccess = Download-ArtifactFromPipeline `
        -Organization $azureDevOpsOrg `
        -Project $azureDevOpsProject `
        -BuildId $buildId `
        -ArtifactName $artifactName `
        -OutputDir $artifactDir

    if (-not $downloadSuccess) {
        Write-Host "Failed to download artifact"
        exit 1
    }

    # Resolve dependencies from artifact
    $installDir = Join-Path $rootPath "localpackages\output"
    $packageVersions = Resolve-ArtifactBasedDependencies `
        -ArtifactDir $artifactDir `
        -MetaPackageName $metaPackageName `
        -SourceUrl $sourceLink `
        -OutputDir $installDir

    if ($packageVersions.Count -eq 0) {
        Write-Host "Failed to resolve dependencies from artifact"
        exit 1
    }

    # Extract WinAppSDK version
    $WinAppSDKVersion = $packageVersions[$metaPackageName]
    Write-Host "WinAppSDK Version: $WinAppSDKVersion"
    Write-Host "##vso[task.setvariable variable=WinAppSDKVersion]$WinAppSDKVersion"

} else {
    Write-Host "=== Using Feed-Based Source ===" -ForegroundColor Cyan

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
}

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
