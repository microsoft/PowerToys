# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

. "$PSScriptRoot\WinAppCli.Common.ps1"

function Get-MwbCiBackend {
    param([string] $Platform, [int] $WindowsBuild)

    if ($Platform -ceq 'x64Win10' -and $WindowsBuild -ge 19041 -and $WindowsBuild -lt 22000) {
        return 'Legacy'
    }
    # ARM64 has no Windows 10 tier in this pilot: the single arm64 test pool is a Windows 11
    # 24H2+ image, so it only ever selects the modern WinApp backend, exactly like x64Win11.
    if (($Platform -ceq 'x64Win11' -or $Platform -ceq 'arm64') -and $WindowsBuild -ge 26100) {
        return 'WinApp'
    }
    throw "BLOCKED_INFRASTRUCTURE: MWB platform $Platform does not match Windows build $WindowsBuild. Win11 (x64 or ARM64) requires the registered modern Sandbox client on 24H2 or newer; no Legacy fallback is allowed."
}

function Get-MwbCiArchitecture {
    param([string] $Platform)

    if ($Platform -ceq 'x64Win10' -or $Platform -ceq 'x64Win11') { return 'x64' }
    if ($Platform -ceq 'arm64') { return 'arm64' }
    throw "BLOCKED_INFRASTRUCTURE: unrecognized MWB test platform $Platform."
}

function Resolve-MwbCiFileSystemPath {
    param([string] $Path, [switch] $AllowMissing)

    $provider = $null
    $drive = $null
    $full = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path, [ref]$provider, [ref]$drive)
    if ($provider.Name -ne 'FileSystem') { throw 'MWB CI preparation paths must use the FileSystem provider.' }
    $ancestor = [IO.Path]::GetFullPath($full)
    $missing = [Collections.Generic.Stack[string]]::new()
    if ($AllowMissing) {
        while (-not (Test-Path -LiteralPath $ancestor)) {
            $parent = [IO.Path]::GetDirectoryName($ancestor)
            if ([string]::IsNullOrEmpty($parent) -or $parent -eq $ancestor) {
                throw 'The MWB CI preparation path has no existing filesystem ancestor.'
            }
            $missing.Push([IO.Path]::GetFileName($ancestor))
            $ancestor = $parent
        }
    }
    # Match Resolve-MwbArchiveFileSystemPath: Resolve-Path can preserve DOS aliases.
    # Canonicalize the existing filesystem ancestor before adding missing descendants.
    $item = Get-Item -LiteralPath $ancestor -Force -ErrorAction Stop
    if ($missing.Count -and -not $item.PSIsContainer) {
        throw 'The MWB CI preparation path has a non-directory ancestor.'
    }
    $normalized = $item.FullName
    while ($missing.Count) { $normalized = [IO.Path]::Combine($normalized, $missing.Pop()) }
    [IO.Path]::GetFullPath($normalized)
}

function Assert-MwbCiPlainPath {
    param([string] $Path)

    if ($Path -notmatch '^[A-Za-z]:\\') { throw 'MWB CI requires local absolute artifact paths.' }
    $ancestor = [IO.Path]::GetFullPath($Path)
    while ($ancestor) {
        if ((Test-Path -LiteralPath $ancestor) -and
            ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'MWB CI artifact paths must not contain reparse points.'
        }
        $ancestor = Split-Path $ancestor -Parent
    }
}

