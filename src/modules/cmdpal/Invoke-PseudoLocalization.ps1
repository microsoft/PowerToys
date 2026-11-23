#requires -Version 7.0

<#
.SYNOPSIS
Generates pseudo-localized copies of English resources for local testing.
.DESCRIPTION
Accepts a neutral RESX file, an en-US RESW file, or a directory to scan recursively.
Directory scans skip bin and obj. Source files are preserved; output uses a culture
suffix for RESX files and a sibling culture directory for RESW files.
The defaults are readable diacritics and the qps-ploc culture.
.EXAMPLE
.\Invoke-PseudoLocalization.ps1 -Path .\Microsoft.CmdPal.UI
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [string]$Path,

    [Parameter()]
    [ValidateSet('brackets', 'diacritics', 'xs')]
    [string]$Mode = 'diacritics',

    [Parameter()]
    [string]$Culture = 'qps-ploc'
)

$scriptPath = Join-Path $PSScriptRoot 'pseudolocalizer.ps1'
. $scriptPath

exit (Invoke-PseudoLocalization -Path $Path -Mode $Mode -Culture $Culture)
