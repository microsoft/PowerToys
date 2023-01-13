[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$depsjsonpath,
    [Parameter(Mandatory = $True, Position = 2)]
    [string]$depsfileslistspath,
    [Parameter(Mandatory = $True, Position = 3)]
    [string]$productwxspath
)

function Update-RuntimeFileList($runtimeToken, $runtimeKey) {
    $depsFilesLists -replace "($runtimeToken = )(.*);", "`$1 {`r`n$(($runtimes[$runtimeKey] | ForEach-Object {'    L"'+$_+'"'} | Sort-Object) -join ",`r`n") };"        
}

function Update-ProductWxsRuntimeFileList($runtimeToken, $runtimeKey) {
    $productWxs -replace "(define $runtimeToken=)(.*)?>", "`$1$($runtimes[$runtimeKey] -join ';')?>"
}

function Update-DotnetFilesComponentGuid()
{
    $productWxs -replace "Dlls_DotnetFiles_Component"" Guid=""([{]?[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}[}]?)""", "Dlls_DotnetFiles_Component"" Guid=""$((New-Guid).ToString().ToUpper())"""
}

# Read the DepsFilesLists.h file
$depsFilesLists = Get-Content $depsfileslistspath;

# Read Product.wxs file
$productWxs = Get-Content $productwxspath;

# Read the deps.json file and convert it to a JSON object
$runtimeFile = Get-Content $depsjsonpath | ConvertFrom-Json;

$runtimes = @{}

Write-Host "Parsing .NET Runtimes from $depsjsonpath `r`n"

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

Write-Host "Writing Microsoft.NETCore.App.Runtime files"
$depsFilesLists = Update-RuntimeFileList "dotnetRuntimeFiles" "Microsoft.NETCore.App.Runtime"
$productWxs = Update-ProductWxsRuntimeFileList "DotnetRuntimeFiles" "Microsoft.NETCore.App.Runtime"

Write-Host "Writing Microsoft.WindowsDesktop.App.Runtime files"
$depsFilesLists = Update-RuntimeFileList "dotnetRuntimeWPFFiles" "Microsoft.WindowsDesktop.App.Runtime"
$productWxs = Update-ProductWxsRuntimeFileList "DotnetRuntimeWPFFiles" "Microsoft.WindowsDesktop.App.Runtime"

Write-Host "Update DotnetFiles Component GUID"
$productWxs = Update-DotnetFilesComponentGuid

Write-Host "Updating $depsfileslistspath"
Set-Content -Path $depsfileslistspath -Value $depsFilesLists

Write-Host "Updating $productwxspath"
Set-Content -Path $productwxspath -Value $productWxs


