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
    [Parameter(Mandatory)][string] $DotNetPath,
    [ValidateSet('x64', 'arm64')][string] $Platform = 'x64',
    [switch] $ReadyToRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. "$PSScriptRoot\MwbSandboxCi.Common.ps1"

function Invoke-MwbCiBuildCommand {
    param([string] $Executable, [string[]] $Arguments, [string] $Directory,
        [string] $Log, [int] $TimeoutSeconds = 1200, [switch] $RequireCleanStandardError)

    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.WorkingDirectory = $Directory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $start.Environment['GIT_TERMINAL_PROMPT'] = '0'
    $start.Environment['GCM_INTERACTIVE'] = 'Never'
    $start.Environment['GIT_CONFIG_NOSYSTEM'] = '1'
    $start.Environment['GIT_CONFIG_GLOBAL'] = 'NUL'
    $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    $start.Environment['DOTNET_ROOT'] = Split-Path $DotNetPath
    $start.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $start.Environment['TEMP'] = Join-Path $WorkRoot 'scratch'
    $start.Environment['TMP'] = $start.Environment['TEMP']
    $start.Environment['PATH'] = (Split-Path $DotNetPath) + ';' +
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer') + ';' + $env:PATH
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
        if ($RequireCleanStandardError -and -not [string]::IsNullOrWhiteSpace($standardError)) {
            throw "MWB source audit reported stderr; the entire checkout must be readable. Inspect $Log."
        }
        $standardOutput.Trim()
    }
    finally {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit(10000) | Out-Null }
        $process.Dispose()
    }
}

function Get-MwbCiGitOptions {
    @('-c', 'credential.helper=', '-c', 'core.longpaths=true', '-c', 'core.autocrlf=false',
        '-c', 'core.hooksPath=NUL')
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
    # The Crossgen2 compiler package is always the win-x64 host tool: it runs on this x64 build
    # agent regardless of the RID it is asked to target. The target-specific native JIT it loads
    # differs by file name only; ARM64 codegen is OS-independent, so the SDK ships it as
    # clrjit_universal_arm64_x64.dll rather than a win-specific name like the x64 JIT.
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
            # The packager verifies the native images, file versions, commits and hashes
            # before invoking even an already-restored SDK compiler.
            return $candidate
        }
    }
    $null = New-Item -ItemType Directory -Path $Directory
    # A standalone project avoids the PowerToys central-package/build imports. PackageDownload
    # restores the SDK's native compiler, not a managed/tool substitute or an arbitrary latest SDK.
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

function Assert-MwbCiNativePreviewFile {
    param([string] $Path, [ValidateSet('x64', 'arm64')][string] $Architecture = 'x64')

    # 0x8664 = IMAGE_FILE_MACHINE_AMD64, 0xAA64 = IMAGE_FILE_MACHINE_ARM64. An exact-equality
    # check also rejects ARM64EC (0xA641) and any managed/emulated substitute when native
    # ARM64 is requested: only the literal native machine value for the target is accepted.
    $expectedMachine = if ($Architecture -eq 'arm64') { 0xAA64 } else { 0x8664 }
    $stream = [IO.File]::OpenRead($Path)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        if ($reader.ReadUInt16() -ne 0x5A4D) { throw 'MWB preview output is not a PE image.' }
        $stream.Position = 0x3C
        $header = $reader.ReadInt32()
        if ($header -lt 64 -or $header + 264 -gt $stream.Length) { throw 'MWB preview output has an invalid PE header.' }
        $stream.Position = $header
        if ($reader.ReadUInt32() -ne 0x4550 -or $reader.ReadUInt16() -ne $expectedMachine) {
            throw "MWB preview output is not native $Architecture."
        }
        $stream.Position = $header + 24
        if ($reader.ReadUInt16() -ne 0x20B) { throw 'MWB preview output is not PE32+.' }
        $stream.Position = $header + 24 + 112 + (14 * 8)
        if ($reader.ReadUInt64() -ne 0) { throw 'MWB preview output unexpectedly requires a managed runtime.' }
    }
    finally { $reader.Dispose() }
}

