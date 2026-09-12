#Requires -Version 7.4
# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'FixtureCommon.ps1')
. (Join-Path $PSScriptRoot 'FixtureRegistration.ps1')
$OutputDirectory = Resolve-FixtureOutputDirectory $OutputDirectory
if (Test-Path -LiteralPath $OutputDirectory) {
    throw 'Choose a new artifact directory for registration tests.'
}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$shortcutFolder = Join-Path $OutputDirectory 'mock-programs'
$fixtures = @($FixtureExpectations.Keys | Where-Object { $_ -notlike 'diagnostics\*' } | ForEach-Object {
    [pscustomobject]@{ file = $_ }
})
$results = [Collections.Generic.List[object]]::new()

function Assert-FixtureTest {
    param([bool]$Condition, [string]$Name)
    $results.Add([pscustomobject]@{ test = $Name; verdict = if ($Condition) { 'PASS' } else { 'FAIL' } })
    if (!$Condition) {
        throw "Registration test failed: $Name"
    }
}

# JSON files stand in for shortcuts; this mock never invokes COM or the desktop Shell.
$shell = [pscustomobject]@{}
$shell | Add-Member -MemberType ScriptMethod -Name CreateShortcut -Value {
    param($path)
    $link = if (Test-Path -LiteralPath $path) {
        Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    }
    else {
        [pscustomobject]@{ TargetPath = ''; Arguments = ''; Description = ''; WorkingDirectory = ''; WindowStyle = 1 }
    }
    $link | Add-Member -NotePropertyName LinkPath -NotePropertyValue $path -Force
    $link | Add-Member -MemberType ScriptMethod -Name Save -Value {
        $this | ConvertTo-Json | Set-Content -LiteralPath $this.LinkPath
    }
    return $link
}
$arguments = @{
    Fixtures = $fixtures
    OutputDirectory = $OutputDirectory
    ShortcutFolder = $shortcutFolder
    Owner = 'PowerToys registration test owner'
    Shell = $shell
    Confirm = $false
}

try {
    $changes = @(Update-FixtureShortcuts @arguments -WhatIf)
    Assert-FixtureTest (!(Test-Path -LiteralPath $shortcutFolder) -and @($changes | Where-Object changed).Count -eq 0) 'WhatIf writes nothing'

    $changes = @(Update-FixtureShortcuts @arguments)
    Assert-FixtureTest (@($changes | Where-Object changed).Count -eq 5 -and
        @(Get-ChildItem -LiteralPath $shortcutFolder -File).Count -eq 5) 'Registers only five runnable fixture shortcuts'
    $changes = @(Update-FixtureShortcuts @arguments)
    Assert-FixtureTest (@($changes | Where-Object changed).Count -eq 5) 'Registration is repeatable for owned shortcuts'

    $linkPath = Join-Path $shortcutFolder 'WrongCertificateUsage.lnk'
    $original = Get-Content -LiteralPath $linkPath -Raw
    foreach ($property in @('TargetPath', 'Description', 'Arguments', 'WorkingDirectory')) {
        $link = $original | ConvertFrom-Json
        $link.$property = 'unrelated-user-value'
        $link | ConvertTo-Json | Set-Content -LiteralPath $linkPath
        $before = @(Get-ChildItem -LiteralPath $shortcutFolder -File | Get-FileHash | Sort-Object Path | ForEach-Object Hash)
        foreach ($remove in @($false, $true)) {
            $rejected = $false
            try {
                Update-FixtureShortcuts @arguments -Remove:$remove | Out-Null
            }
            catch {
                $rejected = $_.Exception.Message -match 'unrelated or modified shortcut'
            }
            $after = @(Get-ChildItem -LiteralPath $shortcutFolder -File | Get-FileHash | Sort-Object Path | ForEach-Object Hash)
            Assert-FixtureTest ($rejected -and !(Compare-Object $before $after)) "Preflight protects $property (Remove=$remove)"
        }
        Set-Content -LiteralPath $linkPath -Value $original -NoNewline
    }

    $changes = @(Update-FixtureShortcuts @arguments -Remove -WhatIf)
    Assert-FixtureTest (@(Get-ChildItem -LiteralPath $shortcutFolder -File).Count -eq 5 -and
        @($changes | Where-Object changed).Count -eq 0) 'Removal WhatIf preserves shortcuts'
    $unrelated = Join-Path $shortcutFolder 'Unrelated.lnk'
    Set-Content -LiteralPath $unrelated -Value 'unrelated placeholder, not a real shortcut'
    $changes = @(Update-FixtureShortcuts @arguments -Remove)
    Assert-FixtureTest (@($changes | Where-Object changed).Count -eq 5 -and
        @(Get-ChildItem -LiteralPath $shortcutFolder -File).Count -eq 1 -and
        (Test-Path -LiteralPath $unrelated)) 'Removal preserves unrelated entries without requiring EXEs'
    Remove-Item -LiteralPath $unrelated
    Update-FixtureShortcuts @arguments | Out-Null
    Update-FixtureShortcuts @arguments -Remove | Out-Null
    Assert-FixtureTest (!(Test-Path -LiteralPath $shortcutFolder)) 'Removal deletes only an empty owned folder'
    $changes = @(Update-FixtureShortcuts @arguments -Remove)
    Assert-FixtureTest (@($changes | Where-Object changed).Count -eq 0) 'Repeated removal is a no-op'

    $items = foreach ($value in @(0, 1, $null)) {
        $item = [pscustomobject]@{ HostValue = $value }
        $item | Add-Member -MemberType ScriptMethod -Name ExtendedProperty -Value {
            param($name)
            if ($name -eq 'System.Link.TargetParsingPath') { return 'mock-fixture.exe' }
            if ($name -eq 'System.AppUserModel.HostEnvironment') { return $this.HostValue }
            throw "Unexpected Shell property: $name"
        }
        $item
    }
    $appsFolder = [pscustomobject]@{ MockItems = $items }
    $appsFolder | Add-Member -MemberType ScriptMethod -Name Items -Value { return $this.MockItems }
    $metadata = @(Get-FixtureAppsFolderMetadata -AppsFolder $appsFolder -Targets @('mock-fixture.exe'))
    Assert-FixtureTest ($metadata.Count -eq 3 -and $metadata[0].canLaunchElevated -and
        !$metadata[1].canLaunchElevated -and !$metadata[2].canLaunchElevated) 'Only observed HostEnvironment zero qualifies'
}
finally {
    $results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'registration-tests.json')
    if (Test-Path -LiteralPath $shortcutFolder) {
        Remove-Item -LiteralPath $shortcutFolder -Recurse -Force
    }
}
$results | Format-Table -AutoSize
