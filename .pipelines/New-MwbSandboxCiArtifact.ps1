# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $ProductRoot,
    [Parameter(Mandatory)][string] $OutputRoot,
    [Parameter(Mandatory)][string] $WorkRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{40}$')][string] $SourceRevision,
    [string] $DotNetPath,
    [ValidateSet('x64', 'arm64')][string] $Platform = 'x64',
    [switch] $ReadyToRun,
    [string] $WinAppCliArchivePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. "$PSScriptRoot\MwbSandboxCi.Common.ps1"

function Invoke-MwbCiBuildCommand {
    param([string] $Executable, [string[]] $Arguments, [string] $Directory,
        [string] $Log, [int] $TimeoutSeconds = 1200)

    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.WorkingDirectory = $Directory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    $start.Environment['DOTNET_ROOT'] = Split-Path $DotNetPath
    $start.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $start.Environment['TEMP'] = Join-Path $WorkRoot 'scratch'
    $start.Environment['TMP'] = $start.Environment['TEMP']
    $start.Environment['PATH'] = (Split-Path $DotNetPath) + ';' + $env:PATH
    $process = [Diagnostics.Process]::Start($start)
    try {
        $process.StandardInput.Close()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            throw "MWB build preparation exceeded $TimeoutSeconds seconds ($([IO.Path]::GetFileName($Log)))."
        }
        if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout, $stderr), 10000)) {
            throw 'MWB build preparation output did not close.'
        }
        $standardOutput = $stdout.GetAwaiter().GetResult()
        $standardError = $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText($Log, "[stdout]`r`n$standardOutput`r`n[stderr]`r`n$standardError")
        if ($process.ExitCode -ne 0) {
            throw "MWB build preparation failed (exit $($process.ExitCode)); inspect $Log."
        }
        $standardOutput.Trim()
    }
    finally {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit(10000) | Out-Null }
        $process.Dispose()
    }
}

function Get-MwbCiFileVersion {
    param([string] $Path)

    [Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
}

function Get-MwbCiRuntimeVersion {
    param([string] $Root)

    # Native CoreCLR ProductVersion can be the four-part comma-separated file version.
    # CoreLib carries the semantic runtime version; the packager subsequently verifies
    # its file version and commit against CoreCLR, the compiler and both native JIT DLLs.
    $version = (Get-MwbCiFileVersion (Join-Path $Root 'System.Private.CoreLib.dll')).ProductVersion
    if ($version -notmatch '^(?<version>\d+\.\d+\.\d+)(?:[-+].*)?$') {
        throw 'MWB CI requires an identifiable self-contained CLR to pin the Crossgen2 package.'
    }
    $Matches.version
}

function Get-MwbCiCrossgen2 {
    param([string] $RuntimeVersion, [string] $Directory, [ValidateSet('x64', 'arm64')][string] $TargetArchitecture = 'x64')

    if ($RuntimeVersion -notmatch '^\d+\.\d+\.\d+$') {
        throw 'MWB CI Crossgen2 requires an exact stable runtime package version.'
    }
    # Crossgen2 runs on the x64 build agent, independently of the target architecture.
    # ARM64 codegen uses the SDK's universal ARM64 JIT, not the Windows x64 JIT.
    $targetJit = if ($TargetArchitecture -eq 'arm64') { 'clrjit_universal_arm64_x64.dll' } else { 'clrjit_win_x64_x64.dll' }
    $candidates = @(
        (Join-Path (Split-Path $DotNetPath) "packs\Microsoft.NETCore.App.Crossgen2.win-x64\$RuntimeVersion\tools\crossgen2.exe")
    )
    if ($env:NUGET_PACKAGES) {
        $candidates += Join-Path $env:NUGET_PACKAGES "microsoft.netcore.app.crossgen2.win-x64\$RuntimeVersion\tools\crossgen2.exe"
    }
    foreach ($candidate in $candidates) {
        if ((Test-Path -LiteralPath $candidate -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path (Split-Path $candidate) 'jitinterface_x64.dll') -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path (Split-Path $candidate) $targetJit) -PathType Leaf)) {
            # Verify native images, versions, commits and hashes in the packager before use.
            return $candidate
        }
    }
    $null = New-Item -ItemType Directory -Path $Directory
    # The normal build SDK restores only the exact compiler package matching the product
    # CLR. It is not a winapp CLI build dependency or a floating compiler substitution.
    $project = Join-Path $Directory 'compiler.csproj'
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup><PackageDownload Include="Microsoft.NETCore.App.Crossgen2.win-x64" Version="[$RuntimeVersion]" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $project
    $config = Join-Path $Directory 'NuGet.Config'
    '<configuration><packageSources><clear /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources></configuration>' |
        Set-Content -LiteralPath $config
    $packages = Join-Path $Directory 'packages'
    $null = Invoke-MwbCiBuildCommand $DotNetPath @('restore', $project, '--configfile', $config,
        '--packages', $packages, '--nologo', '--verbosity', 'minimal') $Directory (Join-Path $Directory 'restore.log') 300
    $compiler = Join-Path $packages "microsoft.netcore.app.crossgen2.win-x64\$RuntimeVersion\tools\crossgen2.exe"
    if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
        throw 'The exact SDK Crossgen2 package did not contain its native x64 compiler.'
    }
    $compiler
}

