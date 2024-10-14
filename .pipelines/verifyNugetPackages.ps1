[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True,Position=1)]
    [string]$solution
)

Write-Host "Verifying Nuget packages for $solution"

dotnet tool restore
if ($lastExitCode -ne 0)
{
    $result = $lastExitCode
    Write-Error 'Error running dotnet tool. Please verify the environment the script is running on.'
    exit $result
}

dotnet consolidate -s $solution

if (-not $?)
{
    Write-Host -ForegroundColor Red "Nuget packages with the same name must all be the same version."
    exit 1
}

exit 0
