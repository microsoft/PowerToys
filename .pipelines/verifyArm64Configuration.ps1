[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True,Position=1)]
    [string]$solution
)

Write-Output "Verifying Arm64 configuration for $solution"

$errorTable = @{}

$MSBuildLoc = & "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" -prerelease -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\Microsoft.Build.dll
if ($null -eq $MSBuildLoc) {
    throw "Unable to locate Microsoft.Build.dll"
}

try {
    Add-Type -Path $MSBuildLoc
}
catch {
    # Catching because it may error on loading all the types from the assembly, but we only need one
}

$solutionFile = [Microsoft.Build.Construction.SolutionFile]::Parse($solution);
$arm64SlnConfigs = $solutionFile.SolutionConfigurations | Where-Object {
    $_.PlatformName -ceq "ARM64"
};

# Should have two configurations. Debug and Release.
if($arm64SlnConfigs.Length -lt 2) {
    Write-Host -ForegroundColor Red "Missing Solution-level Arm64 platforms"
    exit 1;
}

# List projects only.
$projects = $solutionFile.ProjectsInOrder | Where-Object {
    $_.ProjectType -eq "KnownToBeMSBuildFormat" -and
    $_.ProjectName -ne "EnvironmentVariablesUILib" -and  # UI Lib to be shipped as a nuget too, so it will be built for Any CPU
    $_.ProjectName -ne "HostsUILib" -and                 # UI Lib to be shipped as a nuget too, so it will be built for Any CPU
    $_.ProjectName -ne "RegistryPreviewUILib"            # UI Lib to be shipped as a nuget too, so it will be built for Any CPU
};

# Enumerate through the projects and add any project with a mismatched platform and project configuration
foreach ($project in $projects) {
    foreach ($slnConfig in $arm64SlnConfigs.FullName) {
        if ($project.ProjectConfigurations.$slnConfig.FullName -cne $slnConfig) {
            $errorTable[$project.ProjectName] += @(""`
                | Select-Object @{n = "Configuration"; e = { $project.ProjectConfigurations.$slnConfig.FullName ?? "Missing platform" } },
                @{n = "ExpectedConfiguration"; e = { $slnConfig } })
        }
    }
}

if ($errorTable.Count -gt 0) {
    Write-Host -ForegroundColor Red "Verification failed for the following projects:`n"
    $errorTable.Keys | ForEach-Object {
        Write-Host -ForegroundColor Red $_`:;
        $errorTable[$_] | ForEach-Object {
            Write-Host -ForegroundColor Red "$($_.ExpectedConfiguration)=$($_.Configuration)";
        };
        Write-Host -ForegroundColor Red `r
    }
    exit 1;
}

Write-Output "Verification Complete"
exit 0;