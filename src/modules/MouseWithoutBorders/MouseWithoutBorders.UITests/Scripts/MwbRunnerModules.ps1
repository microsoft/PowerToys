# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Get-MwbRunnerModules {
    param([string]$SourcePath)
    $ErrorActionPreference = 'Stop'
    $source = Get-Item -LiteralPath $SourcePath -Force
    if ($source.PSIsContainer -or $source.PSProvider.Name -ne 'FileSystem' -or
        ($source.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Runner module provenance requires a plain source file.'
    }
    $text = Get-Content -LiteralPath $source.FullName -Raw
    $lists = [regex]::Matches($text,
        '(?s)ModuleRegistration\s+knownModules\s*\[\s*\]\s*=\s*\{(?<modules>.*?)\}\s*;')
    if ($lists.Count -ne 1) { throw 'Expected exactly one explicit Runner module-load list.' }
    $block = $lists[0].Groups['modules'].Value
    # Each entry is { L"key", L"path", DefaultEnabled::Value[, gpoCallback] }.
    $entryPattern = '\{\s*L"(?<key>[^"\r\n]+)"\s*,\s*L"(?<path>[^"\r\n]+)"\s*,\s*DefaultEnabled::\w+\s*(?:,\s*powertoys_gpo::\w+\s*)?\}'
    $moduleMatches = [regex]::Matches($block, $entryPattern)
    $remainder = [regex]::Replace($block, $entryPattern, '')
    if (-not $moduleMatches.Count -or $remainder -notmatch '^[\s,]*$') {
        throw 'The Runner module-load list has an unsupported or empty declaration.'
    }
    $paths = @(
        foreach ($match in $moduleMatches) {
            $path = $match.Groups['path'].Value.Replace('\\', '\').Replace('/', '\')
            if ($path -cnotmatch '^(?:WinUI3Apps\\)?PowerToys\.[A-Za-z0-9.]+\.dll$') {
                throw 'The Runner module-load list contains an unconfined or unsupported path.'
            }
            $path
        }
    )
    if (@($paths | Sort-Object -Unique).Count -ne $paths.Count) {
        throw 'The Runner module-load list contains duplicate libraries.'
    }
    [pscustomobject]@{
        SourceSha256 = (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash
        Modules = $paths
    }
}
