# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

# Run with the existing Pester runner:
# Invoke-Pester .\src\settings-ui\Settings.UI.UnitTests\Scripts\DisableModule.Tests.ps1
$scriptPath = Join-Path $PSScriptRoot '..\..\Settings.UI\Assets\Settings\Scripts\DisableModule.ps1'
$legacyGuid = '34de4b3d-13a8-4540-b76d-b9e8d3851756'
$currentGuid = 'f45873b3-b655-43a6-b217-97c00aa0db58'
$removedMessage = 'Removed the Command Not Found reference from the profile file.'
$absentMessage = 'No instance of Command Not Found was found in the profile file.'

function New-ModuleBlock {
    param(
        [string]$Guid = $currentGuid,
        [string]$NewLine = "`r`n"
    )

    $import = 'Import-Module -Name Microsoft.WinGet.CommandNotFound'
    if ($Guid -eq $legacyGuid) {
        $import = 'Import-Module "C:\PowerToys\WinGetCommandNotFound.psd1"'
    }

    return "#$Guid PowerToys CommandNotFound module$NewLine$NewLine$import$NewLine#$Guid$NewLine"
}

function Invoke-ProfileRemoval {
    param(
        [byte[]]$Content,
        [string]$ExpectedMessage = $removedMessage,
        [switch]$ExpectFailure,
        [switch]$ReadOnly,
        [switch]$Missing,
        [switch]$BracketedPath
    )

    # Shadow the automatic variable only inside this function. Never run a real profile.
    $name = "profile-$([Guid]::NewGuid().ToString('N'))"
    if ($BracketedPath) {
        $name = "[$name]"
    }
    $PROFILE = Join-Path $TestDrive "$name.ps1"
    if (-not $Missing) {
        [System.IO.File]::WriteAllBytes($PROFILE, $Content)
    }
    if ($ReadOnly) {
        [System.IO.File]::SetAttributes($PROFILE, [System.IO.FileAttributes]::ReadOnly)
    }

    $output = [System.Collections.Generic.List[string]]::new()
    $failure = $null
    try {
        & $scriptPath 6>&1 | ForEach-Object { $output.Add($_.ToString()) }
    }
    catch {
        $failure = $_
    }
    finally {
        if ($ReadOnly) {
            [System.IO.File]::SetAttributes($PROFILE, [System.IO.FileAttributes]::Normal)
        }
    }

    $text = $output -join "`n"
    if ($ExpectFailure) {
        ($null -ne $failure) | Should Be $true | Out-Null
        $text | Should Match 'Command Not Found removal failed' | Out-Null
        $text.Contains($removedMessage) | Should Be $false | Out-Null
        $text.Contains($absentMessage) | Should Be $false | Out-Null
    }
    else {
        if ($null -ne $failure) {
            throw $failure
        }
        $text | Should Be $ExpectedMessage | Out-Null
    }

    if ($Missing) {
        [System.IO.File]::Exists($PROFILE) | Should Be $false | Out-Null
        return
    }

    $actual = [System.IO.File]::ReadAllBytes($PROFILE)
    if ($ExpectFailure) {
        [Convert]::ToBase64String($actual) | Should Be ([Convert]::ToBase64String($Content)) | Out-Null
    }

    # A successful replacement or a failed write must not leave temporary siblings behind.
    @(Get-ChildItem -LiteralPath $TestDrive -Force -Filter '*.tmp').Count | Should Be 0 | Out-Null
    return ,$actual
}

