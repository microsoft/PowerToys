# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

param(
    [string] $Executable = (Join-Path $PSScriptRoot '..\..\..\..\..\x64\Debug\WinUI3Apps\PowerToys.AdvancedPaste.Cli.exe')
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
    throw "Build the AdvancedPaste.CLI project first. Executable not found: $Executable"
}

$outputDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "AdvancedPasteCliE2E-$([guid]::NewGuid().ToString('N'))"

function Assert-ExitCode([int] $Expected, [string] $Scenario) {
    if ($LASTEXITCODE -ne $Expected) {
        throw "$Scenario returned exit code $LASTEXITCODE; expected $Expected."
    }
}

try {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null

    $helpText = & $Executable transform --help
    Assert-ExitCode 0 'Help'
    if (($helpText -join "`n") -notmatch '--format') {
        throw 'Transform help does not describe --format.'
    }

    $plainText = & $Executable transform --format plain-text --input (Join-Path $PSScriptRoot 'plain.txt')
    Assert-ExitCode 0 'Plain-text file to stdout'
    if (($plainText -join "`n") -notmatch 'This line stays plain text') {
        throw 'Plain-text output did not match the fixture.'
    }

    $markdownPath = Join-Path $outputDirectory 'article.md'
    & $Executable transform --format markdown --input (Join-Path $PSScriptRoot 'article.html') --output $markdownPath
    Assert-ExitCode 0 'HTML file to Markdown file'
    $markdown = [System.IO.File]::ReadAllText($markdownPath)
    if ($markdown -notmatch '\*\*PowerToys\*\*' -or $markdown -match 'doNotIncludeThis') {
        throw 'Markdown file is missing bold text or contains script content.'
    }

    $csv = & $Executable transform --format json --input (Join-Path $PSScriptRoot 'people.csv')
    Assert-ExitCode 0 'CSV file to JSON stdout'
    if (($csv | ConvertFrom-Json)[1][0] -ne 'Ada') {
        throw 'CSV conversion did not contain Ada in the first data row.'
    }

    $xmlPath = Join-Path $outputDirectory 'note.json'
    & $Executable transform --format json --input (Join-Path $PSScriptRoot 'note.xml') --output $xmlPath
    Assert-ExitCode 0 'XML file to JSON file'
    if ((Get-Content -LiteralPath $xmlPath -Raw | ConvertFrom-Json).note.owner -ne 'Ada') {
        throw 'XML conversion did not contain the expected owner.'
    }

    $ini = & $Executable transform --format json --input (Join-Path $PSScriptRoot 'config.ini')
    Assert-ExitCode 0 'INI file to JSON stdout'
    if (($ini | ConvertFrom-Json).general.name -ne 'PowerToys') {
        throw 'INI conversion did not contain the expected name.'
    }

    $originalJson = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'existing.json'))
    $passthrough = & $Executable transform --format json --input (Join-Path $PSScriptRoot 'existing.json')
    Assert-ExitCode 0 'JSON passthrough'
    if (($passthrough -join "`n").TrimEnd() -ne $originalJson.TrimEnd()) {
        throw 'Existing JSON was not preserved.'
    }

    $envelope = 'hello' | & $Executable transform --format plain-text --stdin --json
    Assert-ExitCode 0 'Standard input to machine-readable stdout'
    $result = $envelope | ConvertFrom-Json
    if ($result.status -ne 'success' -or $result.format -ne 'plain-text' -or $result.output.TrimEnd() -ne 'hello') {
        throw 'JSON success envelope did not contain the expected values.'
    }

    & $Executable transform --format plain-text --input (Join-Path $PSScriptRoot 'empty.txt') 2>$null | Out-Null
    Assert-ExitCode 1 'Empty input'

    & $Executable transform --format plain-text --input (Join-Path $outputDirectory 'missing.txt') 2>$null | Out-Null
    Assert-ExitCode 1 'Missing file'

    & $Executable transform --format json --stdin --clipboard 2>$null | Out-Null
    Assert-ExitCode 2 'Conflicting input modes'

    $errorPath = Join-Path $outputDirectory 'error.json'
    & $Executable transform --format json --stdin --clipboard --json 2> $errorPath | Out-Null
    Assert-ExitCode 2 'Machine-readable argument error'
    $errorEnvelope = Get-Content -LiteralPath $errorPath -Raw | ConvertFrom-Json
    if ($errorEnvelope.code -ne 'invalid_input_mode' -or -not $errorEnvelope.usage) {
        throw 'JSON argument error did not include its stable code and usage.'
    }

    'hello' | & $Executable transform --format ocr --stdin 2>$null | Out-Null
    Assert-ExitCode 2 'Unsupported format'

    Write-Host 'Advanced Paste CLI fixture E2E tests passed. No clipboard was accessed.'
    exit 0
}
finally {
    if (Test-Path -LiteralPath $outputDirectory) {
        Remove-Item -LiteralPath $outputDirectory -Recurse -Force
    }
}
