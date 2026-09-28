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

# Match the trusted family required by WinAppSandbox.AssertPrerequisites.
$script:MwbSandboxPackageFamily = 'MicrosoftWindows.WindowsSandbox_cw5n1h2txyewy'

function Import-MwbAppxCompat {
    # pwsh does not always project the inbox Appx module; import it through the Windows
    # PowerShell compatibility layer once, on demand. A failure here is a legitimate,
    # reportable query error, not a reason to fall back to a different check.
    if (-not (Get-Command Get-AppxPackage -ErrorAction SilentlyContinue)) {
        Import-Module Appx -UseWindowsPowerShell -WarningAction SilentlyContinue -ErrorAction Stop
    }
}

function Get-MwbDiagnosticError {
    param([Management.Automation.ErrorRecord] $ErrorRecord, [string] $Code)

    [ordered]@{
        Code = $Code
        HResult = '0x{0:X8}' -f ($ErrorRecord.Exception.HResult -band 0xffffffffL)
    }
}

function Get-MwbSandboxPackages {
    param([switch] $AllUsers, [string] $UserSid)

    Import-MwbAppxCompat
    if ($AllUsers) { Get-AppxPackage -Name MicrosoftWindows.WindowsSandbox -AllUsers -ErrorAction Stop }
    else { Get-AppxPackage -Name MicrosoftWindows.WindowsSandbox -User $UserSid -ErrorAction Stop }
}

function Get-MwbSandboxProvisionedPackages {
    Get-AppxProvisionedPackage -Online -ErrorAction Stop
}

function ConvertTo-MwbPackageDiagnostic {
    param($Package, [switch] $IncludeUsers)

    if ($Package.PackageFullName -cnotmatch '^MicrosoftWindows\.WindowsSandbox_\d+\.\d+\.\d+\.\d+_(?:x64|arm64|x86|neutral)_[A-Za-z0-9.~]*_cw5n1h2txyewy$' -or
        [string]$Package.Version -notmatch '^\d+\.\d+\.\d+\.\d+$' -or
        [string]$Package.Status -notmatch '^[A-Za-z]+(?:, [A-Za-z]+)*$') {
        throw 'The Sandbox package inventory has an unexpected identity or state.'
    }
    $result = [ordered]@{
        PackageFullName = [string]$Package.PackageFullName
        PackageFamilyName = $script:MwbSandboxPackageFamily
        Version = [string]$Package.Version
        Status = [string]$Package.Status
    }
    if ($IncludeUsers) {
        $result.Registrations = @($Package.PackageUserInformation | ForEach-Object {
            $sid = if ($_.UserSecurityId -is [string]) { $_.UserSecurityId } else { [string]$_.UserSecurityId.Sid }
            $state = [string]$_.InstallState
            if ($sid -notmatch '^S-1-(?:\d+-)+\d+$' -or $state -notin @('NotInstalled', 'Staged', 'Installed')) {
                throw 'The Sandbox registration inventory has an unexpected identity or state.'
            }
            [ordered]@{ UserSid = $sid; InstallState = $state }
        })
    }
    $result
}

function Get-MwbSandboxAllUsersPackageReport {
    # Requires the elevated Prepare identity. Distinguishes a confirmed-absent inventory
    # (query succeeded, zero matches) from a query failure (exception): both are reportable
    # facts, never collapsed into a single "not found" state.
    $report = [ordered]@{ QueryError = $null; Packages = @() }
    try {
        $packages = @(Get-MwbSandboxPackages -AllUsers |
            Where-Object { $_.PackageFamilyName -ceq $script:MwbSandboxPackageFamily })
        $report.Packages = @($packages | ForEach-Object {
            ConvertTo-MwbPackageDiagnostic $_ -IncludeUsers
        })
    }
    catch {
        $report.QueryError = Get-MwbDiagnosticError $_ 'AllUsersPackageQueryFailed'
    }
    $report
}

