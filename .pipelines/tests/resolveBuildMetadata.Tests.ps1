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
    It "generates the canonical preview version for scheduled main with the auto default" {
        $result = & $scriptPath `
            -VersionOverride "auto" `
            -SourceBranch "refs/heads/main" `
            -BuildReason "Schedule" `
            -BuildNumber "PowerToys Signed YAML Release Build_2607.30099-main" `
            -DailyVersionSequence "1" `
            -VersionPropsPath (New-VersionProps)

        $result.Intent | Should Be "preview-release"
        $result.Channel | Should Be "preview"
        $result.Version | Should Be "0.100.2111.0"
    }

    It "uses the generated version by default for stable" {
        $result = & $scriptPath `
            -SourceBranch "refs/heads/stable" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2607.30099-stable" `
            -DailyVersionSequence "2" `
            -VersionPropsPath (New-VersionProps)

        $result.Intent | Should Be "stable-release"
        $result.Channel | Should Be "stable"
        $result.Version | Should Be "0.100.2112.0"
    }

    It "treats the pipeline auto sentinel as an automatic version" {
        $result = & $scriptPath `
            -VersionOverride "auto" `
            -SourceBranch "refs/heads/stable" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2607.30099-stable" `
            -DailyVersionSequence "2" `
            -VersionPropsPath (New-VersionProps)

        $result.Version | Should Be "0.100.2112.0"
    }

    It "supports an explicit preview release from stable" {
        $result = & $scriptPath `
            -ReleaseIntent "preview-release" `
            -SourceBranch "refs/heads/stable" `
            -BuildReason "Schedule" `
            -BuildNumber "PowerToys Signed YAML Release Build_2607.30099-stable" `
            -DailyVersionSequence "2" `
            -VersionPropsPath (New-VersionProps)

        $result.Intent | Should Be "preview-release"
        $result.Channel | Should Be "preview"
        $result.Version | Should Be "0.100.2112.0"
        $result.AllowPublicSymbols | Should Be $false
        $result.ShouldPublishPreview | Should Be $true
    }

    It "rejects a scheduled stable build without explicit preview intent" {
        Assert-Throws {
            & $scriptPath `
                -SourceBranch "refs/heads/stable" `
                -BuildReason "Schedule" `
                -BuildNumber "PowerToys Signed YAML Release Build_2607.30099-stable" `
                -DailyVersionSequence "2" `
                -VersionPropsPath (New-VersionProps)
        }
    }

    It "rejects stable release intent from main" {
        Assert-Throws {
            & $scriptPath `
                -ReleaseIntent "stable-release" `
                -SourceBranch "refs/heads/main" `
                -BuildReason "Manual" `
                -BuildNumber "PowerToys Signed YAML Release Build_2607.30099-main" `
                -DailyVersionSequence "2" `
                -VersionPropsPath (New-VersionProps)
        }
    }

    It "keeps private builds independent from the release counter" {
        $result = & $scriptPath `
            -SourceBranch "refs/heads/user/feature" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2607.30003-feature" `
            -BuildDate "20260731" `
            -DailyVersionSequence "9" `
            -VersionPropsPath (New-VersionProps)

        $result.Version | Should Be "0.0.21103.0"
    }

    It "allows an explicit MSI-safe version for private validation" {
        $result = & $scriptPath `
            -VersionOverride "0.100.2151.0" `
            -SourceBranch "refs/heads/LegendaryBlair/preview-version" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2608.03001-preview-version" `
            -VersionPropsPath (New-VersionProps)

        $result.Intent | Should Be "private-validation"
        $result.Channel | Should Be "private"
        $result.Version | Should Be "0.100.2151.0"
        $result.AllowPublicSymbols | Should Be $false
        $result.ShouldPublishPreview | Should Be $false
    }

    It "rejects a private override with a nonzero fourth component" {
        Assert-Throws {
            & $scriptPath `
                -VersionOverride "0.100.2151.1" `
                -SourceBranch "refs/heads/LegendaryBlair/preview-version" `
                -BuildReason "Manual" `
                -BuildNumber "PowerToys Signed YAML Release Build_2608.03001-preview-version" `
                -VersionPropsPath (New-VersionProps)
        }
    }

    It "increments the year digit across a calendar year boundary" {
        $result = & $scriptPath `
            -SourceBranch "refs/heads/main" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2701.02001-main" `
            -DailyVersionSequence "1" `
            -VersionPropsPath (New-VersionProps)

        $result.Version | Should Be "0.100.10021.0"
    }

    It "resets the year digit after the epoch advances with the release train" {
        $result = & $scriptPath `
            -SourceBranch "refs/heads/main" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2701.02001-main" `
            -DailyVersionSequence "1" `
            -VersionPropsPath (New-VersionProps -ReleaseTrain "0.101" -Epoch "2027-01-01")

        $result.Version | Should Be "0.101.21.0"
    }

    It "preserves monotonicity at the year boundary" {
        $lastBuildOfYear = & $scriptPath `
            -SourceBranch "refs/heads/main" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2612.31099-main" `
            -DailyVersionSequence "9" `
            -VersionPropsPath (New-VersionProps)
        $firstBuildOfNextYear = & $scriptPath `
            -SourceBranch "refs/heads/main" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2701.01001-main" `
            -DailyVersionSequence "1" `
            -VersionPropsPath (New-VersionProps)

        [int]($firstBuildOfNextYear.Version -split "\.")[2] | Should BeGreaterThan ([int]($lastBuildOfYear.Version -split "\.")[2])
    }

    It "preserves an explicit stable override" {
        $result = & $scriptPath `
            -VersionOverride "0.100.2" `
            -SourceBranch "refs/heads/stable" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-stable" `
            -DailyVersionSequence "1" `
            -VersionPropsPath (New-VersionProps)

        $result.Version | Should Be "0.100.2.0"
    }

    It "allows an explicit stable override after the automatic sequence is exhausted" {
        $result = & $scriptPath `
            -VersionOverride "0.101.0" `
            -SourceBranch "refs/heads/stable" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-stable" `
            -DailyVersionSequence "10" `
            -VersionPropsPath (New-VersionProps)

        $result.Version | Should Be "0.101.0.0"
    }

    It "rejects a stable override with a nonzero fourth component" {
        Assert-Throws {
            & $scriptPath `
                -VersionOverride "0.100.2.1" `
                -SourceBranch "refs/heads/stable" `
                -BuildReason "Manual" `
                -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-stable" `
                -DailyVersionSequence "1" `
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
                -DailyVersionSequence "1" `
                -VersionPropsPath (New-VersionProps)
        }
    }

    It "preserves a canonical full preview override" {
        $result = & $scriptPath `
            -VersionOverride "0.100.2111.0" `
            -SourceBranch "refs/heads/main" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-main" `
            -DailyVersionSequence "1" `
            -VersionPropsPath (New-VersionProps)

        $result.Version | Should Be "0.100.2111.0"
    }

    It "allows a full preview override after the automatic sequence is exhausted" {
        $result = & $scriptPath `
            -VersionOverride "0.100.2111.0" `
            -SourceBranch "refs/heads/main" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-main" `
            -DailyVersionSequence "10" `
            -VersionPropsPath (New-VersionProps)

        $result.Version | Should Be "0.100.2111.0"
    }

    It "rejects a preview override with a nonzero fourth component" {
        Assert-Throws {
            & $scriptPath `
                -VersionOverride "0.100.2111.1" `
                -SourceBranch "refs/heads/main" `
                -BuildReason "Manual" `
                -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-main" `
                -DailyVersionSequence "1" `
                -VersionPropsPath (New-VersionProps)
        }
    }

    It "rejects a preview override from a different release train" {
        Assert-Throws {
            & $scriptPath `
                -VersionOverride "0.101.2111.0" `
                -SourceBranch "refs/heads/main" `
                -BuildReason "Manual" `
                -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-main" `
                -DailyVersionSequence "1" `
                -VersionPropsPath (New-VersionProps)
        }
    }

    It "rejects daily release sequences above 9" {
        Assert-Throws {
            & $scriptPath `
                -SourceBranch "refs/heads/main" `
                -BuildReason "Manual" `
                -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-main" `
                -DailyVersionSequence "10" `
                -VersionPropsPath (New-VersionProps)
        }
    }

    It "requires a release sequence for main and stable" {
        Assert-Throws {
            & $scriptPath `
                -SourceBranch "refs/heads/main" `
                -BuildReason "Manual" `
                -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-main" `
                -VersionPropsPath (New-VersionProps)
        }
    }

    It "uses the pipeline date for both YDDD and counter alignment" {
        $result = & $scriptPath `
            -SourceBranch "refs/heads/main" `
            -BuildReason "Manual" `
            -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-main" `
            -BuildDate "20260731" `
            -DailyVersionSequence "1" `
            -VersionPropsPath (New-VersionProps)

        $result.Version | Should Be "0.100.2121.0"
    }

    It "requires the epoch to be January 1" {
        Assert-Throws {
            & $scriptPath `
                -SourceBranch "refs/heads/main" `
                -BuildReason "Manual" `
                -BuildNumber "PowerToys Signed YAML Release Build_2607.30001-main" `
                -DailyVersionSequence "1" `
                -VersionPropsPath (New-VersionProps -Epoch "2026-02-01")
        }
    }

    It "rejects release trains that exceed the YDDDB year range" {
        Assert-Throws {
            & $scriptPath `
                -SourceBranch "refs/heads/main" `
                -BuildReason "Manual" `
                -BuildNumber "PowerToys Signed YAML Release Build_3301.01001-main" `
                -DailyVersionSequence "1" `
                -VersionPropsPath (New-VersionProps)
        }
    }
}
