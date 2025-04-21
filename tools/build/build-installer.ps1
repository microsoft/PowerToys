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
    Write-Host ("[MSBUILD] {0} {1}" -f $Solution, $ExtraArgs)
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

# ─────────────────────────────────────────────────────────────
# Build steps
# ─────────────────────────────────────────────────────────────
Write-Host ("[PIPELINE] Start | Platform={0} Configuration={1}" -f $Platform, $Configuration)
Write-Host ''

# ① PowerToys
RestoreThenBuild '.\PowerToys.sln'

# ② Tools
RestoreThenBuild '.\tools\BugReportTool\BugReportTool.sln'
RestoreThenBuild '.\tools\StylesReportTool\StylesReportTool.sln'

# ③ Clean installer (keep *.exe)
Write-Host '[CLEAN] installer (keep *.exe)'
git clean -xfd -e '*.exe' -- .\installer\ | Out-Null

# ④ Installer
RestoreThenBuild '.\installer\PowerToysSetup.sln'
RunMSBuild '.\installer\PowerToysSetup.sln' '/m /t:PowerToysInstaller'
RunMSBuild '.\installer\PowerToysSetup.sln' '/m /t:PowerToysBootstrapper'

# ⑤ Sign .msix
$msixFiles = Get-ChildItem -Path "$repoRoot\installer" -Recurse -Filter *.msix |
Select-Object -ExpandProperty FullName
if ($msixFiles.Count) {
    Write-Host ("[SIGN] {0} .msix file(s)" -f $msixFiles.Count)
    & "$PSScriptRoot\cert-sign-package.ps1" -TargetPaths $msixFiles
}
else {
    Write-Warning '[SIGN] No .msix files found'
}

Write-Host '[PIPELINE] Completed'