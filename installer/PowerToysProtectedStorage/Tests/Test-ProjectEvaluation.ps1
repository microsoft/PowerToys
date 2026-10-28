# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$scratch = Join-Path $PSScriptRoot ("Evaluation-" + [guid]::NewGuid().ToString('N'))
$defaultVersion = ([xml](Get-Content "$repo\src\Version.props" -Raw)).Project.PropertyGroup.Version
try {
    New-Item -ItemType Directory -Path $scratch | Out-Null
    foreach ($props in @(
        "$repo\src\common\ProtectedStorage\ProtectedStorage.Native.props",
        "$repo\src\common\ProtectedStorage\ProtectedStorage.Setup\Installer.props"
    )) {
        $project = Join-Path $scratch 'RestoreEvaluation.proj'
        $escaped = [Security.SecurityElement]::Escape($props)
        # No SDK/VC/Directory.Build.props import, matching dotnet's native restore evaluation.
        [IO.File]::WriteAllText($project, "<Project><Import Project=`"$escaped`" /></Project>")
        foreach ($override in @('', '0.101.3000.0')) {
            $arguments = @('msbuild', $project, '/nologo',
                '/getProperty:Version,ProtectedStorageVersionMajor,ProtectedStorageVersionMinor,ProtectedStorageVersionBuild,ProtectedStorageVersionRevision')
            if ($override) { $arguments += "/p:Version=$override" }
            $result = & dotnet @arguments
            if ($LASTEXITCODE) { throw "Native props restore evaluation failed: $props" }
            $properties = ($result | ConvertFrom-Json).Properties
            $expected = if ($override) { $override } else { $defaultVersion }
            if ($properties.Version -cne $expected) { throw 'Version source or explicit override was not preserved.' }
            $parts = $expected.Split('.')
            if ($properties.ProtectedStorageVersionMajor -cne $parts[0] -or
                $properties.ProtectedStorageVersionMinor -cne $parts[1] -or
                $properties.ProtectedStorageVersionBuild -cne $parts[2] -or
                $properties.ProtectedStorageVersionRevision -cne '0') {
                throw 'Restore-safe native PE version components do not match the release.'
            }
        }
    }
    Write-Host 'PASS native props evaluation without VC imports: repository version and four-part pipeline override.'
} finally {
    if (Test-Path -LiteralPath $scratch) { Remove-Item -LiteralPath $scratch -Recurse -Force }
}
