# Copyright (c) Microsoft Corporation. Licensed under the MIT license.
$ErrorActionPreference = 'Stop'
Write-Output 'Running PowerShell through MXC ProcessContainer'
Set-Content windows-result.txt 'Created by PowerShell in the run workspace' -NoNewline
if (Test-Path notes.txt) {
    Set-Content notes.txt 'Changed only in the copied workspace' -NoNewline
}
Get-ChildItem -Name
