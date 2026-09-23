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
    'Get-MwbCiFileVersion', 'Assert-MwbCiNativePreviewFile', 'New-MwbCiPreview', 'Get-MwbCiPreparationPaths',
    'Get-MwbCiGitOptions', 'Get-MwbCiPreviewPublishCommand', 'Get-MwbCiPreviewRuntimeProvenance')) {
    $definition = $buildAst.Find({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $true)
    . ([scriptblock]::Create($definition.Extent.Text))
}

function New-CiBundleFixture {
    param([string] $Root)

    $bundle = Join-Path $Root 'mwb-sandbox-ci'
    $null = New-Item -ItemType Directory -Path (Join-Path $bundle 'preview')
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
    $pin = Get-MwbPreviewPin
    $preview = foreach ($name in $pin.Files) {
        $file = Join-Path $bundle "preview\$name"
        Set-Content -LiteralPath $file -Value "nonexecutable preview fixture: $name"
        @{ Path = $name; Sha256 = (Get-FileHash -LiteralPath $file).Hash }
    }
    $manifest = @{
        FormatVersion = 1; SourceRevision = 'a' * 40; Platform = 'x64'; Configuration = 'Debug'
        Runtime = @{
            Sha256 = $archiveHash; ManifestSha256 = (Get-FileHash -LiteralPath "$archive.manifest.json").Hash
            ReadyToRun = $false; Files = @($entries)
        }
        Preview = @{
            Repository = $pin.Repository; Commit = $pin.Commit; Version = $pin.Version
            SdkVersion = $pin.SdkVersion; RuntimeVersion = $pin.RuntimeVersion
            PatchSha256 = $pin.PatchSha256; Files = @($preview)
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

Describe 'Native CI preparation command streams and Git audit' {
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

    It 'rejects an incomplete native audit even when the command exits successfully' {
        $command = "[Console]::Out.Write('expected-file.cs'); [Console]::Error.Write('unreadable asset: Filename too long'); exit 0"
        {
            Invoke-MwbCiBuildCommand $powerShell @('-NoLogo', '-NoProfile', '-NonInteractive', '-Command', $command) `
                $directory $log 30 -RequireCleanStandardError
        } | Should Throw 'entire checkout must be readable'
        $text = Get-Content -LiteralPath $log -Raw
        $text | Should Match 'expected-file.cs'
        $text | Should Match 'Filename too long'
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

    It 'audits a real Git checkout beyond MAX_PATH instead of ignoring failed lstat diagnostics' {
        $git = (Get-Command git.exe -ErrorAction Stop).Source
        $options = @(Get-MwbCiGitOptions)
        $null = Invoke-MwbCiBuildCommand $git ($options + @('init', '--quiet')) $directory (Join-Path $WorkRoot 'init.log') 30
        $null = Invoke-MwbCiBuildCommand $git ($options + @('config', '--local', 'core.longpaths', 'true')) `
            $directory (Join-Path $WorkRoot 'config.log') 30
        $configured = Invoke-MwbCiBuildCommand $git @('config', '--local', '--get', 'core.longpaths') `
            $directory (Join-Path $WorkRoot 'configured.log') 30
        $configured | Should Be 'true'

        $relative = 'src\tests\' + ('long-asset-directory-' * 3) + '\' + ('nested-asset-directory-' * 3) +
            '\' + ('deep-asset-directory-' * 4) + '\' + ('deeper-asset-directory-' * 3) +
            '\Square44x44Logo.targetsize-24_altform-unplated.png'
        $asset = Join-Path $directory $relative
        $relative.Length | Should BeGreaterThan 260
        $asset.Length | Should BeGreaterThan 260
        $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($asset))
        [IO.File]::WriteAllText($asset, 'inert long-path fixture')
        [IO.File]::WriteAllText((Join-Path $directory 'src\backend.cs'), 'inert source fixture')
        # Populate the index and change tracked files without any commit or remote operation.
        $null = Invoke-MwbCiBuildCommand $git ($options + @('add', '--', '.')) `
            $directory (Join-Path $WorkRoot 'add.log') 30
        [IO.File]::WriteAllText($asset, 'modified inert long-path fixture')
        [IO.File]::WriteAllText((Join-Path $directory 'src\backend.cs'), 'modified inert source fixture')
        $baselineLog = Join-Path $WorkRoot 'short-path-audit.log'
        $baselineFailed = $false
        try {
            $null = Invoke-MwbCiBuildCommand $git @('-c', 'core.longpaths=false', 'diff', '--name-only') `
                $directory $baselineLog 30 -RequireCleanStandardError
        }
        catch { $baselineFailed = $true }
        if (-not $baselineFailed) {
            throw "The long-path fixture did not reproduce the incomplete audit: $([IO.File]::ReadAllText($baselineLog))"
        }
        Get-Content -LiteralPath $baselineLog -Raw | Should Match 'Filename too long'

        $changed = Invoke-MwbCiBuildCommand $git ($options + @('diff', '--no-ext-diff', '--no-textconv', '--name-only')) `
            $directory (Join-Path $WorkRoot 'long-path-audit.log') 30 -RequireCleanStandardError
        $files = @($changed -split '\r?\n')
        $files.Count | Should Be 2
        ($files -contains 'src/backend.cs') | Should Be $true
        ($files -contains $relative.Replace('\', '/')) | Should Be $true
    }

    It 'applies the production long-path options to every explicit preview Git invocation' {
        $preview = $buildAst.Find({
            param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'New-MwbCiPreview'
        }, $true)
        $commands = @($preview.FindAll({
            param($node)
            $node -is [Management.Automation.Language.CommandAst] -and
                $node.GetCommandName() -eq 'Invoke-MwbCiBuildCommand' -and
                $node.CommandElements[1].Extent.Text -eq '$git'
        }, $true))
        $commands.Count | Should Be 8
        foreach ($command in $commands) {
            $command.CommandElements[2].Extent.Text | Should Match '^\(\$gitOptions \+'
        }
        (@(Get-MwbCiGitOptions) -contains 'core.longpaths=true') | Should Be $true
        $preview.Extent.Text | Should Match "'config', '--local', 'core.longpaths', 'true'"
        $audit = @($commands | Where-Object { $_.Extent.Text -match "'diff', '--no-ext-diff'" })
        $audit.Count | Should Be 1
        $audit[0].Extent.Text | Should Match '-RequireCleanStandardError'
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

function New-CiPreviewAssetsFixture {
    param([string] $Directory, [ValidateSet('x64', 'arm64')][string] $Platform = 'x64')

    $rid = "win-$Platform"
    $framework = 'net10.0-windows10.0.19041.0'
    foreach ($project in @('WinApp.Cli', 'WinApp.UIAutomation', 'WinApp.UIAutomation.Recording')) {
        $downloads = @(
            @{ name = "Microsoft.NETCore.App.Runtime.$rid"; version = '[10.0.12, 10.0.12]' },
            @{ name = 'Microsoft.Windows.SDK.NET.Ref'; version = '[10.0.19041.57, 10.0.19041.57]' }
        )
        $target = @{}
        $libraries = @{}
        if ($project -ceq 'WinApp.Cli') {
            $downloads += @(
                @{ name = "Microsoft.NETCore.App.Runtime.NativeAOT.$rid"; version = '[10.0.12, 10.0.12]' },
                @{ name = 'runtime.win-x64.Microsoft.DotNet.ILCompiler'; version = '[10.0.12, 10.0.12]' }
            )
            foreach ($id in @('Microsoft.DotNet.ILCompiler', "runtime.$rid.Microsoft.DotNet.ILCompiler")) {
                $target["$id/10.0.12"] = @{ type = 'package' }
                $libraries["$id/10.0.12"] = @{ type = 'package'; sha512 = [Convert]::ToBase64String([byte[]](0..63)) }
            }
        }
        $assets = @{
            version = 4
            project = @{ frameworks = @{ $framework = @{ downloadDependencies = $downloads } } }
            targets = @{ "$framework/$rid" = $target }
            libraries = $libraries
        }
        $path = Join-Path $Directory "src\winapp-CLI\$project\obj\project.assets.json"
        $null = New-Item -ItemType Directory -Path (Split-Path $path) -Force
        $assets | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $path
    }
}

Describe 'SDK-selected preview runtime verification' {
    BeforeEach {
        $directory = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-CiPreviewAssetsFixture $directory
        $cliAssets = Join-Path $directory 'src\winapp-CLI\WinApp.Cli\obj\project.assets.json'
        $framework = 'net10.0-windows10.0.19041.0'
    }

    It 'publishes with the frozen SDK/source version settings without overriding unrelated framework versions' {
        $pin = Get-MwbPreviewPin
        $command = Get-MwbCiPreviewPublishCommand -VcVars 'C:\fixture-vs\vcvars64.bat' `
            -DotNet 'C:\fixture-sdk\dotnet.exe' -PublishRoot 'C:\fixture-output' -Pin $pin
        $command | Should Match 'publish "src\\winapp-CLI\\WinApp.Cli\\WinApp.Cli.csproj" -c Release -r win-x64 --self-contained'
        foreach ($property in @('Version', 'AssemblyVersion', 'FileVersion')) {
            $command | Should Match ([regex]::Escape("/p:$property=$($pin.AssemblyVersion)"))
        }
        $command | Should Match ([regex]::Escape("/p:InformationalVersion=$($pin.Version)"))
        $command | Should Not Match 'RuntimeFrameworkVersion|WindowsSdkPackageVersion|NoWarn|WarningsNotAsErrors|TreatWarningsAsErrors=false'
        $buildAst.Extent.Text | Should Match 'version = \$pin.SdkVersion; rollForward = ''disable'''
    }

    It 'cross-publishes for an explicit ARM64 target RID from this x64 build agent' {
        $pin = Get-MwbPreviewPin
        $command = Get-MwbCiPreviewPublishCommand -VcVars 'C:\fixture-vs\vcvarsamd64_arm64.bat' `
            -DotNet 'C:\fixture-sdk\dotnet.exe' -PublishRoot 'C:\fixture-output' -Pin $pin -RuntimeIdentifier 'win-arm64'
        $command | Should Match 'publish "src\\winapp-CLI\\WinApp.Cli\\WinApp.Cli.csproj" -c Release -r win-arm64 --self-contained'
        $command | Should Match ([regex]::Escape('call "C:\fixture-vs\vcvarsamd64_arm64.bat"'))
    }

    It 'records actual .NET runtime/compiler versions while leaving Windows SDK reference versions independent' {
        $result = Get-MwbCiPreviewRuntimeProvenance $directory '10.0.12'
        $result.RuntimeVersion | Should Be '10.0.12'
        $result.TargetFramework | Should Be $framework
        $result.RuntimeIdentifier | Should Be 'win-x64'
        $result.Projects.Count | Should Be 3
        $result.CompilerPackages.Count | Should Be 2
        foreach ($compiler in $result.CompilerPackages) {
            $compiler.Version | Should Be '10.0.12'
            $compiler.Sha512 | Should Not BeNullOrEmpty
        }
        foreach ($project in $result.Projects) {
            $path = Join-Path $directory "src\winapp-CLI\$($project.Project)\obj\project.assets.json"
            $project.AssetsSha256 | Should Be (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
            $project.WindowsSdkReferences[0].VersionRange | Should Be '[10.0.19041.57, 10.0.19041.57]'
        }
    }

    It 'records ARM64 target compiler provenance while separately pinning the win-x64 host executable' {
        $armDirectory = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-CiPreviewAssetsFixture $armDirectory -Platform arm64
        $result = Get-MwbCiPreviewRuntimeProvenance $armDirectory '10.0.12' -Platform arm64
        $result.RuntimeIdentifier | Should Be 'win-arm64'
        $result.Projects.Count | Should Be 3
        $result.CompilerPackages.Count | Should Be 2
        # CompilerPackages holds the target-RID library provenance (sha512-verifiable target
        # dependencies); the actual win-x64 host executable is pinned separately below via its
        # framework download dependency, since only that one can physically run on this agent.
        ($result.CompilerPackages.Id -contains 'Microsoft.DotNet.ILCompiler') | Should Be $true
        ($result.CompilerPackages.Id -contains 'runtime.win-arm64.Microsoft.DotNet.ILCompiler') | Should Be $true
        $cliProject = $result.Projects | Where-Object Project -eq 'WinApp.Cli'
        ($cliProject.RuntimePacks.Id -contains 'runtime.win-x64.Microsoft.DotNet.ILCompiler') | Should Be $true
    }

    It 'rejects runtime/compiler downloads that differ from the pin in any published project' -TestCases @(
        @{ Project = 'WinApp.Cli'; Id = 'Microsoft.NETCore.App.Runtime.win-x64' }
        @{ Project = 'WinApp.UIAutomation'; Id = 'Microsoft.NETCore.App.Runtime.win-x64' }
        @{ Project = 'WinApp.UIAutomation.Recording'; Id = 'Microsoft.NETCore.App.Runtime.win-x64' }
        @{ Project = 'WinApp.Cli'; Id = 'Microsoft.NETCore.App.Runtime.NativeAOT.win-x64' }
        @{ Project = 'WinApp.Cli'; Id = 'runtime.win-x64.Microsoft.DotNet.ILCompiler' }
    ) {
        param($Project, $Id)
        $path = Join-Path $directory "src\winapp-CLI\$Project\obj\project.assets.json"
        $assets = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
        foreach ($entry in $assets.project.frameworks[$framework].downloadDependencies) {
            if ($entry.name -ceq $Id) { $entry.version = '[10.0.11, 10.0.11]' }
        }
        $assets | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $path
        { Get-MwbCiPreviewRuntimeProvenance $directory '10.0.12' } | Should Throw 'does not match the pin'
    }

    It 'rejects compiler target/library versions even if download declarations still match' -TestCases @(
        @{ Id = 'Microsoft.DotNet.ILCompiler' }; @{ Id = 'runtime.win-x64.Microsoft.DotNet.ILCompiler' }
    ) {
        param($Id)
        $assets = Get-Content -LiteralPath $cliAssets -Raw | ConvertFrom-Json -AsHashtable
        $target = $assets.targets["$framework/win-x64"]
        $null = $target.Remove("$Id/10.0.12")
        $target["$Id/10.0.13"] = @{ type = 'package' }
        $assets | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $cliAssets
        { Get-MwbCiPreviewRuntimeProvenance $directory '10.0.12' } | Should Throw 'NativeAOT compiler does not match'
    }

    It 'rejects missing compiler package integrity metadata' {
        $assets = Get-Content -LiteralPath $cliAssets -Raw | ConvertFrom-Json -AsHashtable
        $assets.libraries['Microsoft.DotNet.ILCompiler/10.0.12'].sha512 = ''
        $assets | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $cliAssets
        { Get-MwbCiPreviewRuntimeProvenance $directory '10.0.12' } | Should Throw 'compiler package provenance is missing'
    }

    It 'rejects floating runtime download ranges' -TestCases @(
        @{ Range = '[10.0.12, )' }; @{ Range = '[10.0.12, 10.0.13]' }; @{ Range = '10.0.*' }
    ) {
        param($Range)
        $assets = Get-Content -LiteralPath $cliAssets -Raw | ConvertFrom-Json -AsHashtable
        $assets.project.frameworks[$framework].downloadDependencies[0].version = $Range
        $assets | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $cliAssets
        { Get-MwbCiPreviewRuntimeProvenance $directory '10.0.12' } | Should Throw 'does not match the pin'
    }

    It 'rejects duplicate Windows SDK downloads instead of suppressing NU1505' {
        $assets = Get-Content -LiteralPath $cliAssets -Raw | ConvertFrom-Json -AsHashtable
        $assets.project.frameworks[$framework].downloadDependencies += @{
            name = 'Microsoft.Windows.SDK.NET.Ref'; version = '[10.0.12, 10.0.12]'
        }
        $assets | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $cliAssets
        { Get-MwbCiPreviewRuntimeProvenance $directory '10.0.12' } | Should Throw 'duplicate package downloads'
    }

    It 'requires assets for every published reference project' {
        Remove-Item -LiteralPath (Join-Path $directory 'src\winapp-CLI\WinApp.UIAutomation.Recording\obj\project.assets.json')
        { Get-MwbCiPreviewRuntimeProvenance $directory '10.0.12' } | Should Throw 'missing restored runtime provenance'
    }

    It 'requires the published Windows x64 framework target' {
        $assets = Get-Content -LiteralPath $cliAssets -Raw | ConvertFrom-Json -AsHashtable
        $null = $assets.targets.Remove("$framework/win-x64")
        $assets | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $cliAssets
        { Get-MwbCiPreviewRuntimeProvenance $directory '10.0.12' } | Should Throw 'no restored'
    }

    It 'verifies resolved runtime provenance before publishing portable files and records the observed version' {
        $preview = $buildAst.Find({
            param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'New-MwbCiPreview'
        }, $true).Extent.Text
        $preview.IndexOf('Get-MwbCiPreviewRuntimeProvenance') |
            Should BeLessThan $preview.IndexOf('New-Item -ItemType Directory -Path $Destination')
        $preview | Should Match 'RuntimeVersion = \$runtimeProvenance.RuntimeVersion'
        $preview | Should Match 'RuntimeProvenance = \$runtimeProvenance'
    }
}

Describe 'Portable preview native architecture validation' {
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

    Context 'Frozen preview source preparation fail-closed behavior' {
        BeforeEach {
            $directory = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
            $destination = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
            $DotNetPath = Join-Path $TestDrive 'inert-sdk\dotnet.exe'
            $script:ciPreviewPin = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\mwb-sandbox-preview\source.json') -Raw |
                ConvertFrom-Json
            Mock Get-MwbPreviewPin { $script:ciPreviewPin }
            Mock Invoke-MwbCiBuildCommand { '' }
        }

        It 'rejects changed patch bytes before any source fetch or native execution' {
            $script:ciPreviewPin.PatchSha256 = '0' * 64
            Mock Get-MwbPreviewPin { $script:ciPreviewPin }
            { New-MwbCiPreview $directory $destination } | Should Throw 'hash mismatch'
            Assert-MockCalled Invoke-MwbCiBuildCommand -Times 0 -Exactly -Scope It
            Test-Path -LiteralPath $directory | Should Be $false
        }

        It 'rejects a fetched SHA mismatch before checking out or executing that source' {
            Mock Invoke-MwbCiBuildCommand {
                param($Executable, $Arguments)
                if ($Arguments -contains 'rev-parse') { return '0' * 40 }
                ''
            }
            { New-MwbCiPreview $directory $destination } | Should Throw 'pinned public commit'
            Assert-MockCalled Invoke-MwbCiBuildCommand -Times 1 -Exactly -Scope It -ParameterFilter {
                $Arguments -contains 'fetch' -and $Arguments -contains '114e6ec6cfec9267a43b4b324f2eb9d20a297261'
            }
            Assert-MockCalled Invoke-MwbCiBuildCommand -Times 0 -Exactly -Scope It -ParameterFilter {
                $Arguments -contains 'checkout' -or $Arguments -contains 'apply' -or $Arguments -contains '--version'
            }
        }

        It 'refuses source modifications outside the frozen two-file patch before building' {
            Mock Invoke-MwbCiBuildCommand {
                param($Executable, $Arguments)
                if ($Arguments -contains 'rev-parse') { return $script:ciPreviewPin.Commit }
                if ($Arguments -contains 'diff') { return 'unexpected-source-file.cs' }
                ''
            }
            { New-MwbCiPreview $directory $destination } | Should Throw 'performance-only scope'
            Assert-MockCalled Invoke-MwbCiBuildCommand -Times 0 -Exactly -Scope It -ParameterFilter {
                $Arguments -contains '--version' -or $Arguments -contains '/c'
            }
            Test-Path -LiteralPath $destination | Should Be $false
        }
    }

    It 'accepts only an x64 PE32+ image without a CLR directory' {
        { Assert-MwbCiNativePreviewFile $image } | Should Not Throw
    }

    It 'rejects ARM64 output even when named winapp.exe and x64 is expected' {
        [BitConverter]::GetBytes([uint16]0xAA64).CopyTo($bytes, 132)
        [IO.File]::WriteAllBytes($image, $bytes)
        { Assert-MwbCiNativePreviewFile $image } | Should Throw 'not native x64'
    }

    It 'rejects a managed executable rather than requiring a test-host SDK' {
        [BitConverter]::GetBytes([uint64]1).CopyTo($bytes, (128 + 24 + 112 + (14 * 8)))
        [IO.File]::WriteAllBytes($image, $bytes)
        { Assert-MwbCiNativePreviewFile $image } | Should Throw 'managed runtime'
    }

    It 'accepts a native ARM64 PE32+ image without a CLR directory when ARM64 is requested' {
        [BitConverter]::GetBytes([uint16]0xAA64).CopyTo($bytes, 132)
        [IO.File]::WriteAllBytes($image, $bytes)
        { Assert-MwbCiNativePreviewFile $image -Architecture arm64 } | Should Not Throw
    }

    It 'rejects x64 output when native ARM64 is expected' {
        { Assert-MwbCiNativePreviewFile $image -Architecture arm64 } | Should Throw 'not native arm64'
    }

    It 'rejects the ARM64EC hybrid machine type when native ARM64 is expected' {
        [BitConverter]::GetBytes([uint16]0xA641).CopyTo($bytes, 132)
        [IO.File]::WriteAllBytes($image, $bytes)
        { Assert-MwbCiNativePreviewFile $image -Architecture arm64 } | Should Throw 'not native arm64'
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
        $fixture | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath
        (Get-MwbCiBundle $root ('a' * 40) -Platform arm64).Manifest.Platform | Should Be 'arm64'
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

    It 'rejects changed source, patch, or toolchain provenance' -TestCases @(
        @{ Field = 'Repository' }; @{ Field = 'Commit' }; @{ Field = 'PatchSha256' }
        @{ Field = 'Version' }; @{ Field = 'SdkVersion' }; @{ Field = 'RuntimeVersion' }
    ) {
        param($Field)
        $fixture.Preview[$Field] = 'changed'
        $fixture | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath
        { Get-MwbCiBundle $root ('a' * 40) } | Should Throw 'provenance'
    }

    It 'rejects tampered archive, packager provenance, and portable files' -TestCases @(
        @{ File = 'runtime.zip' }; @{ File = 'runtime.zip.manifest.json' }
        @{ File = 'preview\winapp.exe' }; @{ File = 'preview\libSkiaSharp.dll' }
    ) {
        param($File)
        Add-Content -LiteralPath (Join-Path $bundleRoot $File) -Value 'tampered'
        { Get-MwbCiBundle $root ('a' * 40) } | Should Throw 'hash mismatch'
    }

    It 'rejects unmanifested preview state or credentials' {
        Set-Content -LiteralPath (Join-Path $bundleRoot 'preview\unexpected.json') -Value '{}'
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

    It 'pins the exact performance patch and preserves it across Windows checkouts' {
        $pin = Get-MwbPreviewPin
        $pin.Commit | Should Be '114e6ec6cfec9267a43b4b324f2eb9d20a297261'
        Assert-MwbCiHash (Join-Path $PSScriptRoot "..\mwb-sandbox-preview\$($pin.Patch)") $pin.PatchSha256
        Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\mwb-sandbox-preview\.gitattributes') -Raw |
            Should Match '\*\.patch text eol=lf'
        $build | Should Match 'fetch.*--depth'
        $build | Should Match '\$commit -cne \$pin.Commit'
        $build | Should Match "GIT_TERMINAL_PROMPT'\] = '0'"
        $build | Should Match "GCM_INTERACTIVE'\] = 'Never'"
        $build | Should Match "'apply', '--check'"
    }

    It 'defaults to no preparation for existing build callers' {
        $job | Should Match 'name: mwbSandboxExperiment\r?\n    type: boolean\r?\n    default: false'
        $job | Should Match '\$\{\{ if eq\(parameters.mwbSandboxExperiment, true\) \}\}:\r?\n    - task: UseDotNet@2'
        $job | Should Match '-SourceRevision "\$\(Build.SourceVersion\)" -ReadyToRun'
        $job | Should Match "version: '10.0.401'"
        $buildAst.ParamBlock.Extent.Text | Should Match '\[switch\] \$ReadyToRun'
    }

    It 'prepares compiler and preview only in BUILD and retains the ordinary artifact identity' {
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

    It 'passes the selected backend and a separate protected preview to the initializer' {
        $script | Should Match "Get-MwbCiBackend \`$Platform \`$windowsBuild"
        $script | Should Match '-SandboxBackend'
        $script | Should Match '-SandboxWinAppPath'
        $script | Should Match 'SandboxBackend = \$backend'
        $script | Should Not Match '\$env:WINAPP_CLI_PATH\s*='
        $testJob | Should Match '\.pipelines\\InstallWinAppCli.ps1'
        Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\InstallWinAppCli.ps1') -Raw | Should Match '0.3.2'
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
        $backend | Should Match 'trusted_provider_unavailable'
    }

    It 'retains the existing bounded Limited desktop dispatch and recovery' {
        $script | Should Match '-TimeoutMinutes 45'
        $script | Should Match "\`$task.Principal.RunLevel -ne 'Limited'"
        $script | Should Match "\`$task.Principal.LogonType -ne 'Interactive'"
        $script | Should Match '-MwbRecoveryJournal \$journalPath -TimeoutMinutes 4'
        $script | Should Match 'AddSeconds\(90\)'
        $script | Should Match 'New-TimeSpan -Minutes 20'
    }
}
