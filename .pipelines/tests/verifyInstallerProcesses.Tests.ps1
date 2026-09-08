# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

#requires -Version 7.0

# Self-contained fixtures: no build artifacts, signing tools, or Pester installation required.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$checkerPath = Join-Path $PSScriptRoot '../verifyInstallerProcesses.ps1'
$fixtureParent = Join-Path ([IO.Path]::GetTempPath()) "InstallerProcessCoverage-$([Guid]::NewGuid().ToString('N'))"
$script:passed = 0

function Write-FixtureFile([string] $Path, [string] $Text)
{
    $null = [IO.Directory]::CreateDirectory((Split-Path $Path -Parent))
    [IO.File]::WriteAllText($Path, $Text)
}

function Set-KillNames([string[]] $Names)
{
    $literals = ($Names | ForEach-Object { 'L"' + $_ + '"' }) -join ",`n"
    Write-FixtureFile (Join-Path $script:fixture 'installer/PowerToysSetupCustomActionsVNext/CustomAction.cpp') "static constexpr const wchar_t* processesToTerminate[] = { $literals };"
}

function Set-CorePatterns([string[]] $ExtraPatterns)
{
    $manifest = @{ UseMinimatch = $false; SignBatches = @(
            @{ MatchedPath = @('PowerToys.exe', '*.resources.dll', 'Scripts/*.ps1') },
            @{ MatchedPath = @('WinUI3Apps/Feature.exe') + $ExtraPatterns }
        ) }
    Write-FixtureFile (Join-Path $script:fixture '.pipelines/ESRPSigning_core.json') ($manifest | ConvertTo-Json -Depth 5)
}

function Set-Exceptions([object[]] $Exceptions)
{
    Write-FixtureFile (Join-Path $script:fixture '.pipelines/installerProcessExclusions.json') (ConvertTo-Json -InputObject $Exceptions -Depth 5)
}

function Set-Alias([string] $Source = 'CliShim/PowerToys.CliShim.exe', [string] $Name = 'PowerToys.Test.CLI.exe')
{
    $xml = '<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"><Fragment><DirectoryRef Id="CliFolder"><Component><File Source="$(var.BinDir)' + $Source + '" Name="' + $Name + '" /></Component></DirectoryRef></Fragment></Wix>'
    Write-FixtureFile (Join-Path $script:fixture 'installer/PowerToysSetupVNext/Aliases.wxs') $xml
}

function New-CmdPalPackage([string[]] $Entries)
{
    $path = Join-Path $script:fixture 'CmdPal.msix'
    $zip = [IO.Compression.ZipFile]::Open($path, 'Create')
    try
    {
        foreach ($entry in $Entries)
        {
            $null = $zip.CreateEntry($entry)
        }
    }
    finally
    {
        $zip.Dispose()
    }
    return $path
}

