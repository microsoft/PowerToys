# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $ResultsDirectory,
    [Parameter(Mandatory)][guid] $RunId
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\MwbSandboxCi.Common.ps1"

function Get-MwbDiagnosticEndpointNames {
    @('PowerToys.exe', 'PowerToys.MouseWithoutBorders.exe', 'PowerToys.MouseWithoutBorders.dll',
        'PowerToys.MouseWithoutBordersHelper.exe', 'WinUI3Apps\PowerToys.Settings.exe',
        'WinUI3Apps\PowerToys.QuickAccess.exe')
}

function ConvertTo-MwbRuntimeEvent {
    param($Event, [string] $ProductRoot)

    $text = $Event.ToXml()
    if ($text.Length -gt 256KB) { throw 'The runtime event exceeds its diagnostic size bound.' }
    [xml]$document = $text
    $fields = @{}
    foreach ($node in $document.SelectNodes(
        '//*[local-name()="EventData"]/*[local-name()="Data"] | //*[local-name()="UserData"]//*[not(*)]')) {
        $name = if ($node.HasAttribute('Name')) { $node.GetAttribute('Name') } else { $node.LocalName }
        $fields[$name] = $node.InnerText
    }
    # Messages and VmlEventLog can contain Sandbox command lines and ephemeral passwords.
    # Only named, scalar error-code fields are safe to export.
    $codes = @(foreach ($name in @('Error', 'ErrorCode', 'HResult', 'Result', 'Status', 'Win32ErrorCode')) {
        $value = [string]$fields[$name]
        $number = 0L
        if ($value -match '^0x[0-9a-fA-F]{1,8}$') {
            $number = [Convert]::ToInt64($value.Substring(2), 16)
        }
        elseif ($value -notmatch '^-?\d{1,11}$' -or -not [long]::TryParse($value, [ref]$number) -or
            $number -lt [int]::MinValue -or $number -gt [uint32]::MaxValue) { continue }
        [ordered]@{ Field = $name; Value = '0x{0:X8}' -f ($number -band 0xffffffffL) }
    })
    $result = [ordered]@{
        RecordId = [long]$Event.RecordId
        EventId = [int]$Event.Id
        Level = [int]$Event.Level
        TimestampUtc = $Event.TimeCreated.ToUniversalTime().ToString('o')
        ErrorCodes = $codes
    }
    $threat = $fields['Threat Name']
    if ($threat -is [string] -and $threat -cmatch '^[A-Za-z0-9:/_.!+-]{1,128}$') {
        $result.ThreatName = $threat
    }
    if ($fields['Threat ID'] -cmatch '^\d{1,20}$') { $result.ThreatId = $fields['Threat ID'] }
    $resource = [string]$fields['Path']
    $result.MatchingEndpointFiles = @(Get-MwbDiagnosticEndpointNames | Where-Object {
        $pattern = [regex]::Escape((Join-Path $ProductRoot $_)) + '(?=$|[;\r\n])'
        [regex]::IsMatch($resource, $pattern, [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    })
    $result
}

function Get-MwbRuntimeEventLog {
    param([string] $LogName, [DateTime] $StartedUtc, [DateTime] $CompletedUtc, [string] $ProductRoot)

    $report = [ordered]@{ LogName = $LogName; QueryStatus = 'NotChecked'; EventLimitReached = $false; Events = @() }
    $filter = @{ LogName = $LogName; StartTime = $StartedUtc.ToLocalTime(); EndTime = $CompletedUtc.ToLocalTime() }
    if ($LogName -eq 'Microsoft-Windows-Windows Defender/Operational') {
        $filter.Id = @(1116, 1117, 1118, 1119, 1121)
    }
    else { $filter.Level = @(1, 2, 3) }
    try {
        $report.LogEnabled = (Get-WinEvent -ListLog $LogName -ErrorAction Stop).IsEnabled
        if ($report.LogEnabled -isnot [bool]) { throw 'The runtime event log state is unavailable.' }
        if (-not $report.LogEnabled) {
            $report.QueryStatus = 'Unavailable'
            $report.QueryError = @{ Code = 'RuntimeEventLogDisabled' }
            Write-Warning "MWB event log $LogName is disabled; it was not enabled and no absence conclusion was made."
            return $report
        }
        $events = @(Get-WinEvent -FilterHashtable $filter -MaxEvents 32 -ErrorAction Stop)
        $report.Events = @($events | ForEach-Object { ConvertTo-MwbRuntimeEvent $_ $ProductRoot })
        $report.EventLimitReached = $events.Count -eq 32
        $report.QueryStatus = 'Succeeded'
    }
    catch {
        if ($_.FullyQualifiedErrorId -like 'NoMatchingEventsFound,*') {
            $report.QueryStatus = 'Succeeded'
        }
        else {
            $report.QueryStatus = 'Failed'
            $report.QueryError = Get-MwbDiagnosticError $_ 'RuntimeEventQueryFailed'
            Write-Warning "MWB event diagnostics unavailable for $LogName ($($report.QueryError.HResult)); no absence conclusion was made."
        }
    }
    $report
}

function Get-MwbRuntimeDiagnostics {
    param([Collections.IDictionary] $State, [guid] $RunId)

    if ([guid]$State.RunId -ne $RunId) { throw 'The runtime diagnostic marker belongs to another run.' }
    $product = [IO.Path]::GetFullPath([string]$State.ProductRoot).TrimEnd('\')
    $expected = "C:\ProgramData\PowerToysMwbSandboxCi\$($RunId.ToString('N'))\host-product"
    if ($product -ine $expected) { throw 'The runtime diagnostic product root is not this CI run.' }
    Assert-MwbCiPlainPath $product
    $completed = [DateTime]::UtcNow
    # ConvertFrom-Json can already return a UTC DateTime. Converting it to a string
    # first would discard its kind and reinterpret UTC as local time on the agent.
    $prepared = if ($State.PreparedUtc -is [DateTime]) {
        $State.PreparedUtc.ToUniversalTime()
    }
    else {
        [DateTime]::Parse([string]$State.PreparedUtc, [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()
    }
    if ($prepared -gt $completed) { throw 'The runtime diagnostic time window is reversed.' }
    $started = if ($prepared -lt $completed.AddHours(-2)) { $completed.AddHours(-2) } else { $prepared }
    $report = [ordered]@{
        SchemaVersion = 1; RunId = $RunId.ToString(); QueryStatus = 'Succeeded'
        OsVersion = [Environment]::OSVersion.Version.ToString()
        StartedUtc = $started.ToString('o'); CompletedUtc = $completed.ToString('o')
        TimeWindowClamped = $started -gt $prepared
        Events = @(); Services = @(); EndpointFiles = @()
    }
    $report.Events = @(foreach ($log in @('Microsoft-Windows-Hyper-V-Compute-Admin',
        'Microsoft-Windows-Hyper-V-Compute-Operational',
        'Microsoft-Windows-Hyper-V-Worker-Admin', 'Microsoft-Windows-Containers-Wcifs/Operational',
        'Microsoft-Windows-Windows Defender/Operational')) {
        Get-MwbRuntimeEventLog $log $started $completed $product
    })
    $report.Services = @(foreach ($name in @('vmcompute', 'hns', 'vmms', 'WinDefend')) {
        $item = [ordered]@{ Name = $name; QueryStatus = 'NotChecked'; Present = $null }
        try {
            $item.Status = (Get-Service -Name $name -ErrorAction Stop).Status.ToString()
            $item.Present = $true
            $item.QueryStatus = 'Succeeded'
        }
        catch {
            if ($_.FullyQualifiedErrorId -like 'NoServiceFoundForGivenName,*') {
                $item.QueryStatus = 'Succeeded'
                $item.Present = $false
            }
            else {
                $item.QueryStatus = 'Failed'
                $item.QueryError = Get-MwbDiagnosticError $_ 'RuntimeServiceQueryFailed'
                Write-Warning "MWB service diagnostics unavailable for $name ($($item.QueryError.HResult))."
            }
        }
        $item
    })
    $report.EndpointFiles = @(foreach ($relative in Get-MwbDiagnosticEndpointNames) {
        $item = [ordered]@{ Path = $relative; QueryStatus = 'NotChecked' }
        try {
            $path = Join-Path $product $relative
            Assert-MwbCiPlainPath $path
            $file = Get-Item -LiteralPath $path -ErrorAction Stop
            $item.Length = $file.Length
            $item.LastWriteUtc = $file.LastWriteTimeUtc.ToString('o')
            $item.Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256 -ErrorAction Stop).Hash
            $item.QueryStatus = 'Succeeded'
        }
        catch {
            $item.QueryStatus = 'Failed'
            $item.QueryError = Get-MwbDiagnosticError $_ 'RuntimeFileQueryFailed'
            Write-Warning "MWB file diagnostics unavailable for $relative ($($item.QueryError.HResult)); protection was not bypassed."
        }
        $item
    })
    $report.Defender = [ordered]@{ QueryStatus = 'NotChecked' }
    try {
        $defender = Get-MpComputerStatus -ErrorAction Stop
        foreach ($name in @('AMProductVersion', 'AMEngineVersion', 'AntivirusSignatureVersion')) {
            $value = $defender.$name
            if ($value -isnot [string] -or $value -cnotmatch '^\d+(?:\.\d+){1,4}$') {
                throw 'The Defender diagnostic version is invalid.'
            }
            $report.Defender[$name] = $value
        }
        foreach ($name in @('AMServiceEnabled', 'AntivirusEnabled', 'RealTimeProtectionEnabled')) {
            if ($defender.$name -isnot [bool]) { throw 'The Defender diagnostic protection flag is invalid.' }
            $report.Defender[$name] = $defender.$name
        }
        $report.Defender.QueryStatus = 'Succeeded'
    }
    catch {
        $report.Defender.QueryStatus = 'Failed'
        $report.Defender.QueryError = Get-MwbDiagnosticError $_ 'DefenderStatusQueryFailed'
        Write-Warning "MWB Defender status unavailable ($($report.Defender.QueryError.HResult)); no protection settings were changed."
    }
    if (@(@($report.Events) + @($report.Services) + @($report.EndpointFiles) + @($report.Defender) |
        Where-Object QueryStatus -NE 'Succeeded').Count) { $report.QueryStatus = 'Partial' }
    $report
}

function Export-MwbRuntimeDiagnostics {
    param([string] $ResultsDirectory, [guid] $RunId)

    $directory = Get-MwbPrerequisiteDirectory $ResultsDirectory $RunId
    Assert-MwbCiPlainPath $directory
    $null = New-Item -ItemType Directory -Path $directory -Force
    $output = Join-Path $directory 'runtime-diagnostics.json'
    Assert-MwbCiPlainPath $output
    try {
        $marker = 'C:\ProgramData\PowerToysMwbExperiment\host-provisioning.json'
        Assert-MwbCiPlainPath $marker
        if ((Get-Item -LiteralPath $marker -ErrorAction Stop).Length -gt 1MB) { throw 'The runtime diagnostic marker is too large.' }
        $state = Get-Content -LiteralPath $marker -Raw -ErrorAction Stop | ConvertFrom-Json -AsHashtable
        $report = Get-MwbRuntimeDiagnostics $state $RunId
    }
    catch {
        $report = [ordered]@{
            SchemaVersion = 1; RunId = $RunId.ToString(); QueryStatus = 'Failed'
            QueryError = Get-MwbDiagnosticError $_ 'RuntimeDiagnosticCollectionFailed'
        }
        Write-Warning "MWB runtime diagnostics could not be scoped or collected ($($report.QueryError.HResult))."
    }
    $report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $output -Encoding utf8
    Write-Host "MWB runtime diagnostics: $($report.QueryStatus). Raw event messages and command lines were not exported."
}

Export-MwbRuntimeDiagnostics -ResultsDirectory $ResultsDirectory -RunId $RunId
