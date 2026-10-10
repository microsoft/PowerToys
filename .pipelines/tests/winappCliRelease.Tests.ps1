# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
. "$PSScriptRoot\..\WinAppCli.Common.ps1"
$script:officialWinAppCliPinJson = Get-WinAppCliRelease | ConvertTo-Json -Depth 4

function New-WinAppCliReleaseFixture {
    param([string] $Archive, [string[]] $Files, [string] $Platform = 'x64')

    $bytes = [byte[]]::new(1024)
    [BitConverter]::GetBytes([uint16]0x5A4D).CopyTo($bytes, 0)
    [BitConverter]::GetBytes([int]128).CopyTo($bytes, 0x3C)
    [BitConverter]::GetBytes([uint32]0x4550).CopyTo($bytes, 128)
    $machine = if ($Platform -eq 'arm64') { 0xAA64 } else { 0x8664 }
    [BitConverter]::GetBytes([uint16]$machine).CopyTo($bytes, 132)
    [BitConverter]::GetBytes([uint16]0x20B).CopyTo($bytes, 152)
    $zip = [IO.Compression.ZipFile]::Open($Archive, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in $Files) {
            $stream = $zip.CreateEntry($name).Open()
            try { $stream.Write($bytes, 0, $bytes.Length) }
            finally { $stream.Dispose() }
        }
    }
    finally { $zip.Dispose() }
}

Describe 'Official winapp CLI central dependency pin' {
    It 'pins the public tag and published SHA256 for each native architecture' -TestCases @(
        @{ Platform = 'x64'; Hash = '236e35173f85a88a62cb030bdc07db6590fb7790514d530a050482a0da80313b' }
        @{ Platform = 'arm64'; Hash = '37532ac2a7fd50234230b3aa21605133491a289cb585fe6340442e2eb97956b5' }
    ) {
        param($Platform, $Hash)
        $release = Get-WinAppCliRelease -Platform $Platform
        $release.Repository | Should Be 'https://github.com/microsoft/winappCli'
        $release.Tag | Should Be 'v0.7.0'
        $release.Version | Should Be '0.7.0'
        $release.Platform | Should Be $Platform
        $release.Asset | Should Be "winappcli-$Platform.zip"
        $release.Sha256 | Should Be $Hash
        $release.DownloadUrl | Should Be "https://github.com/microsoft/winappCli/releases/download/v0.7.0/winappcli-$Platform.zip"
        $release.Files.Count | Should Be 3
        $release.ArchiveFiles.Count | Should Be 6
    }

    It 'retains x64 defaults and case-insensitive ARM64 inputs without guessing unsupported architectures' {
        (Get-WinAppCliRelease).Platform | Should Be 'x64'
        (Get-WinAppCliRelease -Platform ARM64).Asset | Should Be 'winappcli-arm64.zip'
        { Get-WinAppCliRelease -Platform x86 } | Should Throw 'does not belong to the set'
    }

    It 'uses the same helper for the standard harness and protected MWB payload' {
        $installer = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\InstallWinAppCli.ps1') -Raw
        $preparation = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\New-MwbSandboxCiArtifact.ps1') -Raw
        foreach ($caller in @($installer, $preparation)) {
            $caller | Should Match 'Save-WinAppCliRelease'
            $caller | Should Not Match 'v0\.3\.2|SdkVersion|PatchSha256|releases/latest|firewall-query'
        }
        $installer | Should Match "IsNullOrWhiteSpace\(\`$Platform\).*'x64'"
    }

    It 'does not confuse the portable CLI with Windows Sandbox client installation or feature enablement' {
        $helper = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\WinAppCli.Common.ps1') -Raw
        $installer = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\InstallWinAppCli.ps1') -Raw
        $installer | Should Match 'not the MicrosoftWindows.WindowsSandbox client package'
        foreach ($script in @($helper, $installer)) {
            $script | Should Not Match '(?im)^\s*(winget|Add-AppxPackage|Enable-WindowsOptionalFeature|Restart-Computer|Set-ExecutionPolicy)\b'
            $script | Should Not Match 'Start-Process.*-Verb RunAs'
        }
    }
}

