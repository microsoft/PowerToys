# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

$scriptPath = Join-Path $PSScriptRoot '..\stopPowerToysProcesses.ps1'

Describe 'stopPowerToysProcesses' {
    It 'succeeds when the requested process is absent' {
        $missingName = "PowerToysSigningMissing$([Guid]::NewGuid().ToString('N'))"

        {
            & $scriptPath `
                -ProcessName $missingName `
                -TimeoutSeconds 5 `
                -RequiredStableSamples 2 `
                -PollIntervalMilliseconds 10
        } | Should Not Throw
    }

    It 'terminates the requested process tree and waits for stable absence' {
        $helperPath = Join-Path $TestDrive 'PowerToysSigningTest.exe'
        Copy-Item (Join-Path $env:WINDIR 'System32\timeout.exe') $helperPath
        $process = Start-Process `
            -FilePath $helperPath `
            -ArgumentList '/T', '30', '/NOBREAK' `
            -WindowStyle Hidden `
            -PassThru
        try
        {
            & $scriptPath `
                -ProcessName 'PowerToysSigningTest' `
                -TimeoutSeconds 10 `
                -RequiredStableSamples 2 `
                -PollIntervalMilliseconds 50 `
                -ProcessExitTimeoutSeconds 2

            $process.Refresh()
            $process.HasExited | Should Be $true
        }
        finally
        {
            if (-not $process.HasExited)
            {
                $process.Kill()
                $process.WaitForExit(2000)
            }

            $process.Dispose()
        }
    }

    It 'reports stability progress when the timeout expires without a final process' {
        $missingName = "PowerToysSigningMissing$([Guid]::NewGuid().ToString('N'))"
        $message = $null

        try
        {
            & $scriptPath `
                -ProcessName $missingName `
                -TimeoutSeconds 1 `
                -RequiredStableSamples 20 `
                -PollIntervalMilliseconds 500
        }
        catch
        {
            $message = $_.Exception.Message
        }

        $message | Should Match 'No process was present in the final sample'
        $message | Should Match 'stable for only [0-9]+/20 samples'
    }
}
