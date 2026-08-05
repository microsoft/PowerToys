param([string]$OwnerSid)
& (Join-Path $PSScriptRoot 'Lifecycle.ps1') -Verb before-reboot -OwnerSid $OwnerSid
exit $LASTEXITCODE
