param (
    [string]$Platform = 'x64',
    [string]$Configuration = 'Debug'
)

# Find repository root starting from the script location
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = $ScriptDir
while ($repoRoot -and -not (Test-Path (Join-Path $repoRoot "PowerToys.sln"))) {
    $parent = Split-Path -Parent $repoRoot
    if ($parent -eq $repoRoot) {
        Write-Error "Could not find PowerToys repository root."
        exit 1
    }
    $repoRoot = $parent
}

# Export script-scope variables used by build-common helpers
Set-Variable -Name RepoRoot -Value $repoRoot -Scope Script -Force

# Load shared helpers
. "$PSScriptRoot\build-common.ps1"

$ProjectsToBuild = @(".\src\runner\runner.vcxproj", ".\src\settings-ui\Settings.UI\PowerToys.Settings.csproj")

$ExtraArgs = "/p:SolutionDir=$repoRoot\"

foreach ($proj in $ProjectsToBuild) {
    Write-Host ("[BUILD-ESSENTIALS] Building {0}" -f $proj)
    RunMSBuild $proj $ExtraArgs $Platform $Configuration
}