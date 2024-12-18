Param(
  # Using the default value of 1.6 for winAppSdkVersionNumber and useExperimentalVersion as false
  [Parameter(Mandatory=$False,Position=1)]
  [string]$winAppSdkVersionNumber = "1.6",

  # When the pipeline calls the PS1 file, the passed parameters are converted to string type
  [Parameter(Mandatory=$False,Position=2)]
  [boolean]$useExperimentalVersion = $False
)

function Update-NugetConfig {
    param (
        [string]$filePath = "nuget.config"
    )

    Write-Host "Updating nuget.config file"
    [xml]$xml = Get-Content -Path $filePath

    # Add localpackages source into nuget.config
    $packageSourcesNode = $xml.configuration.packageSources
    $addNode = $xml.CreateElement("add")
    $addNode.SetAttribute("key", "localpackages")
    $addNode.SetAttribute("value", "localpackages")
    $packageSourcesNode.AppendChild($addNode) | Out-Null

    # Remove <packageSourceMapping> tag and its content
    $packageSourceMappingNode = $xml.configuration.packageSourceMapping
    if ($packageSourceMappingNode) {
        $xml.configuration.RemoveChild($packageSourceMappingNode) | Out-Null
    }

    # print nuget.config after modification
    $xml.OuterXml
    # Save the modified nuget.config file
    $xml.Save($filePath)
}

$sourceLink = "https://microsoft.pkgs.visualstudio.com/ProjectReunion/_packaging/Project.Reunion.nuget.internal/nuget/v3/index.json"

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

# Update packages.config files
Get-ChildItem -Recurse packages.config | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match 'package id="Microsoft.WindowsAppSDK"') {
        $newVersionString = 'package id="Microsoft.WindowsAppSDK" version="' + $WinAppSDKVersion + '"'
        $oldVersionString = 'package id="Microsoft.WindowsAppSDK" version="[-.0-9a-zA-Z]*"'
        $content = $content -replace $oldVersionString, $newVersionString
        Set-Content -Path $_.FullName -Value $content
        Write-Host "Modified " $_.FullName 
    }
}

# Update Directory.Packages.props file
$propsFile = "Directory.Packages.props"
if (Test-Path $propsFile) {
    $content = Get-Content $propsFile -Raw
    if ($content -match '<PackageVersion Include="Microsoft.WindowsAppSDK"') {
        $newVersionString = '<PackageVersion Include="Microsoft.WindowsAppSDK" Version="' + $WinAppSDKVersion + '" />'
        $oldVersionString = '<PackageVersion Include="Microsoft.WindowsAppSDK" Version="[-.0-9a-zA-Z]*" />'
        $content = $content -replace $oldVersionString, $newVersionString
        Set-Content -Path $propsFile -Value $content
        Write-Host "Modified " $propsFile
    }
}

# Update .vcxproj files
Get-ChildItem -Recurse *.vcxproj | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match '\\Microsoft.WindowsAppSDK.') {
        $newVersionString = '\Microsoft.WindowsAppSDK.' + $WinAppSDKVersion + '\'
        $oldVersionString = '\\Microsoft.WindowsAppSDK.[-.0-9a-zA-Z]*\\'
        $content = $content -replace $oldVersionString, $newVersionString
        Set-Content -Path $_.FullName -Value $content
        Write-Host "Modified " $_.FullName
    }
}

# Update .csproj files
Get-ChildItem -Recurse *.csproj | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match 'PackageReference Include="Microsoft.WindowsAppSDK"') {
        $newVersionString = 'PackageReference Include="Microsoft.WindowsAppSDK" Version="'+ $WinAppSDKVersion + '"'
        $oldVersionString = 'PackageReference Include="Microsoft.WindowsAppSDK" Version="[-.0-9a-zA-Z]*"'
        $content = $content -replace $oldVersionString, $newVersionString
        Set-Content -Path $_.FullName -Value $content
        Write-Host "Modified " $_.FullName 
    }
}

Update-NugetConfig
