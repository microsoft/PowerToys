[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True,Position=1)]
    [string]$solution,

    [Parameter(Mandatory=$True,Position=2)]
    [string]$tempDir
)

Write-Host "Verifying Nuget packages for $solution"

Set-Location $tempDir

dotnet tool install dotnet-consolidate --tool-path $tempDir
./dotnet-consolidate.exe -s $solution

if (-not $?)
{
    Write-Host -ForegroundColor Red "Nuget packages needs to be consolidated"
    exit 1
}

exit 0