function Assert-Coverage([string] $Failure = '', [string] $Package = '')
{
    $message = ''
    try
    {
        & $checkerPath -RepoRoot $script:fixture -CmdPalPackagePath $Package
    }
    catch
    {
        $message = $_.Exception.Message
    }
    if ($Failure -and -not $message.Contains($Failure, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Expected failure containing '$Failure', got '$message'."
    }
    if (-not $Failure -and $message)
    {
        throw "Expected coverage to pass, got '$message'."
    }
}

function Test-Fixture([string] $Name, [scriptblock] $Test)
{
    $script:fixture = Join-Path $fixtureParent ([Guid]::NewGuid().ToString('N'))
    Set-CorePatterns @()
    Set-KillNames @('PowerToys.exe', 'Feature.exe', 'HistoricalOnly.exe')
    Write-FixtureFile (Join-Path $script:fixture 'installer/PowerToysSetupVNext/Empty.wxs') '<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs" />'
    Write-FixtureFile (Join-Path $script:fixture '.pipelines/ESRPSigning_cmdpal_msix_content.json') '{ "UseMinimatch": false, "SignBatches": [{ "MatchedPath": ["*.dll"] }, { "MatchedPath": ["*.exe"] }] }'
    & $Test
    $script:passed++
    Write-Host "PASS: $Name"
}

try
{
    Test-Fixture 'case-insensitive exact names, all batches, non-executables, and historical entries' {
        Set-KillNames @('powertoys.EXE', 'FEATURE.EXE', 'HistoricalOnly.exe')
        Assert-Coverage
    }
    Test-Fixture 'missing executable from a later signing batch' {
        Set-CorePatterns @('nested\New.exe')
        Assert-Coverage -Failure 'New.exe'
    }
    Test-Fixture 'a coverage failure returns a nonzero process exit code to CI' {
        Set-CorePatterns @('New.exe')
        $startInfo = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        foreach ($argument in @('-NoProfile', '-File', $checkerPath, '-RepoRoot', $script:fixture))
        {
            $startInfo.ArgumentList.Add($argument)
        }
        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $startInfo
        try
        {
            if (-not $process.Start())
            {
                throw 'Could not start the coverage checker fixture process.'
            }
            $stdout = $process.StandardOutput.ReadToEndAsync()
            $stderr = $process.StandardError.ReadToEndAsync()
            if (-not $process.WaitForExit(30000))
            {
                $process.Kill($true)
                $process.WaitForExit()
                throw 'The coverage checker fixture process timed out.'
            }
            $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
            if ($process.ExitCode -ne 1)
            {
                throw "Expected pwsh -File to exit 1 for missing coverage, got $($process.ExitCode): $output"
            }
        }
        finally
        {
            $process.Dispose()
        }
    }
    Test-Fixture 'a prefix match cannot cover another executable' {
        Set-CorePatterns @('Feature.Helper.exe')
        Assert-Coverage -Failure 'Feature.Helper.exe'
    }
    Test-Fixture 'renamed signed payload requires installed alias coverage' {
        Set-CorePatterns @('CliShim/PowerToys.CliShim.exe')
        Set-Alias
        Assert-Coverage -Failure 'PowerToys.Test.CLI.exe'
    }
    Test-Fixture 'covered alias replaces the uninstalled payload name' {
        Set-CorePatterns @('CliShim/PowerToys.CliShim.exe')
        Set-Alias
        Set-KillNames @('PowerToys.exe', 'Feature.exe', 'PowerToys.Test.CLI.exe')
        Assert-Coverage
    }
    Test-Fixture 'a renamed harvested executable still requires its original name' {
        Set-Alias -Source 'WinUI3Apps/Feature.exe' -Name 'Renamed.exe'
        Set-KillNames @('PowerToys.exe', 'Renamed.exe')
        Assert-Coverage -Failure 'Feature.exe'
    }
    Test-Fixture 'relative signed source with inherited FileSource cannot hide an alias' {
        Write-FixtureFile (Join-Path $script:fixture 'installer/PowerToysSetupVNext/Inherited.wxs') '<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"><Fragment><DirectoryRef Id="INSTALLFOLDER" FileSource="$(var.BinDir)"><Component><File Source="PowerToys.exe" Name="RelativeAlias.exe" /></Component></DirectoryRef></Fragment></Wix>'
        Assert-Coverage -Failure 'RelativeAlias.exe'
    }
    Test-Fixture 'an unresolved installed name fails closed' {
        Set-Alias -Name '$(var.Alias).exe'
        Assert-Coverage -Failure 'Unsupported installed File/@Name'
    }
    Test-Fixture 'an unresolved whole installed name fails closed' {
        Set-Alias -Name '$(var.ExecutableName)'
        Assert-Coverage -Failure 'Unsupported installed File/@Name'
    }
    Test-Fixture 'a clearly non-executable installed name may contain a variable' {
        Set-Alias -Source 'Assets/Feature.resources.dll' -Name '$(var.Language).resources.dll'
        Assert-Coverage
    }
    foreach ($glob in @('*.exe', 'nested/Foo*.exe', '*.ex?'))
    {
        Test-Fixture "unsupported static executable glob $glob" {
            Set-CorePatterns @($glob)
            Assert-Coverage -Failure 'Unsupported executable glob'
        }
    }
    foreach ($pattern in @('New.exe', '*.exe'))
    {
        Test-Fixture "new executable signing policy with $pattern requires explicit handling" {
            $manifest = @{ UseMinimatch = $false; SignBatches = @(@{ MatchedPath = @($pattern) }) }
            Write-FixtureFile (Join-Path $script:fixture '.pipelines/ESRPSigning_future.json') ($manifest | ConvertTo-Json -Depth 5)
            Assert-Coverage -Failure 'Unsupported executable signing policy ESRPSigning_future.json'
        }
    }
    foreach ($invalid in @(
            'static constexpr const wchar_t* processesToTerminate[] = {};',
            'static constexpr const wchar_t* processesToTerminate[] = { SOME_CONSTANT };',
            '// processesToTerminate = { L"PowerToys.exe" };',
            'processesToTerminate = { L"PowerToys.exe",, L"Feature.exe" };'))
    {
        Test-Fixture 'empty, missing, or malformed termination initializer fails closed' {
            Write-FixtureFile (Join-Path $script:fixture 'installer/PowerToysSetupCustomActionsVNext/CustomAction.cpp') $invalid
            Assert-Coverage -Failure 'processesToTerminate'
        }
    }
    Test-Fixture 'an exact reasoned exception covers its signed relative path' {
        Set-CorePatterns @('Tools/Helper.exe')
        Set-Exceptions @(@{ Scope = 'core'; Path = 'Tools/Helper.exe'; Reason = 'Fixture helper has its own shutdown protocol.' })
        Assert-Coverage
    }
    Test-Fixture 'exceptions cannot exempt installation aliases through their signed payload' {
        Set-CorePatterns @('CliShim/PowerToys.CliShim.exe')
        Set-Alias
        Set-Exceptions @(@{ Scope = 'core'; Path = 'CliShim/PowerToys.CliShim.exe'; Reason = 'Fixture exclusion must not hide a renamed executable.' })
        Assert-Coverage -Failure 'Stale installer process exclusion'
    }
    Test-Fixture 'empty exclusion reason is rejected' {
        Set-Exceptions @(@{ Scope = 'core'; Path = 'Helper.exe'; Reason = ' ' })
        Assert-Coverage -Failure 'nonempty Reason'
    }
    Test-Fixture 'stale exclusion is rejected' {
        Set-Exceptions @(@{ Scope = 'core'; Path = 'NoLongerSigned.exe'; Reason = 'Old fixture exception.' })
        Assert-Coverage -Failure 'Stale installer process exclusion'
    }
    Test-Fixture 'wildcard exclusion is rejected' {
        Set-Exceptions @(@{ Scope = 'core'; Path = '*.exe'; Reason = 'Too broad.' })
        Assert-Coverage -Failure 'exact relative executable Path'
    }
    Test-Fixture 'optional exclusions are limited to CmdPal' {
        Set-Exceptions @(@{ Scope = 'core'; Path = 'Helper.exe'; Optional = $true; Reason = 'Invalid scope.' })
        Assert-Coverage -Failure 'only for CmdPal'
    }
    Test-Fixture 'CmdPal wildcard resolves actual zip executable entries across batches' {
        $package = New-CmdPalPackage @('Feature.exe', 'nested/PowerToys.exe', 'unrelated.dll')
        Assert-Coverage -Package $package
    }
    Test-Fixture 'unknown CmdPal executable fails' {
        $package = New-CmdPalPackage @('Feature.exe', 'nested/NewExtension.exe')
        Assert-Coverage -Package $package -Failure 'NewExtension.exe'
    }
    Test-Fixture 'CmdPal exclusions require exact paths, not matching leaf names' {
        Set-Exceptions @(@{ Scope = 'cmdpal'; Path = 'Helper.exe'; Optional = $true; Reason = 'Optional fixture runtime dependency.' })
        $package = New-CmdPalPackage @('Feature.exe', 'nested/Helper.exe')
        Assert-Coverage -Package $package -Failure 'nested/Helper.exe'
    }
    Test-Fixture 'optional CmdPal dependency may be absent' {
        Set-Exceptions @(@{ Scope = 'cmdpal'; Path = 'Helper.exe'; Optional = $true; Reason = 'Optional fixture runtime dependency.' })
        $package = New-CmdPalPackage @('Feature.exe')
        Assert-Coverage -Package $package
    }
    Test-Fixture 'optional CmdPal dependency is matched by full relative path ignoring case' {
        Set-Exceptions @(@{ Scope = 'cmdpal'; Path = 'Helper.exe'; Optional = $true; Reason = 'Optional fixture runtime dependency.' })
        $package = New-CmdPalPackage @('Feature.exe', 'HELPER.EXE')
        Assert-Coverage -Package $package
    }
    Test-Fixture 'an exclusion becomes stale when the kill list covers it' {
        Set-Exceptions @(@{ Scope = 'cmdpal'; Path = 'Feature.exe'; Optional = $true; Reason = 'No longer necessary.' })
        $package = New-CmdPalPackage @('Feature.exe')
        Assert-Coverage -Package $package -Failure 'Stale installer process exclusion'
    }
    Test-Fixture 'reasoned CmdPal exception covers only that signing scope' {
        Set-CorePatterns @('Helper.exe')
        Set-Exceptions @(@{ Scope = 'core'; Path = 'Helper.exe'; Reason = 'Core fixture exception.' })
        $package = New-CmdPalPackage @('Feature.exe', 'Helper.exe')
        Assert-Coverage -Package $package -Failure '[cmdpal: Helper.exe]'
    }
    Test-Fixture 'empty signed CmdPal executable inventory fails closed' {
        $package = New-CmdPalPackage @('unrelated.dll')
        Assert-Coverage -Package $package -Failure 'contains no executables'
    }
    Write-Host "All $script:passed installer process coverage fixture tests passed."
}
finally
{
    # Delete only the uniquely named fixture root created under the system temporary directory.
    $resolved = [IO.Path]::GetFullPath($fixtureParent)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path $resolved -Leaf) -notmatch '^InstallerProcessCoverage-[0-9a-f]{32}$')
    {
        throw "Refusing to remove unexpected fixture directory: $resolved"
    }
    if (Test-Path -LiteralPath $resolved)
    {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
