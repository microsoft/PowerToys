# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

$skillRoot = Split-Path $PSScriptRoot -Parent
$policyScript = Join-Path $skillRoot 'templates\oem\Set-UiTestAdminPasswordPolicy.ps1'

function Get-TestScriptAst {
    param([string]$RelativePath)

    $tokens = $null
    $parseErrors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $skillRoot $RelativePath), [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count -gt 0) {
        throw ($parseErrors | Out-String)
    }
    return $ast
}

# Stubs deliberately shadow the real security/VM cmdlets. An unmocked call fails closed.
function Get-LocalUser {
    [CmdletBinding()]
    param($Name, $SID)
    throw 'Unmocked Get-LocalUser'
}
function Get-LocalGroupMember {
    [CmdletBinding()]
    param($SID)
    throw 'Unmocked Get-LocalGroupMember'
}
function Set-LocalUser {
    [CmdletBinding()]
    param($SID, [bool]$PasswordNeverExpires)
    throw 'Unmocked Set-LocalUser'
}
function Get-VM {
    [CmdletBinding()]
    param($Name)
    throw 'Unmocked Get-VM'
}
function Start-VM {
    [CmdletBinding()]
    param($Name)
    throw 'Unmocked Start-VM'
}
function New-LocalVmSession {
    param($Context, $Credential)
    throw 'Unmocked New-LocalVmSession'
}
function New-GuestSession {
    throw 'Unmocked New-GuestSession'
}
function Invoke-Command {
    [CmdletBinding()]
    param($Session, $FilePath, $ArgumentList)
    throw 'Unmocked Invoke-Command'
}
function Remove-PSSession {
    [CmdletBinding()]
    param($Session)
    throw 'Unmocked Remove-PSSession'
}

