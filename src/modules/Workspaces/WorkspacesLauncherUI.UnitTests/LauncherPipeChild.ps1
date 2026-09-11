# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

param([Parameter(Mandatory = $true)][string]$PipeName)

$ErrorActionPreference = 'Stop'
$deadline = [System.Threading.CancellationTokenSource]::new(30000)
$pipe = [System.IO.Pipes.NamedPipeServerStream]::new(
    $PipeName, [System.IO.Pipes.PipeDirection]::InOut, 1,
    [System.IO.Pipes.PipeTransmissionMode]::Byte, [System.IO.Pipes.PipeOptions]::Asynchronous)
try {
    [Console]::WriteLine('ready')
    [void]$pipe.WaitForConnectionAsync($deadline.Token).GetAwaiter().GetResult()
    [Console]::WriteLine('connected')
    while (-not $deadline.IsCancellationRequested) {
        $read = [Console]::In.ReadLineAsync()
        if (-not $read.Wait(30000)) { break }
        $command = $read.Result
        if ($command -eq 'exit' -or $null -eq $command) { break }
        if ($command -eq 'pipe-handle') {
            [Console]::WriteLine($pipe.SafePipeHandle.DangerousGetHandle().ToInt64().ToString([Globalization.CultureInfo]::InvariantCulture))
            continue
        }
        if ($command -eq 'read-ready') {
            $size = 4 + [Text.Encoding]::UTF8.GetByteCount('{"protocolVersion":1,"type":"ready"}')
            $frame = [byte[]]::new($size)
            $offset = 0
            while ($offset -lt $size) {
                $count = $pipe.ReadAsync($frame, $offset, $size - $offset, $deadline.Token).GetAwaiter().GetResult()
                if ($count -eq 0) { throw 'Unexpected test pipe disconnect' }
                $offset += $count
            }
            [Console]::WriteLine([Convert]::ToBase64String($frame))
            continue
        }
        if ($command -ne 'shutdown') { throw 'Unknown test command' }
        $body = [Text.Encoding]::UTF8.GetBytes('{"protocolVersion":1,"type":"shutdown"}')
        $frame = [BitConverter]::GetBytes([uint32]$body.Length) + $body
        [void]$pipe.WriteAsync($frame, 0, $frame.Length, $deadline.Token).GetAwaiter().GetResult()
        [Console]::WriteLine('sent')
    }
}
finally {
    $pipe.Dispose()
    $deadline.Dispose()
}
