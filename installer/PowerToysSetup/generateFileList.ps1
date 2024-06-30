[CmdletBinding()]
Param(
    # Can be multiple files separated by ; as long as they're on the same directory
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
    [bool]$isLauncherPlugin
)

$fileWxs = Get-Content $wxsFilePath;

$fileExclusionList = @("*.pdb", "*.lastcodeanalysissucceeded", "createdump.exe", "powertoys.exe")

$fileInclusionList = @("*.dll", "*.exe", "*.json", "*.msix", "*.png", "*.gif", "*.ico", "*.cur", "*.svg", "index.html", "reg.js", "gitignore.js", "monacoSpecialLanguages.js", "customTokenColors.js", "*.pri")

$dllsToIgnore = @("System.CodeDom.dll", "WindowsBase.dll")

if ($fileDepsJson -eq [string]::Empty) {
    $fileDepsRoot = $depsPath
} else {
    $multipleDepsJson = $fileDepsJson.Split(";")

    foreach ( $singleDepsJson in $multipleDepsJson )
    {

        $fileDepsRoot = (Get-ChildItem $singleDepsJson).Directory.FullName
        $depsJson = Get-Content $singleDepsJson | ConvertFrom-Json

        $runtimeList = ([array]$depsJson.targets.PSObject.Properties)[-1].Value.PSObject.Properties | Where-Object {
            $_.Name -match "runtimepack.*Runtime"
        };

        $runtimeList | ForEach-Object {
            $_.Value.PSObject.Properties.Value | ForEach-Object {
                $fileExclusionList += $_.PSObject.Properties.Name
            }
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
