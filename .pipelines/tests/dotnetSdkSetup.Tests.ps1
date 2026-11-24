# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

function curl.exe { throw 'The installer download must be mocked.' }

Describe '.NET SDK bootstrap download status' {
    BeforeEach {
        $script:originalExitCode = $global:LASTEXITCODE
        $script:installer = Join-Path $TestDrive 'dotnet-install.ps1'
        $script:executed = Join-Path $TestDrive 'installer-executed.txt'
        Remove-Item -LiteralPath $script:executed -Force -ErrorAction Ignore
        "param(`$InstallDir, `$JsonFile, `$Channel)`nSet-Content -LiteralPath '$script:executed' -Value installed" |
            Set-Content -LiteralPath $script:installer
        $template = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\v2\templates\steps-ensure-dotnet-version.yml') -Raw
        $match = [regex]::Match($template, '(?m)^- pwsh: \|-\r?\n(?<body>(?:^ {4}[^\r\n]*(?:\r?\n|$))+)')
        $match.Success | Should Be $true
        $body = [regex]::Replace($match.Groups['body'].Value, '(?m)^ {4}', '').
            Replace('$(Agent.ToolsDirectory)', $TestDrive).
            Replace('${{parameters.version}}', ' ')
        $script:bootstrap = [scriptblock]::Create($body)
        Mock curl.exe { $global:LASTEXITCODE = $script:downloadExitCode }
    }

    AfterEach {
        $global:LASTEXITCODE = $script:originalExitCode
    }

    It 'executes an existing installer only after download exit <ExitCode>' -TestCases @(
        @{ ExitCode = 0 }
        @{ ExitCode = 35 }
    ) {
        param($ExitCode)
        $script:downloadExitCode = $ExitCode
        $originalPreference = $ErrorActionPreference
        Push-Location $TestDrive
        try {
            $ErrorActionPreference = 'Stop'
            if ($ExitCode -eq 0) {
                & $script:bootstrap
            } else {
                { & $script:bootstrap } | Should Throw 'Failed to download dotnet-install.ps1'
            }

            (Test-Path -LiteralPath $script:executed) | Should Be ($ExitCode -eq 0)
        }
        finally {
            $ErrorActionPreference = $originalPreference
            Pop-Location
        }
    }
}
