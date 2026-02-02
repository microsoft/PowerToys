# Publish dependencies for Settings.UI with RuntimeIdentifier=win-x64

. "$PSScriptRoot\tools\build\build-common.ps1"

# Initialize Visual Studio dev environment
if (-not (Ensure-VsDevEnvironment)) {
    Write-Error "Failed to initialize VS dev environment"
    exit 1
}

$Platform = "x64"
$Configuration = "Release"
$RuntimeId = "win-x64"

$projects = @(
    "src\common\Common.Search\Common.Search.csproj",
    "src\common\LanguageModelProvider\LanguageModelProvider.csproj",
    "src\common\AllExperiments\AllExperiments.csproj",
    "src\common\Common.UI\Common.UI.csproj",
    "src\common\ManagedCommon\ManagedCommon.csproj",
    "src\common\ManagedTelemetry\Telemetry\ManagedTelemetry.csproj"
)

foreach ($project in $projects) {
    $projectName = Split-Path $project -Leaf
    Write-Host "Publishing $projectName..." -ForegroundColor Cyan

    $args = "-t:Publish -p:Configuration=$Configuration -p:Platform=$Platform -p:RuntimeIdentifier=$RuntimeId"
    RunMSBuild $project $args $Platform $Configuration

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to publish $projectName"
        exit $LASTEXITCODE
    }
}

Write-Host "`nAll dependencies published successfully!" -ForegroundColor Green
Write-Host "You can now publish Settings.UI" -ForegroundColor Green
