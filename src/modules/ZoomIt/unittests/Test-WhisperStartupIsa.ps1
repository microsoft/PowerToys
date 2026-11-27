<#
.SYNOPSIS
Checks the x64 ZoomIt Whisper objects for the pre-main AVX startup regression.

.DESCRIPTION
Run after building ZoomIt, from a Visual Studio developer PowerShell.
Inspects the whole whisper.obj, including constructors and STL instantiations,
and rejects CRT initializers in the AVX2 ggml objects. This is not a substitute
for launching ZoomIt and testing SAPI dictation on a CPU without AVX.
#>
[CmdletBinding()]
param(
    [string]$ObjectDirectory = "$PSScriptRoot\..\ZoomIt\x64\Release",
    [string]$Dumpbin = 'dumpbin.exe'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Read-ObjectDump {
    param([string]$ObjectPath, [string]$Option)

    $resolved = (Resolve-Path -LiteralPath $ObjectPath).Path
    $output = @(& $Dumpbin /nologo $Option $resolved 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "dumpbin failed for ${resolved}: $($output -join [Environment]::NewLine)"
    }
    return $output
}

$whisper = Join-Path $ObjectDirectory 'whisper.obj'
$headers = (Read-ObjectDump $whisper '/headers') -join "`n"
if ($headers -notmatch '8664 machine \(x64\)' -or $headers -notmatch '\.CRT\$XCU') {
    throw 'Expected a native x64 whisper.obj with CRT initializers (not an LTCG object).'
}

$instructions = @(Read-ObjectDump $whisper '/disasm:nobytes' |
    Where-Object { $_ -match '^\s+[0-9A-F]+:\s+\S+' })
if ($instructions.Count -eq 0) {
    throw 'No instructions decoded from whisper.obj.'
}

# AVX instructions use v-prefixed mnemonics, including VEX-encoded XMM operations.
$unsupported = @($instructions | Where-Object {
    $_ -match '^\s+[0-9A-F]+:\s+(v\S+|popcnt|lzcnt|tzcnt|andn|bextr|blsi|blsmsk|blsr|bzhi|mulx|pdep|pext|rorx|sarx|shlx|shrx|crc32|pcmpestr[im]|pcmpistr[im]|pcmpgtq)(\s|$)'
})
if ($unsupported.Count -ne 0) {
    throw "Unsupported instructions in whisper.obj:`n$($unsupported | Select-Object -First 10 | Out-String)"
}

foreach ($name in @('ggml', 'ggml-alloc', 'ggml-backend', 'ggml-quants')) {
    $object = Join-Path $ObjectDirectory "$name.obj"
    $headers = (Read-ObjectDump $object '/headers') -join "`n"
    if ($headers -notmatch '8664 machine \(x64\)' -or $headers -notmatch 'SECTION HEADER') {
        throw "Expected a native x64 $name.obj (not an LTCG object)."
    }
    if ($headers -match '\.CRT\$') {
        throw "$name.obj has CRT initialization/termination entries that could run outside the AVX2 guard."
    }
}

Write-Host "PASS: $($instructions.Count) Whisper instructions checked; no AVX, SSE4.2, POPCNT or BMI instructions."
Write-Host 'PASS: all four AVX2 ggml objects are native x64 objects without CRT initializers.'
