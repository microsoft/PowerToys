# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Initialize-MwbReadyToRunMetadata {
    if (-not ('Microsoft.MouseWithoutBorders.SandboxExperiment.ReadyToRunMetadata' -as [type])) {
        Add-Type -Path (Join-Path $PSScriptRoot 'MwbReadyToRunMetadata.cs')
    }
}

function Get-MwbReadyToRunFileIdentity {
    param([string]$Path, [switch]$Native, [ValidateSet('x64', 'arm64')][string]$NativeArchitecture = 'x64')
    $ErrorActionPreference = 'Stop'
    $file = Get-Item -LiteralPath $Path -ErrorAction Stop
    if ($file.PSIsContainer -or $file.PSProvider.Name -ne 'FileSystem') {
        throw "A ReadyToRun input must be an existing file: $Path"
    }
    if ($Native -and -not [Microsoft.MouseWithoutBorders.SandboxExperiment.ReadyToRunMetadata]::IsNativeArchitecture($file.FullName, $NativeArchitecture)) {
        throw "ReadyToRun requires the $NativeArchitecture native compiler/runtime file: $Path"
    }
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($file.FullName)
    $commit = $null
    if ($version.ProductVersion -match '(?:@Commit:\s*|\+)(?<commit>[0-9a-fA-F]{40})$') {
        $commit = $Matches.commit.ToLowerInvariant()
    }
    [pscustomobject]@{
        Path = $file.FullName
        FileVersion = '{0}.{1}.{2}.{3}' -f $version.FileMajorPart, $version.FileMinorPart, $version.FileBuildPart, $version.FilePrivatePart
        ProductVersion = $version.ProductVersion
        Commit = $commit
        Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
}

function Get-MwbReadyToRunCompiler {
    param([string]$Crossgen2Path, [ValidateSet('x64', 'arm64')][string]$TargetArchitecture = 'x64')
    $ErrorActionPreference = 'Stop'
    if ([string]::IsNullOrWhiteSpace($Crossgen2Path) -or
        -not (Test-Path -LiteralPath $Crossgen2Path -PathType Leaf) -or
        [IO.Path]::GetFileName($Crossgen2Path) -ine 'crossgen2.exe') {
        throw 'Pass an existing native SDK crossgen2.exe explicitly with -Crossgen2Path.'
    }
    # The compiler executable and every native JIT dependency are host (x64) binaries
    # regardless of which architecture they are asked to target.
    $compiler = Get-MwbReadyToRunFileIdentity $Crossgen2Path -Native
    if ($compiler.ProductVersion -notmatch '^\d+\.\d+\.\d+(?:[-+].*)?$') {
        throw 'The supplied compiler does not identify a .NET runtime version.'
    }
    $productVersion = [version][regex]::Match($compiler.ProductVersion, '^\d+\.\d+\.\d+').Value
    $fileVersion = [version]$compiler.FileVersion
    if ($productVersion.Major -ne $fileVersion.Major -or $productVersion.Minor -ne $fileVersion.Minor) {
        throw 'The compiler file and product version resources disagree.'
    }
    # Crossgen2 selects its target-architecture JIT by file name. x64 uses the OS-specific
    # clrjit_win_x64_x64.dll; ARM64 codegen is shared across OSes, so the SDK ships it as
    # clrjit_universal_arm64_x64.dll instead of a win-specific name.
    $targetJit = if ($TargetArchitecture -eq 'arm64') { 'clrjit_universal_arm64_x64.dll' } else { 'clrjit_win_x64_x64.dll' }
    $files = @($compiler)
    foreach ($name in @('jitinterface_x64.dll', $targetJit)) {
        $dependency = Get-MwbReadyToRunFileIdentity (Join-Path (Split-Path $compiler.Path) $name) -Native
        if ($dependency.FileVersion -ne $compiler.FileVersion -or
            ($compiler.Commit -and $dependency.Commit -ne $compiler.Commit)) {
            throw "The compiler and its native dependency differ: $name"
        }
        $files += $dependency
    }
    [pscustomobject]@{
        Path = $compiler.Path; ProductVersion = $compiler.ProductVersion; Files = $files
        TargetArchitecture = $TargetArchitecture
    }
}

function Get-MwbReadyToRunRuntime {
    param([string]$StageRoot, $Compiler)
    $ErrorActionPreference = 'Stop'
    $targetArchitecture = if ($Compiler.PSObject.Properties['TargetArchitecture']) { $Compiler.TargetArchitecture } else { 'x64' }
    $runtimeVersion = [regex]::Match($Compiler.ProductVersion, '^\d+\.\d+\.\d+').Value
    $actualVersion = [version]$runtimeVersion
    $declarations = @()
    foreach ($application in @(
        'PowerToys.MouseWithoutBorders', 'PowerToys.MouseWithoutBordersHelper',
        'WinUI3Apps\PowerToys.Settings', 'WinUI3Apps\PowerToys.QuickAccess'
    )) {
        $base = Join-Path $StageRoot $application
        $configuration = Get-Content -LiteralPath "$base.runtimeconfig.json" -Raw | ConvertFrom-Json
        $frameworks = @($configuration.runtimeOptions.includedFrameworks)
        $core = @($frameworks | Where-Object name -eq 'Microsoft.NETCore.App')
        $desktop = @($frameworks | Where-Object name -eq 'Microsoft.WindowsDesktop.App')
        if ($core.Count -ne 1 -or $desktop.Count -ne 1 -or
            $core[0].version -cne $desktop[0].version -or
            $core[0].version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
            throw "ReadyToRun requires matching self-contained Core/Desktop runtime versions: $application"
        }
        $declared = [string]$core[0].version
        $declaredVersion = [version]($declared.Split('-')[0])
        # includedFrameworks is informational for self-contained apps. A serviced
        # runtime can have a newer patch than this declaration; never rewrite it.
        if ($declaredVersion.Major -ne $actualVersion.Major -or $declaredVersion.Minor -ne $actualVersion.Minor -or
            $declaredVersion.Build -gt $actualVersion.Build -or
            ($declared.Contains('-') -and $Compiler.ProductVersion -notmatch ('^' + [regex]::Escape($declared) + '(?:[-+]|$)'))) {
            throw "The compiler/runtime cannot satisfy the framework declaration: $application"
        }
        $declarations += [pscustomobject]@{
            Application = $application; Version = $declared
            RuntimeConfigSha256 = (Get-FileHash -LiteralPath "$base.runtimeconfig.json" -Algorithm SHA256).Hash
        }
        $assembly = [Microsoft.MouseWithoutBorders.SandboxExperiment.ReadyToRunMetadata]::Read("$base.dll")
        if ($null -eq $assembly -or $assembly.Configuration -cne 'Debug') {
            throw "ReadyToRun preparation requires the existing Debug assembly: $application"
        }
    }
    $files = @()
    foreach ($directory in @($StageRoot, (Join-Path $StageRoot 'WinUI3Apps'))) {
        foreach ($name in @('coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'clrjit.dll', 'System.Private.CoreLib.dll')) {
            # coreclr/hostfxr/hostpolicy/clrjit are the staged target-architecture runtime;
            # System.Private.CoreLib.dll is managed IL and is never native-checked.
            $identity = Get-MwbReadyToRunFileIdentity (Join-Path $directory $name) `
                -Native:($name -ne 'System.Private.CoreLib.dll') -NativeArchitecture $targetArchitecture
            if ($identity.FileVersion -ne $Compiler.Files[0].FileVersion -or
                ($Compiler.Files[0].Commit -and $identity.Commit -ne $Compiler.Files[0].Commit)) {
                throw "The compiler and staged CLR build differ: $($identity.Path)"
            }
            $files += $identity
        }
    }
    foreach ($group in $files | Group-Object { [IO.Path]::GetFileName($_.Path) }) {
        if (@($group.Group.Sha256 | Select-Object -Unique).Count -ne 1) {
            throw "Staged root/WinUI3Apps CLR copies differ: $($group.Name)"
        }
    }
    [pscustomobject]@{ Version = $runtimeVersion; Files = $files; DeclaredFrameworks = $declarations }
}

function Get-MwbReadyToRunAssemblies {
    param([string]$StageRoot)
    foreach ($file in Get-ChildItem -LiteralPath $StageRoot -Filter '*.dll' -File -Recurse | Sort-Object FullName) {
        $metadata = [Microsoft.MouseWithoutBorders.SandboxExperiment.ReadyToRunMetadata]::Read($file.FullName)
        if ($null -eq $metadata) { continue }
        [pscustomobject]@{
            Path = $file.FullName
            RelativePath = $file.FullName.Substring($StageRoot.Length + 1)
            Metadata = $metadata
            Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        }
    }
}

function Invoke-MwbReadyToRunCompiler {
    param([string]$CompilerPath, [string]$ResponsePath, [int]$TimeoutSeconds)
    $ErrorActionPreference = 'Stop'
    $start = [Diagnostics.ProcessStartInfo]::new($CompilerPath)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.ArgumentList.Add("@$ResponsePath")
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            throw "Crossgen2 exceeded its bounded compilation time ($TimeoutSeconds seconds)."
        }
        if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout, $stderr), 5000)) {
            throw 'Crossgen2 output streams did not close.'
        }
        $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            throw "Crossgen2 failed (exit $($process.ExitCode)): $($output.Substring([Math]::Max(0, $output.Length - 4000)))"
        }
        $output
    }
    finally {
        try {
            if (-not $process.HasExited) {
                $process.Kill($true)
                if (-not $process.WaitForExit(5000)) {
                    throw 'The owned Crossgen2 process did not exit after termination.'
                }
            }
        }
        finally { $process.Dispose() }
    }
}

function Convert-MwbStagedRuntimeToReadyToRun {
    param(
        [string]$StageRoot,
        [string]$Crossgen2Path,
        [ValidateSet('x64', 'arm64')][string]$TargetArchitecture = 'x64',
        [ValidateRange(1, 16)][int]$Parallelism = 2,
        [ValidateRange(1, 3600)][int]$TimeoutSeconds = 600
    )
    $ErrorActionPreference = 'Stop'
    Initialize-MwbReadyToRunMetadata
    $stage = Get-Item -LiteralPath $StageRoot -Force -ErrorAction Stop
    if ($stage.PSProvider.Name -ne 'FileSystem' -or -not $stage.PSIsContainer) {
        throw 'ReadyToRun staging must be an existing filesystem directory.'
    }
    $root = $stage.FullName.TrimEnd('\')
    $compiler = Get-MwbReadyToRunCompiler $Crossgen2Path -TargetArchitecture $TargetArchitecture
    $runtime = Get-MwbReadyToRunRuntime $root $compiler
    $assemblies = @(Get-MwbReadyToRunAssemblies $root)
    $references = @()
    foreach ($group in $assemblies | Where-Object { $_.Metadata.Name -notlike '*.resources' } | Group-Object { $_.Metadata.Name } | Sort-Object Name) {
        if (@($group.Group.Sha256 | Select-Object -Unique).Count -ne 1) {
            throw "Conflicting assembly copies prevent coherent ReadyToRun compilation: $($group.Name)"
        }
        $references += $group.Group[0]
    }
    $inputs = @($references | Where-Object { $_.Metadata.ILOnly -and -not $_.Metadata.ReadyToRun })
    $nativeOutputs = @()
    foreach ($assembly in $inputs) {
        $native = Join-Path (Split-Path $assembly.Path) ([IO.Path]::GetFileNameWithoutExtension($assembly.Path) + '.ni.dll')
        if (Test-Path -LiteralPath $native) { throw "Refusing an existing compiler output: $native" }
        $nativeOutputs += $native
    }
    $work = Join-Path $root ('.readytorun-' + [guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $work
    $verified = @()
    $watch = [Diagnostics.Stopwatch]::StartNew()
    try {
        $options = @('--single-file-compilation', '--out-near-input', '--targetos:windows',
            "--targetarch:$TargetArchitecture", "--parallelism:$Parallelism", '--optimize')
        $arguments = @($options)
        foreach ($reference in $references) { $arguments += '-r:"{0}"' -f $reference.Path }
        foreach ($assembly in $inputs) { $arguments += '"{0}"' -f $assembly.Path }
        $response = Join-Path $work 'crossgen2.rsp'
        [IO.File]::WriteAllLines($response, $arguments, [Text.UTF8Encoding]::new($false))
        if ($inputs.Count) {
            $null = Invoke-MwbReadyToRunCompiler $compiler.Path $response $TimeoutSeconds
        }
        foreach ($file in @($compiler.Files) + @($runtime.Files) + @($assemblies)) {
            if ((Get-FileHash -LiteralPath $file.Path -Algorithm SHA256).Hash -ne $file.Sha256) {
                throw "A compiler/reference input changed during compilation: $($file.Path)"
            }
        }
        foreach ($declaration in @($runtime.DeclaredFrameworks)) {
            if ($null -eq $declaration) { continue }
            $configurationPath = Join-Path $root ($declaration.Application + '.runtimeconfig.json')
            if ((Get-FileHash -LiteralPath $configurationPath -Algorithm SHA256).Hash -ne $declaration.RuntimeConfigSha256) {
                throw "A runtime configuration changed during compilation: $configurationPath"
            }
        }
        for ($index = 0; $index -lt $inputs.Count; $index++) {
            $assembly = $inputs[$index]
            $native = $nativeOutputs[$index]
            if (-not (Test-Path -LiteralPath $native -PathType Leaf)) { throw "Missing Crossgen2 output: $native" }
            $methods = [Microsoft.MouseWithoutBorders.SandboxExperiment.ReadyToRunMetadata]::Verify($assembly.Path, $native, $TargetArchitecture)
            $copies = @($assemblies | Where-Object Sha256 -eq $assembly.Sha256)
            $verified += [pscustomobject]@{
                Name = $assembly.Metadata.Name; Identity = $assembly.Metadata.Identity; Mvid = $assembly.Metadata.Mvid
                InputSha256 = $assembly.Sha256; OutputSha256 = (Get-FileHash -LiteralPath $native -Algorithm SHA256).Hash
                Paths = @($copies.RelativePath); MethodsPerCopy = $methods; NativePath = $native
            }
        }
        # Verify every output before replacing any private staged input. The caller
        # owns this staging tree and removes it on failure; the build output is never changed.
        foreach ($image in $verified) {
            foreach ($relative in $image.Paths) {
                $destination = Join-Path $root $relative
                [IO.File]::Copy($image.NativePath, $destination, $true)
                if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $image.OutputSha256) {
                    throw "A ReadyToRun assembly copy differs: $relative"
                }
            }
        }
        [pscustomobject]@{
            FormatVersion = 1; RuntimeVersion = $runtime.Version; Compiler = $compiler
            TargetArchitecture = $TargetArchitecture
            RuntimeFiles = $runtime.Files; DeclaredFrameworks = $runtime.DeclaredFrameworks
            Options = $options; CompiledAssemblies = $inputs.Count; ElapsedSeconds = $watch.Elapsed.TotalSeconds
            VerifiedAssemblyCopies = ($verified | ForEach-Object { $_.Paths.Count } | Measure-Object -Sum).Sum
            VerifiedMethods = ($verified | ForEach-Object { $_.MethodsPerCopy * $_.Paths.Count } | Measure-Object -Sum).Sum
            ManagedILAndAttributesUnchanged = $true
            Assemblies = @($verified | Select-Object Name, Identity, Mvid, InputSha256, OutputSha256, Paths, MethodsPerCopy)
        }
    }
    finally {
        foreach ($native in $nativeOutputs) {
            if (Test-Path -LiteralPath $native) { Remove-Item -LiteralPath $native -Force }
        }
        Remove-Item -LiteralPath $work -Recurse -Force
    }
}