Describe 'Disposable guest administrator password policy' {
    BeforeEach {
        $global:LocalVmAdminPolicyTestAccount = [pscustomobject]@{
            Name = 'Custom Admin'
            SID = [Security.Principal.SecurityIdentifier]'S-1-5-21-1-2-3-1001'
            PasswordExpires = [datetime]'2030-01-01'
            Enabled = $true
        }
        Mock Get-LocalUser { $global:LocalVmAdminPolicyTestAccount }
        Mock Get-LocalGroupMember { [pscustomobject]@{ SID = $global:LocalVmAdminPolicyTestAccount.SID } }
        Mock Set-LocalUser { $global:LocalVmAdminPolicyTestAccount.PasswordExpires = $null }
    }

    AfterEach {
        Remove-Variable LocalVmAdminPolicyTestAccount -Scope Global
    }

    It 'sets only the configured administrator SID and verifies non-expiry' {
        $result = & $policyScript -AdminUserName 'Custom Admin' -StandardUser 'Custom User'

        $result.AdminUserName | Should Be 'Custom Admin'
        $result.AdminPasswordNeverExpires | Should Be $true
        Assert-MockCalled Get-LocalGroupMember -Times 1 -Exactly -Scope It -ParameterFilter { $SID -eq 'S-1-5-32-544' }
        Assert-MockCalled Set-LocalUser -Times 1 -Exactly -Scope It -ParameterFilter {
            $SID -eq $global:LocalVmAdminPolicyTestAccount.SID -and $PasswordNeverExpires -eq $true
        }
        Assert-MockCalled Get-LocalUser -Times 1 -Exactly -Scope It -ParameterFilter { $SID -eq $global:LocalVmAdminPolicyTestAccount.SID }
    }

    It 'is safe to repeat for an already non-expiring account' {
        $global:LocalVmAdminPolicyTestAccount.PasswordExpires = $null
        & $policyScript -AdminUserName 'Custom Admin' -StandardUser 'Custom User' | Out-Null
        & $policyScript -AdminUserName 'Custom Admin' -StandardUser 'Custom User' | Out-Null
        $global:LocalVmAdminPolicyTestAccount.PasswordExpires | Should BeNullOrEmpty
        Assert-MockCalled Set-LocalUser -Times 2 -Exactly -Scope It
    }

    It 'does not enable a disabled administrator' {
        $global:LocalVmAdminPolicyTestAccount.Enabled = $false
        & $policyScript -AdminUserName 'Custom Admin' -StandardUser 'Custom User' | Out-Null
        $global:LocalVmAdminPolicyTestAccount.Enabled | Should Be $false
    }

    It 'rejects administrator and standard-user confusion case-insensitively before mutation' {
        { & $policyScript -AdminUserName 'Custom Admin' -StandardUser 'custom ADMIN' } |
            Should Throw 'different local accounts'
        Assert-MockCalled Get-LocalUser -Times 0 -Exactly -Scope It
        Assert-MockCalled Set-LocalUser -Times 0 -Exactly -Scope It
    }

    It 'rejects blank account names' {
        { & $policyScript -AdminUserName ' ' -StandardUser 'Custom User' } |
            Should Throw 'unqualified local account names'
        Assert-MockCalled Set-LocalUser -Times 0 -Exactly -Scope It
    }

    It 'rejects a qualified standard-user alias before mutation' {
        { & $policyScript -AdminUserName 'Custom Admin' -StandardUser '.\Custom Admin' } |
            Should Throw 'unqualified local account names'
        Assert-MockCalled Set-LocalUser -Times 0 -Exactly -Scope It
    }

    It 'surfaces a missing account lookup error' {
        Mock Get-LocalUser { throw 'Local account not found' }
        { & $policyScript -AdminUserName 'Missing Admin' -StandardUser 'Custom User' } |
            Should Throw 'Local account not found'
        Assert-MockCalled Set-LocalUser -Times 0 -Exactly -Scope It
    }

    It 'rejects an empty account lookup result' {
        Mock Get-LocalUser { }
        { & $policyScript -AdminUserName 'Missing Admin' -StandardUser 'Custom User' } |
            Should Throw 'exact local account'
        Assert-MockCalled Set-LocalUser -Times 0 -Exactly -Scope It
    }

    It 'does not accept a wildcard match as an exact administrator name' {
        { & $policyScript -AdminUserName 'Custom*' -StandardUser 'Custom User' } |
            Should Throw 'exact local account'
        Assert-MockCalled Set-LocalUser -Times 0 -Exactly -Scope It
    }

    It 'refuses an account that is not already an administrator' {
        Mock Get-LocalGroupMember { [pscustomobject]@{ SID = 'S-1-5-21-1-2-3-1002' } }
        { & $policyScript -AdminUserName 'Custom Admin' -StandardUser 'Custom User' } |
            Should Throw 'not a member of Administrators'
        Assert-MockCalled Set-LocalUser -Times 0 -Exactly -Scope It
    }

    It 'surfaces group membership lookup errors without mutation' {
        Mock Get-LocalGroupMember { throw 'Cannot read local group' }
        { & $policyScript -AdminUserName 'Custom Admin' -StandardUser 'Custom User' } |
            Should Throw 'Cannot read local group'
        Assert-MockCalled Set-LocalUser -Times 0 -Exactly -Scope It
    }

    It 'surfaces failures setting the per-account flag' {
        Mock Set-LocalUser { throw 'Cannot set local account flag' }
        { & $policyScript -AdminUserName 'Custom Admin' -StandardUser 'Custom User' } |
            Should Throw 'Cannot set local account flag'
    }

    It 'does not report success if password expiry remains enabled' {
        Mock Set-LocalUser { }
        { & $policyScript -AdminUserName 'Custom Admin' -StandardUser 'Custom User' } |
            Should Throw 'Password expiry is still enabled'
    }
}

