# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
[CmdletBinding(DefaultParameterSetName = 'Controller')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Controller')]
    [string] $TestExecutable,
    [Parameter(Mandatory, ParameterSetName = 'Controller')]
    [string] $ResultsDirectory,
    [Parameter(ParameterSetName = 'Controller')]
    [string] $InteractiveUser = [Security.Principal.WindowsIdentity]::GetCurrent().Name,
    [Parameter(ParameterSetName = 'Controller')]
    [string] $Filter,
    [Parameter(ParameterSetName = 'Controller')]
    [ValidateRange(1, 120)]
    [int] $TimeoutMinutes = 45,
    [Parameter(Mandatory, ParameterSetName = 'Worker')]
    [string] $RequestPath
)

$ErrorActionPreference = 'Stop'
$environmentNames = @(
    'DOTNET_ROOT', 'DOTNET_ROOT_X64', 'DOTNET_ROOT_ARM64',
    'WINAPP_CLI_PATH', 'WINAPP_CLI_INVOKE_TIMEOUT_SECONDS',
    'platform', 'TF_BUILD', 'useInstallerForTest', 'POWERTOYS_INSTALL_DIR'
)

if ($PSCmdlet.ParameterSetName -eq 'Worker') {
    # Only the request's location, never its contents, determines where failures may be reported.
    if (-not [IO.Path]::IsPathFullyQualified($RequestPath) -or
        [IO.Path]::GetFileName($RequestPath) -ne 'request.json') {
        throw 'Invalid UI-test request: RequestPath must be an absolute request.json path.'
    }
    $runDirectory = Split-Path ([IO.Path]::GetFullPath($RequestPath)) -Parent
    if ((Split-Path $runDirectory -Leaf) -notmatch '^ui-[0-9a-f]{12}$' -or
        -not (Test-Path -LiteralPath $runDirectory -PathType Container)) {
        throw 'Invalid UI-test request: RequestPath must be inside a run directory.'
    }
    $runId = $null
    $exitCode = 1
    $failure = $null
    $process = $null
    try {
        try {
            $request = Get-Content -LiteralPath $RequestPath -Raw | ConvertFrom-Json -NoEnumerate
        }
        catch {
            throw 'Invalid UI-test request: could not read a JSON object.'
        }
        if ($request -isnot [System.Management.Automation.PSCustomObject]) {
            throw 'Invalid UI-test request: expected a JSON object.'
        }
        if ($request.RunId -is [string] -and $request.RunId -match '^[0-9a-f]{32}$') {
            $runId = $request.RunId
        }
        foreach ($name in @('RunId', 'RunDirectory', 'TestExecutable', 'ResultsDirectory', 'InteractiveUser')) {
            if ($request.$name -isnot [string] -or [string]::IsNullOrWhiteSpace($request.$name)) {
                throw "Invalid UI-test request: $name must be a nonempty string."
            }
        }
        if ($null -eq $runId -or (Split-Path $runDirectory -Leaf) -ne "ui-$($runId.Substring(0, 12))") {
            throw 'Invalid UI-test request: RunId does not match the run directory.'
        }
        foreach ($name in @('RunDirectory', 'TestExecutable', 'ResultsDirectory')) {
            if (-not [IO.Path]::IsPathFullyQualified($request.$name)) {
                throw "Invalid UI-test request: $name must be an absolute path."
            }
            try {
                $request.$name = [IO.Path]::GetFullPath($request.$name)
            }
            catch {
                throw "Invalid UI-test request: $name must be a valid path."
            }
        }
        if ($request.RunDirectory.TrimEnd('\') -ine $runDirectory -or
            $request.ResultsDirectory.TrimEnd('\') -ine $runDirectory) {
            throw 'Invalid UI-test request: RunDirectory and ResultsDirectory must equal the request directory.'
        }
        if (($request.TimeoutMinutes -isnot [int] -and $request.TimeoutMinutes -isnot [long]) -or
            $request.TimeoutMinutes -lt 1 -or $request.TimeoutMinutes -gt 120) {
            throw 'Invalid UI-test request: TimeoutMinutes must be an integer in 1..120.'
        }
        if ($request.Environment -isnot [System.Management.Automation.PSCustomObject]) {
            throw 'Invalid UI-test request: Environment must be an object.'
        }
        foreach ($property in $request.Environment.PSObject.Properties) {
            if ($property.Name -notin $environmentNames -or $property.Value -isnot [string]) {
                throw 'Invalid UI-test request: Environment contains an unsupported name or non-string value.'
            }
        }
        if ($null -ne $request.Filter -and $request.Filter -isnot [string]) {
            throw 'Invalid UI-test request: Filter must be a string.'
        }

        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = [Security.Principal.WindowsPrincipal]::new($identity)
        $sessionId = (Get-Process -Id $PID).SessionId
        $elevated = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        "User=$($identity.Name); Session=$sessionId; Elevated=$elevated" |
            Set-Content -LiteralPath (Join-Path $runDirectory 'desktop.txt')
        if ($elevated -or $sessionId -eq 0 -or $identity.Name -ine $request.InteractiveUser) {
            throw 'UI tests require the requested non-elevated interactive user.'
        }
        if (-not @(Get-Process explorer -ErrorAction SilentlyContinue | Where-Object SessionId -eq $sessionId)) {
            throw 'The interactive test session has no Explorer desktop.'
        }

        foreach ($property in $request.Environment.PSObject.Properties) {
            [Environment]::SetEnvironmentVariable($property.Name, [string]$property.Value, 'Process')
        }
        $start = [Diagnostics.ProcessStartInfo]::new([string]$request.TestExecutable)
        $start.WorkingDirectory = Split-Path $request.TestExecutable -Parent
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        foreach ($argument in @('--report-trx', '--results-directory', [string]$request.ResultsDirectory)) {
            $start.ArgumentList.Add($argument)
        }
        if ($request.Filter) {
            $start.ArgumentList.Add('--filter')
            $start.ArgumentList.Add([string]$request.Filter)
        }
        $process = [Diagnostics.Process]::Start($start)
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit([int]$request.TimeoutMinutes * 60 * 1000)
        if ($timedOut) {
            $process.Kill($true)
            $process.WaitForExit()
        }
        $stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $runDirectory 'stdout.log')
        $stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $runDirectory 'stderr.log')
        if ($timedOut) {
            throw "UI test executable exceeded $($request.TimeoutMinutes) minutes."
        }
        $exitCode = $process.ExitCode
    }
    catch {
        $failure = $_.ToString()
    }
    finally {
        if ($null -ne $process) {
            $process.Dispose()
        }
        $status = @{
            RunId = $runId
            ExitCode = $exitCode
            Error = $failure
        }
        $temporaryStatus = Join-Path $runDirectory 'status.tmp'
        $status | ConvertTo-Json | Set-Content -LiteralPath $temporaryStatus
        Move-Item -LiteralPath $temporaryStatus -Destination (Join-Path $runDirectory 'status.json') -Force
    }
    exit $exitCode
}

