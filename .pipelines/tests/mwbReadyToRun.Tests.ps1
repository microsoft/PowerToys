# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
$experimentRoot = Join-Path $PSScriptRoot '..\..\src\modules\MouseWithoutBorders\Tests\SandboxExperiment'
. (Join-Path $experimentRoot 'MwbReadyToRun.ps1')
Initialize-MwbReadyToRunMetadata

function New-ReadyToRunFixtureAssembly {
    param([string]$Path, [string]$Configuration = 'Debug')
    $type = 'Fixture' + [guid]::NewGuid().ToString('N')
    Add-Type -TypeDefinition @"
using System;
using System.Reflection;
[assembly: AssemblyConfiguration("$Configuration")]
[assembly: AssemblyVersion("1.2.3.4")]
public static class $type {
    [Obsolete("fixture metadata")]
    public static int Add(int first, int second) {
        try { return checked(first + second); }
        catch (OverflowException) { return 0; }
    }
}
"@ -OutputAssembly $Path
}

function New-ReadyToRunFixtureProduct {
    param([string]$Root, [string]$Assembly)
    foreach ($application in @(
        'PowerToys.MouseWithoutBorders', 'PowerToys.MouseWithoutBordersHelper',
        'WinUI3Apps\PowerToys.Settings', 'WinUI3Apps\PowerToys.QuickAccess'
    )) {
        $base = Join-Path $Root $application
        $null = New-Item -ItemType Directory -Path (Split-Path $base) -Force
        Copy-Item -LiteralPath $Assembly -Destination "$base.dll"
        @{
            runtimeOptions = @{
                tfm = 'net10.0'
                includedFrameworks = @(
                    @{ name = 'Microsoft.NETCore.App'; version = '10.0.12' },
                    @{ name = 'Microsoft.WindowsDesktop.App'; version = '10.0.12' })
            }
        } | ConvertTo-Json -Depth 5 | Set-Content "$base.runtimeconfig.json"
    }
}