function Assert-MwbCiHash {
    param([string] $Path, [string] $Sha256)

    Assert-MwbCiPlainPath $Path
    if ($Sha256 -notmatch '^[0-9a-fA-F]{64}$' -or
        -not (Test-Path -LiteralPath $Path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ine $Sha256) {
        throw "MWB CI artifact hash mismatch: $([IO.Path]::GetFileName($Path))."
    }
}

function Get-MwbCiRelativePath {
    param([string] $Root, [string] $RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        $RelativePath -match '(^[\\/]|[:"<>|?*\x00-\x1f]|(^|[\\/])\.\.?([\\/]|$)|[. ]([\\/]|$))') {
        throw 'MWB CI manifests require confined relative file paths.'
    }
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $path = [IO.Path]::GetFullPath((Join-Path $rootPath $RelativePath))
    if (-not $path.StartsWith("$rootPath\", [StringComparison]::OrdinalIgnoreCase)) {
        throw 'MWB CI manifest file escaped its root.'
    }
    $path
}

function Get-MwbCiBundle {
    param([string] $Root, [string] $SourceRevision, [ValidateSet('x64', 'arm64')][string] $Platform = 'x64')

    $bundle = Join-Path $Root 'mwb-sandbox-ci'
    $manifestPath = Join-Path $bundle 'manifest.json'
    Assert-MwbCiPlainPath $manifestPath
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "BLOCKED_INFRASTRUCTURE: build-$Platform-Debug lacks the build-job MWB CI bundle. Rebuild the gated pilot; MWB test preparation never builds or downloads CLI/compiler bits."
    }
    $manifestBytes = [IO.File]::ReadAllBytes($manifestPath)
    $manifest = [Text.Encoding]::UTF8.GetString($manifestBytes).TrimStart([char]0xFEFF) | ConvertFrom-Json
    $manifestHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($manifestBytes))
    $pin = Get-WinAppCliRelease -Platform $Platform
    if ($SourceRevision -notmatch '^[0-9a-fA-F]{40}$' -or
        $manifest.FormatVersion -ne 2 -or $manifest.Platform -cne $Platform -or
        $manifest.Configuration -cne 'Debug' -or $manifest.SourceRevision -ine $SourceRevision -or
        $manifest.WinAppCli.Repository -cne $pin.Repository -or
        $manifest.WinAppCli.Tag -cne $pin.Tag -or
        $manifest.WinAppCli.Version -cne $pin.Version -or
        $manifest.WinAppCli.Asset -cne $pin.Asset -or
        $manifest.WinAppCli.Sha256 -ine $pin.Sha256) {
        throw 'MWB CI bundle provenance does not match the selected Debug revision and official winapp CLI release.'
    }
    Assert-MwbCiHash (Join-Path $bundle 'runtime.zip') $manifest.Runtime.Sha256
    Assert-MwbCiHash (Join-Path $bundle 'runtime.zip.manifest.json') $manifest.Runtime.ManifestSha256
    $runtime = Get-Content -LiteralPath (Join-Path $bundle 'runtime.zip.manifest.json') -Raw | ConvertFrom-Json
    if ($runtime.Sha256 -ine $manifest.Runtime.Sha256 -or $runtime.FileCount -ne @($manifest.Runtime.Files).Count -or
        $manifest.Runtime.ReadyToRun -isnot [bool] -or
        ($manifest.Runtime.ReadyToRun -and -not $runtime.PSObject.Properties['ReadyToRun'])) {
        throw 'MWB CI runtime archive provenance is inconsistent.'
    }
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $manifest.Runtime.Files) {
        $null = Get-MwbCiRelativePath $bundle $entry.Path
        if ($entry.Sha256 -notmatch '^[0-9a-fA-F]{64}$' -or -not $names.Add($entry.Path) -or
            $entry.Path -notin $runtime.Files) {
            throw 'MWB CI runtime file inventory is inconsistent.'
        }
    }
    foreach ($required in @('PowerToys.exe', 'PowerToys.MouseWithoutBorders.exe',
        'PowerToys.MouseWithoutBorders.dll', 'PowerToys.MouseWithoutBordersHelper.exe',
        'WinUI3Apps\PowerToys.Settings.exe', 'WinUI3Apps\PowerToys.QuickAccess.exe')) {
        if (-not $names.Contains($required)) { throw "MWB CI runtime closure is missing $required." }
    }
    if ($manifest.Runtime.ReadyToRun) {
        $compilation = $runtime.ReadyToRun
        if ($compilation.FormatVersion -ne 1 -or $compilation.ManagedILAndAttributesUnchanged -cne $true -or
            $compilation.CompiledAssemblies -lt 1 -or $compilation.CompiledAssemblies -ne @($compilation.Assemblies).Count -or
            @($compilation.Compiler.Files).Count -ne 3) {
            throw 'MWB CI ReadyToRun verification provenance is incomplete.'
        }
        foreach ($assembly in $compilation.Assemblies) {
            foreach ($path in $assembly.Paths) {
                $entries = @($manifest.Runtime.Files | Where-Object Path -CEQ $path)
                if ($entries.Count -ne 1 -or $entries[0].Sha256 -ine $assembly.OutputSha256) {
                    throw 'MWB CI ReadyToRun output hashes disagree with the runtime inventory.'
                }
            }
        }
    }
    if (@($manifest.WinAppCli.Files).Count -ne @($pin.Files).Count) {
        throw 'MWB CI winapp CLI portable file inventory is inconsistent.'
    }
    $cliNames = @($manifest.WinAppCli.Files | ForEach-Object Path)
    foreach ($name in $pin.Files) {
        $entries = @($manifest.WinAppCli.Files | Where-Object Path -CEQ $name)
        if ($entries.Count -ne 1) { throw 'MWB CI winapp CLI portable file inventory is inconsistent.' }
        Assert-MwbCiHash (Join-Path $bundle "winapp-cli\$name") $entries[0].Sha256
    }
    $actualCli = @(Get-ChildItem -LiteralPath (Join-Path $bundle 'winapp-cli') -File -Recurse -Force)
    if ($actualCli.Count -ne $cliNames.Count) {
        throw 'MWB CI winapp CLI contains unmanifested files.'
    }
    [pscustomobject]@{
        Root = $bundle
        Manifest = $manifest
        ManifestSha256 = $manifestHash
    }
}

