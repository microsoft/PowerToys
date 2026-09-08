# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0

<#
.SYNOPSIS
Prepares isolated PowerOCR source snapshots for a Windows App SDK comparison.
.DESCRIPTION
Prepare never builds or restores. Restore runs only the repository's solution restore
path. Build must be invoked explicitly. Existing non-SDK project reference outputs are
reused; no original project, central package version, or shared app output is changed.
#>
[CmdletBinding()]
param(
    [ValidateSet('Prepare', 'Repair', 'Restore', 'Build', 'Verify', 'Run', 'Status')]
    [string]$Mode = 'Prepare',
    [ValidateSet('2.2.0', '2.4.0')]
    [string]$SdkVersion = '2.4.0'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..')).TrimEnd('\', '/')
$sourceRoot = Join-Path $repoRoot 'src/modules/PowerOCR/PowerOCR'
$experimentRoot = Join-Path $repoRoot 'artifacts/PowerOcrCursorSdkComparison'
$latestPath = Join-Path $experimentRoot 'latest.json'
$verificationScript = Join-Path $PSScriptRoot 'Verify-CursorSdkComparison.ps1'
$baselineOutput = Join-Path $repoRoot 'x64/Debug/WinUI3Apps'

function Write-Text([string]$path, [string]$text) {
    [IO.File]::WriteAllText($path, ($text -replace '\r?\n', "`r`n"), [Text.UTF8Encoding]::new($false))
}

function Save-Xml([xml]$document, [string]$path) {
    $settings = New-Object Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.Encoding = [Text.UTF8Encoding]::new($false)
    $settings.NewLineChars = "`r`n"
    $writer = [Xml.XmlWriter]::Create($path, $settings)
    try { $document.Save($writer) } finally { $writer.Dispose() }
}

function Write-ComparisonTargets([string]$projectDirectory) {
    Write-Text (Join-Path $projectDirectory 'Directory.Build.targets') @'
<Project>
  <Import Project="$(RepoRoot)Directory.Build.targets" />
  <PropertyGroup>
    <!-- References are intentionally outside the comparison solution. Keep their baseline configuration. -->
    <ShouldUnsetParentConfigurationAndPlatform>false</ShouldUnsetParentConfigurationAndPlatform>
    <EnableDynamicPlatformResolution>false</EnableDynamicPlatformResolution>
  </PropertyGroup>
  <Target Name="ConfigureCursorComparisonReferences"
          BeforeTargets="AssignProjectConfiguration"
          DependsOnTargets="IncludeTransitiveProjectReferences">
    <Error Condition="'$(Configuration)' != 'Debug' or '$(Platform)' != 'x64'"
           Text="The cursor SDK comparison reuses Debug/x64 dependencies. Select Debug/x64." />
    <ItemGroup>
      <ProjectReference Update="@(ProjectReference)">
        <SetConfiguration>Configuration=Debug</SetConfiguration>
        <SetPlatform>Platform=x64</SetPlatform>
        <!-- AdditionalProperties are global in the child, so recursive reference queries keep these settings. -->
        <AdditionalProperties>%(ProjectReference.AdditionalProperties);SolutionDir=$(RepoRoot);ShouldUnsetParentConfigurationAndPlatform=false;EnableDynamicPlatformResolution=false;BuildProjectReferences=false</AdditionalProperties>
      </ProjectReference>
    </ItemGroup>
  </Target>
</Project>
'@
}

function Copy-InheritedAnalyzerConfig([string]$variantDirectory) {
    # The app normally inherits src/.editorconfig, which is outside the copied source tree.
    # Keep it above project/ so any project-local editorconfig continues to take precedence.
    $source = Join-Path $repoRoot 'src/.editorconfig'
    $destination = Join-Path $variantDirectory '.editorconfig'
    [IO.File]::Copy($source, $destination, $true)
    [pscustomobject]@{sourcePath=$source;path=$destination;sha256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash}
}

function Get-SourceFiles([string]$directory) {
    foreach ($item in Get-ChildItem -LiteralPath $directory -Force) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Unexpected link in source snapshot: $($item.FullName)" }
        if ($item.PSIsContainer) {
            if ($item.Name -notin @('bin', 'obj', '.vs', '.git')) { Get-SourceFiles $item.FullName }
        }
        elseif ($item.Name -notmatch '^build\.' -and $item.Extension -notin @('.user', '.log', '.binlog', '.pdb', '.dll', '.exe')) {
            $item.FullName
        }
    }
}

