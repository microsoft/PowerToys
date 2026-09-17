# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $ProductRoot,
    [Parameter(Mandatory)][string] $TestUser,
    [Parameter(Mandatory)][guid] $RunId,
    [Parameter(Mandatory)][string] $GuestArchivePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Only the protected, hashed sibling initializer is executable here. Keep its output
# outside the public test results, including parser and parameter-binding failures.
$setupPath = Join-Path $PSScriptRoot 'Initialize-AutonomousHost.ps1'
$logRoot = Join-Path $PSScriptRoot 'provisioning-logs'
$arguments = '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "{0}" -ProductRoot "{1}" -TestUser "{2}" -RunId "{3}" -NetworkTimeoutSeconds 900 -GuestArchivePath "{4}"' -f
    $setupPath, $ProductRoot, $TestUser, $RunId, $GuestArchivePath

# Start-Process drains both redirected streams concurrently to files rather than
# accumulating output in memory or waiting on one pipe while the other fills.
$process = Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList $arguments `
    -RedirectStandardOutput (Join-Path $logRoot 'stdout.log') `
    -RedirectStandardError (Join-Path $logRoot 'stderr.log') -NoNewWindow -Wait -PassThru
exit $process.ExitCode
