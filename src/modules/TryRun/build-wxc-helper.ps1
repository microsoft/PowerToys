# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

param(
    [Parameter(Mandatory = $true)][string]$MxcRoot,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [Parameter(Mandatory = $true)][ValidateSet('x64', 'ARM64')][string]$Platform,
    [Parameter(Mandatory = $true)][ValidateSet('Debug', 'Release')][string]$Configuration
)

$ErrorActionPreference = 'Stop'
$target = if ($Platform -eq 'ARM64') { 'aarch64-pc-windows-msvc' } else { 'x86_64-pc-windows-msvc' }
$cargoArguments = @('build', '-p', 'wxc', '--features', 'wslc', '--target', $target, '--message-format=json')
if ($Configuration -eq 'Release') { $cargoArguments += '--release' }
Push-Location (Join-Path $MxcRoot 'src')
try {
    $artifacts = & cargo @cargoArguments
    if ($LASTEXITCODE -ne 0) { throw 'MXC image helper build failed.' }
    $executables = @($artifacts | ForEach-Object {
        $artifact = $_ | ConvertFrom-Json
        if ($artifact.reason -eq 'compiler-artifact' -and $artifact.target.name -eq 'wxc-exec' -and $artifact.executable) {
            $artifact.executable
        }
    })
    if ($executables.Count -ne 1) { throw 'Cargo did not report exactly one wxc-exec executable.' }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    Copy-Item -LiteralPath $executables[0] -Destination (Join-Path $OutputDirectory 'wxc-exec.exe') -Force
}
finally {
    Pop-Location
}