Describe 'Unattended administrator argument wiring' {
    BeforeAll {
        $script:newVmAst = Get-TestScriptAst 'templates\vm\New-UiTestVm.ps1'
        foreach ($name in 'Get-ProvisionArguments', 'New-UnattendContent') {
            $definition = $script:newVmAst.Find({
                param($node)
                $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
            }, $true)
            . ([scriptblock]::Create($definition.Extent.Text))
        }
    }

    BeforeEach {
        $configuration = @{
            AdminUserName = 'Admin & $Ops'
            StandardUser = "O'Brien User"
            ProcessorArchitecture = 'amd64'
            ComputerName = 'PTFIXTURE'
            Locale = 'en-US'
            TimeZone = 'Pacific Standard Time'
        }
    }

    It 'quotes both configured names and XML-escapes account and command tokens' {
        $arguments = Get-ProvisionArguments -Configuration $configuration
        $arguments | Should Be '-AdminUserName "Admin & $Ops" -StandardUser "O''Brien User"'
        [xml]$answer = New-UnattendContent -Configuration $configuration -ObfuscatedPassword 'fixture' `
            -SelectedImageName 'Windows 11 Pro' -ProvisionArguments $arguments `
            -TemplatePath (Join-Path $skillRoot 'templates\vm\unattend\autounattend.xml.template')
        $answer.SelectSingleNode("//*[local-name()='LocalAccount']/*[local-name()='Name']").InnerText |
            Should Be $configuration.AdminUserName
        $answer.SelectSingleNode("//*[local-name()='CommandLine'][starts-with(., 'powershell.exe')]").InnerText |
            Should Be ('powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File C:\OEM\Provision-UiTestVm.ps1 ' + $arguments)
    }

    It 'binds quoted names literally through Windows PowerShell File arguments' {
        # Execute only this inert capture fixture, never provisioning or VM code.
        $capturePath = Join-Path $TestDrive 'Capture-Arguments.ps1'
        @'
param([string]$AdminUserName, [string]$StandardUser)
@{ AdminUserName = $AdminUserName; StandardUser = $StandardUser } | ConvertTo-Json -Compress
'@ | Set-Content $capturePath -Encoding utf8
        $startInfo = New-Object Diagnostics.ProcessStartInfo
        $startInfo.FileName = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
        $startInfo.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}" {1}' -f `
            $capturePath, (Get-ProvisionArguments -Configuration $configuration)
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $process = [Diagnostics.Process]::Start($startInfo)
        try {
            $result = $process.StandardOutput.ReadToEnd() | ConvertFrom-Json
            $process.WaitForExit()
            $process.ExitCode | Should Be 0
            $result.AdminUserName | Should Be $configuration.AdminUserName
            $result.StandardUser | Should Be $configuration.StandardUser
        }
        finally {
            $process.Dispose()
        }
    }

    It 'rejects identical configured accounts before creating a VM' {
        $configuration.StandardUser = $configuration.AdminUserName.ToUpperInvariant()
        { Get-ProvisionArguments -Configuration $configuration } | Should Throw 'different local accounts'
    }

    It 'rejects names that could break native argument quoting' {
        $configuration.AdminUserName = 'Invalid"Admin'
        { Get-ProvisionArguments -Configuration $configuration } | Should Throw 'double quotes'
    }

    It 'uses the same configured arguments for preview and actual unattended media' {
        $calls = @($script:newVmAst.FindAll({
            param($node)
            $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'New-UnattendContent'
        }, $true))
        $calls.Count | Should Be 2
        foreach ($call in $calls) {
            $call.Extent.Text | Should Match '\-ProvisionArguments \$provisionArguments'
        }
    }

    It 'keeps the legacy provisioning default while passing it to the account helper before auto-logon' {
        $provision = Get-TestScriptAst 'templates\oem\Provision-UiTestVm.ps1'
        ($provision.ParamBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'AdminUserName' }).DefaultValue.Value |
            Should Be 'PTAdmin'
        $text = $provision.Extent.Text
        $text | Should Match '\-AdminUserName \$AdminUserName -StandardUser \$standardUser'
        ($text.IndexOf('Set-UiTestAdminPasswordPolicy.ps1') -lt $text.IndexOf('& $autoLogonScript')) | Should Be $true
    }
}

