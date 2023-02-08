[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$runtimedepsjsonpath,
    [Parameter(Mandatory = $True, Position = 2)]
    [string]$wpfdepsjsonpath,
    [Parameter(Mandatory = $True, Position = 3)]
    [string]$depsfileslistspath,
    [Parameter(Mandatory = $True, Position = 4)]
    [string]$productwxspath
)

function Get-RuntimePack ($depsJsonFile, $runtimeName) {
    Write-Host "Parsing $runtimeName Runtime"
    $runtimePackList = ([array]$depsJsonFile.targets.PSObject.Properties)[-1].Value.PSObject.Properties | Where-Object { $_.Name -match "runtimepack.$runtimeName" };
    
    if ($runtimePackList.Length -eq 0) {
        Write-Host -ForegroundColor Red "$runtimeName has not been found"
        exit 1
    }

    # Enumerate through array of custom objects and parse the names of the property values into a HashTable
    $runtimePackList | ForEach-Object { 
        $runtimes += @{"$($_.Name -replace "runtimepack\.(\S+)\.\S+/\S+",'$1')" = $_.Value.PSObject.Properties.Value | ForEach-Object {
                $_.PSObject.Properties.Name 
            }
        } 
    }
    Write-Output $runtimes;
}

function Update-RuntimeHashTable () {
    $runtimes = Get-RuntimePack $runtimeFile "Microsoft.NETCore.App.Runtime"
    $runtimes = Get-RuntimePack $wpfRuntimeFile "Microsoft.WindowsDesktop.App.Runtime"

    # Find the dlls that exist in both the .NET Runtime and WPF Runtime deps list and filter out of WPF
    $runtimeFileComparison = Compare-Object -ReferenceObject $runtimes["Microsoft.NETCore.App.Runtime"] -DifferenceObject $runtimes["Microsoft.WindowsDesktop.App.Runtime"] -IncludeEqual -ExcludeDifferent

    $runtimes["Microsoft.WindowsDesktop.App.Runtime"] = $runtimes["Microsoft.WindowsDesktop.App.Runtime"] | Where-Object { $_ -notin $runtimeFileComparison.InputObject }

    Write-Output $runtimes;
}

function Update-RuntimeFileList($runtimeToken, $runtimeKey) {
    $depsFilesLists -replace "($runtimeToken = )(.*);", "`$1 {`r`n$(($runtimes[$runtimeKey] | ForEach-Object {'    L"'+$_+'"'} | Sort-Object) -join ",`r`n") };"        
}

function Update-ProductWxsRuntimeFileList($runtimeToken, $runtimeKey) {
    $productWxs -replace "(define $runtimeToken=)(.*)?>", "`$1$($runtimes[$runtimeKey] -join ';')?>"
}

function Update-DotnetFilesComponentGuid() {
    $productWxs -replace "Dlls_DotnetFiles_Component"" Guid=""([{]?[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}[}]?)""", "Dlls_DotnetFiles_Component"" Guid=""$((New-Guid).ToString().ToUpper())"""
}

# Read the DepsFilesLists.h file
$depsFilesLists = Get-Content $depsfileslistspath;

# Read Product.wxs file
$productWxs = Get-Content $productwxspath;

# Read the deps.json file and convert it to a JSON object
$runtimeFile = Get-Content $runtimedepsjsonpath | ConvertFrom-Json;
$wpfRuntimeFile = Get-Content $wpfdepsjsonpath | ConvertFrom-Json;

$runtimes = @{}

$runtimes = Update-RuntimeHashTable

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