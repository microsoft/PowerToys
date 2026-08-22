[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if ($Clean) {
    foreach ($directory in @(
            (Join-Path $root 'artifacts\bin'),
            (Join-Path $root 'artifacts\obj')
        )) {
        if (Test-Path -LiteralPath $directory) {
            Remove-Item -LiteralPath $directory -Recurse -Force
        }
    }
}
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
    Select-Object -First 1
if (-not $msbuild) {
    throw 'MSBuild was not found.'
}

function Invoke-PrototypeBuild([string]$project, [string[]]$properties) {
    & $msbuild $project /m /t:Rebuild "/p:Configuration=$Configuration" '/p:Platform=x64' `
        @properties /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed: $project"
    }
}

Invoke-PrototypeBuild (Join-Path $root 'PtLsmr.sln') @()

$engineDefinitions = @(
    [ordered]@{ name = 'engine-5.0.0.0'; major = 5; minor = 0; fail = 0; slow = 0 },
    [ordered]@{ name = 'engine-5.1.0.0'; major = 5; minor = 1; fail = 0; slow = 0 },
    [ordered]@{ name = 'engine-5.2.0.0'; major = 5; minor = 2; fail = 1; slow = 0 },
    [ordered]@{ name = 'engine-5.3.0.0'; major = 5; minor = 3; fail = 0; slow = 0 },
    [ordered]@{ name = 'engine-5.4.0.0'; major = 5; minor = 4; fail = 0; slow = 1 },
    [ordered]@{ name = 'engine-5.4.0.0-collision'; major = 5; minor = 4; fail = 0; slow = 0 }
)

foreach ($definition in $engineDefinitions) {
    $outDirectory = Join-Path $root "artifacts\bin\x64\$Configuration\$($definition.name)"
    $commonObjectDirectory = Join-Path $root "artifacts\obj\common-$($definition.name)\x64\$Configuration"
    $engineObjectDirectory = Join-Path $root "artifacts\obj\engine-$($definition.name)\x64\$Configuration"
    Invoke-PrototypeBuild (Join-Path $root 'Common\PtLsmrCommon.vcxproj') @(
        "/p:OutDir=$outDirectory\",
        "/p:IntDir=$commonObjectDirectory\"
    )
    Invoke-PrototypeBuild (Join-Path $root 'Updater\PtLsmrUpdater.vcxproj') @(
        '/p:BuildProjectReferences=false',
        "/p:OutDir=$outDirectory\",
        "/p:EngineIntDir=$engineObjectDirectory\",
        "/p:EngineVersionMajor=$($definition.major)",
        "/p:EngineVersionMinor=$($definition.minor)",
        '/p:EngineVersionBuild=0',
        '/p:EngineVersionRevision=0',
        "/p:EngineFailQualification=$($definition.fail)",
        "/p:EngineSlowQualification=$($definition.slow)"
    )
}

$runtimeDefinitions = @(
    [ordered]@{ name = 'runtime-track-1-1.0.0.0'; track = 1; major = 1; minor = 0; fail = 0; wrongProduct = 0; variant = 0 },
    [ordered]@{ name = 'runtime-track-1-1.1.0.0'; track = 1; major = 1; minor = 1; fail = 0; wrongProduct = 0; variant = 0 },
    [ordered]@{ name = 'runtime-track-1-1.1.0.0-collision'; track = 1; major = 1; minor = 1; fail = 0; wrongProduct = 0; variant = 1 },
    [ordered]@{ name = 'runtime-track-1-1.2.0.0'; track = 1; major = 1; minor = 2; fail = 1; wrongProduct = 0; variant = 0 },
    [ordered]@{ name = 'runtime-track-1-1.3.0.0'; track = 1; major = 1; minor = 3; fail = 0; wrongProduct = 0; variant = 0 },
    [ordered]@{ name = 'runtime-track-1-1.4.0.0'; track = 1; major = 1; minor = 4; fail = 0; wrongProduct = 0; variant = 0 },
    [ordered]@{ name = 'runtime-track-1-1.5.0.0'; track = 1; major = 1; minor = 5; fail = 0; wrongProduct = 0; variant = 0 },
    [ordered]@{ name = 'runtime-track-1-1.6.0.0'; track = 1; major = 1; minor = 6; fail = 0; wrongProduct = 0; variant = 0 },
    [ordered]@{ name = 'runtime-track-1-1.7.0.0'; track = 1; major = 1; minor = 7; fail = 0; wrongProduct = 0; variant = 0 },
    [ordered]@{ name = 'runtime-track-1-1.8.0.0'; track = 1; major = 1; minor = 8; fail = 0; wrongProduct = 0; variant = 0 },
    [ordered]@{ name = 'runtime-track-2-2.0.0.0'; track = 2; major = 2; minor = 0; fail = 0; wrongProduct = 0; variant = 0 },
    [ordered]@{ name = 'runtime-wrong-product'; track = 1; major = 1; minor = 3; fail = 0; wrongProduct = 1; variant = 0 }
)

foreach ($definition in $runtimeDefinitions) {
    $outDirectory = Join-Path $root "artifacts\bin\x64\$Configuration\$($definition.name)"
    $commonObjectDirectory = Join-Path $root "artifacts\obj\common-$($definition.name)\x64\$Configuration"
    $runtimeObjectDirectory = Join-Path $root "artifacts\obj\runtime-$($definition.name)\x64\$Configuration"
    Invoke-PrototypeBuild (Join-Path $root 'Common\PtLsmrCommon.vcxproj') @(
        "/p:OutDir=$outDirectory\",
        "/p:IntDir=$commonObjectDirectory\"
    )
    Invoke-PrototypeBuild (Join-Path $root 'Runtime\PtLsmrRuntime.vcxproj') @(
        '/p:BuildProjectReferences=false',
        "/p:OutDir=$outDirectory\",
        "/p:RuntimeIntDir=$runtimeObjectDirectory\",
        "/p:RuntimeTrack=$($definition.track)",
        "/p:RuntimeVersionMajor=$($definition.major)",
        "/p:RuntimeVersionMinor=$($definition.minor)",
        '/p:RuntimeVersionBuild=0',
        '/p:RuntimeVersionRevision=0',
        "/p:RuntimeFailReadiness=$($definition.fail)",
        "/p:RuntimeWrongProduct=$($definition.wrongProduct)",
        "/p:RuntimePayloadVariant=$($definition.variant)"
    )
}

Write-Host "Release PE set built under $(Join-Path $root "artifacts\bin\x64\$Configuration")."
