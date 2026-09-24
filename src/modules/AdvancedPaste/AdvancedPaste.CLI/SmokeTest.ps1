# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

param(
    [string] $Executable = (Join-Path $PSScriptRoot '..\..\..\..\x64\Debug\WinUI3Apps\PowerToys.AdvancedPaste.Cli.exe')
)

$ErrorActionPreference = 'Stop'
$tempDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "AdvancedPasteCliSmoke-$([guid]::NewGuid().ToString('N'))"

function Assert-ExitCode([int] $Expected, [string] $Scenario) {
    if ($LASTEXITCODE -ne $Expected) {
        throw "$Scenario returned exit code $LASTEXITCODE; expected $Expected."
    }
}

try {
    New-Item -ItemType Directory -Path $tempDirectory | Out-Null
    $inputPath = Join-Path $tempDirectory 'input.html'
    $outputPath = Join-Path $tempDirectory 'output.md'
    [System.IO.File]::WriteAllText($inputPath, '<p>Hello <strong>world</strong></p>')

    & $Executable --help | Out-Null
    Assert-ExitCode 0 'Help'

    $json = "name,age`nAda,37" | & $Executable transform --format json --stdin
    Assert-ExitCode 0 'Standard input to standard output'
    if (($json | ConvertFrom-Json)[1][0] -ne 'Ada') {
        throw 'CSV input did not produce the expected JSON output.'
    }

    & $Executable transform --format markdown --input $inputPath --output $outputPath
    Assert-ExitCode 0 'File input to file output'
    if ([System.IO.File]::ReadAllText($outputPath) -notmatch 'Hello \*\*world\*\*') {
        throw 'HTML input did not produce the expected Markdown output.'
    }

    $envelope = 'hello' | & $Executable transform --format plain-text --stdin --json | ConvertFrom-Json
    Assert-ExitCode 0 'JSON success output'
    if ($envelope.status -ne 'success' -or $envelope.output.TrimEnd() -ne 'hello') {
        throw 'The JSON success envelope was not valid.'
    }

    & $Executable transform --format json --stdin --clipboard 2>$null
    Assert-ExitCode 2 'Conflicting input modes'

    'hello' | & $Executable transform --format ocr --stdin 2>$null
    Assert-ExitCode 2 'Unsupported format'

    Write-Host 'Advanced Paste CLI smoke tests passed.'
    exit 0
}
finally {
    if (Test-Path -LiteralPath $tempDirectory) {
        Remove-Item -LiteralPath $tempDirectory -Recurse -Force
    }
}