function Assert-BaselineOutputs {
    $names = @('PowerToys.PowerOCR.Core.dll', 'PowerToys.ManagedCommon.dll', 'PowerToys.ManagedTelemetry.dll',
        'PowerToys.Settings.UI.Lib.dll', 'PowerToys.Interop.dll', 'PowerToys.GPOWrapper.dll',
        'PowerDisplay.Models.dll', 'PowerToys.MouseJump.Common.dll', 'PowerToys.MouseJump.Models.dll')
    foreach ($name in $names) {
        if (-not (Test-Path -LiteralPath (Join-Path $baselineOutput $name))) { throw "Existing baseline dependency is missing: $name. Build the normal PowerOCR project first; the comparison does not rebuild references." }
    }
}

function Get-ReferenceProtectedPaths {
    $assets = Get-Content -LiteralPath (Join-Path $sourceRoot 'obj/project.assets.json') -Raw | ConvertFrom-Json
    foreach ($library in $assets.libraries.PSObject.Properties) {
        if ($library.Value.type -ne 'project') { continue }
        $referenceProject = [IO.Path]::GetFullPath((Join-Path $sourceRoot $library.Value.msbuildProject))
        $referenceDirectory = [IO.Path]::GetDirectoryName($referenceProject)
        $assemblyName = $library.Name.Split('/')[0]
        if ($assemblyName -eq 'GPOWrapper') { $assemblyName = 'PowerToys.GPOWrapper' }
        if ($assemblyName -eq 'ZoomItSettingsInterop') { $assemblyName = 'PowerToys.ZoomItSettingsInterop' }
        $referenceProject
        foreach ($name in @('project.assets.json', ([IO.Path]::GetFileName($referenceProject) + '.nuget.g.props'), ([IO.Path]::GetFileName($referenceProject) + '.nuget.g.targets'))) {
            $candidate = Join-Path $referenceDirectory "obj/$name"
            if (Test-Path -LiteralPath $candidate) { $candidate }
        }
        # GetTargetPath can return a project-local assembly, a reference assembly, or a native WinMD.
        foreach ($subdirectory in @('bin/x64/Debug', 'obj/x64/Debug', 'x64/Debug')) {
            $directory = Join-Path $referenceDirectory $subdirectory
            if (-not (Test-Path -LiteralPath $directory)) { continue }
            foreach ($extension in @('dll', 'winmd')) {
                Get-ChildItem -LiteralPath $directory -Filter "$assemblyName.$extension" -File -Recurse | ForEach-Object FullName
            }
        }
        foreach ($extension in @('dll', 'winmd')) {
            $candidate = Join-Path $repoRoot "x64/Debug/$assemblyName.$extension"
            if (Test-Path -LiteralPath $candidate) { $candidate }
        }
    }
}

function Get-ExpectedPackages([string]$version) {
    if ($version -eq '2.4.0') {
        return [ordered]@{
            'Microsoft.WindowsAppSDK'='2.4.0'; 'Microsoft.WindowsAppSDK.Base'='2.0.4'
            'Microsoft.WindowsAppSDK.Foundation'='2.3.9'; 'Microsoft.WindowsAppSDK.InteractiveExperiences'='2.1.6'
            'Microsoft.WindowsAppSDK.WinUI'='2.3.6'; 'Microsoft.WindowsAppSDK.DWrite'='2.1.0'
            'Microsoft.WindowsAppSDK.Widgets'='2.0.5'; 'Microsoft.WindowsAppSDK.AI'='2.4.4'
            'Microsoft.WindowsAppSDK.ML'='2.1.74'; 'Microsoft.WindowsAppSDK.Search'='2.4.4'
            'Microsoft.WindowsAppSDK.Runtime'='2.4.0'; 'Microsoft.Windows.AI.MachineLearning'='2.1.74'
        }
    }
    $assets = Get-Content -LiteralPath (Join-Path $sourceRoot 'obj/project.assets.json') -Raw | ConvertFrom-Json
    $expected = [ordered]@{}
    foreach ($library in $assets.libraries.PSObject.Properties) {
        if ($library.Name -match '^(Microsoft\.WindowsAppSDK(?:\.[^/]+)?|Microsoft\.Windows\.AI\.MachineLearning)/(.+)$') {
            $expected[$Matches[1]] = $Matches[2]
        }
    }
    if ($expected['Microsoft.WindowsAppSDK'] -ne '2.2.0') { throw 'The baseline assets are not the expected SDK 2.2.0 build.' }
    return $expected
}

