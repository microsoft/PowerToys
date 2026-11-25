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

function Select-CliRecipe {
    param([string]$Module, [string]$Recipe)
    Invoke-UI invoke PowerScriptsTemplateCliModule | Out-Null
    Invoke-UI scroll-into-view "PowerScriptsTemplateCliModule_$Module" | Out-Null
    Invoke-UI invoke "PowerScriptsTemplateCliModule_$Module" | Out-Null
    Invoke-UI invoke PowerScriptsTemplateCliAction | Out-Null
    Invoke-UI scroll-into-view "PowerScriptsTemplateCliContent_$Recipe" | Out-Null
    Invoke-UI wait-for "PowerScriptsTemplateCliContent_$Recipe" -t 3000 | Out-Null
    Invoke-UI invoke "PowerScriptsTemplateCliContent_$Recipe" | Out-Null
}

function Assert-SelectedContract {
    param([string]$InputType, [string]$OutputType)
    $window = [System.Windows.Automation.AutomationElement]::FromHandle((Get-Process -Id $AppPid).MainWindowHandle)
    foreach ($contract in @(
        @{ picker = 'PowerScriptsTemplateInputPicker'; item = "PowerScriptsTemplateInput_$InputType" },
        @{ picker = 'PowerScriptsTemplateOutputPicker'; item = "PowerScriptsTemplateOutput_$OutputType" }
    )) {
        Invoke-UI wait-for $contract.picker -t 3000 | Out-Null
        Invoke-UI scroll-into-view $contract.picker | Out-Null
        $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $contract.picker)
        $element = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if (-not $element) { throw "Contract picker is missing: $($contract.picker)" }
        $pattern = $element.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern)
        $selected = @($pattern.Current.GetSelection())
        if ($selected.Count -ne 1 -or $selected[0].Current.AutomationId -ne $contract.item) {
            throw "The recipe did not select its declared contract: $($contract.item)"
        }
    }
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

    Add-Type -Path "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationTypes.dll"
    Add-Type -Path "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationClient.dll"
    foreach ($scenario in @(
        @{ module = 'advanced-paste'; recipe = 'advanced-paste-file-result'; input = 'files'; output = 'files' },
        @{ module = 'advanced-paste'; recipe = 'advanced-paste-ai-text-text'; input = 'text'; output = 'text' },
        @{ module = 'file-locksmith'; recipe = 'file-locksmith-query'; input = 'files'; output = 'text' },
        @{ module = 'file-locksmith'; recipe = 'file-locksmith-wait'; input = 'files'; output = 'none' },
        @{ module = 'file-locksmith'; recipe = 'file-locksmith-paths-query'; input = 'text'; output = 'text' },
        @{ module = 'fancyzones'; recipe = 'fancyzones-layout'; input = 'none'; output = 'none' },
        @{ module = 'fancyzones'; recipe = 'fancyzones-query'; input = 'none'; output = 'text' },
        @{ module = 'power-display'; recipe = 'power-display-profile'; input = 'none'; output = 'none' },
        @{ module = 'image-resizer'; recipe = 'image-resizer-options'; input = 'files'; output = 'files' }
    )) {
        Select-CliRecipe $scenario.module $scenario.recipe
        Assert-SelectedContract $scenario.input $scenario.output
        Write-Output "PASS: $($scenario.recipe) selects $($scenario.input) -> $($scenario.output)."
    }

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
