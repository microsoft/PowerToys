[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True,Position=1)]
    [string]$solution
)

Write-Output $PSScriptRoot

$errorTable = @{}

$MSBuildLoc = vswhere.exe -prerelease -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\Microsoft.Build.dll
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
    $_.PlatformName -eq "ARM64"
};
$projects = $solutionFile.ProjectsInOrder | Where-Object {
    $_.ProjectType -eq "KnownToBeMSBuildFormat"
};

foreach ($project in $projects) {
    foreach ($slnConfig in $arm64SlnConfigs.FullName) {
        if ($project.ProjectConfigurations.$slnConfig.FullName -ne $slnConfig) {
            $errorTable[$project.ProjectName] += @(""
                | Select-Object @{n = "Configuration"; e = { $project.ProjectConfigurations.$slnConfig.FullName } },
                @{n = "ExpectedConfiguration"; e = { $slnConfig } })
        }
    }
}

if ($errorTable.Count -gt 0) {
    Write-Output "The following projects have an invalid Arm64 configuration mapping:`n"
    $errorTable.Keys | ForEach-Object {
        Write-Output $_`:;
        $errorTable[$_] | ForEach-Object {
            Write-Output "$($_.ExpectedConfiguration)=$($_.Configuration)";
        };
        Write-Output `r
    }
    exit 1;
}

exit 0;