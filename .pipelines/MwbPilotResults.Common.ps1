# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Assert-MwbPilotTestResults {
    param(
        [Parameter(Mandatory)][string] $ResultsDirectory,
        [Parameter(Mandatory)][guid] $RunId,
        [string[]] $RequiredMethods = @(
            'AutonomousSandboxSmoke',
            'NativeStartupErrorTextDoesNotRequireUiAutomation',
            'DesktopAndWindowVideosSurviveAnEarlyFailure',
            'ReceiverInfrastructureRemainsResponsiveToUiAutomation',
            'ReceiverRegainsFocusFromStartMenuWithoutMouseInput',
            'StartMenuDismissalDoesNotActOnAnOrdinaryForegroundWindow',
            'StartMenuIdentityRequiresExactOsPathClassAndInteractiveSession'
        )
    )

    $launcherRoot = Join-Path $ResultsDirectory "ui-$($RunId.ToString('N').Substring(0, 12))"
    Assert-MwbCiPlainPath $launcherRoot
    $reports = @(Get-ChildItem -LiteralPath $launcherRoot -Filter '*.trx' -Recurse -File -ErrorAction Stop)
    if ($reports.Count -ne 1) { throw 'MWB full-suite sign-off requires exactly one current-job TRX report.' }
    Assert-MwbCiPlainPath $reports[0].FullName
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($reports[0].FullName, $settings)
    try {
        $document = [Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
    }
    finally { $reader.Dispose() }
    $counter = $document.SelectSingleNode('//*[local-name()="ResultSummary"]/*[local-name()="Counters"]')
    if (-not $counter) { throw 'MWB TRX report is missing execution counters.' }
    $counts = @{}
    foreach ($name in @('total', 'executed', 'passed', 'failed', 'notExecuted')) {
        $number = 0
        if (-not [int]::TryParse($counter.GetAttribute($name), [ref]$number) -or $number -lt 0) {
            throw 'MWB TRX report has invalid execution counters.'
        }
        $counts[$name] = $number
    }
    $results = @($document.SelectNodes('//*[local-name()="Results"]/*[local-name()="UnitTestResult"]'))
    if ($counts.total -lt $RequiredMethods.Count -or $counts.executed -ne $counts.total -or
        $counts.passed -ne $counts.total -or $counts.failed -ne 0 -or $counts.notExecuted -ne 0 -or
        $results.Count -ne $counts.total -or @($results | Where-Object { $_.GetAttribute('outcome') -cne 'Passed' }).Count) {
        throw 'MWB full-suite sign-off requires every test to execute and pass; skipped or incomplete runs are failures.'
    }
    foreach ($method in $RequiredMethods) {
        if (-not @($results | Where-Object {
            $_.GetAttribute('testName') -ceq $method -or
            $_.GetAttribute('testName').StartsWith("$method (", [StringComparison]::Ordinal)
        }).Count) {
            throw "MWB full-suite sign-off is missing required test: $method."
        }
    }
    [pscustomobject]@{ Total = $counts.total; Executed = $counts.executed; Passed = $counts.passed }
}
