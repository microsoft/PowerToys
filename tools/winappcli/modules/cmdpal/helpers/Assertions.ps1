#Requires -Version 7.0
# Assertions.ps1 — back-compat shim. The canonical assertion vocabulary lives at
#   tools/winappcli/modules/_shared/Assertions.ps1
# and is shared across CmdPal + non-CmdPal module checklists.
#
# This file is dot-sourced from cmdpal/_helpers.ps1; keeping it as a thin
# shim avoids touching every test file that references the cmdpal-path
# directly while ensuring there is exactly one source of truth.
$shared = Join-Path $PSScriptRoot '..\..\_shared\Assertions.ps1'
$shared = [System.IO.Path]::GetFullPath($shared)
if (-not (Test-Path $shared)) {
    throw "Assertions.ps1 shim cannot find canonical _shared/Assertions.ps1 at '$shared'"
}
. $shared