function Get-MwbSandboxProvisionedPackageReport {
    $report = [ordered]@{ QueryError = $null; Packages = @() }
    try {
        $packages = @(Get-MwbSandboxProvisionedPackages |
            Where-Object {
                $_.DisplayName -ceq 'MicrosoftWindows.WindowsSandbox' -and
                $_.PackageName -clike 'MicrosoftWindows.WindowsSandbox_*_cw5n1h2txyewy'
            })
        $report.Packages = @($packages | ForEach-Object {
            if ($_.PackageName -cnotmatch '^MicrosoftWindows\.WindowsSandbox_\d+\.\d+\.\d+\.\d+_(?:x64|arm64|x86|neutral)_[A-Za-z0-9.~]*_cw5n1h2txyewy$' -or
                [string]$_.Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
                throw 'The provisioned Sandbox package has an unexpected identity.'
            }
            [ordered]@{
                PackageFullName = [string]$_.PackageName
                PackageFamilyName = $script:MwbSandboxPackageFamily
                Version = [string]$_.Version
            }
        })
    }
    catch {
        $report.QueryError = Get-MwbDiagnosticError $_ 'ProvisionedPackageQueryFailed'
    }
    $report
}

function Get-MwbSandboxInteractiveUserReport {
    # This is an elevated inventory query by SID, not execution as the desktop user.
    # Only the test's existing bounded probe can establish that user's wsb readiness.
    $report = [ordered]@{
        QueryScope = 'ExplicitUserSid'
        UserName = $null; UserSid = $null; QueryError = $null
        PackageCount = $null; PackageAbsent = $null; PackageQueryError = $null; Packages = @()
        WsbVersionProbe = [ordered]@{
            Status = 'NotChecked'; Reason = 'RequiresLimitedInteractiveUser'
            EvidenceFile = 'prerequisite-user.json'
        }
    }
    $userSid = $null
    try {
        $userName = (Get-CimInstance Win32_ComputerSystem -ErrorAction Stop).UserName
        if ([string]::IsNullOrWhiteSpace($userName)) {
            $report.QueryError = [ordered]@{ Code = 'InteractiveUserNotLoggedOn'; HResult = $null }
        }
        else {
            $userSid = [Security.Principal.NTAccount]::new($userName).Translate([Security.Principal.SecurityIdentifier])
            $report.UserName = ($userSid.Translate([Security.Principal.NTAccount]).Value -split '\\')[-1]
            if ($report.UserName -notmatch '^[\p{L}\p{N}_. -]{1,64}$') { $report.UserName = 'OtherUser' }
            $report.UserSid = $userSid.Value
        }
    }
    catch {
        $report.QueryError = Get-MwbDiagnosticError $_ 'InteractiveUserQueryFailed'
    }
    if (-not $userSid) { return $report }

    try {
        $packages = @(Get-MwbSandboxPackages -UserSid $userSid.Value |
            Where-Object { $_.PackageFamilyName -ceq $script:MwbSandboxPackageFamily })
        $report.PackageCount = $packages.Count
        $report.PackageAbsent = ($packages.Count -eq 0)
        $report.Packages = @($packages | ForEach-Object {
            ConvertTo-MwbPackageDiagnostic $_
        })
    }
    catch {
        $report.PackageCount = $null
        $report.PackageAbsent = $null
        $report.PackageQueryError = Get-MwbDiagnosticError $_ 'InteractiveUserPackageQueryFailed'
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
        SchemaVersion = 3
        GeneratedUtc = [DateTime]::UtcNow.ToString('o')
        Platform = if ($Platform -cin @('x64Win10', 'x64Win11', 'arm64')) { $Platform } else { 'Unknown' }
        CollectionContext = 'ElevatedPrepareInventory'
        OSArchitecture = $null
        OSBuildNumber = $null
        OSVersionString = $null
        OSQueryError = $null
        SandboxFeatureState = $null
        SandboxFeatureQueryError = $null
        AllUsersPackages = $null
        ProvisionedPackages = $null
        InteractiveUser = $null
        InventoryConclusion = 'UnknownQueryFailure'
        InteractiveProbe = [ordered]@{
            Status = 'NotChecked'; Reason = 'PrepareDoesNotExecuteAsTestUser'
            EvidenceFile = 'prerequisite-user.json'
        }
        StagesNotChecked = $stagesNotChecked
    }

    try {
        $os = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
        if ([string]$os.BuildNumber -notmatch '^\d+$' -or [string]$os.Version -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') {
            throw 'The OS inventory returned an unexpected version.'
        }
        $report.OSArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        $report.OSBuildNumber = [string]$os.BuildNumber
        $report.OSVersionString = [string]$os.Version
    }
    catch {
        $report.OSQueryError = Get-MwbDiagnosticError $_ 'OperatingSystemQueryFailed'
        $stagesNotChecked.Add('OperatingSystem')
    }

    try {
        $feature = Get-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -ErrorAction Stop
        if ([string]$feature.State -notin @('Enabled', 'Disabled', 'EnablePending', 'DisablePending',
            'DisabledWithPayloadRemoved', 'PartiallyInstalled', 'Superseded')) {
            throw 'The Sandbox feature query returned an unexpected state.'
        }
        $report.SandboxFeatureState = $feature.State.ToString()
    }
    catch {
        $report.SandboxFeatureQueryError = Get-MwbDiagnosticError $_ 'SandboxFeatureQueryFailed'
        $stagesNotChecked.Add('SandboxFeatureState')
    }

    $report.AllUsersPackages = Get-MwbSandboxAllUsersPackageReport
    $report.ProvisionedPackages = Get-MwbSandboxProvisionedPackageReport
    $report.InteractiveUser = Get-MwbSandboxInteractiveUserReport
    if ($report.AllUsersPackages.QueryError) { $stagesNotChecked.Add('AllUsersPackages') }
    if ($report.ProvisionedPackages.QueryError) { $stagesNotChecked.Add('ProvisionedPackages') }
    if ($report.InteractiveUser.QueryError) { $stagesNotChecked.Add('InteractiveUserIdentity') }
    if (-not $report.InteractiveUser.UserSid -or $report.InteractiveUser.PackageQueryError) { $stagesNotChecked.Add('InteractiveUserPackages') }
    $stagesNotChecked.Add('InteractiveUserAlias')
    $stagesNotChecked.Add('WsbVersionProbe')
    if (-not $report.AllUsersPackages.QueryError -and -not $report.ProvisionedPackages.QueryError -and
        -not $report.InteractiveUser.QueryError -and -not $report.InteractiveUser.PackageQueryError) {
        $report.InventoryConclusion = if ($report.InteractiveUser.PackageCount -gt 0) { 'RegisteredForTestUser' }
            elseif (@($report.AllUsersPackages.Packages | ForEach-Object Registrations |
                Where-Object InstallState -EQ 'Installed').Count -gt 0) { 'RegisteredForOtherUser' }
            elseif ($report.AllUsersPackages.Packages.Count -gt 0) { 'StagedOnly' }
            elseif ($report.ProvisionedPackages.Packages.Count -gt 0) { 'ProvisionedOnly' }
            else { 'AbsentEverywhere' }
    }

    $report.StagesNotChecked = @($stagesNotChecked)
    $report
}

