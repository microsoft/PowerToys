# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Unregister-ScheduledTask {
    [CmdletBinding(SupportsShouldProcess)]
    param($TaskName)
    throw 'Unmocked Unregister-ScheduledTask'
}
function New-ScheduledTaskAction {
    param($Execute, $Argument)
    throw 'Unmocked New-ScheduledTaskAction'
}
function New-ScheduledTaskPrincipal {
    param($UserId, $LogonType, $RunLevel)
    throw 'Unmocked New-ScheduledTaskPrincipal'
}
function New-ScheduledTaskSettingsSet {
    param($Priority, $ExecutionTimeLimit, [switch]$AllowStartIfOnBatteries, [switch]$DontStopIfGoingOnBatteries)
    throw 'Unmocked New-ScheduledTaskSettingsSet'
}
function New-ScheduledTask {
    param($Action, $Principal, $Settings)
    throw 'Unmocked New-ScheduledTask'
}
function Register-ScheduledTask {
    param($TaskName, $InputObject, [switch]$Force)
    throw 'Unmocked Register-ScheduledTask'
}
function Start-ScheduledTask {
    param($TaskName)
    throw 'Unmocked Start-ScheduledTask'
}
function Get-ScheduledTask {
    param($TaskName)
    throw 'Unmocked Get-ScheduledTask'
}

Describe 'Local VM interactive task scheduling' {
    BeforeAll {
        $tokens = $null
        $parseErrors = $null
        $path = Join-Path (Split-Path $PSScriptRoot -Parent) 'scripts\Invoke-LocalVmUiTest.ps1'
        $ast = [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$parseErrors)
        if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
        $function = $ast.Find({
            param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
                $node.Name -eq 'Start-InteractiveTask'
        }, $true)
        $remoteBody = $function.Find({
            param($node)
            $node -is [Management.Automation.Language.ScriptBlockExpressionAst]
        }, $true)
        $script:registerInteractiveTask = $remoteBody.ScriptBlock.GetScriptBlock()
    }

    BeforeEach {
        Mock Unregister-ScheduledTask { }
        Mock New-ScheduledTaskAction { 'action' }
        Mock New-ScheduledTaskPrincipal { 'principal' }
        Mock New-ScheduledTaskSettingsSet { 'settings' }
        Mock New-ScheduledTask { 'task' }
        Mock Register-ScheduledTask { }
        Mock Start-ScheduledTask { }
        Mock Get-ScheduledTask {
            [pscustomobject]@{
                State = 'Running'
                Principal = [pscustomobject]@{ UserId = 'fixture-user'; RunLevel = 'Limited' }
            }
        }
    }

    It 'uses normal scheduling and preserves the bounded execution time' {
        & $script:registerInteractiveTask 'fixture-task' 'fixture.exe' 'fixture-arguments' 'fixture-user' 17

        Assert-MockCalled New-ScheduledTaskSettingsSet -Times 1 -Exactly -Scope It -ParameterFilter {
            $Priority -eq 4 -and $ExecutionTimeLimit -eq [TimeSpan]::FromMinutes(17) -and
                $AllowStartIfOnBatteries -and $DontStopIfGoingOnBatteries
        }
    }

    It 'keeps the standard-user interactive token and exact task ownership' {
        $result = & $script:registerInteractiveTask 'fixture-task' 'fixture.exe' 'fixture-arguments' 'fixture-user' 17

        Assert-MockCalled New-ScheduledTaskPrincipal -Times 1 -Exactly -Scope It -ParameterFilter {
            $UserId -eq "$env:COMPUTERNAME\fixture-user" -and $LogonType -eq 'Interactive' -and $RunLevel -eq 'Limited'
        }
        Assert-MockCalled New-ScheduledTaskAction -Times 1 -Exactly -Scope It -ParameterFilter {
            $Execute -eq 'fixture.exe' -and $Argument -eq 'fixture-arguments'
        }
        Assert-MockCalled Unregister-ScheduledTask -Times 1 -Exactly -Scope It -ParameterFilter { $TaskName -eq 'fixture-task' }
        Assert-MockCalled Start-ScheduledTask -Times 1 -Exactly -Scope It -ParameterFilter { $TaskName -eq 'fixture-task' }
        $result.TaskName | Should Be 'fixture-task'
        $result.RunLevel | Should Be 'Limited'
    }

    It 'does not start a task when registration fails' {
        Mock Register-ScheduledTask { throw 'Registration failed' }

        { & $script:registerInteractiveTask 'fixture-task' 'fixture.exe' 'fixture-arguments' 'fixture-user' 17 } |
            Should Throw 'Registration failed'
        Assert-MockCalled Start-ScheduledTask -Times 0 -Exactly -Scope It
    }
}
