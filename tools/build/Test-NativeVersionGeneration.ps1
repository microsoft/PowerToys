# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$fixture = Join-Path ([IO.Path]::GetTempPath()) "PowerToysVersionTest-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path (Join-Path $fixture 'src') -Force | Out-Null
try {
    [xml]$source = Get-Content -LiteralPath (Join-Path $repo 'src\common\version\version.vcxproj') -Raw
    $target = @($source.Project.Target | Where-Object Name -eq 'GenerateVersionData')
    if ($target.Count -ne 1) { throw 'Cannot identify the actual native version-generation target.' }
    [xml]$project = '<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><PropertyGroup/><Target Name="PrepareForBuild"/></Project>'
    foreach ($entry in @{RepoRoot="$fixture\"; VersionChannel='private'; SourceCommit='initial-source'}.GetEnumerator()) {
        $property = $project.CreateElement($entry.Key,$project.DocumentElement.NamespaceURI)
        $property.InnerText = $entry.Value
        $project.DocumentElement.FirstChild.AppendChild($property) | Out-Null
    }
    $project.DocumentElement.AppendChild($project.ImportNode($target[0],$true)) | Out-Null
    $projectFile = Join-Path $fixture 'Version.proj'
    $project.Save($projectFile)
    '<Project/>' | Set-Content -LiteralPath (Join-Path $fixture 'src\Version.props')
    $header = Join-Path $fixture 'Generated Files\version_gen.h'
    function Generate([string]$Version,[string]$Commit='initial-source') {
        & dotnet msbuild $projectFile /t:GenerateVersionData "/p:Version=$Version" "/p:SourceCommit=$Commit" /nologo /v:quiet
        if ($LASTEXITCODE) { throw "Native version fixture failed: $LASTEXITCODE" }
    }
    Generate '1.2.3.4'
    $first = Get-Content -LiteralPath $header -Raw
    if ($first -notmatch '#define VERSION_REVISION 3' -or $first -notmatch '#define VERSION_BUILD 4') {
        throw 'Native version components did not reflect command-line properties.'
    }
    $stamp = (Get-Item -LiteralPath $header).LastWriteTimeUtc
    Generate '1.2.3.4'
    if ((Get-Item -LiteralPath $header).LastWriteTimeUtc -ne $stamp) {
        throw 'No-op native version generation changed the header timestamp.'
    }
    Generate '1.2.4.0' 'next-source'
    $changed = Get-Content -LiteralPath $header -Raw
    if ($changed -notmatch '#define VERSION_REVISION 4' -or $changed -notmatch '#define VERSION_BUILD 0' -or
        $changed -notmatch '#define VERSION_SOURCE_COMMIT L"next-source"') {
        throw 'Native version/source overrides were incorrectly skipped with unchanged Version.props.'
    }
    Write-Host 'PASS native version generation: CLI version/source overrides and stable no-op timestamps.'
}
finally {
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
