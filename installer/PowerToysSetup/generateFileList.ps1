[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [AllowEmptyString()]
    [string]$fileDepsJson,
    [Parameter(Mandatory = $True, Position = 2)]
    [string]$fileListName,
    [Parameter(Mandatory = $True, Position = 3)]
    [string]$wxsFilePath,
    # If there is no deps.json file, just pass path to files
    [Parameter(Mandatory = $False, Position = 4)]
    [string]$depsPath,
    # launcher plugins are being loaded into launcher process,
    # so there are some additional dependencies to skip
    [Parameter(Mandatory = $False, Position = 5)]
    [bool]$isLauncherPlugin,
    # Skip winAppSDK dlls as those are hard-linked
    [Parameter(Mandatory = $False, Position = 6)]
    [bool]$isWinAppSdkProj
)

$fileWxs = Get-Content $wxsFilePath;

#Skip PowerToysInterop files
$coreWxs = Get-Content $PSScriptRoot/"Core.wxs"
$coreWxs | ForEach-Object {
    if ($_ -match "(<?define PowerToysInteropFiles=)(.*)\?>") {
        [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', 'fileList',
        Justification = 'variable is used in another scope')]

        $interopFilesList = $matches[2] -split ';'
        return
    }
}

#Skip WinAppSdk files
if ($isWinAppSdkProj -eq $True) {
    $winAppSDKWxs = Get-Content $PSScriptRoot/"WinAppSDK.wxs"
    $winAppSDKWxs | ForEach-Object {
        if ($_ -match "(<?define WinAppSDKFiles=)(.*)\?>") {
            [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', 'fileList',
            Justification = 'variable is used in another scope')]

            $winAppSDKfilesList = $matches[2] -split ';'
            return
        }
    }
}

$fileExclusionList = @("*Test*", "*.pdb", "*.lastcodeanalysissucceeded", "createdump.exe") + $interopFilesList + $winAppSDKfilesList

$fileInclusionList = @("*.dll", "*.exe", "*.json", "*.msix", "*png", "*gif", "*ico", "*cur", "*svg", "index.html", "reg.js", "monacoSpecialLanguages.js", "resources.pri", "NLog.config")

$dllsToIgnore = @("System.CodeDom.dll", "WindowsBase.dll")

if ($fileDepsJson -eq [string]::Empty) {
    $fileDepsRoot = $depsPath
} else {
    $fileDepsRoot = (Get-ChildItem $fileDepsJson).Directory.FullName
    $depsJson = Get-Content $fileDepsJson | ConvertFrom-Json

    $runtimeList = ([array]$depsJson.targets.PSObject.Properties)[-1].Value.PSObject.Properties | Where-Object {
        $_.Name -match "runtimepack.*Runtime"
    };

    $runtimeList | ForEach-Object {
        $_.Value.PSObject.Properties.Value | ForEach-Object {
            $fileExclusionList += $_.PSObject.Properties.Name
        }
    }
}

$fileExclusionList = $fileExclusionList | Where-Object {$_ -notin $dllsToIgnore}

if ($isLauncherPlugin -eq $True) {
    $fileInclusionList += @("*.deps.json")
    $fileExclusionList += @("Ijwhost.dll", "PowerToys.Common.UI.dll", "PowerToys.GPOWrapper.dll", "PowerToys.GPOWrapperProjection.dll", "PowerToys.PowerLauncher.Telemetry.dll", "PowerToys.ManagedCommon.dll", "PowerToys.Settings.UI.Lib.dll", "Wox.Infrastructure.dll", "Wox.Plugin.dll")
}

$fileList = Get-ChildItem $fileDepsRoot -Include $fileInclusionList -Exclude $fileExclusionList -File -Name

$fileWxs = $fileWxs -replace "(<\?define $($fileListName)=)", "<?define $fileListName=$($fileList -join ';')"

Set-Content -Path $wxsFilePath -Value $fileWxs
