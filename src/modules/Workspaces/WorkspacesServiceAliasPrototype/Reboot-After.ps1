param([string]$OwnerSid)
& (Join-Path $PSScriptRoot 'Lifecycle.ps1') -Verb after-reboot -OwnerSid $OwnerSid
exit $LASTEXITCODE