function Get-ReadyToRunShortPathFixture {
    param([string[]]$Paths)
    $fileSystem = $null
    try {
        $fileSystem = New-Object -ComObject Scripting.FileSystemObject
        foreach ($path in $Paths) {
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

Describe 'MWB explicit ReadyToRun compiler contracts' {
    BeforeEach {
        $compilerPath = Join-Path $TestDrive 'crossgen2.exe'
        Set-Content $compilerPath 'compiler fixture; must not run'
        $global:R2RCompilerVersion = '10.0.1226.42308'
        $global:R2RCompilerProduct = '10.0.12-servicing.26422.108+' + ('a' * 40)
        $global:R2RCompilerDependencyMismatch = $false
        Mock Get-MwbReadyToRunFileIdentity {
            param($Path)
            [pscustomobject]@{
                Path = $Path
                FileVersion = if ($global:R2RCompilerDependencyMismatch -and $Path -like '*jitinterface*') {
                    '10.0.1126.40000'
                } else { $global:R2RCompilerVersion }
                ProductVersion = $global:R2RCompilerProduct
                Commit = 'a' * 40
                Sha256 = [IO.Path]::GetFileName($Path)
            }
        }
    }

    It 'requires an explicit existing compiler file' {
        { Get-MwbReadyToRunCompiler '' } | Should Throw 'explicitly'
        { Get-MwbReadyToRunCompiler (Join-Path $TestDrive 'missing.exe') } | Should Throw 'explicitly'
        { Get-MwbReadyToRunCompiler $TestDrive } | Should Throw 'explicitly'
        $other = Join-Path $TestDrive 'other.exe'
        Set-Content $other 'not a compiler'
        { Get-MwbReadyToRunCompiler $other } | Should Throw 'explicitly'
        Assert-MockCalled Get-MwbReadyToRunFileIdentity -Times 0 -Exactly -Scope It
    }

    It 'records the explicit compiler and both native compiler dependencies' {
        $result = Get-MwbReadyToRunCompiler $compilerPath
        $result.Path | Should Be $compilerPath
        $result.Files.Count | Should Be 3
        $result.Files[1].Path | Should Match 'jitinterface_x64.dll$'
        $result.Files[2].Path | Should Match 'clrjit_win_x64_x64.dll$'
        $result.TargetArchitecture | Should Be 'x64'
    }

    It 'selects the universal ARM64 cross-target JIT for an explicit ARM64 target' {
        $result = Get-MwbReadyToRunCompiler $compilerPath -TargetArchitecture arm64
        $result.Files.Count | Should Be 3
        $result.Files[1].Path | Should Match 'jitinterface_x64.dll$'
        $result.Files[2].Path | Should Match 'clrjit_universal_arm64_x64.dll$'
        $result.TargetArchitecture | Should Be 'arm64'
    }

    It 'rejects missing runtime-version metadata' {
        $global:R2RCompilerProduct = 'unknown'
        { Get-MwbReadyToRunCompiler $compilerPath } | Should Throw 'runtime version'
    }

    It 'rejects mixed native compiler versions' {
        $global:R2RCompilerDependencyMismatch = $true
        { Get-MwbReadyToRunCompiler $compilerPath } | Should Throw 'native dependency differ'
    }

    It 'rejects contradictory compiler version resources' {
        $global:R2RCompilerVersion = '9.0.1226.42308'
        { Get-MwbReadyToRunCompiler $compilerPath } | Should Throw 'version resources disagree'
    }

    AfterAll {
        Remove-Variable R2RCompilerVersion,R2RCompilerProduct,R2RCompilerDependencyMismatch -Scope Global -ErrorAction SilentlyContinue
    }
}

Describe 'MWB native compiler image validation' {
    It 'rejects nonnative or wrong-architecture compiler images' {
        $file = Join-Path $TestDrive 'managed.dll'
        New-ReadyToRunFixtureAssembly $file
        { Get-MwbReadyToRunFileIdentity $file -Native } | Should Throw 'x64 native'
    }

    It 'rejects nonnative images when native ARM64 is explicitly requested' {
        $file = Join-Path $TestDrive 'managed-arm64.dll'
        New-ReadyToRunFixtureAssembly $file
        { Get-MwbReadyToRunFileIdentity $file -Native -NativeArchitecture arm64 } | Should Throw 'arm64 native'
    }
}

Describe 'MWB Debug runtime/compiler coherence' {
    BeforeAll {
        $script:debugAssembly = Join-Path $TestDrive 'debug-fixture.dll'
        $script:releaseAssembly = Join-Path $TestDrive 'release-fixture.dll'
        New-ReadyToRunFixtureAssembly $script:debugAssembly
        New-ReadyToRunFixtureAssembly $script:releaseAssembly 'Release'
    }

    BeforeEach {
        $stage = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-ReadyToRunFixtureProduct $stage $script:debugAssembly
        $compiler = [pscustomobject]@{
            ProductVersion = '10.0.12-servicing.26422.108+' + ('a' * 40)
            Files = @([pscustomobject]@{ FileVersion = '10.0.1226.42308'; Commit = 'a' * 40 })
        }
        $global:R2RMixedRuntimeBuild = $false
        $global:R2RMixedRuntimeHash = $false
        Mock Get-MwbReadyToRunFileIdentity {
            param($Path)
            [pscustomobject]@{
                Path = $Path
                FileVersion = if ($global:R2RMixedRuntimeBuild) { '10.0.1126.1' } else { '10.0.1226.42308' }
                Commit = 'a' * 40
                Sha256 = if ($global:R2RMixedRuntimeHash -and $Path -like '*WinUI3Apps*') {
                    'different copy'
                } else { [IO.Path]::GetFileName($Path) }
            }
        }
    }

    It 'checks both root and WinUI CLR files against one matching runtime build' {
        $result = Get-MwbReadyToRunRuntime $stage $compiler
        $result.Version | Should Be '10.0.12'
        $result.Files.Count | Should Be 10
    }

    It 'rejects a compiler from a different runtime version' {
        $compiler.ProductVersion = '10.0.11-servicing+' + ('a' * 40)
        { Get-MwbReadyToRunRuntime $stage $compiler } | Should Throw 'framework declaration'
    }

    It 'records an older self-contained patch declaration without rewriting it' {
        $path = Join-Path $stage 'WinUI3Apps\PowerToys.Settings.runtimeconfig.json'
        $data = Get-Content $path -Raw | ConvertFrom-Json
        foreach ($framework in $data.runtimeOptions.includedFrameworks) { $framework.version = '10.0.11' }
        $data | ConvertTo-Json -Depth 5 | Set-Content $path
        $before = (Get-FileHash $path).Hash
        $result = Get-MwbReadyToRunRuntime $stage $compiler
        $result.Version | Should Be '10.0.12'
        @($result.DeclaredFrameworks | Where-Object Version -eq '10.0.11').Count | Should Be 1
        (Get-FileHash $path).Hash | Should Be $before
    }

    It 'rejects an incompatible runtime family even with valid Debug metadata' {
        $compiler.ProductVersion = '9.0.12+' + ('a' * 40)
        { Get-MwbReadyToRunRuntime $stage $compiler } | Should Throw 'framework declaration'
    }

    It 'rejects mismatched Core and Desktop frameworks' {
        $path = Join-Path $stage 'PowerToys.MouseWithoutBorders.runtimeconfig.json'
        $data = Get-Content $path -Raw | ConvertFrom-Json
        $data.runtimeOptions.includedFrameworks[1].version = '10.0.11'
        $data | ConvertTo-Json -Depth 5 | Set-Content $path
        { Get-MwbReadyToRunRuntime $stage $compiler } | Should Throw 'Core/Desktop'
    }

    It 'rejects Release without changing the assembly configuration' {
        $path = Join-Path $stage 'PowerToys.MouseWithoutBorders.dll'
        Copy-Item $script:releaseAssembly $path -Force
        $before = (Get-FileHash $path).Hash
        { Get-MwbReadyToRunRuntime $stage $compiler } | Should Throw 'Debug assembly'
        (Get-FileHash $path).Hash | Should Be $before
    }

    It 'rejects different native CLR builds despite matching declared framework versions' {
        $global:R2RMixedRuntimeBuild = $true
        { Get-MwbReadyToRunRuntime $stage $compiler } | Should Throw 'CLR build differ'
    }

    It 'rejects differing root/WinUI copies' {
        $global:R2RMixedRuntimeHash = $true
        { Get-MwbReadyToRunRuntime $stage $compiler } | Should Throw 'CLR copies differ'
    }

    AfterAll {
        Remove-Variable R2RMixedRuntimeBuild,R2RMixedRuntimeHash -Scope Global -ErrorAction SilentlyContinue
    }
}

Describe 'MWB staged compilation isolation' {
    BeforeEach {
        $stage = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $null = New-Item -ItemType Directory $stage
        $assemblyPath = Join-Path $stage 'Fixture.dll'
        New-ReadyToRunFixtureAssembly $assemblyPath
        $originalHash = (Get-FileHash $assemblyPath).Hash
        $global:R2RTestStage = $stage
        Mock Get-MwbReadyToRunCompiler {
            [pscustomobject]@{ Path = 'must-not-run.exe'; Files = @(); ProductVersion = 'fixture' }
        }
        Mock Get-MwbReadyToRunRuntime { [pscustomobject]@{ Version = 'fixture'; Files = @() } }
        Mock Invoke-MwbReadyToRunCompiler { throw 'Unexpected compiler invocation' }
    }

    It 'rejects differing assembly copies before starting compilation' {
        $directory = Join-Path $stage 'WinUI3Apps'
        $null = New-Item -ItemType Directory $directory
        $copy = Join-Path $directory 'Fixture.dll'
        Copy-Item $assemblyPath $copy
        [IO.File]::AppendAllText($copy, 'different file bytes')
        { Convert-MwbStagedRuntimeToReadyToRun $stage 'explicit-path' } | Should Throw 'Conflicting assembly copies'
        Assert-MockCalled Invoke-MwbReadyToRunCompiler -Times 0 -Exactly -Scope It
        (Get-FileHash $assemblyPath).Hash | Should Be $originalHash
    }

    It 'refuses and preserves pre-existing native output' {
        $native = Join-Path $stage 'Fixture.ni.dll'
        'existing native output' | Set-Content $native
        # The compiler inventory receives only valid PE files; a pre-existing
        # output still must be refused before the compiler can overwrite it.
        Mock Get-MwbReadyToRunAssemblies {
            $path = Join-Path $global:R2RTestStage 'Fixture.dll'
            [pscustomobject]@{
                Path = $path; RelativePath = 'Fixture.dll'
                Metadata = [Microsoft.MouseWithoutBorders.SandboxExperiment.ReadyToRunMetadata]::Read($path)
                Sha256 = (Get-FileHash $path).Hash
            }
        }
        { Convert-MwbStagedRuntimeToReadyToRun $stage 'explicit-path' } | Should Throw 'existing compiler output'
        (Get-Content $native -Raw).Trim() | Should Be 'existing native output'
        Assert-MockCalled Invoke-MwbReadyToRunCompiler -Times 0 -Exactly -Scope It
    }

    It 'cleans partial native output without altering staged IL after compiler failure' {
        Mock Invoke-MwbReadyToRunCompiler {
            Set-Content (Join-Path $global:R2RTestStage 'Fixture.ni.dll') 'partial'
            throw 'Compiler failure'
        }
        { Convert-MwbStagedRuntimeToReadyToRun $stage 'explicit-path' } | Should Throw 'Compiler failure'
        (Get-FileHash $assemblyPath).Hash | Should Be $originalHash
        @(Get-ChildItem $stage -Filter '*.ni.dll').Count | Should Be 0
        @(Get-ChildItem $stage -Filter '.readytorun-*' -Directory).Count | Should Be 0
    }

    It 'rejects success without the requested compiled image' {
        Mock Invoke-MwbReadyToRunCompiler { 'compiler returned without output' }
        { Convert-MwbStagedRuntimeToReadyToRun $stage 'explicit-path' } | Should Throw 'Missing Crossgen2 output'
        (Get-FileHash $assemblyPath).Hash | Should Be $originalHash
        @(Get-ChildItem $stage -Filter '.readytorun-*' -Directory).Count | Should Be 0
    }

    It 'rejects an output that is still IL-only before replacing any copy' {
        Mock Invoke-MwbReadyToRunCompiler {
            Copy-Item (Join-Path $global:R2RTestStage 'Fixture.dll') (Join-Path $global:R2RTestStage 'Fixture.ni.dll')
        }
        { Convert-MwbStagedRuntimeToReadyToRun $stage 'explicit-path' } | Should Throw 'ReadyToRun image'
        (Get-FileHash $assemblyPath).Hash | Should Be $originalHash
        @(Get-ChildItem $stage -Filter '*.ni.dll').Count | Should Be 0
    }

    It 'detects inputs changed by compilation before replacement' {
        Mock Invoke-MwbReadyToRunCompiler {
            [IO.File]::AppendAllText((Join-Path $global:R2RTestStage 'Fixture.dll'), 'unexpected mutation')
        }
        { Convert-MwbStagedRuntimeToReadyToRun $stage 'explicit-path' } | Should Throw 'input changed'
        @(Get-ChildItem $stage -Filter '.readytorun-*' -Directory).Count | Should Be 0
    }

    AfterAll {
        Remove-Variable R2RTestStage -Scope Global -ErrorAction SilentlyContinue
    }
}

Describe 'MWB optional archive publication and failure cleanup' {
    BeforeAll {
        $tokens = $null
        $errors = $null
        $script:archiveScript = Join-Path $experimentRoot 'New-MwbRuntimeArchive.ps1'
        $script:archiveAst = [Management.Automation.Language.Parser]::ParseFile(
            $script:archiveScript, [ref]$tokens, [ref]$errors)
        if ($errors.Count) { throw ($errors | Out-String) }
        $start = $script:archiveAst.EndBlock.Statements | Where-Object {
            $_.Extent.Text -eq '$stageOwned = $false'
        }
        $script:publishFixture = [scriptblock]::Create(
            'param($root,$archive,$stage,$files,$ReadyToRun,$Crossgen2Path,$ReadyToRunParallelism,$ReadyToRunTimeoutSeconds,$Platform = ''x64'')' +
            "`n`$ErrorActionPreference = 'Stop'`n" + $script:archiveAst.Extent.Text.Substring($start.Extent.StartOffset))
        $pathFunction = $script:archiveAst.Find({
            param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
                $node.Name -eq 'Resolve-MwbArchiveFileSystemPath'
        }, $true)
        . ([scriptblock]::Create($pathFunction.Extent.Text))
    }

    BeforeEach {
        $root = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $null = New-Item -ItemType Directory $root
        Set-Content (Join-Path $root 'fixture.dll') 'unchanged source bytes'
        $archive = Join-Path $TestDrive (([guid]::NewGuid().ToString('N')) + '.zip')
        $global:R2RPublishedManifest = "$archive.manifest.json"
        $stage = "$archive.staging"
        $files = @('fixture.dll')
        Mock Convert-MwbStagedRuntimeToReadyToRun {
            param($StageRoot)
            Set-Content (Join-Path $StageRoot 'fixture.dll') 'compiled staged bytes'
            [pscustomobject]@{ FormatVersion = 1; ManagedILAndAttributesUnchanged = $true; Compiler = 'explicit fixture' }
        }
    }

    It 'keeps default packaging and its manifest unchanged without invoking a compiler' {
        & $script:publishFixture $root $archive $stage $files $false '' 2 600 | Out-Null
        Assert-MockCalled Convert-MwbStagedRuntimeToReadyToRun -Times 0 -Exactly -Scope It
        Test-Path $archive | Should Be $true
        Test-Path $stage | Should Be $true
        $manifest = Get-Content "$archive.manifest.json" -Raw | ConvertFrom-Json
        ($manifest.PSObject.Properties.Name -contains 'ReadyToRun') | Should Be $false
        [IO.File]::ReadAllText((Join-Path $stage 'fixture.dll')) | Should Be ([IO.File]::ReadAllText((Join-Path $root 'fixture.dll')))
    }

    It 'publishes optional provenance only after staging completes and preserves source bytes' {
        & $script:publishFixture $root $archive $stage $files $true 'explicit-compiler' 2 600 | Out-Null
        $manifest = Get-Content "$archive.manifest.json" -Raw | ConvertFrom-Json
        $manifest.ReadyToRun.ManagedILAndAttributesUnchanged | Should Be $true
        $manifest.ReadyToRun.Compiler | Should Be 'explicit fixture'
        (Get-Content (Join-Path $root 'fixture.dll') -Raw).Trim() | Should Be 'unchanged source bytes'
        Assert-MockCalled Convert-MwbStagedRuntimeToReadyToRun -Times 1 -Exactly -Scope It -ParameterFilter {
            $Crossgen2Path -eq 'explicit-compiler' -and $Parallelism -eq 2 -and $TimeoutSeconds -eq 600
        }
    }

    It 'removes only owned staging and partial outputs after a compiler failure' {
        Mock Convert-MwbStagedRuntimeToReadyToRun {
            param($StageRoot)
            Set-Content (Join-Path $StageRoot 'partial.ni.dll') 'partial native image'
            throw 'Compilation failed'
        }
        { & $script:publishFixture $root $archive $stage $files $true 'explicit-compiler' 2 600 } |
            Should Throw 'Compilation failed'
        Test-Path $stage | Should Be $false
        Test-Path $archive | Should Be $false
        Test-Path "$archive.manifest.json" | Should Be $false
        (Get-Content (Join-Path $root 'fixture.dll') -Raw).Trim() | Should Be 'unchanged source bytes'
    }

    It 'does not delete another owner manifest if publication encounters a collision' {
        $global:R2RCollisionWritten = $false
        Mock Convert-MwbStagedRuntimeToReadyToRun {
            [IO.File]::WriteAllText($global:R2RPublishedManifest, 'other owner provenance')
            $global:R2RCollisionWritten = $true
            [pscustomobject]@{ FormatVersion = 1 }
        }
        $failure = $null
        try { & $script:publishFixture $root $archive $stage $files $true 'explicit-compiler' 2 600 }
        catch { $failure = $_ }
        $global:R2RCollisionWritten | Should Be $true
        [IO.File]::ReadAllText("$archive.manifest.json") | Should Be 'other owner provenance'
        $failure | Should Not BeNullOrEmpty
        Test-Path $archive | Should Be $false
        Test-Path $stage | Should Be $false
    }

    It 'requires explicit opt-in and compiler without creating staging' {
        { & $script:archiveScript -ProductRoot $root -ArchivePath $archive -ReadyToRun } |
            Should Throw 'explicit -Crossgen2Path'
        { & $script:archiveScript -ProductRoot $root -ArchivePath $archive -Crossgen2Path 'unused.exe' } |
            Should Throw 'Use -ReadyToRun'
        Test-Path $stage | Should Be $false
    }

    It 'rejects opt-in staging inside the source build before compilation' {
        { & $script:archiveScript -ProductRoot $root -ArchivePath (Join-Path $root 'runtime.zip') -ReadyToRun -Crossgen2Path 'unused.exe' } |
            Should Throw 'outside the product'
        Assert-MockCalled Convert-MwbStagedRuntimeToReadyToRun -Times 0 -Exactly -Scope It
    }

    It 'rejects contained staging through a PSDrive alias: <SourceAlias>, <DestinationAlias>' -TestCases @(
        @{ SourceAlias = $true; DestinationAlias = $false }
        @{ SourceAlias = $false; DestinationAlias = $true }
        @{ SourceAlias = $true; DestinationAlias = $true }
    ) {
        param($SourceAlias, $DestinationAlias)
        $driveName = 'Mwb' + [guid]::NewGuid().ToString('N')
        $null = New-PSDrive -Name $driveName -PSProvider FileSystem -Root $root
        try {
            $aliasRoot = "${driveName}:\"
            $source = if ($SourceAlias) { $aliasRoot } else { $root }
            $destinationRoot = if ($DestinationAlias) { $aliasRoot } else { $root }
            $destination = Join-Path $destinationRoot 'not-created\deeper\runtime.zip'
            { & $script:archiveScript -ProductRoot $source -ArchivePath $destination -ReadyToRun -Crossgen2Path 'unused.exe' } |
                Should Throw 'outside the product'
            Test-Path (Join-Path $root 'not-created') | Should Be $false
        }
        finally { Remove-PSDrive -Name $driveName }
    }

    It 'rejects mixed DOS long/short aliases before compiler lookup' {
        $fixture = Get-ReadyToRunShortPathFixture @($TestDrive, $env:ProgramFiles, $env:USERPROFILE)
        # Reading an existing short alias needs no new directory or system mutation.
        # Hosts without 8.3 aliases are still covered by the deterministic PSDrive cases.
        if ($null -eq $fixture) {
            Set-TestInconclusive 'No existing DOS short-path alias is available on this host.'
            return
        }
        foreach ($shortSource in @($true, $false)) {
            $source = if ($shortSource) { $fixture.ShortPath } else { $fixture.LongPath }
            $destinationRoot = if ($shortSource) { $fixture.LongPath } else { $fixture.ShortPath }
            $relative = 'PowerToysContainmentMustNotCreate-' + [guid]::NewGuid().ToString('N') + '\deeper\runtime.zip'
            $destination = Join-Path $destinationRoot $relative
            { & $script:archiveScript -ProductRoot $source -ArchivePath $destination -ReadyToRun -Crossgen2Path 'unused.exe' } |
                Should Throw 'outside the product'
            Test-Path (Join-Path $fixture.LongPath ($relative.Split('\')[0])) | Should Be $false
        }
    }

    It 'normalizes new destination descendants through their existing PSDrive ancestor' {
        $driveName = 'Mwb' + [guid]::NewGuid().ToString('N')
        $null = New-PSDrive -Name $driveName -PSProvider FileSystem -Root $root
        try {
            $result = Resolve-MwbArchiveFileSystemPath "${driveName}:\missing\deeper\runtime.zip" -AllowMissing
            $result | Should Be (Join-Path (Get-Item -LiteralPath $root).FullName 'missing\deeper\runtime.zip')
            Test-Path (Join-Path $root 'missing') | Should Be $false
        }
        finally { Remove-PSDrive -Name $driveName }
    }

    It 'allows a sibling sharing the source prefix to reach compiler validation' {
        $sibling = $root + '-sibling'
        $null = New-Item -ItemType Directory $sibling
        { & $script:archiveScript -ProductRoot $root -ArchivePath (Join-Path $sibling 'runtime.zip') `
            -ReadyToRun -Crossgen2Path 'unused.exe' } | Should Throw 'existing native SDK'
        Test-Path (Join-Path $sibling 'runtime.zip.staging') | Should Be $false
    }

    AfterAll {
        Remove-Variable R2RPublishedManifest,R2RCollisionWritten -Scope Global -ErrorAction SilentlyContinue
    }
}

Describe 'MWB explicit native SDK compiler integration' {
    It 'preserves tiny-fixture Debug IL and identical copies with <PathKind> staging' -TestCases @(
        @{ PathKind = 'native' }
        @{ PathKind = 'PSDrive' }
    ) -Skip:(
        -not $env:POWERTOYS_TEST_CROSSGEN2_PATH -or -not $env:POWERTOYS_TEST_RUNTIME_REFERENCE_ROOT
    ) {
        param($PathKind)
        $stage = Join-Path $TestDrive ("tiny-stage-$PathKind")
        $null = New-Item -ItemType Directory (Join-Path $stage 'WinUI3Apps') -Force
        $assemblyPath = Join-Path $stage 'Fixture.dll'
        New-ReadyToRunFixtureAssembly $assemblyPath
        $original = Join-Path $TestDrive ("original-$PathKind.dll")
        Copy-Item $assemblyPath $original
        Copy-Item $assemblyPath (Join-Path $stage 'WinUI3Apps\Fixture.dll')
        foreach ($name in @('System.Private.CoreLib.dll', 'System.Runtime.dll')) {
            Copy-Item (Join-Path $env:POWERTOYS_TEST_RUNTIME_REFERENCE_ROOT $name) (Join-Path $stage $name)
        }
        Mock Get-MwbReadyToRunRuntime { [pscustomobject]@{ Version = 'fixture'; Files = @() } }
        $driveName = $null
        try {
            $stagingPath = $stage
            if ($PathKind -eq 'PSDrive') {
                $driveName = 'Mwb' + [guid]::NewGuid().ToString('N')
                $null = New-PSDrive -Name $driveName -PSProvider FileSystem -Root $stage
                $stagingPath = "${driveName}:\"
            }
            $result = Convert-MwbStagedRuntimeToReadyToRun $stagingPath $env:POWERTOYS_TEST_CROSSGEN2_PATH -TimeoutSeconds 120
        }
        finally { if ($driveName) { Remove-PSDrive -Name $driveName } }
        $result.ManagedILAndAttributesUnchanged | Should Be $true
        ($result.VerifiedMethods -gt 0) | Should Be $true
        (Get-FileHash $assemblyPath).Hash | Should Be ((Get-FileHash (Join-Path $stage 'WinUI3Apps\Fixture.dll')).Hash)
        [Microsoft.MouseWithoutBorders.SandboxExperiment.ReadyToRunMetadata]::Read($assemblyPath).Configuration | Should Be 'Debug'
        [Microsoft.MouseWithoutBorders.SandboxExperiment.ReadyToRunMetadata]::Verify($original, $assemblyPath) | Should BeGreaterThan 0
        @(Get-ChildItem $stage -Filter '*.ni.dll' -Recurse).Count | Should Be 0
        @(Get-ChildItem $stage -Filter '.readytorun-*' -Directory).Count | Should Be 0

        $bytes = [IO.File]::ReadAllBytes($assemblyPath)
        $image = [Reflection.PortableExecutable.PEReader]::new([IO.MemoryStream]::new($bytes))
        try {
            $start = $image.PEHeaders.MetadataStartOffset
            $end = $start + $image.PEHeaders.MetadataSize
        }
        finally { $image.Dispose() }
        $pattern = [byte[]]@(1, 0, 5, 68, 101, 98, 117, 103, 0, 0)
        $found = -1
        for ($index = $start; $index -le $end - $pattern.Length; $index++) {
            $matches = $true
            for ($offset = 0; $offset -lt $pattern.Length; $offset++) {
                if ($bytes[$index + $offset] -ne $pattern[$offset]) { $matches = $false; break }
            }
            if ($matches) { $found = $index; break }
        }
        ($found -ge 0) | Should Be $true
        $bytes[$found + 3] = 82
        $altered = Join-Path $TestDrive ("altered-attribute-$PathKind.dll")
        [IO.File]::WriteAllBytes($altered, $bytes)
        { [Microsoft.MouseWithoutBorders.SandboxExperiment.ReadyToRunMetadata]::Verify($original, $altered) } |
            Should Throw 'attributes changed'
    }

    It 'cross-compiles a tiny fixture assembly for a native ARM64 target from this x64 host and preserves Debug IL' -Skip:(
        -not $env:POWERTOYS_TEST_CROSSGEN2_PATH -or -not $env:POWERTOYS_TEST_ARM64_RUNTIME_REFERENCE_ROOT
    ) {
        $stage = Join-Path $TestDrive 'tiny-stage-arm64'
        $null = New-Item -ItemType Directory (Join-Path $stage 'WinUI3Apps') -Force
        $assemblyPath = Join-Path $stage 'Fixture.dll'
        New-ReadyToRunFixtureAssembly $assemblyPath
        $original = Join-Path $TestDrive 'original-arm64.dll'
        Copy-Item $assemblyPath $original
        Copy-Item $assemblyPath (Join-Path $stage 'WinUI3Apps\Fixture.dll')
        foreach ($name in @('System.Private.CoreLib.dll', 'System.Runtime.dll')) {
            Copy-Item (Join-Path $env:POWERTOYS_TEST_ARM64_RUNTIME_REFERENCE_ROOT $name) (Join-Path $stage $name)
        }
        Mock Get-MwbReadyToRunRuntime { [pscustomobject]@{ Version = 'fixture'; Files = @() } }
        $result = Convert-MwbStagedRuntimeToReadyToRun $stage $env:POWERTOYS_TEST_CROSSGEN2_PATH `
            -TargetArchitecture arm64 -TimeoutSeconds 120
        $result.ManagedILAndAttributesUnchanged | Should Be $true
        $result.TargetArchitecture | Should Be 'arm64'
        ($result.VerifiedMethods -gt 0) | Should Be $true
        (Get-FileHash $assemblyPath).Hash | Should Be ((Get-FileHash (Join-Path $stage 'WinUI3Apps\Fixture.dll')).Hash)
        [Microsoft.MouseWithoutBorders.SandboxExperiment.ReadyToRunMetadata]::Read($assemblyPath).Configuration | Should Be 'Debug'
        [Microsoft.MouseWithoutBorders.SandboxExperiment.ReadyToRunMetadata]::Verify($original, $assemblyPath, 'arm64') | Should BeGreaterThan 0
        @(Get-ChildItem $stage -Filter '*.ni.dll' -Recurse).Count | Should Be 0
        @(Get-ChildItem $stage -Filter '.readytorun-*' -Directory).Count | Should Be 0

        # Prove the produced image is genuinely native ARM64 (Machine 0xAA64) and not x64 or the
        # ARM64EC hybrid (0xA641), i.e. a real cross-compilation output, not a managed fallback.
        $peBytes = [IO.File]::ReadAllBytes($assemblyPath)
        $peHeaderOffset = [BitConverter]::ToInt32($peBytes, 0x3C)
        $machine = [BitConverter]::ToUInt16($peBytes, $peHeaderOffset + 4)
        $machine | Should Be 0xAA64
    }

    It 'terminates only its owned native compiler when the deadline expires' -Skip:(-not $env:POWERTOYS_TEST_CROSSGEN2_PATH) {
        $response = Join-Path $TestDrive 'wait.rsp'
        $assembly = Join-Path $TestDrive 'wait-input.dll'
        New-ReadyToRunFixtureAssembly $assembly
        @('--waitfordebugger', ('--out:"{0}"' -f (Join-Path $TestDrive 'wait-output.dll')), ('"{0}"' -f $assembly)) |
            Set-Content $response
        $watch = [Diagnostics.Stopwatch]::StartNew()
        { Invoke-MwbReadyToRunCompiler $env:POWERTOYS_TEST_CROSSGEN2_PATH $response 1 } | Should Throw 'bounded compilation time'
        ($watch.Elapsed.TotalSeconds -lt 15) | Should Be $true
    }
}
