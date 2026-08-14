[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PowerToysRoot,

    [ValidateRange(1, 100)]
    [int]$StartupIterations = 20,

    [ValidateRange(0, 10)]
    [int]$WarmupIterations = 1,

    [ValidateRange(1, 100)]
    [int]$NavigationIterations = 5,

    [string]$OutputDirectory = (Join-Path $env:TEMP "PowerToys-Settings-Performance"),

    [switch]$CaptureTrace
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$runnerPath = Join-Path $PowerToysRoot "PowerToys.exe"
$settingsPath = Join-Path $PowerToysRoot "WinUI3Apps\PowerToys.Settings.exe"
$outputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$tracePath = Join-Path $outputDirectory "PowerToys-Settings.etl"
$traceStarted = $false
$startedTargetRunner = $false
$priorRunnerPaths = @()
$hadSettingsWindow = $false

function Get-ProcessByPath
{
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$Path
    )

    $expectedPath = [IO.Path]::GetFullPath($Path)
    return @(Get-Process -Name $Name -ErrorAction SilentlyContinue | Where-Object {
        try
        {
            [string]::Equals([IO.Path]::GetFullPath($_.Path), $expectedPath, [StringComparison]::OrdinalIgnoreCase)
        }
        catch
        {
            $false
        }
    })
}

function Stop-Processes
{
    param([Parameter(Mandatory)][AllowEmptyCollection()][System.Diagnostics.Process[]]$Processes)

    foreach ($process in $Processes)
    {
        if ($process.ProcessName -eq "PowerToys.Settings")
        {
            try
            {
                $null = $process.CloseMainWindow()
                if ($process.WaitForExit(3000))
                {
                    continue
                }
            }
            catch
            {
            }
        }

        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }

    foreach ($process in $Processes)
    {
        try
        {
            $null = $process.WaitForExit(5000)
        }
        catch
        {
        }
    }
}

function Get-ModuleProfile
{
    $settingsFile = Join-Path $env:LOCALAPPDATA "Microsoft\PowerToys\settings.json"
    if (-not (Test-Path -LiteralPath $settingsFile))
    {
        return $null
    }

    try
    {
        $settings = Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json
        $modules = @($settings.enabled.PSObject.Properties)
        $enabled = @($modules | Where-Object { $_.Value -eq $true } | ForEach-Object Name | Sort-Object)
        $disabled = @($modules | Where-Object { $_.Value -eq $false } | ForEach-Object Name | Sort-Object)
        return [ordered]@{
            EnabledCount = $enabled.Count
            DisabledCount = $disabled.Count
            Enabled = $enabled
            Disabled = $disabled
        }
    }
    catch
    {
        return [ordered]@{
            Error = $_.Exception.Message
        }
    }
}

function Get-AutomationRoot
{
    param(
        [Parameter(Mandatory)]
        [int]$AppPid,

        [int]$TimeoutMs = 30000
    )

    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    do
    {
        $process = Get-Process -Id $AppPid -ErrorAction Stop
        $process.Refresh()
        if ($process.MainWindowHandle -ne [IntPtr]::Zero)
        {
            return [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
        }

        Start-Sleep -Milliseconds 10
    }
    while ($stopwatch.ElapsedMilliseconds -lt $TimeoutMs)

    throw "The Settings window handle was not available within $TimeoutMs ms."
}

function Wait-ForNewSettingsProcess
{
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [int[]]$ExistingProcessIds,

        [Parameter(Mandatory)]
        [Diagnostics.Stopwatch]$Stopwatch,

        [int]$TimeoutMs = 30000
    )

    do
    {
        $process = Get-ProcessByPath -Name "PowerToys.Settings" -Path $settingsPath |
            Where-Object { $_.Id -notin $ExistingProcessIds } |
            Select-Object -First 1

        if ($null -ne $process)
        {
            return $process
        }

        Start-Sleep -Milliseconds 20
    }
    while ($Stopwatch.ElapsedMilliseconds -lt $TimeoutMs)

    throw "PowerToys.Settings.exe did not start within $TimeoutMs ms."
}

function Find-AutomationElement
{
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,

        [Parameter(Mandatory)]
        [string]$Selector
    )

    $automationIdCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $Selector)
    $nameCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $Selector)
    $condition = [System.Windows.Automation.OrCondition]::new(
        [System.Windows.Automation.Condition[]]@($automationIdCondition, $nameCondition))

    return $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Wait-ForElement
{
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,

        [Parameter(Mandatory)]
        [string]$Selector,

        [int]$TimeoutMs = 30000
    )

    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    do
    {
        $element = Find-AutomationElement -Root $Root -Selector $Selector
        if ($null -ne $element)
        {
            return $element
        }

        Start-Sleep -Milliseconds 10
    }
    while ($stopwatch.ElapsedMilliseconds -lt $TimeoutMs)

    throw "'$Selector' was not found within $TimeoutMs ms."
}

