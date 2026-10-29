# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

function Assert-PreviewReleaseBodyLayout {
    param([Parameter(Mandatory)][string]$Body)

    $beginMarker = "<!-- BEGIN POWERTOYS PREVIEW AGENT -->"
    $endMarker = "<!-- END POWERTOYS PREVIEW AGENT -->"
    $start = $Body.IndexOf($beginMarker, [StringComparison]::Ordinal)
    $end = $Body.IndexOf($endMarker, [StringComparison]::Ordinal)
    if ($start -ge 0 -and $end -gt $start) {
        $Body = $Body.Substring($start + $beginMarker.Length, $end - $start - $beginMarker.Length)
    }

    if ($Body -match '(?m)^ {0,3}#(?:[ \t]+|$)' -or
        $Body -match '(?m)^[^\r\n]+\r?\n {0,3}=+[ \t]*\r?$') {
        throw "Preview release body must not contain a level-one title heading. Keep the release name in the GitHub title field; start the body with the introduction and use level-two section headings."
    }
}
