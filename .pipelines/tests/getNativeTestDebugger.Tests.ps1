# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

. (Join-Path $PSScriptRoot '..\getNativeTestDebugger.ps1') -OutputDirectory $PSScriptRoot

function Test-DebuggerPreparationThrows {
    param([scriptblock]$Operation)
    # Pester 3's Should Throw does not recognize PowerShell 7 exceptions.
    try { & $Operation | Out-Null; return $false } catch { return $true }
}

Describe 'Pinned native debugger package' {
    It 'pins an HTTPS Microsoft-hosted archive and its payload identity' {
        ([uri]$uri).Scheme | Should Be 'https'
        ([uri]$uri).Host | Should Be 'windbg.download.prss.microsoft.com'
        $expectedHash.Length | Should Be 64
        ($offset -gt 0) | Should Be $true
        ($length -gt 0 -and $length -lt 500MB) | Should Be $true
    }

    It 'rejects a modified or incomplete archive before extracting or executing anything' {
        $path = Join-Path $TestDrive 'invalid.msix'
        'not the pinned archive' | Set-Content -LiteralPath $path
        (Test-DebuggerPreparationThrows { Assert-NativeDebuggerArchive $path }) | Should Be $true
    }

    It 'confines extraction to the fresh output directory' {
        $root = [IO.Path]::GetFullPath((Join-Path $TestDrive 'debugger'))
        (Get-NativeDebuggerEntryPath $root 'amd64/cdb.exe') | Should Be (Join-Path $root 'amd64\cdb.exe')
        (Test-DebuggerPreparationThrows { Get-NativeDebuggerEntryPath $root '../outside.exe' }) | Should Be $true
        (Test-DebuggerPreparationThrows { Get-NativeDebuggerEntryPath $root 'amd64/../../outside.exe' }) | Should Be $true
    }

    It 'refuses relative or preexisting output directories without downloading' {
        (Test-DebuggerPreparationThrows { Get-NativeTestDebugger 'relative' }) | Should Be $true
        (Test-DebuggerPreparationThrows { Get-NativeTestDebugger $TestDrive }) | Should Be $true
    }
}
