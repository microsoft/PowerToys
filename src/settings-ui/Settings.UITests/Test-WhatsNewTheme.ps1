# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Captures the What's New theme-override regression matrix for visual review.
.DESCRIPTION
Requires winapp CLI and English Settings UI. Open General and a separate What's
New window containing inline code (for example April 2026). The script captures
Light, Dark, Windows default and a live return to Light in the same window,
then restores the original PowerToys theme. It does not change Windows settings.
Review the PNGs for readable caption buttons, headings, links and code; capture
success alone is not a visual pass. Repeat with the opposite Windows app mode.
#>
param(
    [Parameter(Mandatory)]
    [int]$AppPid,
    [string]$ExpectedExecutable = (Join-Path $PSScriptRoot '..\..\..\x64\Debug\WinUI3Apps\PowerToys.Settings.exe'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\..\..\artifacts\test-results\whats-new-theme-ui')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
$process = Get-Process -Id $AppPid
$expectedPath = (Resolve-Path -LiteralPath $ExpectedExecutable).Path
if (-not [string]::Equals($process.Path, $expectedPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw "PID $AppPid is running '$($process.Path)', not the expected build '$expectedPath'."
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

function Invoke-WinApp {
    param([string[]]$Arguments)
    $output = & winapp @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "winapp $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }

    $output
}

function Get-AppWindows {
    $all = Invoke-WinApp -Arguments @('ui', 'list-windows', '--json') | ConvertFrom-Json
    @($all | Where-Object { $_.processId -eq $AppPid })
}

function Find-Control {
    param([string]$Name, [System.Windows.Automation.ControlType]$Type)
    $root = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$script:mainHwnd)
    $nameCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $typeCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $Type)
    $condition = [System.Windows.Automation.AndCondition]::new($nameCondition, $typeCondition)
    $control = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if (-not $control) {
        throw "Missing $Type '$Name'. Open General in the selected Settings process."
    }

    $control
}

function Get-Theme {
    $combo = Find-Control -Name 'Theme' -Type ([System.Windows.Automation.ControlType]::ComboBox)
    $selection = $combo.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern)
    $selection.Current.GetSelection()[0].Current.Name
}

function Select-Theme {
    param([string]$Theme)
    $combo = Find-Control -Name 'Theme' -Type ([System.Windows.Automation.ControlType]::ComboBox)
    $expand = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand()
    $option = Find-Control -Name $Theme -Type ([System.Windows.Automation.ControlType]::ListItem)
    $select = $option.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $select.Select()
    if ((Get-Theme) -ne $Theme) {
        throw "Theme selection did not change to '$Theme'."
    }
}

$windows = Get-AppWindows
$main = @($windows | Where-Object { $_.title -eq 'PowerToys Settings' })
$notes = @($windows | Where-Object { $_.title -eq "What's new in PowerToys" })
if ($main.Count -ne 1 -or $notes.Count -ne 1) {
    throw 'Open General and a separate What''s New window in the selected Settings process before rerunning.'
}

$script:mainHwnd = $main[0].hwnd
$notesHwnd = $notes[0].hwnd
# Expose General before accessing UIA; an obscured WinUI tree may omit its controls.
$prerequisiteImage = Join-Path $OutputDirectory '00-Settings-prerequisite.png'
Invoke-WinApp -Arguments @('ui', 'screenshot', '-w', "$script:mainHwnd", '--focus', '-o', $prerequisiteImage) | Out-Null
$originalTheme = Get-Theme
if ($originalTheme -notin @('Light', 'Dark', 'Windows default')) {
    throw "Unexpected original theme '$originalTheme'; no theme changes made."
}

$results = [Collections.Generic.List[object]]::new()
try {
    $index = 0
    foreach ($theme in @('Light', 'Dark', 'Windows default', 'Light')) {
        Select-Theme $theme
        Start-Sleep -Milliseconds 500
        $live = @(Get-AppWindows | Where-Object { $_.hwnd -eq $notesHwnd })
        if ($live.Count -ne 1) {
            throw "What's New did not survive switching to $theme in the same window."
        }

        $index++
        $image = Join-Path $OutputDirectory ("{0:00}-{1}.png" -f $index, ($theme -replace ' ', '-'))
        Invoke-WinApp -Arguments @('ui', 'screenshot', '-w', "$notesHwnd", '--focus', '-o', $image) | Out-Null
        $results.Add([pscustomobject]@{
            theme = $theme
            sameWindow = $true
            screenshot = $image
            visualReview = 'REQUIRED: captions, headings, links, inline and fenced code foreground/background/border'
        })
    }
}
finally {
    Select-Theme $originalTheme
    $windowsTheme = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize'
    [pscustomobject]@{
        executable = $process.Path
        processId = $AppPid
        notesHwnd = $notesHwnd
        windowsAppsUseLightTheme = $windowsTheme.AppsUseLightTheme
        originalPowerToysTheme = $originalTheme
        captureMatrixCompleted = ($results.Count -eq 4)
        captures = $results.ToArray()
        remainingManualChecks = @(
            'Repeat with Windows app mode Light and Dark; script does not modify Windows settings.',
            'Check active, inactive, hovered and maximized caption buttons.',
            'Check fenced code blocks using release notes containing one.',
            'Check high contrast and return to the original contrast theme.'
        )
    } | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $OutputDirectory 'results.json')
}

Write-Output "Captured $($results.Count) same-window theme states. Review PNGs in $OutputDirectory; this script does not claim visual PASS automatically."