function Get-MwbPrerequisiteDirectory {
    param([string] $ResultsDirectory, [guid] $RunId)

    Assert-MwbCiPlainPath $ResultsDirectory
    Join-Path $ResultsDirectory "mwb-prerequisites-$RunId"
}

function Write-MwbSandboxPrerequisiteReport {
    param([string] $ResultsDirectory, [guid] $RunId, [string] $Platform)

    $directory = Get-MwbPrerequisiteDirectory $ResultsDirectory $RunId
    Assert-MwbCiPlainPath $directory
    $null = New-Item -ItemType Directory -Path $directory -Force
    try { $report = Get-MwbSandboxPrerequisiteReport -Platform $Platform }
    catch {
        $report = [ordered]@{
            SchemaVersion = 3; CollectionContext = 'ElevatedPrepareInventory'
            Status = 'QueryFailed'; QueryError = Get-MwbDiagnosticError $_ 'ReportGenerationFailed'
            InteractiveProbe = [ordered]@{ Status = 'NotChecked'; Reason = 'PrepareDoesNotExecuteAsTestUser' }
        }
    }
    $report | ConvertTo-Json -Depth 10 |
        Set-Content -LiteralPath (Join-Path $directory 'prerequisite-admin.json') -Encoding utf8
    Write-Host 'MWB Sandbox allowlisted prerequisite-admin.json written before preparation gates.'
}
