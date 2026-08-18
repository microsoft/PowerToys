[CmdletBinding()]
param(
    [ValidateSet('bootstrap', 'provision-two', 'status', 'upgrade', 'cleanup')]
    [string]$Verb,
    [string]$FirstOwnerSid = 'S-1-5-21-1959867211-618815089-525172305-1122',
    [string]$SecondOwnerSid = 'S-1-5-21-1959867211-618815089-525172305-1123',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$controller = Join-Path $PSScriptRoot "artifacts\bin\x64\$Configuration\PtLsmrController.exe"
if (-not (Test-Path $controller)) { throw "Controller is missing: $controller" }

switch ($Verb) {
    'bootstrap' {
        & $controller --bootstrap-install
    }
    'provision-two' {
        $failedOwners = @()
        foreach ($owner in $FirstOwnerSid, $SecondOwnerSid) {
            & $controller --provision-v1 --owner-sid $owner
            if ($LASTEXITCODE -ne 0) {
                $failedOwners += "${owner} (exit $LASTEXITCODE)"
            }
        }
        if ($failedOwners.Count -ne 0) {
            throw "Provision failed for: $($failedOwners -join '; ')"
        }
    }
    'status' {
        & $controller --status --owner-sid $FirstOwnerSid
        & $controller --status --owner-sid $SecondOwnerSid
    }
    'upgrade' {
        & $controller --upgrade-v2
    }
    'cleanup' {
        $failedOwners = @()
        foreach ($owner in $FirstOwnerSid, $SecondOwnerSid) {
            & $controller --cleanup --owner-sid $owner
            if ($LASTEXITCODE -ne 0) {
                $failedOwners += "${owner} (exit $LASTEXITCODE)"
            }
        }
        if ($failedOwners.Count -ne 0) {
            throw "Cleanup failed for: $($failedOwners -join '; ')"
        }
    }
}
if ($LASTEXITCODE -ne 0) { throw "$Verb failed: $LASTEXITCODE" }
