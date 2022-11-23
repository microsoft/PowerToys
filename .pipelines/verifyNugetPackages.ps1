[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True,Position=1)]
    [string]$solution
)

Write-Host "Verifying Nuget packages for $solution"

dotnet tool restore
dotnet consolidate -s $solution

if (-not $?)
{
    Write-Host -ForegroundColor Red "Nuget packages needs to be consolidated"
    exit 1
}

exit 0
