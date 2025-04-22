param (
    [string]$Platform = 'arm64',
    [string]$Configuration = 'Release'
)

$repoRoot = Resolve-Path "$PSScriptRoot\..\.."
Set-Location $repoRoot

function RunMSBuild {
    param (
        [string]$Solution, 
        [string]$ExtraArgs  
    )

    $base = @(
        $Solution
        "/p:Platform=`"$Platform`""
        "/p:Configuration=$Configuration"
        '/verbosity:normal'
        '/clp:Summary;PerformanceSummary;ErrorsOnly;WarningsOnly'
        '/nologo'
    )

    $cmd = $base + ($ExtraArgs -split ' ')
    Write-Host ("[MSBUILD] {0} {1}" -f $Solution, ($cmd -join ' '))
    & msbuild.exe @cmd

    if ($LASTEXITCODE -ne 0) {
        Write-Error ("Build failed: {0}  {1}" -f $Solution, $ExtraArgs)
        exit $LASTEXITCODE
    }

}

function RestoreThenBuild {
    param ([string]$Solution)

    # 1) restore
    RunMSBuild $Solution '/t:restore /p:RestorePackagesConfig=true'
    # 2) build  -------------------------------------------------
    RunMSBuild $Solution '/m'
}

Write-Host ("Make sure wix is installed and available")
& "$PSScriptRoot\ensure-wix.ps1"

Write-Host ("[PIPELINE] Start | Platform={0} Configuration={1}" -f $Platform, $Configuration)
Write-Host ''

$cmdpalOutputPath = Join-Path $repoRoot "$Platform\$Configuration\WinUI3Apps\CmdPal"

if (Test-Path $cmdpalOutputPath) {
    Write-Host "[CLEAN] Removing previous output: $cmdpalOutputPath"
    Remove-Item $cmdpalOutputPath -Recurse -Force -ErrorAction Ignore
}

RestoreThenBuild '.\PowerToys.sln'

$msixSearchRoot = Join-Path $repoRoot "$Platform\$Configuration"
$msixFiles = Get-ChildItem -Path $msixSearchRoot -Recurse -Filter *.msix |
Select-Object -ExpandProperty FullName

if ($msixFiles.Count) {
    Write-Host ("[SIGN] .msix file(s): {0}" -f ($msixFiles -join '; '))
    & "$PSScriptRoot\cert-sign-package.ps1" -TargetPaths $msixFiles
}
else {
    Write-Warning "[SIGN] No .msix files found in $msixSearchRoot"
}

RestoreThenBuild '.\tools\BugReportTool\BugReportTool.sln'
RestoreThenBuild '.\tools\StylesReportTool\StylesReportTool.sln'

Write-Host '[CLEAN] installer (keep *.exe)'
git clean -xfd -e '*.exe' -- .\installer\ | Out-Null

RunMSBuild  '.\installer\PowerToysSetup.sln' '/t:restore /p:RestorePackagesConfig=true'

RunMSBuild '.\installer\PowerToysSetup.sln' '/m /t:PowerToysInstaller /p:PerUser=true'

RunMSBuild '.\installer\PowerToysSetup.sln' '/m /t:PowerToysBootstrapper /p:PerUser=true'

Write-Host '[PIPELINE] Completed'