function Get-MwbCiPreviewPublishCommand {
    param([string] $VcVars, [string] $DotNet, [string] $PublishRoot, $Pin, [string] $RuntimeIdentifier = 'win-x64')

    @"
@echo off
call "$VcVars" >nul
if errorlevel 1 exit /b %errorlevel%
"$DotNet" publish "src\winapp-CLI\WinApp.Cli\WinApp.Cli.csproj" -c Release -r $RuntimeIdentifier --self-contained -o "$PublishRoot" /p:Version=$($Pin.AssemblyVersion) /p:AssemblyVersion=$($Pin.AssemblyVersion) /p:FileVersion=$($Pin.AssemblyVersion) /p:InformationalVersion=$($Pin.Version) /p:IncludeSourceRevisionInInformationalVersion=false --nologo -v:minimal
exit /b %errorlevel%
"@
}

function Get-MwbCiPreviewRuntimeProvenance {
    param([string] $Directory, [string] $ExpectedVersion, [ValidateSet('x64', 'arm64')][string] $Platform = 'x64')

    if ($ExpectedVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'The preview runtime pin must be an exact version.' }
    $framework = 'net10.0-windows10.0.19041.0'
    $rid = "win-$Platform"
    $targetName = "$framework/$rid"
    $projects = @()
    $compilerPackages = @()
    $runtimeVersion = $null
    foreach ($project in @('WinApp.Cli', 'WinApp.UIAutomation', 'WinApp.UIAutomation.Recording')) {
        $path = Join-Path $Directory "src\winapp-CLI\$project\obj\project.assets.json"
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "The published preview is missing restored runtime provenance: $project."
        }
        $assets = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        $frameworkNode = $assets.project.frameworks.PSObject.Properties[$framework]
        $target = $assets.targets.PSObject.Properties[$targetName]
        if (-not $frameworkNode -or -not $target) {
            throw "The published preview has no restored $targetName target: $project."
        }
        $downloads = @($frameworkNode.Value.downloadDependencies)
        if (@($downloads | Group-Object name | Where-Object Count -GT 1).Count) {
            throw "The published preview has duplicate package downloads: $project."
        }
        $required = @("Microsoft.NETCore.App.Runtime.$rid")
        if ($project -ceq 'WinApp.Cli') {
            # The NativeAOT *target* runtime pack matches the requested RID; the ILCompiler
            # *host* package is always win-x64 because it runs on this x64 build agent, and it
            # is restored as a framework download dependency (not a target library) whenever
            # cross-compiling, exactly like the runtime packs above.
            $required += @("Microsoft.NETCore.App.Runtime.NativeAOT.$rid", 'runtime.win-x64.Microsoft.DotNet.ILCompiler')
        }
        $runtimePacks = foreach ($id in $required) {
            $entries = @($downloads | Where-Object name -EQ $id)
            if ($entries.Count -ne 1 -or
                $entries[0].version -notmatch '^\[(?<version>\d+\.\d+\.\d+)(?:,\s*\k<version>)?\]$' -or
                $Matches.version -cne $ExpectedVersion) {
                throw "The published preview runtime/compiler package does not match the pin: $project / $id."
            }
            $runtimeVersion = $Matches.version
            [ordered]@{ Id = $id; Version = $runtimeVersion }
        }
        if ($project -ceq 'WinApp.Cli') {
            # These two package ids are the ones NuGet actually records as regular target
            # dependencies for the requested RID (with sha512 library provenance): the
            # host-agnostic meta package, and the ILCompiler package matching the *target* RID
            # itself. The host x64 executable package is verified above via its download pin;
            # for a non-cross (x64) build the target RID *is* x64, so both checks agree.
            foreach ($id in @('Microsoft.DotNet.ILCompiler', "runtime.$rid.Microsoft.DotNet.ILCompiler")) {
                $entries = @($target.Value.PSObject.Properties | Where-Object {
                    $_.Name.StartsWith("$id/", [StringComparison]::OrdinalIgnoreCase)
                })
                if ($entries.Count -ne 1 -or $entries[0].Name.Substring($id.Length + 1) -cne $ExpectedVersion) {
                    throw "The published preview NativeAOT compiler does not match the pin: $id."
                }
                $library = $assets.libraries.PSObject.Properties[$entries[0].Name]
                if (-not $library -or $library.Value.type -cne 'package' -or
                    [string]::IsNullOrWhiteSpace($library.Value.sha512)) {
                    throw "The published preview compiler package provenance is missing: $id."
                }
                $compilerPackages += [ordered]@{
                    Id = $id; Version = $entries[0].Name.Substring($id.Length + 1); Sha512 = $library.Value.sha512
                }
            }
        }
        $projects += [ordered]@{
            Project = $project
            AssetsSha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
            RuntimePacks = @($runtimePacks)
            WindowsSdkReferences = @($downloads | Where-Object name -EQ 'Microsoft.Windows.SDK.NET.Ref' |
                ForEach-Object { [ordered]@{ Id = $_.name; VersionRange = $_.version } })
        }
    }
    [ordered]@{
        RuntimeVersion = $runtimeVersion; TargetFramework = $framework; RuntimeIdentifier = $rid
        Projects = $projects; CompilerPackages = $compilerPackages
    }
}