$TestExecutable = (Resolve-Path -LiteralPath $TestExecutable).Path
$ResultsDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
try {
    $sid = [Security.Principal.NTAccount]::new($InteractiveUser).Translate([Security.Principal.SecurityIdentifier])
    $InteractiveUser = $sid.Translate([Security.Principal.NTAccount]).Value
}
catch {
    throw "Could not resolve the interactive user '$InteractiveUser' to a Windows account."
}
$runId = [guid]::NewGuid().ToString('N')
# Keep MTP's already-long screenshot paths below MAX_PATH.
$runDirectory = Join-Path $ResultsDirectory "ui-$($runId.Substring(0, 12))"
$requestPath = Join-Path $runDirectory 'request.json'
$statusPath = Join-Path $runDirectory 'status.json'
$taskName = "PowerToys-InteractiveUiTest-$runId"
$originalDacl = $null
$registered = $false
try {
    New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $runDirectory | Out-Null
    $acl = Get-Acl -LiteralPath $runDirectory
    $originalDacl = $acl.GetSecurityDescriptorSddlForm([Security.AccessControl.AccessControlSections]::Access)
    $rule = [Security.AccessControl.FileSystemAccessRule]::new(
        $sid, 'Modify', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $acl.AddAccessRule($rule)
    Set-Acl -LiteralPath $runDirectory -AclObject $acl

    # Scheduled tasks inherit the desktop environment, not the pipeline agent's private runtime/tools.
    # Transfer only test configuration; never serialize the agent's credentials or complete environment.
    $environment = @{}
    foreach ($name in $environmentNames) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if ($null -ne $value) {
            $environment[$name] = $value
        }
    }
    @{
        RunId = $runId
        RunDirectory = $runDirectory
        TestExecutable = $TestExecutable
        ResultsDirectory = $runDirectory
        InteractiveUser = $InteractiveUser
        Filter = $Filter
        TimeoutMinutes = $TimeoutMinutes
        Environment = $environment
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $requestPath

    $powerShell = (Get-Process -Id $PID).Path
    $arguments = '-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -File "{0}" -RequestPath "{1}"' -f $PSCommandPath, $requestPath
    $action = New-ScheduledTaskAction -Execute $powerShell -Argument $arguments
    $principal = New-ScheduledTaskPrincipal -UserId $InteractiveUser -LogonType Interactive -RunLevel Limited
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes ($TimeoutMinutes + 1)) `
        -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
    $registered = $true
    Write-Host "Running $TestExecutable non-elevated as $InteractiveUser. Launcher evidence: $runDirectory"
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $launchObserved = $false
    $schedulerLaunchObserved = $false
    Start-ScheduledTask -TaskName $taskName
    while ($true) {
        Start-Sleep -Seconds 5
        $info = Get-ScheduledTaskInfo -TaskName $taskName
        $task = Get-ScheduledTask -TaskName $taskName
        $schedulerLaunchObserved = $schedulerLaunchObserved -or $task.State -eq 'Running' -or $info.LastRunTime.Year -ge 2000
        $launchObserved = $launchObserved -or $schedulerLaunchObserved -or
            (Test-Path -LiteralPath (Join-Path $runDirectory 'desktop.txt'))
        # A writable status file alone is not completion evidence. Queued tasks are not terminal.
        if ($task.State -in @('Ready', 'Disabled') -and $schedulerLaunchObserved) {
            if (-not (Test-Path -LiteralPath $statusPath)) {
                throw "Interactive task exited without status (result $($info.LastTaskResult)). Evidence: $runDirectory"
            }
            # Refresh the result after observing completion, not from a possibly running snapshot.
            $info = Get-ScheduledTaskInfo -TaskName $taskName
            break
        }
        if (-not $launchObserved -and $timer.Elapsed.TotalSeconds -gt 30) {
            throw "The test task never entered the interactive desktop. Evidence: $runDirectory"
        }
        if ($timer.Elapsed.TotalMinutes -gt ($TimeoutMinutes + 1)) {
            throw "Timed out waiting for the interactive test task. Evidence: $runDirectory"
        }
    }
    $status = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json -NoEnumerate
    if ($status.RunId -isnot [string] -or $status.RunId -ne $runId) {
        throw 'Interactive test status does not match the requested run.'
    }
    if ($status -isnot [System.Management.Automation.PSCustomObject] -or
        ($status.ExitCode -isnot [int] -and $status.ExitCode -isnot [long]) -or
        $status.ExitCode -lt [int]::MinValue -or $status.ExitCode -gt [int]::MaxValue -or
        'Error' -notin $status.PSObject.Properties.Name -or
        ($null -ne $status.Error -and $status.Error -isnot [string])) {
        throw 'Interactive test status has an invalid ExitCode or Error.'
    }
    if (($info.LastTaskResult -isnot [int] -and $info.LastTaskResult -isnot [long] -and
            $info.LastTaskResult -isnot [uint32]) -or
        $info.LastTaskResult -lt [int]::MinValue -or $info.LastTaskResult -gt [uint32]::MaxValue) {
        throw 'Interactive task has an invalid scheduler result.'
    }
    # This corroborates completion, not isolation from other processes sharing the worker's account.
    # Process.ExitCode is signed, while Task Scheduler may report the same Windows code unsigned.
    if (([long]$status.ExitCode -band [long][uint32]::MaxValue) -ne
        ([long]$info.LastTaskResult -band [long][uint32]::MaxValue)) {
        throw "Interactive task result $($info.LastTaskResult) does not match status exit code $($status.ExitCode)."
    }
    foreach ($name in @('desktop.txt', 'stdout.log', 'stderr.log')) {
        $path = Join-Path $runDirectory $name
        if (Test-Path -LiteralPath $path) {
            Get-Content -LiteralPath $path | Write-Host
        }
    }
    if ($status.Error) {
        throw "Interactive UI-test launch failed: $($status.Error)"
    }
    exit [int]$status.ExitCode
}
finally {
    try {
        if ($registered) {
            if ((Get-ScheduledTask -TaskName $taskName).State -in @('Running', 'Queued')) {
                Stop-ScheduledTask -TaskName $taskName
            }
            Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
        }
    }
    finally {
        if ($null -ne $originalDacl) {
            $acl = Get-Acl -LiteralPath $runDirectory
            $acl.SetSecurityDescriptorSddlForm($originalDacl, [Security.AccessControl.AccessControlSections]::Access)
            Set-Acl -LiteralPath $runDirectory -AclObject $acl
        }
    }
}