function Test-ElementExists
{
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,

        [Parameter(Mandatory)]
        [string]$Selector
    )

    return $null -ne (Find-AutomationElement -Root $Root -Selector $Selector)
}

function Invoke-AutomationElement
{
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,

        [Parameter(Mandatory)]
        [string]$Selector
    )

    $element = Wait-ForElement -Root $Root -Selector $Selector -TimeoutMs 5000
    $pattern = $null

    if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern))
    {
        ([System.Windows.Automation.InvokePattern]$pattern).Invoke()
        return
    }

    if ($element.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern))
    {
        ([System.Windows.Automation.TogglePattern]$pattern).Toggle()
        return
    }

    if ($element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern))
    {
        ([System.Windows.Automation.SelectionItemPattern]$pattern).Select()
        return
    }

    if ($element.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern))
    {
        $expandPattern = [System.Windows.Automation.ExpandCollapsePattern]$pattern
        if ($expandPattern.Current.ExpandCollapseState -ne [System.Windows.Automation.ExpandCollapseState]::Expanded)
        {
            $expandPattern.Expand()
        }

        return
    }

    throw "'$Selector' does not expose an invokable UI Automation pattern."
}

function Ensure-NavigationGroupExpanded
{
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Root,

        [Parameter(Mandatory)]
        [string]$ParentSelector,

        [Parameter(Mandatory)]
        [string]$ChildSelector
    )

    if (-not (Test-ElementExists -Root $Root -Selector $ChildSelector))
    {
        Invoke-AutomationElement -Root $Root -Selector $ParentSelector
        Wait-ForElement -Root $Root -Selector $ChildSelector -TimeoutMs 5000 | Out-Null
    }
}

function Get-Percentile
{
    param(
        [Parameter(Mandatory)]
        [double[]]$Values,

        [Parameter(Mandatory)]
        [ValidateRange(0, 1)]
        [double]$Percentile
    )

    if ($Values.Count -eq 0)
    {
        return 0
    }

    $sorted = @($Values | Sort-Object)
    $index = [Math]::Max(0, [Math]::Ceiling($Percentile * $sorted.Count) - 1)
    return [Math]::Round($sorted[$index], 1)
}

function Get-Summary
{
    param(
        [Parameter(Mandatory)]
        [object[]]$Samples,

        [Parameter(Mandatory)]
        [string]$Property
    )

    $values = @($Samples | ForEach-Object { [double]$_.$Property })
    return [ordered]@{
        Count = $values.Count
        Median = Get-Percentile -Values $values -Percentile 0.5
        P95 = Get-Percentile -Values $values -Percentile 0.95
        Min = [Math]::Round(($values | Measure-Object -Minimum).Minimum, 1)
        Max = [Math]::Round(($values | Measure-Object -Maximum).Maximum, 1)
    }
}

if (-not (Test-Path -LiteralPath $runnerPath))
{
    throw "PowerToys runner not found: $runnerPath"
}

