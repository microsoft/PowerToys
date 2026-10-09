[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True,Position=1)]
    [string]$solution
)

Write-Host "Verifying Nuget packages for $solution"

dotnet tool restore
dotnet consolidate -s $solution
if ($lastExitCode -ne 0)
{
    $result = $lastExitCode
    Write-Error "Error running dotnet consolidate, with the exit code $lastExitCode. Please verify logs and running environment."
    exit $result
}

if (-not $?)
{
    Write-Host -ForegroundColor Red "Nuget packages with the same name must all be the same version."
    exit 1
}

# Ignore NU1503 on vcxproj files 
dotnet restore $solution /nowarn:NU1503 
if ($lastExitCode -ne 0)
{
    $result = $lastExitCode
    Write-Error "Error running dotnet restore, with the exit code $lastExitCode. Please verify logs on the nuget package versions."
    exit $result
}

exit 0