if ($Mode -eq 'Prepare') {
    Assert-BaselineOutputs
    $files = @(Get-SourceFiles $sourceRoot | Sort-Object)
    $sourceFiles = @($files | ForEach-Object {
        [pscustomobject]@{ relativePath=$_.Substring($sourceRoot.Length + 1); sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
    })
    $protectedPaths = @('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'NuGet.Config',
        'src/Common.Dotnet.props', 'src/Common.Dotnet.CsWinRT.props', 'src/Common.Dotnet.PrepareGeneratedFolder.targets',
        'src/Common.SelfContained.props', 'src/.editorconfig', 'src/modules/PowerOCR/PowerOCR/obj/project.assets.json') | ForEach-Object { Join-Path $repoRoot $_ }
    $protectedPaths += @(Get-ChildItem -LiteralPath $baselineOutput -File | Where-Object { $_.Name -match '^(PowerToys\.|PowerDisplay\.Models|MouseJump\.)' -or $_.Name -in @('Microsoft.InputStateManager.dll','Microsoft.UI.Input.dll','dwmcorei.dll','CoreMessagingXP.dll','dcompi.dll','Microsoft.UI.Xaml.dll','Microsoft.WindowsAppRuntime.dll') } | ForEach-Object FullName)
    $protectedPaths += @(Get-ReferenceProtectedPaths)
    $protected = @($protectedPaths | Sort-Object -Unique | ForEach-Object { [pscustomobject]@{path=$_;sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash} })
    $session = [DateTime]::Now.ToString('yyyyMMdd_HHmmss') + '_' + [Guid]::NewGuid().ToString('N').Substring(0,8)
    $sessionRoot = Join-Path $experimentRoot $session
    New-Item -ItemType Directory -Path $sessionRoot -Force | Out-Null
    foreach ($version in @('2.2.0', '2.4.0')) {
        $variant = Join-Path $sessionRoot $version
        $projectDirectory = Join-Path $variant 'project'
        New-Item -ItemType Directory -Path $projectDirectory -Force | Out-Null
        $inheritedConfigFiles = @(Copy-InheritedAnalyzerConfig $variant)
        foreach ($file in $sourceFiles) {
            $destination = Join-Path $projectDirectory $file.relativePath
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
            [IO.File]::Copy((Join-Path $sourceRoot $file.relativePath), $destination, $false)
        }
        $projectPath = Join-Path $projectDirectory 'PowerOCR.csproj'
        [xml]$project = Get-Content -LiteralPath $projectPath -Raw
        $project.SelectSingleNode('/Project/PropertyGroup/OutputPath').InnerText = '$(MSBuildThisFileDirectory)..\bin\$(Platform)\$(Configuration)\'
        $properties = $project.CreateElement('PropertyGroup')
        foreach ($pair in @(@('BuildProjectReferences','false'), @('RestoreRecursive','false'))) {
            $node = $project.CreateElement($pair[0]); $node.InnerText = $pair[1]; [void]$properties.AppendChild($node)
        }
        [void]$project.DocumentElement.AppendChild($properties)
        $references = @()
        foreach ($reference in $project.SelectNodes('/Project/ItemGroup/ProjectReference')) {
            $referencePath = [IO.Path]::GetFullPath((Join-Path $sourceRoot $reference.Include))
            if (-not (Test-Path -LiteralPath $referencePath)) { throw "Missing project reference: $referencePath" }
            $reference.SetAttribute('Include', $referencePath)
            $references += $referencePath
        }
        Save-Xml $project $projectPath
        $escapedRepo = [Security.SecurityElement]::Escape($repoRoot)
        $buildProps = @'
<Project>
  <Import Project="__REPO_ROOT__\Directory.Build.props" />
  <PropertyGroup>
    <RestoreConfigFile>$(MSBuildThisFileDirectory)..\NuGet.Config</RestoreConfigFile>
  </PropertyGroup>
</Project>
'@
        Write-Text (Join-Path $projectDirectory 'Directory.Build.props') ($buildProps.Replace('__REPO_ROOT__', $escapedRepo))
        Write-ComparisonTargets $projectDirectory
        $overrides = ''
        if ($version -eq '2.4.0') {
            $overrides = @'
  <ItemGroup>
    <PackageVersion Update="Microsoft.WindowsAppSDK" Version="2.4.0" />
    <PackageVersion Update="Microsoft.WindowsAppSDK.Foundation" Version="2.3.9" />
    <PackageVersion Update="Microsoft.WindowsAppSDK.AI" Version="2.4.4" />
    <PackageVersion Update="Microsoft.WindowsAppSDK.Runtime" Version="2.4.0" />
  </ItemGroup>
'@
        }
        Write-Text (Join-Path $projectDirectory 'Directory.Packages.props') "<Project>`r`n  <Import Project=`"$escapedRepo\Directory.Packages.props`" />`r`n$overrides`r`n</Project>"
        Write-Text (Join-Path $variant 'NuGet.Config') @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="PowerToysPublicDependencies" value="https://pkgs.dev.azure.com/shine-oss/PowerToys/_packaging/PowerToysPublicDependencies/nuget/v3/index.json" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="PowerToysPublicDependencies"><package pattern="*" /></packageSource>
    <packageSource key="nuget.org">
      <package pattern="Microsoft.WindowsAppSDK*" />
      <package pattern="Microsoft.Windows.AI.MachineLearning" />
    </packageSource>
  </packageSourceMapping>
</configuration>
'@
        $solutionPath = Join-Path $variant 'PowerOCR.Comparison.slnx'
        Write-Text $solutionPath @'
<Solution>
  <Configurations><Platform Name="x64" /></Configurations>
  <Project Path="project/PowerOCR.csproj"><Platform Solution="*|x64" Project="x64" /></Project>
</Solution>
'@
        $manifest = [ordered]@{
            schema=1; sdkVersion=$version; repoRoot=$repoRoot; sourceRoot=$sourceRoot
            projectPath=$projectPath; solutionPath=$solutionPath; outputPath=(Join-Path $variant 'bin/x64/Debug')
            expectedPackages=(Get-ExpectedPackages $version); references=$references
            protectedFiles=$protected; sourceFiles=$sourceFiles; inheritedConfigFiles=$inheritedConfigFiles
        }
        $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $variant 'comparison.json') -Encoding UTF8
    }
    [pscustomobject]@{sessionRoot=$sessionRoot;createdUtc=[DateTime]::UtcNow.ToString('O')} |
        ConvertTo-Json | Set-Content -LiteralPath $latestPath -Encoding UTF8
    Write-Host "Prepared both SDK variants from the same source snapshot: $sessionRoot"
    Write-Host 'No restore or build was run.'
    exit 0
}

if (-not (Test-Path -LiteralPath $latestPath)) { throw 'Run -Mode Prepare first.' }
$latest = Get-Content -LiteralPath $latestPath -Raw | ConvertFrom-Json
$variantRoot = [IO.Path]::GetFullPath((Join-Path $latest.sessionRoot $SdkVersion))
if (-not $variantRoot.StartsWith($experimentRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid comparison directory.' }
$manifest = Get-Content -LiteralPath (Join-Path $variantRoot 'comparison.json') -Raw | ConvertFrom-Json

if ($Mode -eq 'Repair') {
    Write-ComparisonTargets ([IO.Path]::GetDirectoryName($manifest.projectPath))
    $inheritedConfigFiles = @(Copy-InheritedAnalyzerConfig $variantRoot)
    $manifest | Add-Member -NotePropertyName inheritedConfigFiles -NotePropertyValue $inheritedConfigFiles -Force
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $variantRoot 'comparison.json') -Encoding UTF8
    Write-Host "Updated comparison reference and analyzer configuration: $($manifest.projectPath)"
    Write-Host 'No restore or build was run. Reload the project in Visual Studio.'
}
elseif ($Mode -in @('Restore', 'Build')) {
    Assert-BaselineOutputs
    foreach ($protected in $manifest.protectedFiles) {
        if ((Get-FileHash -LiteralPath $protected.path -Algorithm SHA256).Hash -ne $protected.sha256) {
            throw "Baseline changed since Prepare: $($protected.path). Prepare a new pair before comparing SDK versions."
        }
    }
    $topLevelProjects = @(Get-ChildItem -LiteralPath $variantRoot -File | Where-Object { $_.Extension -in @('.sln','.slnx','.slnf','.csproj','.vcxproj') })
    if ($topLevelProjects.Count -ne 1 -or $topLevelProjects[0].FullName -ne $manifest.solutionPath) { throw 'Comparison directory must contain only its isolated solution, with the csproj in a subdirectory.' }
    $pwsh = (Get-Process -Id $PID).Path
    $buildScript = Join-Path $repoRoot 'tools/build/build.ps1'
    $arguments = @('-NoProfile', '-File', $buildScript, '-Platform', 'x64', '-Configuration', 'Debug', '-Path', $variantRoot)
    if ($Mode -eq 'Restore') { $arguments += '-RestoreOnly' }
    if ($Mode -eq 'Build') {
        Remove-Item -LiteralPath (Join-Path $variantRoot 'build-success.json') -ErrorAction SilentlyContinue
    }
    $arguments += @('/p:BuildProjectReferences=false', '/p:RestoreRecursive=false')
    & $pwsh @arguments
    if ($LASTEXITCODE -ne 0) { throw "$Mode failed with exit code $LASTEXITCODE. See $variantRoot/build.debug.x64.errors.log" }
    & $verificationScript -VariantRoot $variantRoot -RequireOutput:($Mode -eq 'Build')
    if ($Mode -eq 'Build') {
        [pscustomobject]@{sdkVersion=$SdkVersion;completedUtc=[DateTime]::UtcNow.ToString('O');exeHash=(Get-FileHash -LiteralPath (Join-Path $manifest.outputPath 'PowerToys.PowerOCR.exe')).Hash} |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $variantRoot 'build-success.json') -Encoding UTF8
    }
}
elseif ($Mode -eq 'Verify') {
    & $verificationScript -VariantRoot $variantRoot -RequireOutput
}
elseif ($Mode -eq 'Run') {
    if (-not (Test-Path -LiteralPath (Join-Path $variantRoot 'build-success.json'))) { throw 'Run -Mode Build successfully before launching this variant.' }
    & $verificationScript -VariantRoot $variantRoot -RequireOutput
    if (@(Get-Process -Name 'PowerToys.PowerOCR' -ErrorAction SilentlyContinue).Count) { throw 'Exit the current PowerOCR process before launching the comparison; Esc alone leaves it running.' }
    $executable = Join-Path $manifest.outputPath 'PowerToys.PowerOCR.exe'
    $buildResult = Get-Content -LiteralPath (Join-Path $variantRoot 'build-success.json') -Raw | ConvertFrom-Json
    if ((Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash -ne $buildResult.exeHash) { throw 'The executable changed after the successful comparison build.' }
    $process = Start-Process -FilePath $executable -WorkingDirectory $manifest.outputPath -WindowStyle Hidden -PassThru
    [pscustomobject]@{sdkVersion=$SdkVersion;processId=$process.Id;executable=$executable;startedUtc=[DateTime]::UtcNow.ToString('O')} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $variantRoot 'run.json') -Encoding UTF8
    Write-Host "Started SDK $SdkVersion comparison, PID $($process.Id). Use your Text Extractor shortcut to open the overlay."
}
else {
    [pscustomobject]@{Sdk=$SdkVersion;Project=$manifest.projectPath;Solution=$manifest.solutionPath;Output=$manifest.outputPath;Restored=(Test-Path -LiteralPath (Join-Path $variantRoot 'restore-verification.json'));Built=(Test-Path -LiteralPath (Join-Path $variantRoot 'build-success.json'))} | ConvertTo-Json
}
