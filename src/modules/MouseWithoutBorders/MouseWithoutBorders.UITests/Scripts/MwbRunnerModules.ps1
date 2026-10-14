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
        '(?s)std::vector<std::wstring_view>\s+knownModules\s*=\s*\{(?<modules>.*?)\};')
    if ($lists.Count -ne 1) { throw 'Expected exactly one explicit Runner module-load list.' }
    $block = $lists[0].Groups['modules'].Value
    $moduleMatches = [regex]::Matches($block, 'L"(?<path>[^"\r\n]+)"')
    $remainder = [regex]::Replace($block, 'L"[^"\r\n]+"', '')
    if (-not $moduleMatches.Count -or $remainder -notmatch '^[\s,]*$') {
        throw 'The Runner module-load list has an unsupported or empty declaration.'
    }
    $paths = @(
        foreach ($match in $moduleMatches) {
            $path = $match.Groups['path'].Value.Replace('/', '\')
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