function Expand-MwbCiRuntime {
    param([string] $Archive, [string] $Destination, [object[]] $Files)

    Assert-MwbCiPlainPath $Archive
    Assert-MwbCiPlainPath $Destination
    if (Test-Path -LiteralPath $Destination) { throw 'MWB CI host staging must be a new private directory.' }
    $expected = @{}
    foreach ($file in $Files) {
        $path = Get-MwbCiRelativePath $Destination $file.Path
        if ($expected.ContainsKey($path)) { throw 'MWB CI runtime has duplicate paths.' }
        $expected[$path] = $file.Sha256
    }
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        $entries = @{}
        foreach ($entry in $zip.Entries) {
            $relative = $entry.FullName -replace '^\.\/', ''
            if (-not $relative -or $relative.EndsWith('/')) { continue }
            $path = Get-MwbCiRelativePath $Destination $relative
            if (-not $expected.ContainsKey($path) -or $entries.ContainsKey($path) -or
                (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) {
                throw 'MWB CI archive contains an unexpected, duplicate, or symbolic-link file.'
            }
            $entries[$path] = $entry
        }
        if ($entries.Count -ne $expected.Count) { throw 'MWB CI archive file inventory is incomplete.' }
        foreach ($path in $entries.Keys) {
            $null = New-Item -ItemType Directory -Path (Split-Path $path) -Force
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entries[$path], $path, $false)
            Assert-MwbCiHash $path $expected[$path]
        }
    }
    finally { $zip.Dispose() }
}

# The MicrosoftWindows.WindowsSandbox package family that the modern WinApp backend and
# WinAppSandbox.cs (AssertPrerequisites) both depend on. Kept in one place so the CI
# diagnostic report and the product check can never silently drift apart.
$script:MwbSandboxPackageFamily = 'MicrosoftWindows.WindowsSandbox_cw5n1h2txyewy'

function Import-MwbAppxCompat {
    # pwsh does not always project the inbox Appx module; import it through the Windows
    # PowerShell compatibility layer once, on demand. A failure here is a legitimate,
    # reportable query error, not a reason to fall back to a different check.
    if (-not (Get-Command Get-AppxPackage -ErrorAction SilentlyContinue)) {
        Import-Module Appx -UseWindowsPowerShell -WarningAction SilentlyContinue -ErrorAction Stop
    }
}

function Get-MwbSandboxAllUsersPackageReport {
    # Requires the elevated Prepare identity. Distinguishes a confirmed-absent inventory
    # (query succeeded, zero matches) from a query failure (exception): both are reportable
    # facts, never collapsed into a single "not found" state.
    $report = [ordered]@{ QueryError = $null; Packages = @() }
    try {
        Import-MwbAppxCompat
        $packages = @(Get-AppxPackage -AllUsers -ErrorAction Stop |
            Where-Object { $_.PackageFamilyName -ceq $script:MwbSandboxPackageFamily })
        $report.Packages = @($packages | ForEach-Object {
            [ordered]@{
                PackageFullName = $_.PackageFullName
                Version = $_.Version.ToString()
                Status = ($_.Status | Out-String).Trim()
                Architecture = $_.Architecture.ToString()
            }
        })
    }
    catch {
        $report.QueryError = $_.Exception.Message
    }
    $report
}

