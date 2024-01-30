[CmdLetBinding()]
Param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$SearchFolder,
    [Parameter(Mandatory=$true, Position=1)]
    [string]$VsConsolePath,
    [Parameter(Mandatory=$true, Position=2)]
    [string]$MatchPattern,
    [Parameter(Mandatory=$true, Position=3)]
    [string]$LogPath,
    [Parameter(Mandatory=$true, Position=4)]
    [string]$ResultPath
)

Write-Output "Starting UI tests"

$Command = "$VsConsolePath\vstest.console.exe $SearchFolder /ResultsDirectory:$ResultPath"
Invoke-Expression $Command