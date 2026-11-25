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
$creationOpen = $false

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
    Invoke-UI wait-for PowerScriptsTemplateProjectName -t 5000 | Out-Null
    $creationOpen = $true
    Invoke-UI wait-for PowerScriptsNewScriptDialog --gone -t 3000 | Out-Null
    Invoke-UI wait-for PowerScriptsNewOperationChoice --gone -t 3000 | Out-Null
    Write-Output 'PASS: New script opens editable creation directly without a duplicate action chooser.'

    Invoke-UI invoke PowerScriptsTemplateRuntime_pt | Out-Null
    Invoke-UI wait-for PowerScriptsTemplateCliModule -t 5000 | Out-Null
    Invoke-UI wait-for PowerScriptsTemplateCliAction -t 5000 | Out-Null
    $action = Invoke-UI get-value PowerScriptsTemplateCliAction --json | ConvertFrom-Json
    if (-not $action.text) { throw 'The CLI action catalogue is empty.' }
    Write-Output 'PASS: PowerToys CLI calling remains available inside normal script creation.'

    Invoke-UI invoke PowerScriptsTemplateRuntime_powershell | Out-Null
    Invoke-UI invoke PowerScriptsTemplateOutput_none | Out-Null
    Invoke-UI invoke PowerScriptsTemplateOutput_files | Out-Null
    Invoke-UI invoke PowerScriptsTemplateOutput_text | Out-Null
    Invoke-UI invoke PART_BackButton | Out-Null
    $creationOpen = $false
    Invoke-UI wait-for PowerScriptsCreateScript -t 5000 | Out-Null
    Write-Output 'PASS: normal script outputs and back navigation remain available.'

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
    if ($creationOpen) {
        & $winapp ui invoke PART_BackButton -a $AppPid 2>&1 | Out-Null
    }
}