function Get-MwbSandboxProvisionedPackageReport {
    $report = [ordered]@{ QueryError = $null; Packages = @() }
    try {
        Import-MwbAppxCompat
        $packages = @(Get-AppxProvisionedPackage -Online -ErrorAction Stop |
            Where-Object { $_.PackageName -ceq 'MicrosoftWindows.WindowsSandbox' })
        $report.Packages = @($packages | ForEach-Object {
            [ordered]@{ PackageName = $_.PackageName; Version = $_.Version; Architecture = $_.Architecture }
        })
    }
    catch {
        $report.QueryError = $_.Exception.Message
    }
    $report
}

function Invoke-MwbWsbVersionProbe {
    param(
        [Parameter(Mandatory)][string] $ExecutablePath,
        [string[]] $Arguments = @('--version'),
        [int] $TimeoutMilliseconds = 15000
    )

    # A standalone, mockable/directly-testable bounded probe: never waits past
    # TimeoutMilliseconds, never returns the raw command line, and reports a query error
    # instead of throwing so a hung or missing modern client cannot fail the caller.
    $probe = [ordered]@{
        Attempted = $true; ExitCode = $null; TimedOut = $false
        DurationSeconds = $null; VersionText = $null; Error = $null
    }
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $process = $null
    try {
        $start = [Diagnostics.ProcessStartInfo]::new($ExecutablePath)
        foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
        $start.UseShellExecute = $false
        $start.RedirectStandardInput = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $start.CreateNoWindow = $true
        $process = [Diagnostics.Process]::Start($start)
        $process.StandardInput.Close()
        if (-not $process.WaitForExit($TimeoutMilliseconds)) {
            $probe.TimedOut = $true
            $process.Kill()
            $null = $process.WaitForExit(5000)
        }
        else {
            $probe.ExitCode = $process.ExitCode
            $stdout = $process.StandardOutput.ReadToEnd()
            # Keep only a short, already-public version line; never the raw command line
            # or full banner text, which can carry unrelated diagnostic noise.
            $versionLine = @($stdout -split '\r?\n' | Where-Object { $_ -match '^\d+\.\d+\.\d+\s*$' } | Select-Object -First 1)
            if ($versionLine.Count -eq 1) { $probe.VersionText = $versionLine[0].Trim() }
        }
    }
    catch {
        $probe.Error = $_.Exception.Message
    }
    finally {
        $probe.DurationSeconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 3)
        if ($process) { $process.Dispose() }
    }
    $probe
}

