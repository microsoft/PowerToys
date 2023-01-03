[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$depsjsonpath,
    [Paramter(Mandatory = $True, Position = 2)]
    [string]$depsfileslistspath
)

function Update-RuntimeFileList($runtimeToken, $runtimeKey) {
   $depsFilesLists -replace "($runtimeToken = )(.*);", "`$1 {`r`n$(($runtimes[$runtimeKey] | ForEach-Object {'    L"'+$_+'"'} | Sort-Object) -join ",`r`n") };"    
    
    }

# Read the DepsFilesLists.h file
$depsFilesLists = Get-Content $depsfileslistspath;

$runtimeFile = Get-Content $depsjsonpath | ConvertFrom-Json;

$runtimes = @{}

Write-Host "Parsing .NET Runtimes from $path `r`n"

$runtimeList = ([array]$runtimeFile.targets.PSObject.Properties)[-1].Value.PSObject.Properties | Where-Object { $_.Name -match "runtimepack" };

if ($runtimeList.Length -eq 0) {
    Write-Host -ForegroundColor Red "No runtimes have been detected"
    exit 1
}

# Enumerate through array of custom objects and parse the names of the property values into a HashTable
$runtimeList | ForEach-Object { 
    $runtimes += @{"$($_.Name -replace "runtimepack\.(\S+)\.\S+/\S+",'$1')" = $_.Value.PSObject.Properties.Value | ForEach-Object {
            $_.PSObject.Properties.Name 
        }
    } 
} 
Write-Host "Updating DepsFilesLists.h"

Write-Host "Writing Microsoft.NETCore.App.Runtime files"
$depsFilesLists = Update-RuntimeFileList "dotnetRuntimeFiles" "Microsoft.NETCore.App.Runtime"

Write-Host "Writing Microsoft.WindowsDesktop.App.Runtime files"
$depsFilesLists = Update-RuntimeFileList "dotnetRuntimeWPFFiles" "Microsoft.WindowsDesktop.App.Runtime"

Write-Host "Updating $depsfileslistspath"
Set-Content $depsjsonpath


