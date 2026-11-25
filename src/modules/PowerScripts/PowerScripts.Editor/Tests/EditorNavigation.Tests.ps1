# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

param(
    [Parameter(Mandatory)][int]$AppPid,
    [string]$ScriptName,
    [string]$WinAppPath = 'winapp.exe'
)

$ErrorActionPreference = 'Stop'
$winapp = (Get-Command $WinAppPath -ErrorAction Stop).Source
$dialogOpen = $false

function Invoke-UI {
    param([Parameter(ValueFromRemainingArguments)][string[]]$Arguments)
    $output = & $winapp ui @Arguments -a $AppPid 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($output -join "`n") }
    $output
}

try {
    Invoke-UI wait-for PowerScriptsCreateScript -t 5000 | Out-Null
    Invoke-UI wait-for PowerScriptsGlobalSettings -t 5000 | Out-Null

    Invoke-UI invoke PowerScriptsCreateScript | Out-Null
    $dialogOpen = $true
    Invoke-UI wait-for PowerScriptsNewScriptChoice -t 5000 | Out-Null
    Invoke-UI wait-for PowerScriptsCreateScript --property IsEnabled --value False -t 3000 | Out-Null
    & $winapp ui invoke PowerScriptsCreateScript -a $AppPid 2>&1 | Out-Null
    Invoke-UI wait-for PowerScriptsNewOperationChoice -t 3000 | Out-Null
    Invoke-UI invoke CloseButton | Out-Null
    $dialogOpen = $false
    Invoke-UI wait-for PowerScriptsCreateScript --property IsEnabled --value True -t 3000 | Out-Null
    Write-Output 'PASS: modal re-entry is blocked and cancellation restores creation.'

    Invoke-UI invoke PowerScriptsCreateScript | Out-Null
    $dialogOpen = $true
    Invoke-UI wait-for PowerScriptsNewScriptChoice -t 5000 | Out-Null
    Invoke-UI invoke PowerScriptsNewScriptChoice --action invoke | Out-Null
    $dialogOpen = $false
    Invoke-UI wait-for PowerScriptsTemplateProjectName -t 5000 | Out-Null
    Invoke-UI invoke PART_BackButton | Out-Null
    Invoke-UI wait-for PowerScriptsCreateScript -t 5000 | Out-Null
    Write-Output 'PASS: the script chooser supports InvokePattern and opens editable creation.'

    Invoke-UI invoke PowerScriptsCreateScript | Out-Null
    $dialogOpen = $true
    Invoke-UI wait-for PowerScriptsNewOperationChoice -t 5000 | Out-Null
    Invoke-UI invoke PowerScriptsNewOperationChoice --action invoke | Out-Null
    $dialogOpen = $false
    Invoke-UI wait-for PowerScriptsOperationSelector -t 5000 | Out-Null
    Invoke-UI invoke PART_BackButton | Out-Null
    Invoke-UI wait-for PowerScriptsCreateScript -t 5000 | Out-Null
    Write-Output 'PASS: the operation chooser supports InvokePattern and opens native actions.'

    if ($ScriptName) {
        Invoke-UI invoke $ScriptName --type ListItem | Out-Null
        Invoke-UI wait-for PowerScriptsHeroEditScript -t 5000 | Out-Null
        Invoke-UI wait-for PowerScriptsCopySkillInstructionsButton --property IsEnabled --value True -t 5000 | Out-Null
        Invoke-UI invoke PART_BackButton | Out-Null
        Invoke-UI wait-for PowerScriptsCreateScript -t 5000 | Out-Null
        Write-Output 'PASS: script details retain the AI setup action beside editing.'
    }

    if (-not (Get-Process -Id $AppPid).Responding) { throw 'The Editor is unresponsive.' }
}
finally {
    if ($dialogOpen) {
        & $winapp ui invoke CloseButton -a $AppPid 2>&1 | Out-Null
    }
}