function Get-MwbSandboxInteractiveUserReport {
    # The interactive desktop user is the exact identity the modern backend's own
    # AssertPrerequisites check (WinAppSandbox.cs, FindPackagesForUser) runs as. Reporting
    # the same user's package/alias state here explains a later product failure instead of
    # only restating it.
    $report = [ordered]@{
        UserName = $null; UserSid = $null; QueryError = $null
        PackageCount = $null; PackageAbsent = $null; PackageQueryError = $null; Packages = @()
        AliasPath = $null; AliasExists = $null; AliasIsReparsePoint = $null; AliasQueryError = $null
        WsbVersionProbe = [ordered]@{
            Attempted = $false; ExitCode = $null; TimedOut = $false
            DurationSeconds = $null; VersionText = $null; Error = $null
        }
    }
    $userSid = $null
    try {
        $userName = (Get-CimInstance Win32_ComputerSystem -ErrorAction Stop).UserName
        if ([string]::IsNullOrWhiteSpace($userName)) {
            $report.QueryError = 'No interactive desktop user is currently logged on.'
        }
        else {
            $userSid = [Security.Principal.NTAccount]::new($userName).Translate([Security.Principal.SecurityIdentifier])
            $report.UserName = $userSid.Translate([Security.Principal.NTAccount]).Value
            $report.UserSid = $userSid.Value
        }
    }
    catch {
        $report.QueryError = $_.Exception.Message
    }
    if (-not $userSid) { return $report }

    try {
        Import-MwbAppxCompat
        $packages = @(Get-AppxPackage -User $userSid.Value -ErrorAction Stop |
            Where-Object { $_.PackageFamilyName -ceq $script:MwbSandboxPackageFamily })
        $report.PackageCount = $packages.Count
        $report.PackageAbsent = ($packages.Count -eq 0)
        $report.Packages = @($packages | ForEach-Object {
            [ordered]@{
                PackageFullName = $_.PackageFullName
                Version = $_.Version.ToString()
                Status = ($_.Status | Out-String).Trim()
            }
        })
    }
    catch {
        $report.PackageQueryError = $_.Exception.Message
    }

    $aliasPath = $null
    try {
        $profile = Get-CimInstance Win32_UserProfile -Filter "SID='$($userSid.Value)'" -ErrorAction Stop
        if ($profile -and $profile.LocalPath) {
            $aliasPath = Join-Path $profile.LocalPath 'AppData\Local\Microsoft\WindowsApps\wsb.exe'
            $report.AliasPath = $aliasPath
            $report.AliasExists = Test-Path -LiteralPath $aliasPath -PathType Leaf
            if ($report.AliasExists) {
                $attributes = (Get-Item -LiteralPath $aliasPath -Force).Attributes
                $report.AliasIsReparsePoint = [bool]($attributes -band [IO.FileAttributes]::ReparsePoint)
            }
        }
        else {
            $report.AliasQueryError = 'The interactive user profile path could not be resolved.'
        }
    }
    catch {
        $report.AliasQueryError = $_.Exception.Message
    }

    if ($report.AliasExists) {
        $report.WsbVersionProbe = Invoke-MwbWsbVersionProbe -ExecutablePath $aliasPath
    }
    $report
}

function Get-MwbSandboxPrerequisiteReport {
    param([string] $Platform)

    # A stage that could not be evaluated (an earlier unrelated exception, or a
    # dependency the identity/environment does not support) is recorded explicitly in
    # StagesNotChecked; it is never silently omitted or reported as a passing check.
    $stagesNotChecked = [Collections.Generic.List[string]]::new()
    $report = [ordered]@{
        GeneratedUtc = [DateTime]::UtcNow.ToString('o')
        Platform = $Platform
        OSArchitecture = $null
        OSBuildNumber = $null
        OSVersionString = $null
        OSQueryError = $null
        SandboxFeatureState = $null
        SandboxFeatureQueryError = $null
        AllUsersPackages = $null
        ProvisionedPackages = $null
        InteractiveUser = $null
        StagesNotChecked = $stagesNotChecked
    }

    try {
        $os = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
        $report.OSArchitecture = $os.OSArchitecture
        $report.OSBuildNumber = $os.BuildNumber
        $report.OSVersionString = $os.Version
    }
    catch {
        $report.OSQueryError = $_.Exception.Message
        $stagesNotChecked.Add('OperatingSystem')
    }

    try {
        $feature = Get-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -ErrorAction Stop
        $report.SandboxFeatureState = $feature.State.ToString()
    }
    catch {
        $report.SandboxFeatureQueryError = $_.Exception.Message
        $stagesNotChecked.Add('SandboxFeatureState')
    }

    $report.AllUsersPackages = Get-MwbSandboxAllUsersPackageReport
    $report.ProvisionedPackages = Get-MwbSandboxProvisionedPackageReport
    $report.InteractiveUser = Get-MwbSandboxInteractiveUserReport
    if ($report.InteractiveUser.QueryError) { $stagesNotChecked.Add('InteractiveUserIdentity') }
    if ($report.InteractiveUser.PackageQueryError) { $stagesNotChecked.Add('InteractiveUserPackages') }
    if ($report.InteractiveUser.AliasQueryError) { $stagesNotChecked.Add('InteractiveUserAlias') }
    if (-not $report.InteractiveUser.WsbVersionProbe.Attempted) { $stagesNotChecked.Add('WsbVersionProbe') }

    $report.StagesNotChecked = @($stagesNotChecked)
    $report
}