Describe 'Existing guest administrator policy refresh' {
    BeforeAll {
        $script:hostAst = Get-TestScriptAst 'scripts\Initialize-LocalVmHost.ps1'
        $script:updateAst = Get-TestScriptAst 'scripts\Update-LocalVmGuest.ps1'
        $hostStatements = @($script:hostAst.EndBlock.Statements)
        $refresh = $hostStatements | Where-Object { $_.Extent.Text -like 'if ($null -ne $guestVm*' }
        $script:hostRefresh = [scriptblock]::Create('param([string]$PSScriptRoot)' + "`n" + $refresh.Extent.Text)
        $updateRefresh = $script:updateAst.EndBlock.Statements | Where-Object {
            $_ -is [Management.Automation.Language.TryStatementAst] -and $_.Extent.Text -match 'adminPolicyScript'
        }
        $script:updateRefresh = [scriptblock]::Create('param([string]$PSScriptRoot)' + "`n" + $updateRefresh.Extent.Text)
    }

    BeforeEach {
        $vmName = 'FixtureVM'
        $configuration = @{ AdminUserName = 'Custom Admin'; StandardUser = 'Custom User' }
        $guestVm = [pscustomobject]@{ State = 'Running' }
        $CredentialPath = 'unused-fixture-credential.xml'
        $SkipWindowsUpdate = $true
        Mock Start-VM { }
        Mock Import-Clixml { 'mocked credential; no file is read' }
        Mock New-LocalVmSession { 'mocked session' }
        Mock Invoke-Command { }
        Mock Remove-PSSession { }
    }

    It 'refreshes the configured administrator even when Windows Update is skipped' {
        & {
            [CmdletBinding(SupportsShouldProcess)]
            param()
            & $script:hostRefresh -PSScriptRoot (Join-Path $skillRoot 'scripts')
        }
        Assert-MockCalled Invoke-Command -Times 1 -Exactly -Scope It -ParameterFilter {
            $FilePath -like '*\templates\oem\Set-UiTestAdminPasswordPolicy.ps1' -and
            $ArgumentList[0] -eq 'Custom Admin' -and $ArgumentList[1] -eq 'Custom User'
        }
        Assert-MockCalled Remove-PSSession -Times 1 -Exactly -Scope It
        Assert-MockCalled Start-VM -Times 0 -Exactly -Scope It
    }

    It 'does not refresh accounts or read credentials under WhatIf' {
        & {
            [CmdletBinding(SupportsShouldProcess)]
            param()
            & $script:hostRefresh -PSScriptRoot (Join-Path $skillRoot 'scripts')
        } -WhatIf
        Assert-MockCalled Import-Clixml -Times 0 -Exactly -Scope It
        Assert-MockCalled Invoke-Command -Times 0 -Exactly -Scope It
    }

    It 'surfaces refresh failures and still removes the session' {
        Mock Invoke-Command { throw 'Guest account policy failed' }
        {
            & {
                [CmdletBinding(SupportsShouldProcess)]
                param()
                & $script:hostRefresh -PSScriptRoot (Join-Path $skillRoot 'scripts')
            }
        } | Should Throw 'Guest account policy failed'
        Assert-MockCalled Remove-PSSession -Times 1 -Exactly -Scope It
    }

    It 'passes explicit custom account names through the Windows servicing entry point' {
        $AdminUserName = 'Custom Admin'
        $StandardUser = 'Custom User'
        $session = 'mocked session'
        & $script:updateRefresh -PSScriptRoot (Join-Path $skillRoot 'scripts')
        Assert-MockCalled Invoke-Command -Times 1 -Exactly -Scope It -ParameterFilter {
            $ArgumentList[0] -eq 'Custom Admin' -and $ArgumentList[1] -eq 'Custom User'
        }
        Assert-MockCalled Remove-PSSession -Times 1 -Exactly -Scope It
    }

    It 'preserves the updater default by selecting the credential local name rather than PTAdmin' {
        $AdminUserName = ''
        $credential = [pscustomobject]@{ UserName = 'FIXTURE\Custom Admin' }
        $default = $script:updateAst.EndBlock.Statements | Where-Object {
            $_.Extent.Text.StartsWith('if ([string]::IsNullOrWhiteSpace($AdminUserName))')
        }
        . ([scriptblock]::Create($default.Extent.Text))
        $AdminUserName | Should Be 'Custom Admin'
        ($script:updateAst.ParamBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'StandardUser' }).DefaultValue.Value |
            Should Be 'PTUser'
    }

    It 'keeps CheckOnly ahead of the mutating host refresh' {
        $text = $script:hostAst.Extent.Text
        ($text.IndexOf('if ($CheckOnly)') -lt $text.IndexOf('$guestVm = Get-VM')) | Should Be $true
        $text | Should Match '\-AdminUserName \$configuration.AdminUserName\s+`\s*\-StandardUser \$configuration.StandardUser'
    }
}
