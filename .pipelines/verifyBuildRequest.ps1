[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True,Position=1)]
    [string]$commit
)

$gitHubCommit = Invoke-RestMethod -Method Get "https://api.github.com/microsoft/PowerToys/$commit"
if(($githubCommit.files.filename -notmatch ".md").Length -eq 0)
{
    Write-Host '##vso[task.setvariable variable=skipBuild;isOutput=true]Yes'
    Write-Host 'Skipping Build'
}