Describe 'Hash-verified official winapp CLI release extraction' {
    BeforeEach {
        $root = (New-Item -ItemType Directory -Path (Join-Path $TestDrive ([guid]::NewGuid().ToString('N')))).FullName
        $archive = Join-Path $root 'release.zip'
        $destination = Join-Path $root 'portable'
        $work = Join-Path $root 'download'
        $script:releaseFixturePin = $script:officialWinAppCliPinJson | ConvertFrom-Json
        New-WinAppCliReleaseFixture $archive $script:releaseFixturePin.ArchiveFiles
        $script:releaseFixturePin.Sha256 = (Get-FileHash -LiteralPath $archive).Hash
        $script:releaseFixtureArchive = $archive
        Mock Get-WinAppCliRelease { $script:releaseFixturePin }
        Mock Get-WinAppCliFileVersion { '0.7.0' }
        Mock Expand-WinAppCliFile {
            param($Entry, $Destination)
            [IO.Compression.ZipFileExtensions]::ExtractToFile($Entry, $Destination, $false)
        }
        Mock Invoke-WebRequest {
            param($Uri, $OutFile)
            Copy-Item -LiteralPath $script:releaseFixtureArchive -Destination $OutFile
        }
    }

    It 'verifies the download and emits official metadata plus each native file hash without deploying PDBs' {
        $release = Save-WinAppCliRelease -Destination $destination -WorkRoot $work
        $release.Tag | Should Be 'v0.7.0'
        $release.Sha256 | Should Be (Get-FileHash -LiteralPath $archive).Hash
        $release.Asset | Should Be 'winappcli-x64.zip'
        $release.Files.Count | Should Be 3
        @(Get-ChildItem -LiteralPath $destination -File).Count | Should Be 3
        @(Get-ChildItem -LiteralPath $destination -Filter '*.pdb').Count | Should Be 0
        foreach ($file in $release.Files) {
            $file.Sha256 | Should Be (Get-FileHash -LiteralPath (Join-Path $destination $file.Path)).Hash
        }
        Assert-MockCalled Invoke-WebRequest -Times 1 -Exactly -Scope It -ParameterFilter {
            $Uri -ceq 'https://github.com/microsoft/winappCli/releases/download/v0.7.0/winappcli-x64.zip'
        }
        ($release.Keys -contains 'Commit') | Should Be $false
        ($release.Keys -contains 'PatchSha256') | Should Be $false
        ($release.Keys -contains 'SdkVersion') | Should Be $false
        ($release.Keys -contains 'RuntimeVersion') | Should Be $false
    }

    It 'accepts a predownloaded archive only after the same SHA256 verification' {
        $release = Save-WinAppCliRelease -Destination $destination -WorkRoot $work -ArchivePath $archive
        $release.Sha256 | Should Be $script:releaseFixturePin.Sha256
        Assert-MockCalled Invoke-WebRequest -Times 0 -Exactly -Scope It
        Test-Path -LiteralPath $work | Should Be $false
    }

    It 'rejects a changed SHA256 before extraction or executable-version inspection' {
        $script:releaseFixturePin.Sha256 = '0' * 64
        { Save-WinAppCliRelease -Destination $destination -WorkRoot $work } | Should Throw 'SHA256 mismatch'
        Test-Path -LiteralPath $destination | Should Be $false
        Assert-MockCalled Get-WinAppCliFileVersion -Times 0 -Exactly -Scope It
    }

    It 'does not fall back to stale bytes when downloading fails' {
        $null = New-Item -ItemType Directory -Path $work
        Copy-Item -LiteralPath $archive -Destination (Join-Path $work 'winappcli-x64.zip')
        Mock Invoke-WebRequest { throw 'fixture download failed' }
        { Save-WinAppCliRelease -Destination $destination -WorkRoot $work } | Should Throw 'fixture download failed'
        Test-Path -LiteralPath $destination | Should Be $false
        Assert-MockCalled Get-WinAppCliFileVersion -Times 0 -Exactly -Scope It
    }

    It 'rejects a corrupt ZIP even if the fixture hash matches' {
        Set-Content -LiteralPath $archive -Value 'not a ZIP'
        $script:releaseFixturePin.Sha256 = (Get-FileHash -LiteralPath $archive).Hash
        { Save-WinAppCliRelease -Destination $destination -WorkRoot $work } | Should Throw 'Central Directory'
        Test-Path -LiteralPath $destination | Should Be $false
    }

    It 'rejects missing expected archive files before deploying anything' {
        Remove-Item -LiteralPath $archive
        New-WinAppCliReleaseFixture $archive @($script:releaseFixturePin.ArchiveFiles | Where-Object { $_ -ne 'libSkiaSharp.dll' })
        $script:releaseFixturePin.Sha256 = (Get-FileHash -LiteralPath $archive).Hash
        { Save-WinAppCliRelease -Destination $destination -WorkRoot $work } | Should Throw 'inventory is incomplete'
        Test-Path -LiteralPath $destination | Should Be $false
    }

    It 'rejects unexpected paths, nested layouts and duplicate ZIP members before extraction' -TestCases @(
        @{ Name = '..\outside.exe' }; @{ Name = 'nested/winapp.exe' }
        @{ Name = 'winapp.exe' }; @{ Name = 'winapp.exe:stream' }
        @{ Name = 'unexpected.dll' }
    ) {
        param($Name)
        Remove-Item -LiteralPath $archive
        New-WinAppCliReleaseFixture $archive ($script:releaseFixturePin.ArchiveFiles + $Name)
        $script:releaseFixturePin.Sha256 = (Get-FileHash -LiteralPath $archive).Hash
        { Save-WinAppCliRelease -Destination $destination -WorkRoot $work } | Should Throw 'unexpected, duplicate'
        Test-Path -LiteralPath $destination | Should Be $false
    }

    It 'rejects a symbolic-link archive member even when its name matches the official layout' {
        $zip = [IO.Compression.ZipFile]::Open($archive, [IO.Compression.ZipArchiveMode]::Update)
        try { $zip.GetEntry('winapp.exe').ExternalAttributes = -1577058304 }
        finally { $zip.Dispose() }
        $script:releaseFixturePin.Sha256 = (Get-FileHash -LiteralPath $archive).Hash
        { Save-WinAppCliRelease -Destination $destination -WorkRoot $work } | Should Throw 'symbolic-link'
        Test-Path -LiteralPath $destination | Should Be $false
    }

    It 'removes partially extracted output on an extraction failure' {
        Mock Expand-WinAppCliFile {
            param($Entry, $Destination)
            [IO.File]::WriteAllText($Destination, 'partial fixture')
            throw 'fixture extraction failed'
        }
        { Save-WinAppCliRelease -Destination $destination -WorkRoot $work } | Should Throw 'fixture extraction failed'
        Test-Path -LiteralPath $destination | Should Be $false
    }

    It 'removes output when the executable version is not the stable pinned release' -TestCases @(
        @{ Version = '0.6.3-prerelease.52' }; @{ Version = '0.7.0-prerelease.3' }
        @{ Version = '0.7.1' }; @{ Version = '' }
    ) {
        param($Version)
        $script:releaseFixtureVersion = $Version
        Mock Get-WinAppCliFileVersion { $script:releaseFixtureVersion }
        { Save-WinAppCliRelease -Destination $destination -WorkRoot $work } | Should Throw 'release version does not match'
        Test-Path -LiteralPath $destination | Should Be $false
    }

    It 'deploys the native ARM64 closure without an x64 executable or cross-compilation' {
        Remove-Item -LiteralPath $archive
        New-WinAppCliReleaseFixture $archive $script:releaseFixturePin.ArchiveFiles -Platform arm64
        $script:releaseFixturePin.Platform = 'arm64'
        $script:releaseFixturePin.Asset = 'winappcli-arm64.zip'
        $script:releaseFixturePin.DownloadUrl = 'https://github.com/microsoft/winappCli/releases/download/v0.7.0/winappcli-arm64.zip'
        $script:releaseFixturePin.Sha256 = (Get-FileHash -LiteralPath $archive).Hash
        $release = Save-WinAppCliRelease -Destination $destination -WorkRoot $work -Platform arm64
        $release.Asset | Should Be 'winappcli-arm64.zip'
        Assert-MockCalled Get-WinAppCliRelease -Times 1 -Exactly -Scope It -ParameterFilter { $Platform -eq 'arm64' }
        Assert-MockCalled Invoke-WebRequest -Times 1 -Exactly -Scope It -ParameterFilter {
            $Uri -like '*/v0.7.0/winappcli-arm64.zip'
        }
    }

    It 'rejects the wrong native architecture and removes partially extracted output' {
        { Save-WinAppCliRelease -Destination $destination -WorkRoot $work -Platform arm64 } | Should Throw 'not native arm64'
        Test-Path -LiteralPath $destination | Should Be $false
    }

    It 'never overwrites an existing CLI installation directory' {
        $null = New-Item -ItemType Directory -Path $destination
        Set-Content -LiteralPath (Join-Path $destination 'sentinel') -Value 'existing installation'
        { Save-WinAppCliRelease -Destination $destination -WorkRoot $work } | Should Throw 'new destination'
        Get-Content -LiteralPath (Join-Path $destination 'sentinel') | Should Be 'existing installation'
        Assert-MockCalled Invoke-WebRequest -Times 0 -Exactly -Scope It
    }
}
