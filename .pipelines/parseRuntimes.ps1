[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$path
)


$runtimeFile = Get-Content $path | ConvertFrom-Json;

$runtimes = @{}

Write-Host "Parsing .NET Runtimes from $path `r`n"

$runtimeList = (([array]$runtimeFile.targets.PSObject.Properties)[-1].Value.PSObject.Properties 
    | Where-Object { $_.Name -match "runtimepack" });

if ($runtimeList.Length -eq 0) {
    Write-Host -ForegroundColor Red "No runtimes have been detected"
    exit 1
}

# Enumerate through array of custom objects and parse the names of the property values into a HashTable
$runtimeList | ForEach-Object { 
    $runtimes += @{
        "$($_.Name -replace "runtimepack\.(\S+)\.\S+/\S+",'$1')" = $_.Value.PSObject.Properties.Value 
        | ForEach-Object {
            $_.PSObject.Properties.Name 
        }
    } 
} 

Write-Host "Runtimes Detected:`r`n"
$runtimes.Keys | ForEach-Object {
    Write-Host "$_ : $($runtimes.$_.Length) files `r`n"; 
}

exit 0