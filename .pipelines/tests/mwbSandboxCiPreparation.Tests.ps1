# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
. "$PSScriptRoot\..\MwbSandboxCi.Common.ps1"
$buildPath = Join-Path $PSScriptRoot '..\New-MwbSandboxCiArtifact.ps1'
$tokens = $null
$parseErrors = $null
$buildAst = [Management.Automation.Language.Parser]::ParseFile($buildPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors) { throw 'MWB CI preparation contains PowerShell parse errors.' }
# Load definitions only: no downloads, native compiler execution, OS mutation, or UI.
foreach ($name in @('Get-MwbCiCrossgen2', 'Invoke-MwbCiBuildCommand', 'Get-MwbCiRuntimeVersion',
    'Get-MwbCiFileVersion', 'Get-MwbCiPreparationPaths')) {
    $definition = $buildAst.Find({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $true)
    . ([scriptblock]::Create($definition.Extent.Text))
}

function New-CiBundleFixture {
    param([string] $Root)

    $bundle = Join-Path $Root 'mwb-sandbox-ci'
    $null = New-Item -ItemType Directory -Path (Join-Path $bundle 'winapp-cli')
    $runtimeFiles = @('PowerToys.exe', 'PowerToys.MouseWithoutBorders.exe',
        'PowerToys.MouseWithoutBorders.dll', 'PowerToys.MouseWithoutBordersHelper.exe',
        'WinUI3Apps\PowerToys.Settings.exe', 'WinUI3Apps\PowerToys.QuickAccess.exe')
    $stage = Join-Path $Root 'fixture-files'
    $entries = foreach ($name in $runtimeFiles) {
        $file = Join-Path $stage $name
        $null = New-Item -ItemType Directory -Path (Split-Path $file) -Force
        Set-Content -LiteralPath $file -Value "nonexecutable fixture: $name"
        @{ Path = $name; Sha256 = (Get-FileHash -LiteralPath $file).Hash }
    }
    $archive = Join-Path $bundle 'runtime.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($stage, $archive)
    $archiveHash = (Get-FileHash -LiteralPath $archive).Hash
    @{ Sha256 = $archiveHash; FileCount = $runtimeFiles.Count; Files = $runtimeFiles } |
        ConvertTo-Json -Depth 3 | Set-Content -LiteralPath "$archive.manifest.json"
    $pin = Get-WinAppCliRelease
    $cli = foreach ($name in $pin.Files) {
        $file = Join-Path $bundle "winapp-cli\$name"
        Set-Content -LiteralPath $file -Value "nonexecutable CLI fixture: $name"
        @{ Path = $name; Sha256 = (Get-FileHash -LiteralPath $file).Hash }
    }
    $manifest = @{
        FormatVersion = 2; SourceRevision = 'a' * 40; Platform = 'x64'; Configuration = 'Debug'
        Runtime = @{
            Sha256 = $archiveHash; ManifestSha256 = (Get-FileHash -LiteralPath "$archive.manifest.json").Hash
            ReadyToRun = $false; Files = @($entries)
        }
        WinAppCli = @{
            Repository = $pin.Repository; Tag = $pin.Tag; Version = $pin.Version
            Asset = $pin.Asset; Sha256 = $pin.Sha256; Files = @($cli)
        }
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $bundle 'manifest.json')
    $manifest
}

function Get-CiShortPathFixture {
    $fileSystem = $null
    try {
        $fileSystem = New-Object -ComObject Scripting.FileSystemObject
        foreach ($path in @($TestDrive, $env:ProgramFiles, $env:USERPROFILE)) {
            $folder = $null
            try {
                $longPath = (Get-Item -LiteralPath $path -Force).FullName.TrimEnd('\')
                $folder = $fileSystem.GetFolder($longPath)
                $shortPath = [string]$folder.ShortPath
                if ($shortPath -ine $longPath) {
                    return [pscustomobject]@{ LongPath = $longPath; ShortPath = $shortPath }
                }
            }
            finally {
                if ($null -ne $folder) { $null = [Runtime.InteropServices.Marshal]::ReleaseComObject($folder) }
            }
        }
    }
    finally {
        if ($null -ne $fileSystem) { $null = [Runtime.InteropServices.Marshal]::ReleaseComObject($fileSystem) }
    }
}

Describe 'Canonical CI preparation path containment' {
    BeforeEach {
        $fixtureRoot = (New-Item -ItemType Directory -Path (Join-Path $TestDrive ([guid]::NewGuid().ToString('N')))).FullName
        $sourceRoot = Join-Path $fixtureRoot 'source'
        $productRoot = Join-Path $sourceRoot 'x64\Debug'
        $outputRoot = Join-Path $fixtureRoot 'output\bundle'
        $workRoot = Join-Path $fixtureRoot 'work\audit'
        $Platform = 'x64'
        $null = New-Item -ItemType Directory -Path $productRoot -Force
        Set-Content -LiteralPath (Join-Path $productRoot 'PowerToys.MouseWithoutBorders.dll') -Value 'inert fixture'
    }

    It 'returns canonical identities without creating output or work directories' {
        $result = Get-MwbCiPreparationPaths $productRoot $outputRoot $workRoot $sourceRoot
        $result.ProductRoot | Should Be (Get-Item -LiteralPath $productRoot).FullName
        $result.SourceRoot | Should Be (Get-Item -LiteralPath $sourceRoot).FullName
        $result.OutputRoot | Should Be $outputRoot
        $result.WorkRoot | Should Be $workRoot
        Test-Path -LiteralPath $outputRoot | Should Be $false
        Test-Path -LiteralPath $workRoot | Should Be $false
    }

    It 'accepts distinct sibling names rather than comparing raw string prefixes' {
        $result = Get-MwbCiPreparationPaths $productRoot (Join-Path $fixtureRoot 'bundle') `
            (Join-Path $fixtureRoot 'bundle-work') $sourceRoot
        $result.WorkRoot | Should Be (Join-Path $fixtureRoot 'bundle-work')
    }

    It 'requires the filesystem provider for every identity' -TestCases @(
        @{ Parameter = 'ProductRoot' }; @{ Parameter = 'SourceRoot' }
        @{ Parameter = 'OutputRoot' }; @{ Parameter = 'WorkRoot' }
    ) {
        param($Parameter)
        $parameters = @{ ProductRoot = $productRoot; SourceRoot = $sourceRoot; OutputRoot = $outputRoot; WorkRoot = $workRoot }
        $parameters[$Parameter] = 'Env:PATH'
        { Get-MwbCiPreparationPaths @parameters } | Should Throw 'FileSystem provider'
    }

    It 'rejects a missing descendant with a file instead of a directory ancestor' {
        $ancestor = Join-Path $fixtureRoot 'file'
        Set-Content -LiteralPath $ancestor -Value 'inert fixture'
        { Resolve-MwbCiFileSystemPath (Join-Path $ancestor 'missing\bundle') -AllowMissing } |
            Should Throw 'non-directory ancestor'
    }

    It 'normalizes product, source and missing destinations through a real PSDrive' {
        $driveName = 'MwbCi' + [guid]::NewGuid().ToString('N')
        $null = New-PSDrive -Name $driveName -PSProvider FileSystem -Root $fixtureRoot
        try {
            $result = Get-MwbCiPreparationPaths "${driveName}:\source\x64\Debug" "${driveName}:\output\bundle" `
                "${driveName}:\work\audit" "${driveName}:\source"
            $result.ProductRoot | Should Be $productRoot
            $result.SourceRoot | Should Be $sourceRoot
            $result.OutputRoot | Should Be $outputRoot
            $result.WorkRoot | Should Be $workRoot
        }
        finally { Remove-PSDrive -Name $driveName }
    }

    It 'rejects product containment through mixed PSDrive/native aliases' -TestCases @(
        @{ SourceAlias = $true; DestinationAlias = $false }
        @{ SourceAlias = $false; DestinationAlias = $true }
        @{ SourceAlias = $true; DestinationAlias = $true }
    ) {
        param($SourceAlias, $DestinationAlias)
        $driveName = 'MwbCi' + [guid]::NewGuid().ToString('N')
        $null = New-PSDrive -Name $driveName -PSProvider FileSystem -Root $productRoot
        try {
            $product = if ($SourceAlias) { "${driveName}:\" } else { $productRoot }
            $destination = if ($DestinationAlias) { "${driveName}:\missing\bundle" } else { Join-Path $productRoot 'missing\bundle' }
            { Get-MwbCiPreparationPaths $product $destination $workRoot $sourceRoot } | Should Throw 'outside the original product'
            { Get-MwbCiPreparationPaths $product $outputRoot $destination $sourceRoot } | Should Throw 'outside the original product'
        }
        finally { Remove-PSDrive -Name $driveName }
    }

    It 'rejects source-checkout containment through a PSDrive alias' {
        $driveName = 'MwbCi' + [guid]::NewGuid().ToString('N')
        $null = New-PSDrive -Name $driveName -PSProvider FileSystem -Root $sourceRoot
        try {
            $nested = Join-Path $sourceRoot 'missing\audit'
            { Get-MwbCiPreparationPaths $productRoot $outputRoot $nested "${driveName}:\" } |
                Should Throw 'outside the source checkout'
            { Get-MwbCiPreparationPaths $productRoot "${driveName}:\missing\bundle" $workRoot $sourceRoot } |
                Should Throw 'outside the source checkout'
        }
        finally { Remove-PSDrive -Name $driveName }
    }

    It 'rejects overlapping new bundle/work paths through PSDrive aliases' -TestCases @(
        @{ Output = 'missing\bundle'; Work = 'missing\bundle' }
        @{ Output = 'missing\bundle'; Work = 'missing\bundle\work' }
        @{ Output = 'missing\bundle\output'; Work = 'missing\bundle' }
    ) {
        param($Output, $Work)
        $driveName = 'MwbCi' + [guid]::NewGuid().ToString('N')
        $null = New-PSDrive -Name $driveName -PSProvider FileSystem -Root $fixtureRoot
        try {
            { Get-MwbCiPreparationPaths $productRoot (Join-Path $fixtureRoot $Output) "${driveName}:\$Work" $sourceRoot } |
                Should Throw 'outside the source checkout and published bundle'
        }
        finally { Remove-PSDrive -Name $driveName }
    }

    It 'rejects real DOS long/short product and source containment before preparation' {
        $alias = Get-CiShortPathFixture
        if ($null -eq $alias) {
            Set-TestInconclusive 'No existing DOS short-path alias is available on this host; deterministic PSDrive coverage remains active.'
            return
        }
        foreach ($shortSource in @($true, $false)) {
            $source = if ($shortSource) { $alias.ShortPath } else { $alias.LongPath }
            $destinationRoot = if ($shortSource) { $alias.LongPath } else { $alias.ShortPath }
            $destination = Join-Path $destinationRoot ("mwb-ci-missing-" + [guid]::NewGuid().ToString('N') + '\deeper')
            { Get-MwbCiPreparationPaths $source $destination $workRoot $sourceRoot } | Should Throw 'outside the original product'
            { Get-MwbCiPreparationPaths $source $outputRoot $destination $sourceRoot } | Should Throw 'outside the original product'
            { Get-MwbCiPreparationPaths $productRoot $outputRoot $destination $source } | Should Throw 'outside the source checkout'
            { Get-MwbCiPreparationPaths $productRoot $destination $workRoot $source } | Should Throw 'outside the source checkout'
            Test-Path -LiteralPath $destination | Should Be $false
        }
    }

    It 'rejects real DOS aliases of identical or nested missing bundle/work directories' {
        $alias = Get-CiShortPathFixture
        if ($null -eq $alias) {
            Set-TestInconclusive 'No existing DOS short-path alias is available on this host; deterministic PSDrive coverage remains active.'
            return
        }
        $leaf = 'mwb-ci-missing-' + [guid]::NewGuid().ToString('N')
        $longOutput = Join-Path $alias.LongPath $leaf
        $shortOutput = Join-Path $alias.ShortPath $leaf
        foreach ($pair in @(
            @($longOutput, $shortOutput),
            @($longOutput, (Join-Path $shortOutput 'work')),
            @((Join-Path $longOutput 'output'), $shortOutput))) {
            { Get-MwbCiPreparationPaths $productRoot $pair[0] $pair[1] $sourceRoot } |
                Should Throw 'outside the source checkout and published bundle'
        }
        Test-Path -LiteralPath $longOutput | Should Be $false
    }
}

Describe 'MWB CI manifest and initializer agreement' {
    BeforeAll {
        $tokens = $null
        $errors = $null
        $ci = [Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $PSScriptRoot '..\mwbSandboxExperiment.ps1'), [ref]$tokens, [ref]$errors)
        if ($errors.Count) { throw 'CI manifest script contains parse errors.' }
        $assignment = $ci.Find({
            param($node)
            $node -is [Management.Automation.Language.AssignmentStatementAst] -and
                $node.Left.Extent.Text -ceq '$previewPath'
        }, $true)
        $script:legacyPreviewManifest = [scriptblock]::Create($assignment.Extent.Text + @'

@{ SandboxBackend = 'Legacy'; SandboxWinAppPath = $previewPath } | ConvertTo-Json | ConvertFrom-Json
'@)
        $guard = $ci.Find({
            param($node)
            $node -is [Management.Automation.Language.IfStatementAst] -and
                $node.Clauses[0].Item1.Extent.Text -match '\$marker\.SandboxBackend'
        }, $true)
        $script:backendGuard = [scriptblock]::Create('param($marker,$manifest)' + "`n" + $guard.Extent.Text)
        $initializer = [Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $PSScriptRoot '..\..\src\modules\MouseWithoutBorders\Tests\SandboxExperiment\Initialize-AutonomousHost.ps1'),
            [ref]$tokens, [ref]$errors)
        if ($errors.Count) { throw 'Initializer parameter contract contains parse errors.' }
        $script:bindInitializer = [scriptblock]::Create($initializer.ParamBlock.Extent.Text + @'

@{ SandboxBackend = $SandboxBackend; SandboxWinAppPath = $SandboxWinAppPath } | ConvertTo-Json | ConvertFrom-Json
'@)
    }

    It 'round-trips the Legacy optional preview path consistently without provisioning' {
        $manifest = & $script:legacyPreviewManifest
        $marker = & $script:bindInitializer -ProductRoot 'C:\fixture' -TestUser 'VM\standard' -GuestArchivePath 'C:\fixture.zip'
        $manifest.SandboxWinAppPath.GetType().FullName | Should Be 'System.String'
        $marker.SandboxWinAppPath.GetType().FullName | Should Be 'System.String'
        { & $script:backendGuard $marker $manifest } | Should Not Throw
    }

    It 'accepts the exact modern path and still rejects changed identities' {
        $manifest = @{ SandboxBackend = 'WinApp'; SandboxWinAppPath = 'C:\preview\winapp.exe' } |
            ConvertTo-Json | ConvertFrom-Json
        $marker = & $script:bindInitializer -ProductRoot 'C:\fixture' -TestUser 'VM\standard' `
            -GuestArchivePath 'C:\fixture.zip' -SandboxBackend WinApp -SandboxWinAppPath $manifest.SandboxWinAppPath
        { & $script:backendGuard $marker $manifest } | Should Not Throw
        $marker.SandboxWinAppPath = 'C:\different\winapp.exe'
        { & $script:backendGuard $marker $manifest } | Should Throw 'changed after preparation'
        $marker.SandboxWinAppPath = $manifest.SandboxWinAppPath
        $marker.SandboxBackend = 'Legacy'
        { & $script:backendGuard $marker $manifest } | Should Throw 'changed after preparation'
    }
}

Describe 'Explicit MWB CI hybrid backend selection' {
    It 'retains Win10 Legacy while selecting modern only for the Win11 x64/ARM64 jobs' -TestCases @(
        @{ Platform = 'x64Win10'; Build = 19044; Expected = 'Legacy' }
        @{ Platform = 'x64Win10'; Build = 19045; Expected = 'Legacy' }
        @{ Platform = 'x64Win11'; Build = 26100; Expected = 'WinApp' }
        @{ Platform = 'x64Win11'; Build = 26200; Expected = 'WinApp' }
        @{ Platform = 'arm64'; Build = 26100; Expected = 'WinApp' }
        @{ Platform = 'arm64'; Build = 26200; Expected = 'WinApp' }
    ) {
        param($Platform, $Build, $Expected)
        Get-MwbCiBackend $Platform $Build | Should Be $Expected
    }

    It 'blocks missing modern OS prerequisites and mismatched images without fallback' -TestCases @(
        @{ Platform = 'x64Win11'; Build = 22631 }
        @{ Platform = 'x64Win11'; Build = 19045 }
        @{ Platform = 'x64Win10'; Build = 26100 }
        @{ Platform = 'arm64'; Build = 22631 }
        @{ Platform = 'arm64'; Build = 19045 }
        @{ Platform = 'arm64Win10'; Build = 19044 }
        @{ Platform = ''; Build = 26100 }
    ) {
        param($Platform, $Build)
        { Get-MwbCiBackend $Platform $Build } | Should Throw 'BLOCKED_INFRASTRUCTURE'
    }
}

Describe 'Explicit MWB CI test-platform architecture mapping' {
    It 'maps each test platform to its unambiguous build architecture' -TestCases @(
        @{ Platform = 'x64Win10'; Expected = 'x64' }
        @{ Platform = 'x64Win11'; Expected = 'x64' }
        @{ Platform = 'arm64'; Expected = 'arm64' }
    ) {
        param($Platform, $Expected)
        Get-MwbCiArchitecture $Platform | Should Be $Expected
    }

    It 'rejects unrecognized test platforms rather than guessing an architecture' -TestCases @(
        @{ Platform = 'arm64Win10' }; @{ Platform = 'arm64Win11' }; @{ Platform = 'x86' }; @{ Platform = '' }
    ) {
        param($Platform)
        { Get-MwbCiArchitecture $Platform } | Should Throw 'BLOCKED_INFRASTRUCTURE'
    }
}

Describe 'Native CI compiler preparation command streams' {
    BeforeEach {
        $WorkRoot = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $directory = Join-Path $WorkRoot 'command'
        $null = New-Item -ItemType Directory -Path $directory, (Join-Path $WorkRoot 'scratch')
        $DotNetPath = (Get-Process -Id $PID).Path
        $powerShell = $DotNetPath
        $log = Join-Path $WorkRoot 'command.log'
    }

    It 'returns stdout only from a real native command while logging both streams' {
        $command = "[Console]::Out.Write('machine-identity'); [Console]::Error.Write('diagnostic-only'); exit 0"
        $result = Invoke-MwbCiBuildCommand $powerShell @('-NoLogo', '-NoProfile', '-NonInteractive', '-Command', $command) `
            $directory $log 30
        $result | Should Be 'machine-identity'
        $text = Get-Content -LiteralPath $log -Raw
        $text | Should Match '\[stdout\]\r?\nmachine-identity'
        $text | Should Match '\[stderr\]\r?\ndiagnostic-only'
    }

    It 'does not turn stderr-only output into a machine identity' {
        $command = "[Console]::Error.Write('diagnostic-only'); exit 0"
        $result = Invoke-MwbCiBuildCommand $powerShell @('-NoLogo', '-NoProfile', '-NonInteractive', '-Command', $command) `
            $directory $log 30
        $result | Should BeNullOrEmpty
        Get-Content -LiteralPath $log -Raw | Should Match 'diagnostic-only'
    }

    It 'retains both native streams before rejecting a nonzero exit code' {
        $command = "[Console]::Out.Write('stdout evidence'); [Console]::Error.Write('stderr evidence'); exit 37"
        {
            Invoke-MwbCiBuildCommand $powerShell @('-NoLogo', '-NoProfile', '-NonInteractive', '-Command', $command) `
                $directory $log 30
        } | Should Throw 'exit 37'
        $text = Get-Content -LiteralPath $log -Raw
        $text | Should Match 'stdout evidence'
        $text | Should Match 'stderr evidence'
    }

}

Describe 'Build-only pinned SDK compiler resolution' {
    BeforeEach {
        $directory = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $DotNetPath = Join-Path $TestDrive 'fixture-sdk\dotnet.exe'
        $savedNuGetPackages = $env:NUGET_PACKAGES
        $env:NUGET_PACKAGES = Join-Path $TestDrive ("fixture-package-cache-" + [guid]::NewGuid().ToString('N'))
        Mock Invoke-MwbCiBuildCommand {
            param($Executable, $Arguments, $Directory)
            $compiler = Join-Path $Directory 'packages\microsoft.netcore.app.crossgen2.win-x64\10.0.12\tools\crossgen2.exe'
            $null = New-Item -ItemType Directory -Path (Split-Path $compiler) -Force
            Set-Content -LiteralPath $compiler -Value 'nonexecutable compiler fixture'
        }
    }

    AfterEach {
        $env:NUGET_PACKAGES = $savedNuGetPackages
    }

    It 'reuses an exact already-restored native SDK compiler without downloading again' {
        $nativeRoot = Join-Path $env:NUGET_PACKAGES 'microsoft.netcore.app.crossgen2.win-x64\10.0.12\tools'
        $null = New-Item -ItemType Directory -Path $nativeRoot -Force
        foreach ($name in @('crossgen2.exe', 'jitinterface_x64.dll', 'clrjit_win_x64_x64.dll')) {
            Set-Content -LiteralPath (Join-Path $nativeRoot $name) -Value 'nonexecutable compiler fixture'
        }
        Get-MwbCiCrossgen2 '10.0.12' $directory | Should Be (Join-Path $nativeRoot 'crossgen2.exe')
        Assert-MockCalled Invoke-MwbCiBuildCommand -Times 0 -Exactly -Scope It
        Test-Path -LiteralPath $directory | Should Be $false
    }

    It 'reuses the same host x64 compiler package for an ARM64 cross-compilation target' {
        $nativeRoot = Join-Path $env:NUGET_PACKAGES 'microsoft.netcore.app.crossgen2.win-x64\10.0.12\tools'
        $null = New-Item -ItemType Directory -Path $nativeRoot -Force
        foreach ($name in @('crossgen2.exe', 'jitinterface_x64.dll', 'clrjit_universal_arm64_x64.dll')) {
            Set-Content -LiteralPath (Join-Path $nativeRoot $name) -Value 'nonexecutable compiler fixture'
        }
        Get-MwbCiCrossgen2 '10.0.12' $directory -TargetArchitecture arm64 | Should Be (Join-Path $nativeRoot 'crossgen2.exe')
        Assert-MockCalled Invoke-MwbCiBuildCommand -Times 0 -Exactly -Scope It
        Test-Path -LiteralPath $directory | Should Be $false
    }

    It 'does not reuse an x64-only staged compiler for an ARM64 target missing its universal JIT' {
        $nativeRoot = Join-Path $env:NUGET_PACKAGES 'microsoft.netcore.app.crossgen2.win-x64\10.0.12\tools'
        $null = New-Item -ItemType Directory -Path $nativeRoot -Force
        foreach ($name in @('crossgen2.exe', 'jitinterface_x64.dll', 'clrjit_win_x64_x64.dll')) {
            Set-Content -LiteralPath (Join-Path $nativeRoot $name) -Value 'nonexecutable compiler fixture'
        }
        Get-MwbCiCrossgen2 '10.0.12' $directory -TargetArchitecture arm64 | Should Not Be (Join-Path $nativeRoot 'crossgen2.exe')
        Assert-MockCalled Invoke-MwbCiBuildCommand -Times 1 -Exactly -Scope It
    }

    It 'restores only the exact SDK native Crossgen2 package using NuGet' {
        $compiler = Get-MwbCiCrossgen2 '10.0.12' $directory
        $compiler | Should Match 'crossgen2\.win-x64\\10\.0\.12\\tools\\crossgen2\.exe$'
        Get-Content -LiteralPath (Join-Path $directory 'compiler.csproj') -Raw |
            Should Match 'PackageDownload Include="Microsoft.NETCore.App.Crossgen2.win-x64" Version="\[10.0.12\]"'
        Get-Content -LiteralPath (Join-Path $directory 'NuGet.Config') -Raw |
            Should Match '<clear />.*https://api.nuget.org/v3/index.json'
        Assert-MockCalled Invoke-MwbCiBuildCommand -Times 1 -Exactly -Scope It -ParameterFilter {
            $Arguments[0] -eq 'restore' -and $Arguments -contains '--configfile' -and $Arguments -contains '--packages'
        }
    }

    It 'rejects floating or injected package versions before touching NuGet' -TestCases @(
        @{ Version = 'latest' }; @{ Version = '10.0.*' }; @{ Version = '10.0.12" />' }
    ) {
        param($Version)
        { Get-MwbCiCrossgen2 $Version $directory } | Should Throw 'exact stable runtime'
        Assert-MockCalled Invoke-MwbCiBuildCommand -Times 0 -Exactly -Scope It
        Test-Path -LiteralPath $directory | Should Be $false
    }

    It 'does not silently substitute a different compiler when a package is incomplete' {
        Mock Invoke-MwbCiBuildCommand {}
        { Get-MwbCiCrossgen2 '10.0.12' $directory } | Should Throw 'did not contain'
    }

    It 'pins the actual serviced CoreLib version rather than an older runtimeconfig declaration' {
        Mock Get-MwbCiFileVersion {
            [pscustomobject]@{ ProductVersion = '10.0.12-servicing.26422.108+' + ('a' * 40) }
        }
        Get-MwbCiRuntimeVersion $TestDrive | Should Be '10.0.12'
        Assert-MockCalled Get-MwbCiFileVersion -Times 1 -Exactly -Scope It -ParameterFilter {
            $Path -like '*\System.Private.CoreLib.dll'
        }
    }

    It 'rejects nonsemantic file-version resources rather than guessing a compiler version' {
        Mock Get-MwbCiFileVersion {
            [pscustomobject]@{ ProductVersion = '10,0,1226,42308 @Commit: ' + ('a' * 40) }
        }
        { Get-MwbCiRuntimeVersion $TestDrive } | Should Throw 'identifiable self-contained CLR'
    }
}

Describe 'Portable release native architecture validation' {
    BeforeEach {
        $image = Join-Path $TestDrive 'native-fixture.bin'
        $bytes = [byte[]]::new(1024)
        [BitConverter]::GetBytes([uint16]0x5A4D).CopyTo($bytes, 0)
        [BitConverter]::GetBytes([int]128).CopyTo($bytes, 0x3C)
        [BitConverter]::GetBytes([uint32]0x4550).CopyTo($bytes, 128)
        [BitConverter]::GetBytes([uint16]0x8664).CopyTo($bytes, 132)
        [BitConverter]::GetBytes([uint16]0x20B).CopyTo($bytes, 152)
        [IO.File]::WriteAllBytes($image, $bytes)
    }

    It 'accepts only an x64 PE32+ image without a CLR directory' {
        { Assert-WinAppCliNativeFile $image } | Should Not Throw
    }

    It 'rejects ARM64 output even when named winapp.exe and x64 is expected' {
        [BitConverter]::GetBytes([uint16]0xAA64).CopyTo($bytes, 132)
        [IO.File]::WriteAllBytes($image, $bytes)
        { Assert-WinAppCliNativeFile $image } | Should Throw 'not native x64'
    }

    It 'rejects a managed executable rather than requiring a test-host SDK' {
        [BitConverter]::GetBytes([uint64]1).CopyTo($bytes, (128 + 24 + 112 + (14 * 8)))
        [IO.File]::WriteAllBytes($image, $bytes)
        { Assert-WinAppCliNativeFile $image } | Should Throw 'managed runtime'
    }

    It 'accepts a native ARM64 PE32+ image without a CLR directory when ARM64 is requested' {
        [BitConverter]::GetBytes([uint16]0xAA64).CopyTo($bytes, 132)
        [IO.File]::WriteAllBytes($image, $bytes)
        { Assert-WinAppCliNativeFile $image -Platform arm64 } | Should Not Throw
    }

    It 'rejects x64 output when native ARM64 is expected' {
        { Assert-WinAppCliNativeFile $image -Platform arm64 } | Should Throw 'not native arm64'
    }

    It 'rejects the ARM64EC hybrid machine type when native ARM64 is expected' {
        [BitConverter]::GetBytes([uint16]0xA641).CopyTo($bytes, 132)
        [IO.File]::WriteAllBytes($image, $bytes)
        { Assert-WinAppCliNativeFile $image -Platform arm64 } | Should Throw 'not native arm64'
    }
}

Describe 'MWB CI bundle provenance and coherent host staging' {
    BeforeEach {
        $root = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $fixture = New-CiBundleFixture $root
        $bundleRoot = Join-Path $root 'mwb-sandbox-ci'
        $manifestPath = Join-Path $bundleRoot 'manifest.json'
    }

    It 'accepts the pinned Debug build and extracts host files from the exact guest archive' {
        $bundle = Get-MwbCiBundle $root ('a' * 40)
        $hostRoot = Join-Path $root 'private-host'
        Expand-MwbCiRuntime (Join-Path $bundle.Root 'runtime.zip') $hostRoot $bundle.Manifest.Runtime.Files
        foreach ($entry in $fixture.Runtime.Files) {
            (Get-FileHash -LiteralPath (Join-Path $hostRoot $entry.Path)).Hash | Should Be $entry.Sha256
            (Get-FileHash -LiteralPath (Join-Path $root "fixture-files\$($entry.Path)")).Hash | Should Be $entry.Sha256
        }
        (Get-FileHash -LiteralPath (Join-Path $bundle.Root 'runtime.zip')).Hash | Should Be $fixture.Runtime.Sha256
    }

    It 'accepts an explicitly requested ARM64 bundle when the manifest actually declares ARM64' {
        $fixture.Platform = 'arm64'
        $pin = Get-WinAppCliRelease -Platform arm64
        $fixture.WinAppCli.Asset = $pin.Asset
        $fixture.WinAppCli.Sha256 = $pin.Sha256
        $fixture | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath
        (Get-MwbCiBundle $root ('a' * 40) -Platform arm64).Manifest.Platform | Should Be 'arm64'
    }

    It 'rejects an x64 CLI asset in an otherwise ARM64 bundle' {
        $fixture.Platform = 'arm64'
        $fixture | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath
        { Get-MwbCiBundle $root ('a' * 40) -Platform arm64 } | Should Throw 'provenance'
    }

    It 'rejects the old private-preview bundle format' {
        $fixture.FormatVersion = 1
        $fixture | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath
        { Get-MwbCiBundle $root ('a' * 40) } | Should Throw 'provenance'
    }

    It 'rejects a Platform mismatch between the request and the actual manifest in either direction' -TestCases @(
        @{ ManifestPlatform = 'arm64'; RequestedPlatform = 'x64' }
        @{ ManifestPlatform = 'x64'; RequestedPlatform = 'arm64' }
    ) {
        param($ManifestPlatform, $RequestedPlatform)
        $fixture.Platform = $ManifestPlatform
        $fixture | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath
        { Get-MwbCiBundle $root ('a' * 40) -Platform $RequestedPlatform } | Should Throw 'provenance'
    }

    It 'blocks an old artifact without build preparation rather than repackaging on the test agent' {
        Remove-Item -LiteralPath $manifestPath
        { Get-MwbCiBundle $root ('a' * 40) } | Should Throw 'BLOCKED_INFRASTRUCTURE'
    }

    It 'rejects the wrong source revision' {
        { Get-MwbCiBundle $root ('b' * 40) } | Should Throw 'provenance'
    }

    It 'rejects Release or ARM artifacts even when their contents hash correctly' -TestCases @(
        @{ Field = 'Configuration'; Value = 'Release' }
        @{ Field = 'Platform'; Value = 'arm64' }
    ) {
        param($Field, $Value)
        $fixture[$Field] = $Value
        $fixture | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath
        { Get-MwbCiBundle $root ('a' * 40) } | Should Throw 'provenance'
    }

    It 'rejects changed official release provenance' -TestCases @(
        @{ Field = 'Repository' }; @{ Field = 'Tag' }; @{ Field = 'Sha256' }
        @{ Field = 'Version' }; @{ Field = 'Asset' }
    ) {
        param($Field)
        $fixture.WinAppCli[$Field] = 'changed'
        $fixture | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath
        { Get-MwbCiBundle $root ('a' * 40) } | Should Throw 'provenance'
    }

    It 'rejects tampered archive, packager provenance, and portable files' -TestCases @(
        @{ File = 'runtime.zip' }; @{ File = 'runtime.zip.manifest.json' }
        @{ File = 'winapp-cli\winapp.exe' }; @{ File = 'winapp-cli\libSkiaSharp.dll' }
    ) {
        param($File)
        Add-Content -LiteralPath (Join-Path $bundleRoot $File) -Value 'tampered'
        { Get-MwbCiBundle $root ('a' * 40) } | Should Throw 'hash mismatch'
    }

    It 'rejects unmanifested CLI state or credentials' {
        Set-Content -LiteralPath (Join-Path $bundleRoot 'winapp-cli\unexpected.json') -Value '{}'
        { Get-MwbCiBundle $root ('a' * 40) } | Should Throw 'unmanifested'
    }

    It 'requires the packager R2R verification manifest when R2R is selected' {
        $fixture.Runtime.ReadyToRun = $true
        $fixture | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath
        { Get-MwbCiBundle $root ('a' * 40) } | Should Throw 'runtime archive provenance'
    }

    It 'requires verified R2R output hashes to agree with every identical staged copy' -TestCases @(
        @{ Tampered = $false }; @{ Tampered = $true }
    ) {
        param($Tampered)
        $runtimePath = Join-Path $bundleRoot 'runtime.zip.manifest.json'
        $runtime = Get-Content -LiteralPath $runtimePath -Raw | ConvertFrom-Json
        $entry = @($fixture.Runtime.Files | Where-Object Path -EQ 'PowerToys.MouseWithoutBorders.dll')[0]
        $verification = @{
            FormatVersion = 1; ManagedILAndAttributesUnchanged = $true; CompiledAssemblies = 1
            Compiler = @{ Files = @('crossgen2', 'jitinterface', 'clrjit') }
            Assemblies = @(@{
                Paths = @($entry.Path); OutputSha256 = $(if ($Tampered) { '0' * 64 } else { $entry.Sha256 })
            })
        }
        $runtime | Add-Member -NotePropertyName ReadyToRun -NotePropertyValue $verification
        $runtime | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $runtimePath
        $fixture.Runtime.ManifestSha256 = (Get-FileHash -LiteralPath $runtimePath).Hash
        $fixture.Runtime.ReadyToRun = $true
        $fixture | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath
        if ($Tampered) {
            { Get-MwbCiBundle $root ('a' * 40) } | Should Throw 'output hashes disagree'
        } else {
            (Get-MwbCiBundle $root ('a' * 40)).Manifest.Runtime.ReadyToRun | Should Be $true
        }
    }

    It 'refuses to overwrite an existing host product' {
        $hostRoot = Join-Path $root 'fixture-files'
        { Expand-MwbCiRuntime (Join-Path $bundleRoot 'runtime.zip') $hostRoot $fixture.Runtime.Files } |
            Should Throw 'new private directory'
    }

    It 'rejects missing archive members before staging any host files' {
        $hostRoot = Join-Path $root 'private-host'
        $entries = @($fixture.Runtime.Files) + @{ Path = 'missing.dll'; Sha256 = 'A' * 64 }
        { Expand-MwbCiRuntime (Join-Path $bundleRoot 'runtime.zip') $hostRoot $entries } |
            Should Throw 'incomplete'
        Test-Path -LiteralPath $hostRoot | Should Be $false
    }

    It 'rejects unexpected zip members before staging any host files' {
        $hostRoot = Join-Path $root 'private-host'
        $entries = @($fixture.Runtime.Files | Select-Object -Skip 1)
        { Expand-MwbCiRuntime (Join-Path $bundleRoot 'runtime.zip') $hostRoot $entries } |
            Should Throw 'unexpected'
        Test-Path -LiteralPath $hostRoot | Should Be $false
    }

    It 'rejects path escapes, streams, and DOS aliases' -TestCases @(
        @{ Path = '..\other.exe' }; @{ Path = 'C:\other.exe' }; @{ Path = '\\server\other.exe' }
        @{ Path = 'file.dll:stream' }; @{ Path = '.\file.dll' }; @{ Path = 'sub\..\file.dll' }
        @{ Path = 'file.dll.' }; @{ Path = 'directory \file.dll' }
    ) {
        param($Path)
        { Get-MwbCiRelativePath $root $Path } | Should Throw 'relative file paths'
    }
}

Describe 'MWB gated preparation source contracts' {
    BeforeEach {
        $script = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\mwbSandboxExperiment.ps1') -Raw
        $build = $buildAst.Extent.Text
        $templates = Join-Path $PSScriptRoot '..\v2\templates'
        $job = Get-Content -LiteralPath (Join-Path $templates 'job-build-project.yml') -Raw
        $testJob = Get-Content -LiteralPath (Join-Path $templates 'job-test-project.yml') -Raw
    }

    It 'uses the shared official dependency without private source builds or patches' {
        $build | Should Match 'Save-WinAppCliRelease'
        $build | Should Not Match 'git.exe|GitOptions|SdkVersion|NativeAOT|vcvars|dotnet publish|PatchSha256|preview-source'
        Test-Path -LiteralPath (Join-Path $PSScriptRoot '..\mwb-sandbox-preview\source.json') | Should Be $false
        Test-Path -LiteralPath (Join-Path $PSScriptRoot '..\mwb-sandbox-preview\firewall-query.patch') | Should Be $false
    }

    It 'defaults to no preparation for existing build callers' {
        $job | Should Match 'name: mwbSandboxExperiment\r?\n    type: boolean\r?\n    default: false'
        $job | Should Match '\$\{\{ if eq\(parameters.mwbSandboxExperiment, true\) \}\}:\r?\n    - pwsh:'
        $job | Should Match '-SourceRevision "\$\(Build.SourceVersion\)" -ReadyToRun'
        $job | Should Not Match '10\.0\.401|mwb-ci-tools|UseDotNet@2'
        $job | Should Match '-DotNetPath "\$\(DOTNET_ROOT\)\\dotnet.exe"'
        $job | Should Match "version: '10.0'"
        $buildAst.ParamBlock.Extent.Text | Should Match '\[switch\] \$ReadyToRun'
        $buildAst.ParamBlock.Extent.Text | Should Match "ValidateSet\('x64', 'arm64'\).*Platform = 'x64'"
    }

    It 'prepares the compiler and protected CLI only in BUILD and retains the ordinary artifact identity' {
        $job | Should Match 'JobOutputArtifactName: build-\$\(BuildPlatform\)-\$\(BuildConfiguration\)'
        $job | Should Match 'buildInstallers.*'
        $script | Should Not Match 'New-MwbRuntimeArchive|dotnet (restore|publish)|Invoke-WebRequest'
        $build | Should Match 'outside the source checkout and published bundle'
        $build | Should Match 'Remove-Item -LiteralPath "\$archive.staging"'
    }

    It 'uses the same lean archive for a protected host and guest without modifying original outputs' {
        $script | Should Match 'Copy-Item -LiteralPath \(Join-Path \$bundle.Root ''runtime.zip''\) -Destination \$guestArchive'
        $script | Should Match 'Expand-MwbCiRuntime -Archive \$guestArchive -Destination \$hostProductRoot'
        $script | Should Match 'Assert-MwbProtectedDirectory \$hostProductRoot'
        $script | Should Match 'Protect-MwbCiPayloadTree \$hostProductRoot'
        $script | Should Match "\`$acl.SetOwner\(\`$owner\)"
        $script | Should Match 'SourceProductRoot = \$payload.ProductRoot'
        $script | Should Match '\$env:POWERTOYS_INSTALL_DIR = \$manifest.HostProductRoot'
        $script | Should Match 'Read-MwbProvisioningMarker \$manifest.HostProductRoot'
    }

    It 'passes the selected backend and a separate protected official CLI to the initializer' {
        $script | Should Match "Get-MwbCiBackend \`$Platform \`$windowsBuild"
        $script | Should Match '-SandboxBackend'
        $script | Should Match '-SandboxWinAppPath'
        $script | Should Match 'SandboxBackend = \$backend'
        $script | Should Not Match '\$env:WINAPP_CLI_PATH\s*='
        $testJob | Should Match '\.pipelines\\InstallWinAppCli.ps1'
        Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\InstallWinAppCli.ps1') -Raw | Should Match 'Save-WinAppCliRelease'
        $script | Should Match '\$bundle.Manifest.WinAppCli.Files'
    }

    It 'blocks disabled features without attempting OS installation or silently accepting Enabled as modern readiness' {
        $script | Should Match 'Get-WindowsOptionalFeature -Online'
        $script | Should Match 'BLOCKED_INFRASTRUCTURE: Sandbox feature'
        $script | Should Match 'Enabled feature state alone is not readiness'
        $script | Should Not Match '(?m)^\s*(Enable-WindowsOptionalFeature|Restart-Computer|Set-ExecutionPolicy)\b|Start-Process.*-Verb RunAs'
        $testJob | Should Match "parameters.useLatestWebView2 \}\}' -ne 'False'"
        $backend = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\..\src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests\WinAppSandbox.cs') -Raw
        $backend | Should Match 'FindPackagesForUser\(string.Empty, SandboxPackageFamily\)'
        $backend | Should Match '\["--version"\]'
        $backend | Should Match 'MicrosoftWindows.WindowsSandbox_cw5n1h2txyewy'
    }

    It 'retains the existing bounded Limited desktop dispatch and recovery' {
        $script | Should Match "\`$suiteTimeoutMinutes = if \(\`$manifest.SandboxBackend -eq 'WinApp'\) \{ 80 \} else \{ 45 \}"
        $script | Should Match '-TimeoutMinutes \$suiteTimeoutMinutes'
        $script | Should Match "\`$task.Principal.RunLevel -ne 'Limited'"
        $script | Should Match "\`$task.Principal.LogonType -ne 'Interactive'"
        $script | Should Match '-MwbRecoveryJournal \$journalPath -TimeoutMinutes 4'
        $script | Should Match 'AddSeconds\(90\)'
        $script | Should Match 'New-TimeSpan -Minutes 20'
    }

    It 'writes the prerequisite report before any BLOCKED_INFRASTRUCTURE preflight throw or run-root creation' {
        $reportIndex = $script.IndexOf('Write-MwbSandboxPrerequisiteReport')
        $featureThrowIndex = $script.IndexOf('BLOCKED_INFRASTRUCTURE: Sandbox feature')
        $noUserThrowIndex = $script.IndexOf('BLOCKED_INFRASTRUCTURE: no logged-on interactive desktop user')
        $runRootIndex = $script.IndexOf('New-MwbProtectedDirectory $runRoot')
        $reportIndex | Should BeGreaterThan -1
        $reportIndex | Should BeLessThan $featureThrowIndex
        $reportIndex | Should BeLessThan $noUserThrowIndex
        $reportIndex | Should BeLessThan $runRootIndex
        $script | Should Match 'Write-MwbSandboxPrerequisiteReport -ResultsDirectory \$ResultsDirectory -RunId \$RunId'
    }

    It 'always publishes the prerequisite report artifact even when Prepare never runs' {
        $steps = Get-Content -LiteralPath (Join-Path $templates 'steps-mwb-sandbox-experiment.yml') -Raw
        $steps | Should Match 'displayName: Collect allowlisted MWB prerequisite diagnostics even without TRX\r?\n\s*condition: always\(\)'
        $steps | Should Match 'Export-MwbSandboxPrerequisites.ps1'
        $steps | Should Match 'task: PublishPipelineArtifact@1\r?\n\s*displayName: Publish MWB Sandbox prerequisite report\r?\n\s*condition: always\(\)'
        $steps | Should Match 'targetPath: ''\$\(Common.TestResultsDirectory\)\\mwb-prerequisites-\$\(System.JobId\)'''
    }
}

Describe 'MWB Sandbox prerequisite diagnostics' {
    BeforeEach {
        Mock Import-MwbAppxCompat {}
    }

    Context 'MWB admin inventory conclusions and early failure evidence' {
        BeforeEach {
            $script:allUsersInventory = @{ QueryError = $null; Packages = @() }
            $script:provisionedInventory = @{ QueryError = $null; Packages = @() }
            $script:userInventory = @{
                QueryError = $null; PackageQueryError = $null; PackageCount = 0
                UserSid = 'S-1-5-21-123-456-789-1001'
            }
            Mock Get-MwbSandboxAllUsersPackageReport { $script:allUsersInventory }
            Mock Get-MwbSandboxProvisionedPackageReport { $script:provisionedInventory }
            Mock Get-MwbSandboxInteractiveUserReport { $script:userInventory }
            Mock Get-CimInstance { [pscustomobject]@{ BuildNumber = '26100'; Version = '10.0.26100' } }
            Mock Get-WindowsOptionalFeature { [pscustomobject]@{ State = 'Enabled' } }
        }

        It 'distinguishes absence everywhere, other-user registration, staged-only and provisioned-only inventories' -TestCases @(
            @{ Kind = 'AbsentEverywhere' }
            @{ Kind = 'RegisteredForOtherUser' }
            @{ Kind = 'StagedOnly' }
            @{ Kind = 'ProvisionedOnly' }
            @{ Kind = 'RegisteredForTestUser' }
        ) {
            param($Kind)
            switch ($Kind) {
                'RegisteredForOtherUser' { $script:allUsersInventory.Packages = @(@{ Registrations = @(@{ InstallState = 'Installed' }) }) }
                'StagedOnly' { $script:allUsersInventory.Packages = @(@{ Registrations = @(@{ InstallState = 'Staged' }) }) }
                'ProvisionedOnly' { $script:provisionedInventory.Packages = @(@{ Version = '0.8.107.0' }) }
                'RegisteredForTestUser' { $script:userInventory.PackageCount = 1 }
            }
            $report = Get-MwbSandboxPrerequisiteReport 'x64Win11'
            $report.InventoryConclusion | Should Be $Kind
            $report.InteractiveProbe.Status | Should Be 'NotChecked'
            $report.InteractiveProbe.Reason | Should Be 'PrepareDoesNotExecuteAsTestUser'
        }

        It 'never converts a failed inventory query into confirmed package absence' -TestCases @(
            @{ Query = 'AllUsers' }; @{ Query = 'Provisioned' }; @{ Query = 'Interactive' }
        ) {
            param($Query)
            switch ($Query) {
                'AllUsers' { $script:allUsersInventory.QueryError = @{ Code = 'AllUsersPackageQueryFailed' } }
                'Provisioned' { $script:provisionedInventory.QueryError = @{ Code = 'ProvisionedPackageQueryFailed' } }
                'Interactive' { $script:userInventory.PackageQueryError = @{ Code = 'InteractiveUserPackageQueryFailed' } }
            }
            (Get-MwbSandboxPrerequisiteReport 'x64Win11').InventoryConclusion | Should Be 'UnknownQueryFailure'
        }

        It 'persists allowlisted evidence when a feature query fails before privileged setup or TRX exists' {
            Mock Get-WindowsOptionalFeature { throw 'password=do-not-publish WindowsSandboxClient --secret do-not-publish' }
            $id = [guid]::NewGuid()
            $root = (New-Item -ItemType Directory -Path (Join-Path $TestDrive ([guid]::NewGuid().ToString('N')))).FullName
            Write-MwbSandboxPrerequisiteReport -ResultsDirectory $root -RunId $id -Platform x64Win11
            $path = Join-Path $root "mwb-prerequisites-$id\prerequisite-admin.json"
            Test-Path -LiteralPath $path | Should Be $true
            $raw = Get-Content -LiteralPath $path -Raw
            $raw | Should Not Match 'do-not-publish|WindowsSandboxClient|password|--secret'
            $report = $raw | ConvertFrom-Json
            $report.SandboxFeatureQueryError.Code | Should Be 'SandboxFeatureQueryFailed'
            $report.SandboxFeatureQueryError.HResult | Should Match '^0x[0-9A-F]{8}$'
            $report.InteractiveProbe.Status | Should Be 'NotChecked'
            @(Get-ChildItem -LiteralPath $root -Filter '*.trx' -Recurse).Count | Should Be 0
        }

        It 'never uses the elevated agent to execute the interactive user alias or exposes raw package paths' {
            $common = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\MwbSandboxCi.Common.ps1') -Raw
            $diagnostics = $common.Substring($common.IndexOf('# Match the trusted family'))
            $diagnostics | Should Not Match 'Start-Process|ProcessStartInfo|Invoke-Expression|InstallLocation|AliasPath'
            $diagnostics | Should Not Match '\.Exception\.Message|Format-List|Out-String'
            $diagnostics | Should Match 'Get-MwbSandboxPackages -UserSid \$userSid.Value'
        }
    }

    It 'distinguishes a confirmed-absent all-users package inventory from a query failure' {
        Mock Get-MwbSandboxPackages { @() }
        $absent = Get-MwbSandboxAllUsersPackageReport
        $absent.QueryError | Should Be $null
        $absent.Packages.Count | Should Be 0

        Mock Get-MwbSandboxPackages { throw 'Access is denied. password=never-public' }
        $failed = Get-MwbSandboxAllUsersPackageReport
        $failed.QueryError.Code | Should Be 'AllUsersPackageQueryFailed'
        $failed.QueryError.HResult | Should Match '^0x[0-9A-F]{8}$'
        ($failed | ConvertTo-Json -Depth 5) | Should Not Match 'never-public|Access is denied'
        $failed.Packages.Count | Should Be 0
    }

    It 'reads native AppxUserSecurityId objects without exporting their account names' {
        Mock Get-MwbSandboxPackages {
            [pscustomobject]@{
                PackageFamilyName = 'MicrosoftWindows.WindowsSandbox_cw5n1h2txyewy'
                PackageFullName = 'MicrosoftWindows.WindowsSandbox_0.8.107.0_x64__cw5n1h2txyewy'
                Version = [version]'0.8.107.0'; Status = 'Ok'
                PackageUserInformation = @([pscustomobject]@{
                    UserSecurityId = [pscustomobject]@{ Sid = 'S-1-5-21-123-456-789-1001'; Username = 'private-account-name' }
                    InstallState = 'Installed'
                })
            }
        }
        $report = Get-MwbSandboxAllUsersPackageReport
        $report.QueryError | Should Be $null
        $report.Packages.Count | Should Be 1
        $report.Packages[0].Registrations[0].UserSid | Should Be 'S-1-5-21-123-456-789-1001'
        $report.Packages[0].Registrations[0].InstallState | Should Be 'Installed'
        ($report | ConvertTo-Json -Depth 6) | Should Not Match 'private-account-name|Username'
    }

    It 'reports a matched all-users package only when the family name matches exactly' {
        Mock Get-MwbSandboxPackages {
            @(
                [pscustomobject]@{
                    PackageFamilyName = 'MicrosoftWindows.WindowsSandbox_cw5n1h2txyewy'
                    PackageFullName = 'MicrosoftWindows.WindowsSandbox_1.0.0.0_x64__cw5n1h2txyewy'
                    Version = [version]'1.0.0.0'; Status = 'Ok'
                    InstallLocation = 'private-path-not-exported'
                    PackageUserInformation = @([pscustomobject]@{ UserSecurityId = 'S-1-5-21-123-456-789-1001'; InstallState = 'Installed' })
                }
                [pscustomobject]@{ PackageFamilyName = 'Unrelated_8wekyb3d8bbwe'; PackageFullName = 'Unrelated'; Version = [version]'1.0.0.0'; Status = 'Ok'; Architecture = 'X64' }
            )
        }
        $report = Get-MwbSandboxAllUsersPackageReport
        $report.QueryError | Should Be $null
        $report.Packages.Count | Should Be 1
        $report.Packages[0].PackageFullName | Should Be 'MicrosoftWindows.WindowsSandbox_1.0.0.0_x64__cw5n1h2txyewy'
        $report.Packages[0].Registrations[0].InstallState | Should Be 'Installed'
        ($report | ConvertTo-Json -Depth 8) | Should Not Match 'InstallLocation|private-path'
    }

    It 'distinguishes a confirmed-absent provisioned package inventory from a query failure' {
        Mock Get-MwbSandboxProvisionedPackages { @() }
        $absent = Get-MwbSandboxProvisionedPackageReport
        $absent.QueryError | Should Be $null
        $absent.Packages.Count | Should Be 0

        Mock Get-MwbSandboxProvisionedPackages { throw 'The requested operation requires elevation.' }
        $failed = Get-MwbSandboxProvisionedPackageReport
        $failed.QueryError.Code | Should Be 'ProvisionedPackageQueryFailed'
    }

    It 'finds provisioned packages by DisplayName and full trusted PackageName rather than a bare name' {
        Mock Get-MwbSandboxProvisionedPackages {
            @(
                [pscustomobject]@{ DisplayName = 'MicrosoftWindows.WindowsSandbox'; PackageName = 'MicrosoftWindows.WindowsSandbox_0.8.107.0_neutral_~_cw5n1h2txyewy'; Version = '0.8.107.0'; Architecture = 'Neutral' }
                [pscustomobject]@{ DisplayName = 'MicrosoftWindows.WindowsSandbox'; PackageName = 'MicrosoftWindows.WindowsSandbox_0.8.107.0_x64__cw5n1h2txyewy'; Version = '0.8.107.0'; Architecture = 'X64' }
                [pscustomobject]@{ DisplayName = 'MicrosoftWindows.WindowsSandbox'; PackageName = 'MicrosoftWindows.WindowsSandbox_0.8.107.0_x64__otherpublisher'; Version = '0.8.107.0'; Architecture = 'X64' }
                [pscustomobject]@{ DisplayName = 'MicrosoftWindows.WindowsSandboxExtra'; PackageName = 'MicrosoftWindows.WindowsSandboxExtra_0.8.107.0_x64__cw5n1h2txyewy'; Version = '0.8.107.0'; Architecture = 'X64' }
            )
        }
        $report = Get-MwbSandboxProvisionedPackageReport
        $report.QueryError | Should Be $null
        $report.Packages.Count | Should Be 2
        $report.Packages[0].PackageFamilyName | Should Be 'MicrosoftWindows.WindowsSandbox_cw5n1h2txyewy'
        $report.Packages[0].PackageFullName | Should Be 'MicrosoftWindows.WindowsSandbox_0.8.107.0_neutral_~_cw5n1h2txyewy'
        $report.Packages[1].PackageFullName | Should Be 'MicrosoftWindows.WindowsSandbox_0.8.107.0_x64__cw5n1h2txyewy'
    }

    It 'inventories the exact user by SID without executing that user alias under the admin token' {
        $realAccount = "$env:USERDOMAIN\$env:USERNAME"
        Mock Get-CimInstance {
            param($ClassName)
            if ($ClassName -ceq 'Win32_ComputerSystem') { return [pscustomobject]@{ UserName = $realAccount } }
            throw "unexpected CIM class $ClassName"
        }
        Mock Get-MwbSandboxPackages { @() }

        $noAlias = Get-MwbSandboxInteractiveUserReport
        $noAlias.QueryScope | Should Be 'ExplicitUserSid'
        $noAlias.QueryError | Should Be $null
        $noAlias.PackageCount | Should Be 0
        $noAlias.PackageAbsent | Should Be $true
        $noAlias.UserName | Should Not Match '\\'
        $noAlias.WsbVersionProbe.Status | Should Be 'NotChecked'
        $noAlias.WsbVersionProbe.Reason | Should Be 'RequiresLimitedInteractiveUser'
        $noAlias.WsbVersionProbe.EvidenceFile | Should Be 'prerequisite-user.json'
        Assert-MockCalled Get-MwbSandboxPackages -Times 1 -Exactly -ParameterFilter { $UserSid -eq $noAlias.UserSid }
    }

    It 'reports a query error, not a false absence, when the interactive user cannot be determined' {
        Mock Get-CimInstance { [pscustomobject]@{ UserName = $null } }
        $report = Get-MwbSandboxInteractiveUserReport
        $report.QueryError.Code | Should Be 'InteractiveUserNotLoggedOn'
        $report.PackageCount | Should Be $null
        $report.PackageAbsent | Should Be $null
        $report.WsbVersionProbe.Status | Should Be 'NotChecked'
    }

    It 'records unreached stages explicitly rather than omitting or reporting them as passing' {
        Mock Get-CimInstance {
            param($ClassName)
            if ($ClassName -ceq 'Win32_OperatingSystem') { return [pscustomobject]@{ OSArchitecture = '64-bit'; BuildNumber = '26200'; Version = '10.0.26200' } }
            [pscustomobject]@{ UserName = $null }
        }
        Mock Get-WindowsOptionalFeature { throw 'The requested operation requires elevation.' }
        Mock Get-MwbSandboxPackages { throw 'Package query failed.' }
        Mock Get-MwbSandboxProvisionedPackages { throw 'Provisioning query failed.' }
        $report = Get-MwbSandboxPrerequisiteReport -Platform 'x64Win11'
        $report.SchemaVersion | Should Be 3
        $report.CollectionContext | Should Be 'ElevatedPrepareInventory'
        $report.SandboxFeatureQueryError.Code | Should Be 'SandboxFeatureQueryFailed'
        $report.InventoryConclusion | Should Be 'UnknownQueryFailure'
        ($report.StagesNotChecked -ccontains 'SandboxFeatureState') | Should Be $true
        ($report.StagesNotChecked -ccontains 'AllUsersPackages') | Should Be $true
        ($report.StagesNotChecked -ccontains 'ProvisionedPackages') | Should Be $true
        ($report.StagesNotChecked -ccontains 'InteractiveUserIdentity') | Should Be $true
        ($report.StagesNotChecked -ccontains 'InteractiveUserPackages') | Should Be $true
        ($report.StagesNotChecked -ccontains 'InteractiveUserAlias') | Should Be $true
        ($report.StagesNotChecked -ccontains 'WsbVersionProbe') | Should Be $true
        # Round-trips without throwing, so a partially-failed report is still an exportable artifact.
        { $report | ConvertTo-Json -Depth 8 } | Should Not Throw
    }
}
