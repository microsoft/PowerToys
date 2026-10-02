# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
. "$PSScriptRoot\..\MwbSandboxCi.Common.ps1"
. "$PSScriptRoot\..\MwbSandboxClientSetup.Common.ps1"
$tokens = $null
$parseErrors = $null
$setupAst = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot '..\Install-MwbSandboxClient.ps1'), [ref]$tokens, [ref]$parseErrors)
if ($parseErrors) { throw 'Client setup contains parse errors.' }
foreach ($name in @('Test-MwbClientTaskCompletion', 'New-MwbClientProtectedDirectory',
    'Assert-MwbClientProtectedDirectory')) {
    $definition = $setupAst.Find({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $true)
    . ([scriptblock]::Create($definition.Extent.Text))
}

function New-MwbClientReportFixture {
    [ordered]@{
        SchemaVersion = 1; RunId = '01234567-89ab-cdef-0123-456789abcdef'
        UserSid = 'S-1-5-21-123-456-789-1001'; IsElevated = $false; SessionId = 1
        Status = 'Failed'; Action = 'None'; ErrorCode = $null; ErrorHResult = $null
        Before = @{ Status = 'NotChecked' }; After = @{ Status = 'NotChecked' }
        Cleanup = @{ Status = 'NotStarted' }
    }
}

Describe 'Actual current-user modern client readiness' {
    BeforeEach {
        Mock Get-MwbClientPackages { @{ Healthy = $true } }
        Mock Test-Path { $true } -ParameterFilter { $LiteralPath -like '*\Microsoft\WindowsApps\wsb.exe' }
        Mock Get-Item { @{ Attributes = [IO.FileAttributes]::ReparsePoint } } `
            -ParameterFilter { $LiteralPath -like '*\Microsoft\WindowsApps\wsb.exe' }
        Mock Invoke-MwbClientCommand {
            @{ ExitCode = 0; TimedOut = $false; OutputComplete = $true; Output = 'wsb 1.2.3' }
        }
    }

    It 'requires a healthy registration, alias reparse point and actual zero version exit' {
        $result = Get-MwbClientReadiness
        $result.Status | Should Be 'Ready'
        $result.PackageCount | Should Be 1
        $result.VersionExitCode | Should Be 0
        Assert-MockCalled Get-MwbClientPackages -Times 1 -Exactly -Scope It -ParameterFilter { -not $AllUsers }
        Assert-MockCalled Invoke-MwbClientCommand -Times 1 -Exactly -Scope It `
            -ParameterFilter { $Arguments[0] -eq '--version' -and $TimeoutSeconds -eq 15 }
    }

    It 'accepts recognized three- or four-part version formats and strips banners' -TestCases @(
        @{ Output = '0.8.107.0'; Expected = 'wsb 0.8.107.0' }
        @{ Output = 'wsb 0.8.107.0'; Expected = 'wsb 0.8.107.0' }
        @{ Output = 'Windows Sandbox CLI version: 0.8.107'; Expected = 'wsb 0.8.107' }
        @{ Output = "Unrelated banner`r`n0.8.107.0`r`nUnrelated details"; Expected = 'wsb 0.8.107.0' }
        @{ Output = 'wsb.exe v0.8.107.0'; Expected = 'wsb 0.8.107.0' }
    ) {
        param($Output, $Expected)
        Mock Invoke-MwbClientCommand {
            @{ ExitCode = 0; TimedOut = $false; OutputComplete = $true; Output = $Output }
        }
        $result = Get-MwbClientReadiness
        $result.Status | Should Be 'Ready'
        $result.Version | Should Be $Expected
        ($result | ConvertTo-Json) | Should Not Match 'Unrelated'
    }

    It 'does not equate an absent package with readiness' {
        Mock Get-MwbClientPackages { }
        (Get-MwbClientReadiness).PackageCount | Should Be 0
        Assert-MockCalled Invoke-MwbClientCommand -Times 0 -Exactly -Scope It
    }

    It 'rejects an unhealthy package' {
        Mock Get-MwbClientPackages { @{ Healthy = $false } }
        (Get-MwbClientReadiness).Status | Should Be 'NotReady'
        Assert-MockCalled Invoke-MwbClientCommand -Times 0 -Exactly -Scope It
    }

    It 'rejects a plain file in place of the execution alias' {
        Mock Get-Item { @{ Attributes = [IO.FileAttributes]::Normal } } `
            -ParameterFilter { $LiteralPath -like '*\Microsoft\WindowsApps\wsb.exe' }
        (Get-MwbClientReadiness).AliasIsReparsePoint | Should Be $false
        Assert-MockCalled Invoke-MwbClientCommand -Times 0 -Exactly -Scope It
    }

    It 'rejects unsuccessful version results without exporting raw output' -TestCases @(
        @{ ExitCode = 23; TimedOut = $false; Complete = $true; Text = 'wsb 1.2.3' }
        @{ ExitCode = 0; TimedOut = $true; Complete = $true; Text = 'wsb 1.2.3' }
        @{ ExitCode = 0; TimedOut = $false; Complete = $false; Text = 'wsb 1.2.3' }
        @{ ExitCode = 0; TimedOut = $false; Complete = $true; Text = 'DO_NOT_PUBLISH secret' }
    ) {
        param($ExitCode, $TimedOut, $Complete, $Text)
        Mock Invoke-MwbClientCommand {
            @{ ExitCode = $ExitCode; TimedOut = $TimedOut; OutputComplete = $Complete; Output = $Text }
        }
        $result = Get-MwbClientReadiness
        $result.Status | Should Be 'NotReady'
        ($result | ConvertTo-Json) | Should Not Match 'DO_NOT_PUBLISH'
    }

    It 'distinguishes query failures from confirmed absence' {
        Mock Get-MwbClientPackages { throw 'DO_NOT_PUBLISH private deployment error' }
        $result = Get-MwbClientReadiness
        $result.Status | Should Be 'QueryFailed'
        $result.PackageCount | Should Be $null
        $result.ErrorHResult | Should Match '^0x[0-9A-F]{8}$'
        ($result | ConvertTo-Json) | Should Not Match 'DO_NOT_PUBLISH'
    }
}

Describe 'Trusted inbox Sandbox launch association' {
    BeforeEach {
        $extensionKey = [pscustomobject]@{ Default = '' }
        $extensionKey | Add-Member ScriptMethod GetValue { param($Name) $this.Default }
        $commandKey = [pscustomobject]@{ Default = "$env:windir\System32\WindowsSandbox.exe `"%1`"" }
        $commandKey | Add-Member ScriptMethod GetValue { param($Name) $this.Default }
        Mock Assert-MwbCiPlainPath { }
        Mock Get-AuthenticodeSignature {
            @{ Status = 'Valid'; SignerCertificate = @{ Subject = 'CN=Microsoft Windows, O=Microsoft Corporation, C=US' } }
        }
        Mock Get-Item { $extensionKey } -ParameterFilter { $LiteralPath -eq 'Registry::HKEY_CLASSES_ROOT\.wsb' }
        Mock Get-Item { $commandKey } -ParameterFilter { $LiteralPath -eq 'Registry::HKEY_CLASSES_ROOT\Windows.Sandbox\shell\open\command' }
    }

    It 'accepts the OS ProgID when modern Windows leaves the extension default empty' {
        Get-MwbClientInboxLauncher | Should Be "$env:windir\System32\WindowsSandbox.exe"
        Assert-MockCalled Get-Item -Times 1 -Exactly -Scope It -ParameterFilter {
            $LiteralPath -eq 'Registry::HKEY_CLASSES_ROOT\Windows.Sandbox\shell\open\command'
        }
    }

    It 'accepts an explicit canonical extension association' {
        $extensionKey.Default = 'Windows.Sandbox'
        Get-MwbClientInboxLauncher | Should Be "$env:windir\System32\WindowsSandbox.exe"
    }

    It 'does not execute a changed association or extra arguments' {
        $commandKey.Default = "$env:windir\System32\WindowsSandbox.exe --unknown `"%1`""
        { Get-MwbClientInboxLauncher } | Should Throw 'SandboxAssociationMismatch'
    }
}

Describe 'Bounded modern client command capture without Sandbox or product execution' {
    It 'drains simultaneous stdout and stderr and preserves the real exit code' {
        $command = '[Console]::Out.Write(("x" * 100000)); [Console]::Error.Write(("y" * 100000)); exit 23'
        $result = Invoke-MwbClientCommand (Get-Process -Id $PID).Path @('-NoProfile', '-Command', $command) 10
        $result.ExitCode | Should Be 23
        $result.OutputComplete | Should Be $true
        $result.Output.Length | Should Be 100000
    }

    It 'terminates only its owned command on deadline' {
        $timer = [Diagnostics.Stopwatch]::StartNew()
        $result = Invoke-MwbClientCommand (Get-Process -Id $PID).Path @('-NoProfile', '-Command', 'Start-Sleep -Seconds 30') 1
        $result.TimedOut | Should Be $true
        ($timer.Elapsed.TotalSeconds -lt 6) | Should Be $true
    }

    It 'does not block on pipes inherited by a surviving child' {
        $childIdPath = Join-Path $TestDrive 'inherited-pipe-child.txt'
        $literal = $childIdPath.Replace("'", "''")
        $command = @"
`$start = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id `$PID).Path)
`$start.UseShellExecute = `$false
`$start.ArgumentList.Add('-NoProfile')
`$start.ArgumentList.Add('-Command')
`$start.ArgumentList.Add('Start-Sleep -Seconds 20')
`$child = [Diagnostics.Process]::Start(`$start)
`$child.Id | Set-Content -LiteralPath '$literal'
exit 0
"@
        $timer = [Diagnostics.Stopwatch]::StartNew()
        try {
            $result = Invoke-MwbClientCommand (Get-Process -Id $PID).Path @('-NoProfile', '-Command', $command) 10
            $result.ExitCode | Should Be 0
            $result.OutputComplete | Should Be $false
            ($timer.Elapsed.TotalSeconds -lt 8) | Should Be $true
        }
        finally {
            if (Test-Path -LiteralPath $childIdPath) {
                Stop-Process -Id ([int](Get-Content -LiteralPath $childIdPath)) -ErrorAction SilentlyContinue
            }
        }
    }
}

Describe 'Only trusted staged manifests can be registered' {
    BeforeEach {
        $originalProgramFiles = $env:ProgramFiles
        $env:ProgramFiles = Join-Path $TestDrive 'ProgramFiles'
        $package = @{
            Family = 'MicrosoftWindows.WindowsSandbox_cw5n1h2txyewy'
            FullName = 'MicrosoftWindows.WindowsSandbox_1.2.3.4_x64__cw5n1h2txyewy'
            Version = '1.2.3.4'; Signature = 'Store'; Development = $false; Healthy = $true
        }
        $package.Location = Join-Path "$env:ProgramFiles\WindowsApps" $package.FullName
        $null = New-Item -ItemType Directory -Path $package.Location -Force
        Set-Content -LiteralPath (Join-Path $package.Location 'AppxManifest.xml') -Value @'
<Package><Identity Name="MicrosoftWindows.WindowsSandbox" Version="1.2.3.4"
Publisher="CN=Microsoft Windows, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" /></Package>
'@
        $fakeAcl = [pscustomobject]@{}
        $fakeAcl | Add-Member ScriptMethod GetOwner { param($Type) [Security.Principal.SecurityIdentifier]::new('S-1-5-18') }
        $fakeAcl | Add-Member ScriptMethod GetAccessRules { param($Explicit, $Inherited, $Type) @() }
        Mock Get-Acl { $fakeAcl }
    }
    AfterEach { $env:ProgramFiles = $originalProgramFiles }

    It 'accepts an existing exact OS-protected Store package manifest' {
        Assert-MwbClientManifest $package | Should Be ([IO.Path]::GetFullPath((Join-Path $package.Location 'AppxManifest.xml')))
    }

    It 'rejects nonmatching or untrusted package metadata' -TestCases @(
        @{ Field = 'Family'; Value = 'Fake_cw5n1h2txyewy' }
        @{ Field = 'Signature'; Value = 'Developer' }
        @{ Field = 'Development'; Value = $true }
        @{ Field = 'Healthy'; Value = $false }
        @{ Field = 'FullName'; Value = 'MicrosoftWindows.WindowsSandbox_1.2.3.4_x64__other' }
    ) {
        param($Field, $Value)
        $package[$Field] = $Value
        { Assert-MwbClientManifest $package } | Should Throw 'UntrustedStagedPackage'
    }

    It 'rejects an unpacked manifest outside the exact WindowsApps package directory' {
        $package.Location = $TestDrive
        { Assert-MwbClientManifest $package } | Should Throw 'UntrustedPackageLocation'
    }

    It 'rejects mismatched publisher identity' {
        Set-Content -LiteralPath (Join-Path $package.Location 'AppxManifest.xml') -Value '<Package><Identity Name="Fake" /></Package>'
        { Assert-MwbClientManifest $package } | Should Throw 'ManifestIdentityMismatch'
    }

    It 'rejects a non-OS owner' {
        $fakeAcl | Add-Member ScriptMethod GetOwner {
            param($Type) [Security.Principal.SecurityIdentifier]::new('S-1-5-32-545')
        } -Force
        { Assert-MwbClientManifest $package } | Should Throw 'UntrustedPackageOwner'
    }

    It 'rejects a package writable by an ordinary user' {
        $fakeAcl | Add-Member ScriptMethod GetAccessRules {
            param($Explicit, $Inherited, $Type)
            @([pscustomobject]@{
                AccessControlType = 'Allow'; FileSystemRights = [Security.AccessControl.FileSystemRights]::Write
                PropagationFlags = [Security.AccessControl.PropagationFlags]::None
                IdentityReference = [Security.Principal.SecurityIdentifier]::new('S-1-5-32-545')
            })
        } -Force
        { Assert-MwbClientManifest $package } | Should Throw 'WritablePackageLocation'
    }

    It 'registers through the supported current-user command without force or trust bypass flags' {
        Mock Invoke-MwbClientPowerShell { @{ TimedOut = $false; OutputComplete = $true; ExitCode = 0 } }
        Register-MwbClientManifest $package
        Assert-MockCalled Invoke-MwbClientPowerShell -Times 1 -Exactly -Scope It -ParameterFilter {
            $TimeoutSeconds -eq 120 -and $Command -match 'Add-AppxPackage -Register' -and
            $Command -match '-DisableDevelopmentMode' -and $Command -notmatch 'AllowUnsigned|Force|AllUsers'
        }
    }

    It 'preserves an allowlisted deployment HRESULT but never raw deployment errors' {
        Mock Invoke-MwbClientPowerShell {
            @{ TimedOut = $false; OutputComplete = $true; ExitCode = 1; Output = '{"ErrorHResult":"0x80070005"}' }
        }
        try { Register-MwbClientManifest $package; throw 'Expected failure' }
        catch {
            $_.Exception.Message | Should Be 'RegistrationFailed'
            Get-MwbClientErrorHResult $_ | Should Be '0x80070005'
        }
    }

    It 'reports malformed registration failure output explicitly' {
        Mock Invoke-MwbClientPowerShell {
            @{ ExitCode = 1; TimedOut = $false; OutputComplete = $true; Output = 'invalid diagnostic payload' }
        }
        { Register-MwbClientManifest $package } | Should Throw 'RegistrationResponseInvalid'
    }

    It 'fails bounded registration timeouts' {
        Mock Invoke-MwbClientPowerShell { @{ TimedOut = $true; OutputComplete = $false; ExitCode = $null } }
        { Register-MwbClientManifest $package } | Should Throw 'RegistrationTimeout'
    }
}

Describe 'Explicit setup policy and precise owned cleanup' {
    BeforeEach {
        $report = New-MwbClientReportFixture
        $request = @{ RunId = $report.RunId; InstallTimeoutSeconds = 0; Package = $null }
        $directory = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $null = New-Item -ItemType Directory -Path $directory
        Mock Get-MwbClientReadiness { @{ Status = 'NotReady'; Healthy = $false; AliasIsReparsePoint = $false } }
        Mock Assert-MwbClientNoInstances { }
        Mock Register-MwbClientManifest { }
        $fakeLauncher = [pscustomobject]@{ HasExited = $true }
        $fakeLauncher | Add-Member ScriptMethod Dispose { }
        Mock Start-MwbClientFirstLaunch { $fakeLauncher }
        Mock Complete-MwbClientFirstLaunch { @{ Status = 'Passed'; GuestObserved = $true; LauncherStopped = $true } }
        Mock Start-Sleep { }
    }

    It 'no-ops for an already ready user without inventory, registration, launch or cleanup' {
        Mock Get-MwbClientReadiness { @{ Status = 'Ready' } }
        $request.PackageQueryFailed = $true
        Invoke-MwbClientSetupWorker $request $report $directory
        $report.Status | Should Be 'Ready'
        $report.Action | Should Be 'None'
        Assert-MockCalled Assert-MwbClientNoInstances -Times 0 -Exactly -Scope It
        Assert-MockCalled Register-MwbClientManifest -Times 0 -Exactly -Scope It
        Assert-MockCalled Start-MwbClientFirstLaunch -Times 0 -Exactly -Scope It
    }

    It 'refuses an unknown existing Sandbox without adopting, registering or launching' {
        Mock Assert-MwbClientNoInstances { throw 'ExistingSandboxDesktop' }
        Invoke-MwbClientSetupWorker $request $report $directory
        $report.Status | Should Be 'Failed'
        $report.ErrorCode | Should Be 'ExistingSandboxDesktop'
        Assert-MockCalled Start-MwbClientFirstLaunch -Times 0 -Exactly -Scope It
        Assert-MockCalled Register-MwbClientManifest -Times 0 -Exactly -Scope It
        Assert-MockCalled Complete-MwbClientFirstLaunch -Times 0 -Exactly -Scope It
    }

    It 'registers staged packages and requires subsequent current-user readiness' {
        $request.Package = @{ Family = 'inert fixture' }
        $script:clientProbeCount = 0
        Mock Get-MwbClientReadiness {
            $script:clientProbeCount++
            @{ Status = if ($script:clientProbeCount -eq 1) { 'NotReady' } else { 'Ready' } }
        }
        Invoke-MwbClientSetupWorker $request $report $directory
        $report.Status | Should Be 'Ready'
        $report.Action | Should Be 'RegisterStagedPackage'
        Assert-MockCalled Register-MwbClientManifest -Times 1 -Exactly -Scope It
        Assert-MockCalled Start-MwbClientFirstLaunch -Times 0 -Exactly -Scope It
    }

    It 'completes an absent-package first launch only after readiness and owned cleanup succeed' {
        $script:clientProbeCount = 0
        Mock Get-MwbClientReadiness {
            $script:clientProbeCount++
            @{ Status = if ($script:clientProbeCount -eq 1) { 'NotReady' } else { 'Ready' } }
        }
        Invoke-MwbClientSetupWorker $request $report $directory
        $report.Status | Should Be 'Ready'
        $report.Action | Should Be 'InboxFirstLaunch'
        $report.Cleanup.Status | Should Be 'Passed'
        Assert-MockCalled Complete-MwbClientFirstLaunch -Times 1 -Exactly -Scope It
        Test-Path (Join-Path $directory 'worker-report.json') | Should Be $true
    }

    It 'captures failed installation with before/after and cleanup even without any TRX' {
        Invoke-MwbClientSetupWorker $request $report $directory
        $report.Status | Should Be 'Failed'
        $report.ErrorCode | Should Be 'ClientSetupDeadlineExceeded'
        $report.Before.Status | Should Be 'NotReady'
        $report.After.Status | Should Be 'NotReady'
        $report.Cleanup.Status | Should Be 'Passed'
        @(Get-ChildItem $directory -Filter '*.trx').Count | Should Be 0
        Assert-MockCalled Complete-MwbClientFirstLaunch -Times 1 -Exactly -Scope It
    }

    It 'fails an otherwise successful installation when cleanup is unconfirmed' {
        $script:clientProbeCount = 0
        Mock Get-MwbClientReadiness {
            $script:clientProbeCount++
            @{ Status = if ($script:clientProbeCount -eq 1) { 'NotReady' } else { 'Ready' } }
        }
        Mock Complete-MwbClientFirstLaunch { throw 'DO_NOT_PUBLISH unknown process command line' }
        Invoke-MwbClientSetupWorker $request $report $directory
        $report.Status | Should Be 'Failed'
        $report.Cleanup.ErrorCode | Should Be 'FirstLaunchCleanupUnconfirmed'
        ($report | ConvertTo-Json -Depth 8) | Should Not Match 'DO_NOT_PUBLISH'
    }

    It 'does not silently fall back to first launch after staged registration fails' {
        $request.Package = @{ Family = 'inert fixture' }
        Mock Register-MwbClientManifest { throw 'RegistrationFailed' }
        Invoke-MwbClientSetupWorker $request $report $directory
        $report.ErrorCode | Should Be 'RegistrationFailed'
        $report.After.Status | Should Be 'NotReady'
        Assert-MockCalled Get-MwbClientReadiness -Times 2 -Exactly -Scope It
        Assert-MockCalled Start-MwbClientFirstLaunch -Times 0 -Exactly -Scope It
    }

    It 'records fresh after-state if a failed registration nevertheless changed the package' {
        $request.Package = @{ Family = 'inert fixture' }
        $script:clientProbeCount = 0
        Mock Get-MwbClientReadiness {
            $script:clientProbeCount++
            @{ Status = if ($script:clientProbeCount -eq 1) { 'NotReady' } else { 'Ready' } }
        }
        Mock Register-MwbClientManifest { throw 'RegistrationFailed' }
        Invoke-MwbClientSetupWorker $request $report $directory
        $report.Status | Should Be 'Failed'
        $report.After.Status | Should Be 'Ready'
        $report.ErrorCode | Should Be 'RegistrationFailed'
    }
}

Describe 'Cleanup observes but never terminates an uncorrelated instance' {
    BeforeEach {
        $process = [pscustomobject]@{ HasExited = $true; Killed = $false }
        $process | Add-Member ScriptMethod Kill { $this.Killed = $true }
        $process | Add-Member ScriptMethod WaitForExit { param($Milliseconds) $true }
        $directory = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $null = New-Item -ItemType Directory -Path $directory
        $id = [guid]::NewGuid()
        Mock Start-Sleep { }
    }

    It 'confirms exact guest marker, launcher exit and empty instance inventory' {
        Set-Content -LiteralPath (Join-Path $directory 'guest-started.txt') -Value $id
        Mock Assert-MwbClientNoInstances { }
        $result = Complete-MwbClientFirstLaunch $process $directory $id 0
        $result.Status | Should Be 'Passed'
        $result.GuestObserved | Should Be $true
        $process.Killed | Should Be $false
        Assert-MockCalled Assert-MwbClientNoInstances -Times 1 -Exactly -Scope It -ParameterFilter { $ProbeProvider }
    }

    It 'fails without stopping an unknown remaining instance' {
        Mock Assert-MwbClientNoInstances { throw 'ExistingSandboxInstance' }
        { Complete-MwbClientFirstLaunch $process $directory $id 0 } | Should Throw 'FirstLaunchCleanupUnconfirmed'
        $process.Killed | Should Be $false
    }

    It 'can terminate only the exact owned launcher handle and still reports unconfirmed cleanup' {
        $process.HasExited = $false
        { Complete-MwbClientFirstLaunch $process $directory $id 0 } | Should Throw 'FirstLaunchCleanupUnconfirmed'
        $process.Killed | Should Be $true
    }

    It 'does not accept a guest marker from another run' {
        Set-Content -LiteralPath (Join-Path $directory 'guest-started.txt') -Value ([guid]::NewGuid())
        { Complete-MwbClientFirstLaunch $process $directory $id 0 } | Should Throw 'GuestMarkerMismatch'
        $process.Killed | Should Be $false
    }
}

Describe 'Corroborated task completion and public diagnostic boundaries' {
    BeforeEach {
        $report = New-MwbClientReportFixture
        $task = @{ State = 'Ready' }
        $info = @{ LastTaskResult = 0 }
        $status = @{ RunId = $report.RunId; ExitCode = 0 }
    }

    It 'requires observed scheduler completion and matching exit status' {
        Test-MwbClientTaskCompletion $task $info $true $status $report.RunId | Should Be $true
    }

    It 'does not trust status while queued/running or without observed launch' -TestCases @(
        @{ State = 'Queued'; Observed = $true }
        @{ State = 'Running'; Observed = $true }
        @{ State = 'Ready'; Observed = $false }
    ) {
        param($State, $Observed)
        $task.State = $State
        Test-MwbClientTaskCompletion $task $info $Observed $status $report.RunId | Should Be $false
    }

    It 'rejects a mismatched run or scheduler failure' -TestCases @(
        @{ Mismatch = 'Run' }; @{ Mismatch = 'Exit' }
    ) {
        param($Mismatch)
        if ($Mismatch -eq 'Run') { $status.RunId = [guid]::NewGuid().ToString() }
        else { $info.LastTaskResult = 42 }
        { Test-MwbClientTaskCompletion $task $info $true $status $report.RunId } | Should Throw 'TaskResultMismatch'
    }

    It 'strips raw output, paths, process arguments and unapproved state' {
        $report.RawError = 'DO_NOT_PUBLISH'
        $report.Before.ProcessArguments = 'DO_NOT_PUBLISH'
        $report.Cleanup.Password = 'DO_NOT_PUBLISH'
        $result = ConvertTo-MwbPublicClientSetup $report $report.RunId $report.UserSid
        ($result | ConvertTo-Json -Depth 8) | Should Not Match 'DO_NOT_PUBLISH|RawError|ProcessArguments|Password'
        $result.Before.Status | Should Be 'NotChecked'
    }

    It 'rejects non-allowlisted errors even if they look like safe identifier strings' {
        $report.ErrorCode = 'SecretAuthenticationToken'
        { ConvertTo-MwbPublicClientSetup $report $report.RunId $report.UserSid } | Should Throw 'InvalidClientSetupReport'
    }

    It 'rejects wrong identity and elevated/session-zero readiness' -TestCases @(
        @{ Field = 'IsElevated'; Value = $true }
        @{ Field = 'SessionId'; Value = 0 }
        @{ Field = 'UserSid'; Value = 'S-1-5-18' }
    ) {
        param($Field, $Value)
        $expected = $report.UserSid
        $report[$Field] = $Value
        { ConvertTo-MwbPublicClientSetup $report $report.RunId $expected } | Should Throw 'InvalidClientSetupReport'
    }
}

Describe 'Effective pipeline setup boundaries' {
    It 'skips Legacy without dispatch or operating system package changes' {
        $directory = Join-Path $TestDrive 'legacy-results'
        $id = [guid]::NewGuid()
        $result = Invoke-MwbClientCommand (Get-Process -Id $PID).Path @(
            '-NoProfile', '-File', (Join-Path $PSScriptRoot '..\Install-MwbSandboxClient.ps1'),
            '-Platform', 'x64Win10', '-RunId', $id.ToString(), '-ResultsDirectory', $directory) 10
        $result.ExitCode | Should Be 0
        $evidence = Get-Content (Join-Path $directory "mwb-prerequisites-$id\client-setup.json") -Raw | ConvertFrom-Json
        $evidence.Status | Should Be 'SkippedLegacy'
        $evidence.Action | Should Be 'None'
        $evidence.DispatchCleanup | Should Be 'NotStarted'
    }

    It 'places explicit setup before Prepare only in the gated MWB step template' {
        $text = Get-Content (Join-Path $PSScriptRoot '..\v2\templates\steps-mwb-sandbox-experiment.yml') -Raw
        ($text.IndexOf('Install-MwbSandboxClient.ps1') -lt $text.IndexOf('-Mode Prepare')) | Should Be $true
        $text | Should Match "condition: and\(succeeded\(\), ne\(variables\['TestPlatform'\], 'x64Win10'\)\)"
        $text | Should Match 'timeoutInMinutes: 12'
        $text | Should Match '(?s)Collect allowlisted MWB prerequisite diagnostics.*?condition: always\(\)'
        $text | Should Match '(?s)Publish MWB Sandbox prerequisite report.*?condition: always\(\)'
        $job = Get-Content (Join-Path $PSScriptRoot '..\v2\templates\job-test-project.yml') -Raw
        $job | Should Match '(?s)if eq\(parameters.mwbSandboxExperiment, true\).*?steps-mwb-sandbox-experiment.yml'
    }

    It 'dispatches the exact desktop user with interactive Limited priority four and bounded scheduler lifetime' {
        $setupAst.Extent.Text | Should Match '-UserId \$interactiveUser -LogonType Interactive -RunLevel Limited'
        $setupAst.Extent.Text | Should Match '-Priority 4 -ExecutionTimeLimit'
        $setupAst.Extent.Text | Should Match '\$identity.User.Value -cne \$request.UserSid'
        $setupAst.Extent.Text | Should Match '\$identity.Name -ine \(Get-CimInstance Win32_ComputerSystem\).UserName'
        $setupAst.Extent.Text | Should Match 'PurgeAccessRules'
        $setupAst.Extent.Text | Should Match 'Unregister-ScheduledTask -TaskName \$taskName'
    }

    It 'never enables features, reboots, changes policy or kills by process name' {
        $common = Get-Content (Join-Path $PSScriptRoot '..\MwbSandboxClientSetup.Common.ps1') -Raw
        ($common + $setupAst.Extent.Text) | Should Not Match 'Enable-WindowsOptionalFeature|Restart-Computer|Set-ExecutionPolicy|Stop-Process -Name|taskkill|AllowUnsigned|ForceApplicationShutdown'
        (Get-Command Start-MwbClientFirstLaunch).Definition | Should Match 'shutdown.exe /s /f /t 0'
        (Get-Command Start-MwbClientFirstLaunch).Definition | Should Match 'Get-MwbClientInboxLauncher'
        (Get-Command Get-MwbClientInboxLauncher).Definition | Should Match 'Get-AuthenticodeSignature'
    }
}
