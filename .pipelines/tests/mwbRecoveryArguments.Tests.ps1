# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

$payload = Join-Path $PSScriptRoot '..\..\src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests\Payload'
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $payload 'EndpointSupport.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'Endpoint support contains parse errors.' }
$function = $ast.Find({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'ConvertTo-EndpointArgument'
}, $true)
. ([scriptblock]::Create($function.Extent.Text))

Describe 'Recovery child Windows argument quoting' {
    It 'round-trips trailing separators and spaces through an actual Windows PowerShell child' {
        $receiver = Join-Path $TestDrive 'receive arguments.ps1'
        'param([string]$ProductRoot, [string]$Next); @($ProductRoot, $Next) | ConvertTo-Json -Compress' |
            Set-Content -LiteralPath $receiver
        foreach ($path in @('C:\', 'C:\PT\', 'C:\Product folder\', '\\server\share\')) {
            $arguments = @('-NoLogo', '-NoProfile', '-NonInteractive', '-File', $receiver,
                '-ProductRoot', $path, '-Next', 'next argument')
            $start = [Diagnostics.ProcessStartInfo]::new()
            $start.FileName = "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"
            $start.UseShellExecute = $false
            $start.CreateNoWindow = $true
            $start.RedirectStandardOutput = $true
            $start.Arguments = (@($arguments | ForEach-Object { ConvertTo-EndpointArgument $_ })) -join ' '
            $process = [Diagnostics.Process]::Start($start)
            try {
                $output = $process.StandardOutput.ReadToEndAsync()
                if (-not $process.WaitForExit(10000)) { throw 'Argument receiver timed out.' }
                $process.ExitCode | Should Be 0
                $values = $output.GetAwaiter().GetResult() | ConvertFrom-Json
                $values[0] | Should Be $path
                $values[1] | Should Be 'next argument'
            }
            finally {
                if (-not $process.HasExited) { $process.Kill() }
                $process.Dispose()
            }
        }
    }
}
