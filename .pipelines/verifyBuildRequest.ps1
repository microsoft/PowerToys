[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True,Position=1)]
    [string]$commit
)

$gitHubCommit = Invoke-RestMethod -Method Get "https://api.github.com/repos/microsoft/PowerToys/commits/$commit"
# If there are no files updated in the commit that are .md, set skipBuild variable
if(([array]($githubCommit.files.filename) -notmatch ".md").Length -eq 0)
{
    Write-Host '##vso[task.setvariable variable=skipBuild;isOutput=true]Yes'
    Write-Host 'Skipping Build'
}