Describe 'DisableModule profile preservation' {
    It 'removes a complete current block and preserves a trailing function byte-for-byte' {
        $prefix = "function Before { 'before' }`r`n`r`n"
        $suffix = "function After { 'after' }`n# No final newline"
        $bytes = [Text.Encoding]::UTF8.GetBytes($prefix + (New-ModuleBlock) + $suffix)
        $actual = Invoke-ProfileRemoval -Content $bytes
        [Convert]::ToBase64String($actual) | Should Be ([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($prefix + $suffix)))
    }

    It 'preserves the tail when the closing marker is missing' {
        $content = "#$currentGuid PowerToys CommandNotFound module`r`nImport-Module -Name Microsoft.WinGet.CommandNotFound`r`nfunction Keep { 'user content' }"
        Invoke-ProfileRemoval -Content ([Text.Encoding]::UTF8.GetBytes($content)) -ExpectFailure | Out-Null
    }

    foreach ($guid in @($legacyGuid, $currentGuid)) {
        It "removes a complete $guid block" {
            $actual = Invoke-ProfileRemoval -Content ([Text.Encoding]::UTF8.GetBytes((New-ModuleBlock -Guid $guid)))
            $actual.Length | Should Be 0
        }

        It "refuses a $guid closing marker without an opening marker" {
            $content = "#$guid`nfunction Keep { 'user content' }"
            Invoke-ProfileRemoval -Content ([Text.Encoding]::UTF8.GetBytes($content)) -ExpectFailure | Out-Null
        }

        It "refuses a $guid opening marker without a closing marker" {
            $content = "#$guid PowerToys CommandNotFound module"
            Invoke-ProfileRemoval -Content ([Text.Encoding]::UTF8.GetBytes($content)) -ExpectFailure | Out-Null
        }
    }

    It 'removes multiple separate current and legacy blocks without touching intervening content' {
        $userFunction = "function Keep { 'between blocks' }`n"
        $suffix = '# tail'
        $content = (New-ModuleBlock) + $userFunction + (New-ModuleBlock -Guid $legacyGuid -NewLine "`n") + (New-ModuleBlock) + $suffix
        $actual = Invoke-ProfileRemoval -Content ([Text.Encoding]::UTF8.GetBytes($content))
        [Text.Encoding]::UTF8.GetString($actual) | Should Be ($userFunction + $suffix)
    }

    $malformedCases = @{
        'mixed GUIDs' = "#$currentGuid PowerToys CommandNotFound module`n#$legacyGuid`n"
        'reverse mixed GUIDs' = "#$legacyGuid PowerToys CommandNotFound module`n#$currentGuid`n"
        'duplicate opening markers' = "#$currentGuid PowerToys CommandNotFound module`n" + (New-ModuleBlock)
        'duplicate closing markers' = (New-ModuleBlock) + "#$currentGuid`n"
        'nested legacy block' = "#$currentGuid PowerToys CommandNotFound module`n" + (New-ModuleBlock -Guid $legacyGuid) + "#$currentGuid`n"
        'valid block followed by an unclosed block' = (New-ModuleBlock) + "#$legacyGuid PowerToys CommandNotFound module`nfunction Keep { 'tail' }"
        'valid block followed by an orphan closing marker' = (New-ModuleBlock) + "#$legacyGuid`nfunction Keep { 'tail' }"
        'an orphan closing marker before a valid block' = "#$legacyGuid`n" + (New-ModuleBlock)
    }
    foreach ($name in $malformedCases.Keys) {
        It "leaves the entire profile unchanged for $name" {
            Invoke-ProfileRemoval -Content ([Text.Encoding]::UTF8.GetBytes($malformedCases[$name])) -ExpectFailure | Out-Null
        }
    }

    It 'does not treat GUID substrings or marker-like comments as boundaries' {
        $content = @"
`$id = '$currentGuid'
# A note about $legacyGuid
Write-Output '#$currentGuid PowerToys CommandNotFound module'
#${currentGuid}-not-a-marker
#$legacyGuid unrelated comment
function Keep { 'unchanged' }
"@
        $bytes = [Text.Encoding]::UTF8.GetBytes($content)
        $actual = Invoke-ProfileRemoval -Content $bytes -ExpectedMessage $absentMessage
        [Convert]::ToBase64String($actual) | Should Be ([Convert]::ToBase64String($bytes))
    }

    It 'does not remove marker examples inside strings or block comments' {
        $example = New-ModuleBlock
        $prefix = "`$example = @'`n$example'@`n<#`n$example#>`n"
        $suffix = "function Keep { 'tail' }"
        $actual = Invoke-ProfileRemoval -Content ([Text.Encoding]::UTF8.GetBytes($prefix + (New-ModuleBlock) + $suffix))
        [Text.Encoding]::UTF8.GetString($actual) | Should Be ($prefix + $suffix)
    }

    It 'preserves marker-like text between complete blocks' {
        $text = "Write-Output '$currentGuid'`n#$legacyGuid unrelated comment`n"
        $content = (New-ModuleBlock) + $text + (New-ModuleBlock -Guid $legacyGuid)
        $actual = Invoke-ProfileRemoval -Content ([Text.Encoding]::UTF8.GetBytes($content))
        [Text.Encoding]::UTF8.GetString($actual) | Should Be $text
    }

    It 'accepts indentation and trailing horizontal whitespace on marker lines' {
        $content = "  #$currentGuid PowerToys CommandNotFound module `t`nImport-Module -Name Microsoft.WinGet.CommandNotFound`n`t#$currentGuid `t`n# tail"
        $actual = Invoke-ProfileRemoval -Content ([Text.Encoding]::UTF8.GetBytes($content))
        [Text.Encoding]::UTF8.GetString($actual) | Should Be '# tail'
    }

    $encodings = @{
        'UTF-8 without BOM' = [Text.UTF8Encoding]::new($false)
        'UTF-8 with BOM' = [Text.UTF8Encoding]::new($true)
        'UTF-16 LE' = [Text.UnicodeEncoding]::new($false, $true)
        'UTF-16 BE' = [Text.UnicodeEncoding]::new($true, $true)
        'UTF-32 LE' = [Text.UTF32Encoding]::new($false, $true)
        'UTF-32 BE' = [Text.UTF32Encoding]::new($true, $true)
        'Windows-1252' = [Text.Encoding]::GetEncoding(1252)
    }
    foreach ($name in $encodings.Keys) {
        It "preserves $name bytes, BOM, mixed line endings, and non-ASCII content" {
            $encoding = $encodings[$name]
            $prefix = "# caf$([char]0xE9) $([char]0x20AC)`r`n`r`n"
            $suffix = "function Keep { '$([char]0xE9)' }`n# tail`r# no final newline"
            $bytes = $encoding.GetPreamble() + $encoding.GetBytes($prefix + (New-ModuleBlock -NewLine "`n") + $suffix)
            $expected = $encoding.GetPreamble() + $encoding.GetBytes($prefix + $suffix)
            $actual = Invoke-ProfileRemoval -Content $bytes
            [Convert]::ToBase64String($actual) | Should Be ([Convert]::ToBase64String($expected))
        }
    }

    foreach ($newLine in @("`r`n", "`n", "`r")) {
        It "removes a block with line ending bytes $([Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($newLine)))" {
            $content = (New-ModuleBlock -NewLine $newLine) + '# tail'
            $actual = Invoke-ProfileRemoval -Content ([Text.Encoding]::UTF8.GetBytes($content))
            [Text.Encoding]::UTF8.GetString($actual) | Should Be '# tail'
        }
    }

    It 'removes a block whose closing marker has no final newline' {
        $content = (New-ModuleBlock).TrimEnd("`r", "`n")
        $actual = Invoke-ProfileRemoval -Content ([Text.Encoding]::UTF8.GetBytes($content))
        $actual.Length | Should Be 0
    }

    It 'uses the profile path literally when it contains wildcard characters' {
        $actual = Invoke-ProfileRemoval -Content ([Text.Encoding]::UTF8.GetBytes((New-ModuleBlock))) -BracketedPath
        $actual.Length | Should Be 0
    }

    It 'does not rewrite an empty profile' {
        $actual = Invoke-ProfileRemoval -Content @() -ExpectedMessage $absentMessage
        $actual.Length | Should Be 0
    }

    It 'does not rewrite a BOM-only profile' {
        $bytes = [Text.Encoding]::UTF8.GetPreamble()
        $actual = Invoke-ProfileRemoval -Content $bytes -ExpectedMessage $absentMessage
        [Convert]::ToBase64String($actual) | Should Be ([Convert]::ToBase64String($bytes))
    }

    It 'refuses undecodable BOM-encoded content without replacing bytes' {
        $bytes = [Text.Encoding]::UTF8.GetPreamble() + [Text.Encoding]::UTF8.GetBytes((New-ModuleBlock)) + [byte[]]@(0xFF)
        Invoke-ProfileRemoval -Content $bytes -ExpectFailure | Out-Null
    }

    It 'reports a missing profile as an error rather than successful removal' {
        Invoke-ProfileRemoval -Missing -ExpectFailure
    }

    It 'reports a read-only profile as an error without changing its bytes' {
        Invoke-ProfileRemoval -Content ([Text.Encoding]::UTF8.GetBytes((New-ModuleBlock))) -ReadOnly -ExpectFailure | Out-Null
    }
}
