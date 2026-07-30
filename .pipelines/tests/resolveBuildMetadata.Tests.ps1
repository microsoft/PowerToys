# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

$scriptPath = Join-Path $PSScriptRoot "..\resolveBuildMetadata.ps1"

function New-VersionProps {
    param(
        [string]$ReleaseTrain = "0.100",
        [string]$Epoch = "2026-01-01"
    )

    $path = Join-Path $TestDrive "Version.props"
    @"
<Project>
  <PropertyGroup>
    <ReleaseTrainVersion>$ReleaseTrain</ReleaseTrainVersion>
    <ReleaseTrainEpoch>$Epoch</ReleaseTrainEpoch>
  </PropertyGroup>
</Project>
"@ | Set-Content -LiteralPath $path
    return $path
}

function Assert-Throws {
    param([scriptblock]$Action)

    $threw = $false
    try {
        & $Action | Out-Null
    }
    catch {
        $threw = $true
    }

    $threw | Should Be $true
}

Describe "resolveBuildMetadata" {
    It "generates the canonical preview version for main" {
        $result = & $scriptPath `
            -SourceBranch "refs/heads/main" `
            -BuildReason "Schedule" `
            -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-main" `
            -VersionPropsPath (New-VersionProps)

        $result.Intent | Should Be "preview-release"
        $result.Channel | Should Be "preview"
        $result.Version | Should Be "0.100.21101.0"
    }

    It "uses the generated version by default for stable" {
        $result = & $scriptPath `
            -SourceBranch "refs/heads/stable" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2607.30002-stable" `
            -VersionPropsPath (New-VersionProps)

        $result.Intent | Should Be "stable-release"
        $result.Channel | Should Be "stable"
        $result.Version | Should Be "0.100.21102.0"
    }

    It "uses the canonical date component for private builds" {
        $result = & $scriptPath `
            -SourceBranch "refs/heads/user/feature" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2607.30003-feature" `
            -VersionPropsPath (New-VersionProps)

        $result.Version | Should Be "0.0.21103.0"
    }

    It "continues the extended day count across a year boundary" {
        $result = & $scriptPath `
            -SourceBranch "refs/heads/main" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2701.02001-main" `
            -VersionPropsPath (New-VersionProps)

        $result.Version | Should Be "0.100.36701.0"
    }

    It "resets the extended day after the epoch advances with the release train" {
        $result = & $scriptPath `
            -SourceBranch "refs/heads/main" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2701.02001-main" `
            -VersionPropsPath (New-VersionProps -ReleaseTrain "0.101" -Epoch "2027-01-01")

        $result.Version | Should Be "0.101.201.0"
    }

    It "preserves an explicit stable override" {
        $result = & $scriptPath `
            -VersionOverride "0.100.2" `
            -SourceBranch "refs/heads/stable" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-stable" `
            -VersionPropsPath (New-VersionProps)

        $result.Version | Should Be "0.100.2.0"
    }

    It "rejects a stable override with a nonzero fourth component" {
        Assert-Throws {
            & $scriptPath `
                -VersionOverride "0.100.2.1" `
                -SourceBranch "refs/heads/stable" `
                -BuildReason "Manual" `
                -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-stable" `
                -VersionPropsPath (New-VersionProps)
        }
    }

    It "rejects a stable override outside MSI major and minor limits" {
        Assert-Throws {
            & $scriptPath `
                -VersionOverride "0.256.2.0" `
                -SourceBranch "refs/heads/stable" `
                -BuildReason "Manual" `
                -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-stable" `
                -VersionPropsPath (New-VersionProps)
        }
    }

    It "preserves a canonical full preview override" {
        $result = & $scriptPath `
            -VersionOverride "0.100.21101.0" `
            -SourceBranch "refs/heads/main" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-main" `
            -VersionPropsPath (New-VersionProps)

        $result.Version | Should Be "0.100.21101.0"
    }

    It "rejects a preview override with a nonzero fourth component" {
        Assert-Throws {
            & $scriptPath `
                -VersionOverride "0.100.21101.1" `
                -SourceBranch "refs/heads/main" `
                -BuildReason "Manual" `
                -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-main" `
                -VersionPropsPath (New-VersionProps)
        }
    }

    It "rejects a preview override from a different release train" {
        Assert-Throws {
            & $scriptPath `
                -VersionOverride "0.101.21101.0" `
                -SourceBranch "refs/heads/main" `
                -BuildReason "Manual" `
                -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-main" `
                -VersionPropsPath (New-VersionProps)
        }
    }

    It "rejects daily revisions above 99" {
        Assert-Throws {
            & $scriptPath `
                -SourceBranch "refs/heads/main" `
                -BuildReason "Manual" `
                -BuildNumber "PowerToys Signed YAML Release Build_2607.30100-main" `
                -VersionPropsPath (New-VersionProps)
        }
    }

    It "requires the epoch to be January 1" {
        Assert-Throws {
            & $scriptPath `
                -SourceBranch "refs/heads/main" `
                -BuildReason "Manual" `
                -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-main" `
                -VersionPropsPath (New-VersionProps -Epoch "2026-02-01")
        }
    }

    It "rejects generated versions that exceed the MSI component limit" {
        Assert-Throws {
            & $scriptPath `
                -SourceBranch "refs/heads/main" `
                -BuildReason "Manual" `
                -BuildNumber "PowerToys Signed YAML Release Build_2712.31001-main" `
                -VersionPropsPath (New-VersionProps)
        }
    }
}
