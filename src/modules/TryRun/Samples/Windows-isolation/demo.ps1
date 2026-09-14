# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

$ErrorActionPreference = 'Stop'
$observations = @()
Write-Output 'Windows isolation demo: reading the copied input.'
Get-Content -LiteralPath notes.txt
Set-Content -LiteralPath notes.txt -Value 'Changed inside the Windows run copy.' -NoNewline
Set-Content -LiteralPath windows-result.txt -Value 'Created inside the Windows run workspace.' -NoNewline
$observations += @{ resource = 'notes.txt and windows-result.txt'; access = 'write'; outcome = 'succeeded'; detail = 'The script modified a copied input and created a result beside it.' }

if ($env:TRYRUN_HOST_PROBE) {
    try {
        Get-Content -LiteralPath $env:TRYRUN_HOST_PROBE -ErrorAction Stop | Out-Null
        $observations += @{ resource = 'Harmless host-only fixture'; access = 'read'; outcome = 'unexpectedly readable'; detail = 'The script could read the ungranted fixture. Inspect the configured policy and native denial report.' }
        Write-Output 'UNEXPECTED: the host-only fixture was readable.'
    }
    catch {
        $observations += @{ resource = 'Harmless host-only fixture'; access = 'read'; outcome = 'blocked or unavailable'; detail = $_.Exception.Message }
        Write-Output 'The host-only fixture could not be read. The host control and MXC capture provide separate evidence.'
    }
}
else {
    $observations += @{ resource = 'Harmless host-only fixture'; access = 'read'; outcome = 'not tested'; detail = 'Use the Windows isolation demo button to supply the host-side control fixture.' }
}

@{ version = 1; observations = $observations } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath tryrun-observations.json -Encoding UTF8
Write-Output 'Done. Compare the file changes, inspect the isolation report, and export selected results.'