if (-not (Test-Path -LiteralPath $settingsPath))
{
    throw "PowerToys Settings not found: $settingsPath"
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$allExistingRunners = @(Get-Process -Name "PowerToys" -ErrorAction SilentlyContinue)
$targetRunner = Get-ProcessByPath -Name "PowerToys" -Path $runnerPath | Select-Object -First 1
$hadSettingsWindow = @(Get-Process -Name "PowerToys.Settings" -ErrorAction SilentlyContinue).Count -gt 0

try
{
    if ($null -eq $targetRunner)
    {
        $priorRunnerPaths = @($allExistingRunners | ForEach-Object {
            try
            {
                $_.Path
            }
            catch
            {
                $null
            }
        } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)

        Stop-Processes -Processes $allExistingRunners
        Start-Sleep -Seconds 2

        $targetRunner = Start-Process $runnerPath -PassThru
        $startedTargetRunner = $true
        Start-Sleep -Seconds 8
    }

    if ($CaptureTrace)
    {
        $wprStatus = (& wpr -status 2>&1 | Out-String)
        if ($wprStatus -notmatch "WPR (is not recording|recording is not in progress)")
        {
            throw "A WPR recording is already in progress."
        }

        & wpr -start CPU -start DiskIO -start FileIO -start XAMLActivity -start XAMLAppResponsiveness -start DotNET -filemode | Out-Null
        if ($LASTEXITCODE -ne 0)
        {
            throw "Failed to start WPR."
        }

        $traceStarted = $true
    }

    $moduleProfileBefore = Get-ModuleProfile
    $startupSamples = @()
    $totalStartupIterations = $WarmupIterations + $StartupIterations

    for ($iteration = 1; $iteration -le $totalStartupIterations; $iteration++)
    {
        Stop-Processes -Processes @(Get-ProcessByPath -Name "PowerToys.Settings" -Path $settingsPath)
        Start-Sleep -Milliseconds 500

        $existingIds = @(Get-Process -Name "PowerToys.Settings" -ErrorAction SilentlyContinue | ForEach-Object Id)
        $stopwatch = [Diagnostics.Stopwatch]::StartNew()
        Start-Process $runnerPath -ArgumentList "--open-settings=Dashboard" | Out-Null

        $settingsProcess = Wait-ForNewSettingsProcess -ExistingProcessIds $existingIds -Stopwatch $stopwatch
        $processDiscoveredMs = $stopwatch.Elapsed.TotalMilliseconds
        $automationRoot = Get-AutomationRoot -AppPid $settingsProcess.Id

        Wait-ForElement -Root $automationRoot -Selector "DashboardNavItem" | Out-Null
        $shellReadyMs = $stopwatch.Elapsed.TotalMilliseconds

        Wait-ForElement -Root $automationRoot -Selector "DashboardSortButton" | Out-Null
        $dashboardReadyMs = $stopwatch.Elapsed.TotalMilliseconds

        Start-Sleep -Milliseconds 500
        $settingsProcess.Refresh()
        $sample = [ordered]@{
            Iteration = $iteration
            IsWarmup = $iteration -le $WarmupIterations
            ProcessDiscoveredMs = [Math]::Round($processDiscoveredMs, 1)
            ShellReadyMs = [Math]::Round($shellReadyMs, 1)
            DashboardReadyMs = [Math]::Round($dashboardReadyMs, 1)
            CpuMs = [Math]::Round($settingsProcess.TotalProcessorTime.TotalMilliseconds, 1)
            WorkingSetMB = [Math]::Round($settingsProcess.WorkingSet64 / 1MB, 1)
            PrivateMemoryMB = [Math]::Round($settingsProcess.PrivateMemorySize64 / 1MB, 1)
            HandleCount = $settingsProcess.HandleCount
        }

        $startupSamples += [pscustomobject]$sample
        Write-Host "Startup $iteration/$totalStartupIterations`: dashboard ready in $($sample.DashboardReadyMs) ms"
    }

    $settingsProcess = Get-ProcessByPath -Name "PowerToys.Settings" -Path $settingsPath | Select-Object -First 1
    if ($null -eq $settingsProcess)
    {
        throw "Settings process exited before navigation benchmarks."
    }

    $automationRoot = Get-AutomationRoot -AppPid $settingsProcess.Id
    $navigationCases = @(
        [pscustomobject]@{
            Name = "General"
            ParentSelector = $null
            NavSelector = "GeneralNavItem"
            ReadySelector = "Languages_ComboBox"
        },
        [pscustomobject]@{
            Name = "AdvancedPaste"
            ParentSelector = "SystemToolsNavItem"
            NavSelector = "AdvancedPasteNavItem"
            ReadySelector = "AdvancedPasteShortcutExpander"
        },
        [pscustomobject]@{
            Name = "FancyZones"
            ParentSelector = "WindowingAndLayoutsNavItem"
            NavSelector = "FancyZonesNavItem"
            ReadySelector = "EnableFancyZonesToggleSwitch"
        },
        [pscustomobject]@{
            Name = "MouseUtilities"
            ParentSelector = "InputOutputNavItem"
            NavSelector = "MouseUtilitiesNavItem"
            ReadySelector = "MouseUtils_CursorWrapTestId"
        }
    )

    foreach ($case in $navigationCases)
    {
        if ($null -ne $case.ParentSelector)
        {
            Ensure-NavigationGroupExpanded -Root $automationRoot -ParentSelector $case.ParentSelector -ChildSelector $case.NavSelector
        }
    }

    $navigationSamples = @()
    for ($iteration = 1; $iteration -le $NavigationIterations; $iteration++)
    {
        foreach ($case in $navigationCases)
        {
            if ($null -ne $case.ParentSelector)
            {
                Ensure-NavigationGroupExpanded -Root $automationRoot -ParentSelector $case.ParentSelector -ChildSelector $case.NavSelector
            }

            Invoke-AutomationElement -Root $automationRoot -Selector "DashboardNavItem"
            Wait-ForElement -Root $automationRoot -Selector "DashboardSortButton" | Out-Null
            Start-Sleep -Milliseconds 100

            $stopwatch = [Diagnostics.Stopwatch]::StartNew()
            Invoke-AutomationElement -Root $automationRoot -Selector $case.NavSelector
            Wait-ForElement -Root $automationRoot -Selector $case.ReadySelector | Out-Null
            $navigationReadyMs = $stopwatch.Elapsed.TotalMilliseconds

            $settingsProcess.Refresh()
            $sample = [ordered]@{
                Iteration = $iteration
                Page = $case.Name
                NavigationReadyMs = [Math]::Round($navigationReadyMs, 1)
                CpuMs = [Math]::Round($settingsProcess.TotalProcessorTime.TotalMilliseconds, 1)
                WorkingSetMB = [Math]::Round($settingsProcess.WorkingSet64 / 1MB, 1)
                PrivateMemoryMB = [Math]::Round($settingsProcess.PrivateMemorySize64 / 1MB, 1)
                HandleCount = $settingsProcess.HandleCount
            }

            $navigationSamples += [pscustomobject]$sample
            Write-Host "Navigation $iteration/$NavigationIterations $($case.Name): $($sample.NavigationReadyMs) ms"
        }
    }

    if ($traceStarted)
    {
        & wpr -stop $tracePath | Out-Null
        if ($LASTEXITCODE -ne 0)
        {
            throw "Failed to stop WPR."
        }

        $traceStarted = $false
    }

    $measuredStartupSamples = @($startupSamples | Where-Object { -not $_.IsWarmup })
    $navigationSummary = [ordered]@{}
    foreach ($case in $navigationCases)
    {
        $pageSamples = @($navigationSamples | Where-Object { $_.Page -eq $case.Name })
        $navigationSummary[$case.Name] = Get-Summary -Samples $pageSamples -Property "NavigationReadyMs"
    }

    $result = [ordered]@{
        Timestamp = (Get-Date).ToString("o")
        PowerToysRoot = $PowerToysRoot
        RunnerVersion = (Get-Item $runnerPath).VersionInfo.FileVersion
        SettingsVersion = (Get-Item $settingsPath).VersionInfo.FileVersion
        Machine = [ordered]@{
            ComputerName = $env:COMPUTERNAME
            OS = (Get-CimInstance Win32_OperatingSystem).Caption
            OSVersion = [Environment]::OSVersion.Version.ToString()
            Processor = (Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name)
            Architecture = $env:PROCESSOR_ARCHITECTURE
        }
        ModuleProfileBefore = $moduleProfileBefore
        ModuleProfileAfter = Get-ModuleProfile
        Configuration = [ordered]@{
            StartupIterations = $StartupIterations
            WarmupIterations = $WarmupIterations
            NavigationIterations = $NavigationIterations
            TraceCaptured = $CaptureTrace.IsPresent
        }
        StartupSummary = [ordered]@{
            ProcessDiscoveredMs = Get-Summary -Samples $measuredStartupSamples -Property "ProcessDiscoveredMs"
            ShellReadyMs = Get-Summary -Samples $measuredStartupSamples -Property "ShellReadyMs"
            DashboardReadyMs = Get-Summary -Samples $measuredStartupSamples -Property "DashboardReadyMs"
            CpuMs = Get-Summary -Samples $measuredStartupSamples -Property "CpuMs"
            WorkingSetMB = Get-Summary -Samples $measuredStartupSamples -Property "WorkingSetMB"
            PrivateMemoryMB = Get-Summary -Samples $measuredStartupSamples -Property "PrivateMemoryMB"
            HandleCount = Get-Summary -Samples $measuredStartupSamples -Property "HandleCount"
        }
        NavigationSummary = $navigationSummary
        StartupSamples = $startupSamples
        NavigationSamples = $navigationSamples
        TracePath = if ($CaptureTrace) { $tracePath } else { $null }
    }

    $resultPath = Join-Path $outputDirectory "results.json"
    $result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding utf8
    Write-Host "Results: $resultPath"
    if ($CaptureTrace)
    {
        Write-Host "Trace: $tracePath"
    }
}
finally
{
    $restoreRunnerPath = $null

    if ($traceStarted)
    {
        & wpr -cancel *> $null
    }

    Stop-Processes -Processes @(Get-ProcessByPath -Name "PowerToys.Settings" -Path $settingsPath)

    if ($startedTargetRunner)
    {
        Stop-Processes -Processes @(Get-ProcessByPath -Name "PowerToys" -Path $runnerPath)

        foreach ($priorRunnerPath in $priorRunnerPaths)
        {
            if (Test-Path -LiteralPath $priorRunnerPath)
            {
                Start-Process $priorRunnerPath | Out-Null
            }
        }

        $restoreRunnerPath = $priorRunnerPaths | Select-Object -First 1
    }
    else
    {
        $restoreRunnerPath = $runnerPath
    }

    if ($hadSettingsWindow -and -not [string]::IsNullOrWhiteSpace($restoreRunnerPath))
    {
        Start-Sleep -Seconds 5
        Start-Process $restoreRunnerPath -ArgumentList "--open-settings=Dashboard" | Out-Null
    }
}