function New-MwbCiPreview {
    param([string] $Directory, [string] $Destination, [ValidateSet('x64', 'arm64')][string] $Platform = 'x64')

    $pin = Get-MwbPreviewPin
    $patch = Get-MwbPreviewPatchPath
    Assert-MwbCiHash $patch $pin.PatchSha256
    $null = New-Item -ItemType Directory -Path $Directory
    $git = (Get-Command git.exe -ErrorAction Stop).Source
    $gitOptions = @(Get-MwbCiGitOptions)
    $null = Invoke-MwbCiBuildCommand $git ($gitOptions + @('init', '--quiet')) $Directory (Join-Path $Directory 'init.log') 30
    # Also cover Git invoked indirectly by the upstream build, not only this helper's commands.
    $null = Invoke-MwbCiBuildCommand $git ($gitOptions + @('config', '--local', 'core.longpaths', 'true')) `
        $Directory (Join-Path $Directory 'git-config.log') 30
    $null = Invoke-MwbCiBuildCommand $git ($gitOptions + @('fetch', '--quiet', '--depth', '1', '--no-tags',
        $pin.Repository, $pin.Commit)) $Directory (Join-Path $Directory 'fetch.log') 180
    $commit = Invoke-MwbCiBuildCommand $git ($gitOptions + @('rev-parse', 'FETCH_HEAD')) $Directory (Join-Path $Directory 'revision.log') 30
    if ($commit -cne $pin.Commit) { throw 'The fetched preview source did not match its pinned public commit.' }
    $null = Invoke-MwbCiBuildCommand $git ($gitOptions + @('-c', 'advice.detachedHead=false',
        'checkout', '--quiet', '--detach', $pin.Commit)) $Directory (Join-Path $Directory 'checkout.log') 30
    $null = Invoke-MwbCiBuildCommand $git ($gitOptions + @('apply', '--check', $patch)) $Directory (Join-Path $Directory 'patch-check.log') 30
    $null = Invoke-MwbCiBuildCommand $git ($gitOptions + @('apply', $patch)) $Directory (Join-Path $Directory 'patch.log') 30
    $changed = Invoke-MwbCiBuildCommand $git ($gitOptions + @('diff', '--no-ext-diff', '--no-textconv', '--name-only')) `
        $Directory (Join-Path $Directory 'changed.log') 30 -RequireCleanStandardError
    $expected = @('src/winapp-CLI/WinApp.Cli.Tests/SandboxUxRegressionTests.cs',
        'src/winapp-CLI/WinApp.Cli/ExecutionTargets/WindowsSandbox/WindowsSandboxBackend.cs')
    if (@(Compare-Object $expected @($changed -split '\r?\n')).Count) {
        throw 'The preview patch changed files outside its frozen performance-only scope.'
    }
    # Pin the previously validated source-build toolchain; no global SDK or CLI update.
    @{ sdk = @{ version = $pin.SdkVersion; rollForward = 'disable'; allowPrerelease = $false } } |
        ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $Directory 'global.json')
    $sdk = Invoke-MwbCiBuildCommand $DotNetPath @('--version') $Directory (Join-Path $Directory 'sdk.log') 30
    if ($sdk -cne $pin.SdkVersion) { throw 'The preview build SDK differs from its frozen pin.' }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $vcRequirements = @('Microsoft.VisualStudio.Component.VC.Tools.x86.x64')
    if ($Platform -eq 'arm64') { $vcRequirements += 'Microsoft.VisualStudio.Component.VC.Tools.ARM64' }
    $vsRequiresArguments = @()
    foreach ($requirement in $vcRequirements) { $vsRequiresArguments += @('-requires', $requirement) }
    $vs = Invoke-MwbCiBuildCommand $vswhere (@('-latest', '-prerelease', '-products', '*') + $vsRequiresArguments +
        @('-property', 'installationPath')) $Directory (Join-Path $Directory 'vs.log') 30
    # x64 targets build with the plain x64 host/target script; ARM64 is cross-compiled from
    # this x64 build agent using the host=amd64/target=arm64 cross-toolchain script.
    $vcvarsName = if ($Platform -eq 'arm64') { 'vcvarsamd64_arm64.bat' } else { 'vcvars64.bat' }
    $vcvars = Join-Path $vs "VC\Auxiliary\Build\$vcvarsName"
    if (-not (Test-Path -LiteralPath $vcvars -PathType Leaf)) {
        throw "BLOCKED_INFRASTRUCTURE: the build agent lacks the $Platform NativeAOT Visual Studio toolchain."
    }
    $publish = Join-Path $Directory 'portable'
    # Environment setup and NativeAOT compilation must share one cmd.exe process.
    $commandFile = Join-Path $Directory 'publish.cmd'
    Get-MwbCiPreviewPublishCommand -VcVars $vcvars -DotNet $DotNetPath -PublishRoot $publish -Pin $pin -RuntimeIdentifier "win-$Platform" |
        Set-Content -LiteralPath $commandFile -Encoding ascii
    $null = Invoke-MwbCiBuildCommand $env:ComSpec @('/d', '/c', $commandFile) $Directory (Join-Path $Directory 'publish.log')
    # The exact SDK selects servicing packs independently for .NET and the Windows SDK.
    # A global RuntimeFrameworkVersion would incorrectly override the latter as well.
    $runtimeProvenance = Get-MwbCiPreviewRuntimeProvenance $Directory $pin.RuntimeVersion -Platform $Platform
    $null = New-Item -ItemType Directory -Path $Destination
    $files = foreach ($name in $pin.Files) {
        $source = Join-Path $publish $name
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "The preview portable output is missing $name." }
        Assert-MwbCiNativePreviewFile $source -Architecture $Platform
        if ($name -ceq 'winapp.exe' -and
            (Get-MwbCiFileVersion $source).ProductVersion -cne $pin.Version) {
            throw 'The private preview informational version does not match its pinned source baseline.'
        }
        Copy-Item -LiteralPath $source -Destination $Destination
        [ordered]@{ Path = $name; Sha256 = (Get-FileHash -LiteralPath (Join-Path $Destination $name) -Algorithm SHA256).Hash }
    }
    [ordered]@{
        Repository = $pin.Repository; Commit = $commit; PatchSha256 = $pin.PatchSha256
        Version = $pin.Version; SdkVersion = $sdk; RuntimeVersion = $runtimeProvenance.RuntimeVersion
        RuntimeProvenance = $runtimeProvenance
        VisualStudioVersion = Invoke-MwbCiBuildCommand $vswhere (@('-latest', '-prerelease', '-products', '*') + $vsRequiresArguments +
            @('-property', 'installationVersion')) $Directory (Join-Path $Directory 'vs-version.log') 30
        Files = @($files)
    }
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
        throw 'MWB preview audit/build work must be outside the source checkout and published bundle.'
    }
    if ($product -notmatch "\\$Platform\\Debug`$" -or
        -not (Test-Path -LiteralPath (Join-Path $product 'PowerToys.MouseWithoutBorders.dll') -PathType Leaf)) {
        throw "MWB CI preparation accepts only the built $Platform Debug product."
    }
    [pscustomobject]@{ ProductRoot = $product; SourceRoot = $source; OutputRoot = $output; WorkRoot = $work }
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
if ($ReadyToRun) {
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
$preview = New-MwbCiPreview (Join-Path $work 'preview-source') (Join-Path $output 'preview') -Platform $Platform
[ordered]@{
    FormatVersion = 1; SourceRevision = $SourceRevision.ToLowerInvariant()
    Platform = $Platform; Configuration = 'Debug'
    Runtime = [ordered]@{
        Sha256 = $runtime.Sha256
        ManifestSha256 = (Get-FileHash -LiteralPath "$archive.manifest.json" -Algorithm SHA256).Hash
        ReadyToRun = [bool]$ReadyToRun
        Files = @($files)
    }
    Preview = $preview
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $output 'manifest.json')
Write-Host "Prepared private MWB $Platform Debug bundle (ReadyToRun=$([bool]$ReadyToRun)); original product files were not modified."