function Get-MwbCiPreparationPaths {
    param([string] $ProductRoot, [string] $OutputRoot, [string] $WorkRoot, [string] $SourceRoot)

    $product = (Resolve-MwbCiFileSystemPath $ProductRoot).TrimEnd('\')
    $source = (Resolve-MwbCiFileSystemPath $SourceRoot).TrimEnd('\')
    $output = (Resolve-MwbCiFileSystemPath $OutputRoot -AllowMissing).TrimEnd('\')
    $work = (Resolve-MwbCiFileSystemPath $WorkRoot -AllowMissing).TrimEnd('\')
    foreach ($path in @($product, $source, $output, $work)) {
        Assert-MwbCiPlainPath $path
    }
    if (-not (Test-Path -LiteralPath $product -PathType Container) -or
        -not (Test-Path -LiteralPath $source -PathType Container)) {
        throw 'MWB CI product and source roots must be existing directories.'
    }
    foreach ($path in @($output, $work)) {
        if ($path -match '["%!\r\n]' -or $path -ieq $product -or
            $path.StartsWith("$product\", [StringComparison]::OrdinalIgnoreCase) -or
            (Test-Path -LiteralPath $path)) {
            throw 'MWB CI preparation requires new private output/work directories outside the original product.'
        }
        if ($path -ieq $source -or $path.StartsWith("$source\", [StringComparison]::OrdinalIgnoreCase)) {
            throw 'MWB CI preparation must be outside the source checkout and published bundle.'
        }
    }
    if ($work -ieq $output -or
        $work.StartsWith("$output\", [StringComparison]::OrdinalIgnoreCase) -or
        $output.StartsWith("$work\", [StringComparison]::OrdinalIgnoreCase)) {
        throw 'MWB preparation work must be outside the source checkout and published bundle.'
    }
    if ($product -notmatch "\\$Platform\\Debug`$" -or
        -not (Test-Path -LiteralPath (Join-Path $product 'PowerToys.MouseWithoutBorders.dll') -PathType Leaf)) {
        throw "MWB CI preparation accepts only the built $Platform Debug product."
    }
    [pscustomobject]@{ ProductRoot = $product; SourceRoot = $source; OutputRoot = $output; WorkRoot = $work }
}

function Get-MwbCiDebugUcrtPath {
    param([string] $SdkRoot, [string] $TargetVersion, [ValidateSet('x64', 'arm64')][string] $Platform)

    if ($TargetVersion -notmatch '^\d+\.\d+\.\d+\.\d+$' -or
        -not [IO.Path]::IsPathFullyQualified($SdkRoot)) {
        throw 'The debug UCRT requires an explicit installed Windows SDK root and pinned version.'
    }
    $path = Join-Path $SdkRoot "bin\$TargetVersion\$Platform\ucrt\ucrtbased.dll"
    Assert-MwbCiPlainPath $path
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "The pinned Windows SDK debug UCRT is missing for $Platform."
    }
    $path
}

function Get-MwbCiWindowsTargetVersion {
    param([xml] $Properties)

    $versions = @($Properties.SelectNodes('/*[local-name()="Project"]/*[local-name()="PropertyGroup"]/*[local-name()="WindowsTargetPlatformVersion"]') |
        ForEach-Object { $_.InnerText } | Select-Object -Unique)
    if ($versions.Count -ne 1 -or $versions[0] -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw 'MWB CI requires one pinned C++ Windows SDK version.'
    }
    $versions[0]
}

$paths = Get-MwbCiPreparationPaths $ProductRoot $OutputRoot $WorkRoot (Join-Path $PSScriptRoot '..')
$product = $paths.ProductRoot
$sourceRoot = $paths.SourceRoot
$output = $paths.OutputRoot
$work = $paths.WorkRoot
$WorkRoot = $work
$null = New-Item -ItemType Directory -Path $output, $work, (Join-Path $work 'scratch')
$experiment = Join-Path $sourceRoot 'src\modules\MouseWithoutBorders\Tests\SandboxExperiment'
$archive = Join-Path $output 'runtime.zip'
$options = @{}
$sdkRoot = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots' -Name KitsRoot10).KitsRoot10
[xml]$cppProps = Get-Content -LiteralPath (Join-Path $sourceRoot 'Cpp.Build.props') -Raw
$options.DebugUcrtPath = Get-MwbCiDebugUcrtPath -SdkRoot $sdkRoot -TargetVersion (Get-MwbCiWindowsTargetVersion $cppProps) -Platform $Platform
if ($ReadyToRun) {
    if (-not $DotNetPath) { $DotNetPath = (Get-Command dotnet.exe -ErrorAction Stop).Source }
    $runtimeVersion = Get-MwbCiRuntimeVersion $product
    $options.ReadyToRun = $true
    $options.Crossgen2Path = Get-MwbCiCrossgen2 $runtimeVersion (Join-Path $work 'compiler') -TargetArchitecture $Platform
}
& (Join-Path $experiment 'New-MwbRuntimeArchive.ps1') -ProductRoot $product -ArchivePath $archive -Platform $Platform @options | Out-Null
$runtime = Get-Content -LiteralPath "$archive.manifest.json" -Raw | ConvertFrom-Json
$files = foreach ($relative in $runtime.Files) {
    $path = Get-MwbCiRelativePath "$archive.staging" $relative
    [ordered]@{ Path = $relative; Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
}
# The archive, not a second packaging pass, is the source for both endpoints.
Remove-Item -LiteralPath "$archive.staging" -Recurse -Force
$cli = Save-WinAppCliRelease -WorkRoot (Join-Path $work 'winapp-cli') -Destination (Join-Path $output 'winapp-cli') `
    -Platform $Platform -ArchivePath $WinAppCliArchivePath
[ordered]@{
    FormatVersion = 2; SourceRevision = $SourceRevision.ToLowerInvariant()
    Platform = $Platform; Configuration = 'Debug'
    Runtime = [ordered]@{
        Sha256 = $runtime.Sha256
        ManifestSha256 = (Get-FileHash -LiteralPath "$archive.manifest.json" -Algorithm SHA256).Hash
        ReadyToRun = [bool]$ReadyToRun
        Files = @($files)
    }
    WinAppCli = $cli
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $output 'manifest.json')
Write-Host "Prepared private MWB $Platform Debug bundle with official winapp CLI $($cli.Tag) (ReadyToRun=$([bool]$ReadyToRun)); original product files were not modified."
