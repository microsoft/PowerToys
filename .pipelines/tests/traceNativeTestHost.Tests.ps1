# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

. (Join-Path $PSScriptRoot '..\traceNativeTestHost.ps1') -Action Stop -LogDirectory 'C:\logs'

function Test-NativeThrows {
    param([scriptblock]$Operation)
    # Pester 3's Should Throw does not recognize PowerShell 7 exceptions.
    try { & $Operation | Out-Null; return $false } catch { return $true }
}

function New-FakeNativeProcess {
    param([int]$Id, [datetime]$Created, [string]$Image)
    $process = [pscustomobject]@{
        Id = $Id; StartTime = $Created; HasExited = $false; MainModule = @{ FileName = $Image }
        Handle = 1; ExitCode = 0; Disposed = $false; Killed = $false
    }
    $process | Add-Member ScriptMethod Dispose { $this.Disposed = $true }
    $process | Add-Member ScriptMethod Kill { $this.Killed = $true; $this.HasExited = $true }
    $process | Add-Member ScriptMethod WaitForExit { param($Milliseconds) return $this.HasExited }
    return $process
}

Describe 'Native trace diagnostic identity and idle gating' {
    BeforeEach {
        $script:now = [datetime]::Now
        $script:since = $script:now.AddMinutes(-1).ToUniversalTime()
        $script:log = 'C:\logs\native-tests.diag.host.run.log'
        $script:text = "TpTrace Information: 0 : 12628, 1, $($script:now.ToString('yyyy/MM/dd, HH:mm:ss.fff')), 27080224738, testhost.exe, DefaultEngineInvoker.Invoke: Testhost process started with args :[--parentprocessid, 17628],[--diag, $script:log]`n" +
            "Register extension with identifier data 'executor://CppUnitTestExecutor/v1' inside file 'C:\VS\TestPlatform\Extensions\Microsoft.VisualStudio.TestTools.CppUnitTestFramework.CppUnitTestExtension.dll'`n" +
            "PEReaderHelper.GetAssemblyType: Determined assemblyType:'Native' for source: 'C:\build\tests\Workspaces.Lib.UnitTests.dll'"
        $script:file = [pscustomobject]@{
            FullName = $script:log; CreationTimeUtc = $script:now.ToUniversalTime()
            LastWriteTimeUtc = $script:now.ToUniversalTime(); Length = 500
        }
        $script:samples = @{}
    }

    It 'parses the diagnostic PID, exact diag path, native executor and source' {
        $identity = Get-NativeHostIdentity $script:text $script:log $script:since
        $identity.ProcessId | Should Be 12628
        $identity.ParentId | Should Be 17628
        $identity.Sources[0] | Should Match 'Workspaces.Lib.UnitTests.dll'
    }

    It 'accepts the explicit x86 host image' {
        (Get-NativeHostIdentity ($script:text.Replace('testhost.exe', 'testhost.x86.exe')) $script:log $script:since).Image | Should Be 'testhost.x86.exe'
    }

    It 'rejects a stale diagnostic header' {
        Get-NativeHostIdentity $script:text $script:log $script:now.AddMinutes(1).ToUniversalTime() | Should BeNullOrEmpty
    }

    It 'rejects a diagnostic for a different path' {
        Get-NativeHostIdentity $script:text 'C:\other\native-tests.diag.host.run.log' $script:since | Should BeNullOrEmpty
    }

    It 'rejects managed hosts and name-only matches' {
        Get-NativeHostIdentity ($script:text.Replace('executor://CppUnitTestExecutor/v1', 'executor://MSTest')) $script:log $script:since | Should BeNullOrEmpty
        Get-NativeHostIdentity ($script:text.Replace('testhost.exe', 'unrelated.exe')) $script:log $script:since | Should BeNullOrEmpty
    }

    It 'rejects logs without native source evidence' {
        Get-NativeHostIdentity ($script:text.Replace("assemblyType:'Native'", "assemblyType:'Managed'")) $script:log $script:since | Should BeNullOrEmpty
    }

    It 'waits for 120 seconds of observed inactivity' {
        Test-NativeLogIdle $script:file $script:samples $script:since $script:now | Should Be $false
        Test-NativeLogIdle $script:file $script:samples $script:since $script:now.AddSeconds(119) | Should Be $false
        Test-NativeLogIdle $script:file $script:samples $script:since $script:now.AddSeconds(120) | Should Be $true
    }

    It 'resets inactivity on either file length or timestamp progress' {
        $null = Test-NativeLogIdle $script:file $script:samples $script:since $script:now
        $script:file.Length++
        Test-NativeLogIdle $script:file $script:samples $script:since $script:now.AddSeconds(120) | Should Be $false
        $script:file.LastWriteTimeUtc = $script:file.LastWriteTimeUtc.AddSeconds(1)
        Test-NativeLogIdle $script:file $script:samples $script:since $script:now.AddSeconds(240) | Should Be $false
    }

    It 'ignores preexisting files even if their write timestamp is new' {
        $script:file.CreationTimeUtc = $script:since.AddMinutes(-1)
        Test-NativeLogIdle $script:file $script:samples $script:since $script:now | Should Be $false
        $script:samples.Count | Should Be 0
    }
}

Describe 'Native trace retained process ownership' {
    BeforeEach {
        $script:created = [datetime]::UtcNow.AddSeconds(-10)
        $script:since = $script:created.AddMinutes(-1)
        $script:target = New-FakeNativeProcess 100 $script:created 'C:\VS\TestPlatform\testhost.exe'
        $script:parentProcess = New-FakeNativeProcess 200 $script:created.AddSeconds(-2) 'C:\VS\TestPlatform\vstest.console.exe'
        $script:identity = [pscustomobject]@{
            ProcessId = 100; ParentId = 200; Image = 'testhost.exe'; LogTimeUtc = $script:created.AddSeconds(1)
            Adapter = 'C:\VS\TestPlatform\Extensions\Microsoft.VisualStudio.TestTools.CppUnitTestFramework.CppUnitTestExtension.dll'
        }
        $script:os = [pscustomobject]@{
            ProcessId = 100; ParentProcessId = 200; ExecutablePath = $script:target.MainModule.FileName; CreationDate = $script:created
        }
        Mock Open-NativeProcess { if ($ProcessId -eq 100) { return $script:target }; return $script:parentProcess }
        Mock Get-NativeProcessInfo { return $script:os }
    }

    It 'retains both validated process objects until capture finishes' {
        $record = Open-NativeHost $script:identity $script:since
        $record.Target.Id | Should Be 100
        $record.Parent.Id | Should Be 200
        $script:target.Disposed | Should Be $false
        Test-NativeHostAlive $record | Should Be $true
        $script:target.HasExited = $true
        Test-NativeHostAlive $record | Should Be $false
    }

    It 'rejects an OS parent that differs from the diagnostic parent' {
        $script:os.ParentProcessId = 201
        Test-NativeThrows { Open-NativeHost $script:identity $script:since } | Should Be $true
        $script:target.Disposed | Should Be $true
        $script:parentProcess.Disposed | Should Be $true
    }

    It 'rejects a parent executable other than vstest.console' {
        $script:parentProcess.MainModule.FileName = 'C:\VS\TestPlatform\pwsh.exe'
        Test-NativeThrows { Open-NativeHost $script:identity $script:since } | Should Be $true
    }

    It 'rejects a target executable other than the exact logged host' {
        $script:target.MainModule.FileName = 'C:\VS\TestPlatform\unrelated.exe'
        $script:os.ExecutablePath = $script:target.MainModule.FileName
        Test-NativeThrows { Open-NativeHost $script:identity $script:since } | Should Be $true
    }

    It 'rejects a target or parent predating Start' {
        $script:target.StartTime = $script:since.AddSeconds(-1)
        Test-NativeThrows { Open-NativeHost $script:identity $script:since } | Should Be $true
        $script:target.StartTime = $script:created
        $script:parentProcess.StartTime = $script:since.AddSeconds(-1)
        Test-NativeThrows { Open-NativeHost $script:identity $script:since } | Should Be $true
    }

    It 'rejects reuse of a logged PID by a process created after the header' {
        $script:target.StartTime = $script:identity.LogTimeUtc.AddSeconds(1)
        $script:os.CreationDate = $script:target.StartTime
        Test-NativeThrows { Open-NativeHost $script:identity $script:since } | Should Be $true
    }

    It 'rejects reuse between the process handle and OS lookup' {
        $script:os.CreationDate = $script:created.AddSeconds(1)
        Test-NativeThrows { Open-NativeHost $script:identity $script:since } | Should Be $true
    }

    It 'rejects an adapter outside the actual host installation' {
        $script:identity.Adapter = 'C:\other\CppUnitTestExtension.dll'
        Test-NativeThrows { Open-NativeHost $script:identity $script:since } | Should Be $true
    }

    It 'rejects an exited parent and a reused watcher identity' {
        $script:parentProcess.HasExited = $true
        Test-NativeThrows { Open-NativeHost $script:identity $script:since } | Should Be $true
        Test-NativeProcessIdentity $script:target 100 $script:created.AddSeconds(-1) | Should Be $false
    }
}

Describe 'Native trace debugger preflight and capture validation' {
    BeforeEach {
        Mock Test-Path { return $true }
        Mock Get-AuthenticodeSignature { return @{ Status = 'Valid'; SignerCertificate = @{ Subject = 'CN=Microsoft Corporation, O=Microsoft Corporation, C=US' } } }
    }

    It 'accepts a signed debugger path containing spaces' {
        Resolve-NativeDebugger 'C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\cdb.exe' | Should Be 'C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\cdb.exe'
    }

    It 'fails before starting a watcher when CDB is missing' {
        Mock Test-Path { return $false } -ParameterFilter { $PathType -eq 'Leaf' }
        Mock Start-Process { throw 'Must not launch.' }
        Test-NativeThrows { Start-NativeTestHostTrace 'C:\logs' 'C:\build' 'C:\missing\cdb.exe' } | Should Be $true
        Assert-MockCalled Start-Process -Times 0 -Exactly -Scope It
    }

    It 'rejects unsigned and non-Microsoft debuggers' {
        Mock Get-AuthenticodeSignature { return @{ Status = 'NotSigned'; SignerCertificate = @{ Subject = '' } } }
        Test-NativeThrows { Resolve-NativeDebugger 'C:\tools\cdb.exe' } | Should Be $true
    }

    It 'rejects another publisher even with a valid signature' {
        Mock Get-AuthenticodeSignature { return @{ Status = 'Valid'; SignerCertificate = @{ Subject = 'CN=Example, O=Example' } } }
        Test-NativeThrows { Resolve-NativeDebugger 'C:\tools\cdb.exe' } | Should Be $true
    }

    It 'rejects UNC paths, relative paths and symbol command separators' {
        Test-NativeThrows { Get-NativeLocalPath '\\server\share\cdb.exe' } | Should Be $true
        Test-NativeThrows { Get-NativeLocalPath '.\cdb.exe' } | Should Be $true
        Test-NativeThrows { Get-NativeLocalPath 'C:\symbols;srv*https://example.org' } | Should Be $true
        Test-NativeThrows { Get-NativeLocalPath 'C:\symbols" -c q' } | Should Be $true
    }

    It 'requires thread frames and an actual module table, not an exit code or banner' {
        $good = "00 000000e1``ad2ff938 00007ff8``9287fdce ntdll!NtDelayExecution+0x14`nstart             end                 module name`n00007ff8``92810000 00007ff8``92a08000 ntdll (export symbols)"
        Test-NativeStackOutput $good | Should Be $true
        Test-NativeStackOutput ($good -replace '^00 ', '') | Should Be $true
        Test-NativeStackOutput 'Microsoft Windows Debugger; cannot debug pid 123, Win32 error 0n87' | Should Be $false
        Test-NativeStackOutput ($good -replace '00 000000e1[^\n]+', '') | Should Be $false
        Test-NativeStackOutput ($good + "`nCould not attach to process") | Should Be $false
    }
}

Describe 'Native trace actual adapter and loaded test metadata' {
    It 'uses the host installation and loaded module directories rather than a guessed VS version' {
        $adapter = 'C:\ActualVS\TestPlatform\Extensions\Microsoft.VisualStudio.TestTools.CppUnitTestFramework.CppUnitTestExtension.dll'
        $testDll = 'C:\build\tests\Workspaces\Workspaces.Lib.UnitTests.dll'
        $record = [pscustomobject]@{
            Installation = 'C:\ActualVS\TestPlatform'
            Identity = @{ Adapter = $adapter; Sources = @($testDll) }
            Target = @{
                MainModule = @{ FileName = 'C:\ActualVS\TestPlatform\testhost.exe' }
                Modules = @(@{ FileName = $testDll }, @{ FileName = 'C:\other\unrelated.dll' })
            }
            Parent = @{ MainModule = @{ FileName = 'C:\ActualVS\TestPlatform\vstest.console.exe' } }
        }
        Mock Get-NativeFileMetadata { return @{ Path = $Path; FileVersion = 'actual-version'; SHA256 = 'actual-hash' } }
        $inputs = Get-NativeCaptureInputs $record 'C:\build'
        $inputs.SymbolPath.Contains('C:\build\tests\Workspaces') | Should Be $true
        $inputs.SymbolPath.Contains('C:\ActualVS\TestPlatform\Extensions') | Should Be $true
        $inputs.LoadedRelevantModules.Count | Should Be 1
        $inputs.NativeSources[0] | Should Be $testDll
        Assert-MockCalled Get-NativeFileMetadata -Times 1 -Exactly -Scope It -ParameterFilter { $Path -eq $adapter }
        Assert-MockCalled Get-NativeFileMetadata -Times 1 -Exactly -Scope It -ParameterFilter { $Path -eq $testDll }
        Assert-MockCalled Get-NativeFileMetadata -Times 0 -Exactly -Scope It -ParameterFilter { $Path -eq 'C:\other\unrelated.dll' }
    }
}

Describe 'Native trace bounded watcher' {
    BeforeEach {
        $script:owner = [pscustomobject]@{
            StartedUtc = [datetime]::UtcNow.AddMinutes(-3).ToString('o'); DebuggerPath = 'C:\SDK\cdb.exe'
            LogDirectory = 'C:\logs'; BinariesDirectory = 'C:\build'; RunId = 'abc'; WatcherId = 500
        }
        $script:target = New-FakeNativeProcess 100 ([datetime]::UtcNow) 'C:\VS\testhost.exe'
        $script:parentProcess = New-FakeNativeProcess 200 ([datetime]::UtcNow) 'C:\VS\vstest.console.exe'
        $script:record = [pscustomobject]@{
            Target = $script:target; Parent = $script:parentProcess
            TargetStartUtc = [datetime]::UtcNow; ParentStartUtc = [datetime]::UtcNow
        }
        Mock Resolve-NativeDebugger { return 'C:\SDK\cdb.exe' }
        Mock Get-NativeProcessInfo { return @{ ProcessId = 500 } }
        Mock Test-Path { return $false }
        Mock Get-ChildItem { return @{ FullName = 'C:\logs\native-tests.diag.host.current.log'; Length = 100; Name = 'native-tests.diag.host.current.log' } }
        Mock Test-NativeLogIdle { return $true }
        Mock Get-Content { return 'current host diagnostics' }
        Mock Get-NativeHostIdentity { return @{ ProcessId = 100; ParentId = 200 } }
        Mock Open-NativeHost { return $script:record }
        Mock Get-NativeCaptureInputs { return @{ SymbolPath = 'C:\build'; Files = @() } }
        Mock Write-NativeJson { }
        Mock Write-NativeWatchState { }
        Mock Test-NativeHostAlive { return $true }
        Mock Invoke-NativeStackCapture { return 'Captured' }
        Mock Start-Sleep { }
    }

    It 'takes only two captures with ten seconds between them and releases both handles' {
        Watch-NativeTestHost $script:owner 'C:\logs\owned'
        Assert-MockCalled Invoke-NativeStackCapture -Times 2 -Exactly -Scope It
        Assert-MockCalled Start-Sleep -Times 10 -Exactly -Scope It -ParameterFilter { $Seconds -eq 1 }
        Assert-MockCalled Write-NativeWatchState -Times 1 -Exactly -Scope It -ParameterFilter { $Status -eq 'Completed' -and $Captures -eq 2 }
        $script:target.Disposed | Should Be $true
        $script:parentProcess.Disposed | Should Be $true
        $script:target.Killed | Should Be $false
    }

    It 'persists capture failures instead of pretending diagnostics succeeded' {
        Mock Invoke-NativeStackCapture { throw 'Attach failed despite exit zero.' }
        Test-NativeThrows { Watch-NativeTestHost $script:owner 'C:\logs\owned' } | Should Be $true
        Assert-MockCalled Write-NativeWatchState -Times 1 -Exactly -Scope It -ParameterFilter { $Status -eq 'Failed' }
        $script:target.Disposed | Should Be $true
        $script:target.Killed | Should Be $false
    }

    It 'does not launch another debugger after Stop' {
        Mock Invoke-NativeStackCapture { return 'Stopped' }
        Watch-NativeTestHost $script:owner 'C:\logs\owned'
        Assert-MockCalled Invoke-NativeStackCapture -Times 1 -Exactly -Scope It
        Assert-MockCalled Write-NativeWatchState -Times 1 -Exactly -Scope It -ParameterFilter { $Status -eq 'Completed' -and $Captures -eq 0 }
    }

    It 'never attaches if the retained host exited before capture' {
        Mock Test-NativeHostAlive { return $false }
        Watch-NativeTestHost $script:owner 'C:\logs\owned'
        Assert-MockCalled Invoke-NativeStackCapture -Times 0 -Exactly -Scope It
    }
}

Describe 'Native trace owned debugger cleanup and Stop' {
    BeforeEach {
        $script:created = [datetime]::UtcNow
        $script:target = New-FakeNativeProcess 100 $script:created 'C:\VS\testhost.exe'
        $script:debuggerProcess = New-FakeNativeProcess 300 $script:created 'C:\SDK\cdb.exe'
        $script:owner = @{
            RunId = '0123456789abcdef0123456789abcdef'; LogDirectory = 'C:\logs'
            WatcherId = 500; WatcherStartUtc = $script:created.ToString('o')
        }
        Mock Start-NativeCdbProcess { return $script:debuggerProcess }
        Mock Write-NativeJson { }
        Mock Test-Path { return $true }
        Mock Get-Content { return ($script:owner | ConvertTo-Json) }
        Mock Set-Content { }
    }

    It 'kills only its retained nonsuspending debugger when stop is requested' {
        Invoke-NativeStackCapture $script:target 'C:\SDK\cdb.exe' 'C:\build;C:\VS' 'C:\logs\snapshot.log' 'C:\logs\stop.requested' | Should Be 'Stopped'
        $script:debuggerProcess.Killed | Should Be $true
        $script:debuggerProcess.Disposed | Should Be $true
        $script:target.Killed | Should Be $false
        Assert-MockCalled Start-NativeCdbProcess -Times 1 -Exactly -Scope It -ParameterFilter {
            $StartInfo.ArgumentList.Contains('-pv') -and $StartInfo.ArgumentList.Contains('-pvr') -and
            -not $StartInfo.ArgumentList.Contains('-pd') -and $StartInfo.ArgumentList.Contains('-noshell') -and
            $StartInfo.ArgumentList.Contains('-sins') -and $StartInfo.ArgumentList.Contains('-netsyms:no') -and
            $StartInfo.ArgumentList.Contains('~* k 24; !locks; lm; qd') -and
            $StartInfo.ArgumentList.Contains('100')
        }
    }

    It 'fails an exit-zero capture without stack frames' {
        $script:debuggerProcess.HasExited = $true
        Mock Get-Content { return 'Cannot debug pid 100, Win32 error 0n87' }
        Test-NativeThrows { Invoke-NativeStackCapture $script:target 'C:\SDK\cdb.exe' 'C:\build' 'C:\logs\snapshot.log' 'C:\logs\stop.requested' } | Should Be $true
        Assert-MockCalled Write-NativeJson -Times 1 -Exactly -Scope It -ParameterFilter { $Value.Status -eq 'Failed' -and $Value.ExitCode -eq 0 }
    }

    It 'times out and kills only its retained nonsuspending CDB after sixty seconds' {
        $script:clockCalls = 0
        Mock Get-Date { $script:clockCalls++; return $script:created.AddSeconds(61 * ($script:clockCalls - 1)) }
        Mock Test-Path { return $false }
        Test-NativeThrows { Invoke-NativeStackCapture $script:target 'C:\SDK\cdb.exe' 'C:\build' 'C:\logs\snapshot.log' 'C:\logs\stop.requested' } | Should Be $true
        Assert-MockCalled Write-NativeJson -Times 1 -Exactly -Scope It -ParameterFilter { $Value.Status -eq 'TimedOut' }
        $script:debuggerProcess.Killed | Should Be $true
        $script:debuggerProcess.Disposed | Should Be $true
        $script:target.Killed | Should Be $false
    }

    It 'records a natural target exit during attach without claiming a captured stack or failing tests' {
        $script:debuggerProcess.HasExited = $true
        Mock Start-NativeCdbProcess { $script:target.HasExited = $true; return $script:debuggerProcess }
        Mock Get-Content { return 'Unable to examine process id 100, HRESULT 0x80004002' }
        Invoke-NativeStackCapture $script:target 'C:\SDK\cdb.exe' 'C:\build' 'C:\logs\snapshot.log' 'C:\logs\stop.requested' | Should Be 'TargetExited'
        Assert-MockCalled Write-NativeJson -Times 1 -Exactly -Scope It -ParameterFilter { $Value.Status -eq 'TargetExited' -and -not $Value.TargetStillRunning }
        $script:target.Killed | Should Be $false
    }

    It 'refuses to signal or terminate a reused watcher PID' {
        $script:watcherProcess = New-FakeNativeProcess 500 $script:created.AddSeconds(1) 'C:\PowerShell\pwsh.exe'
        Mock Open-NativeProcess { return $script:watcherProcess }
        Test-NativeThrows { Stop-NativeTestHostTrace 'C:\logs' } | Should Be $true
        Assert-MockCalled Set-Content -Times 0 -Exactly -Scope It
        $script:watcherProcess.Killed | Should Be $false
        $script:watcherProcess.Disposed | Should Be $true
    }

    It 'signals only the run-specific stop path for an exact owned watcher' {
        $script:watcherProcess = New-FakeNativeProcess 500 $script:created 'C:\PowerShell\pwsh.exe'
        $script:watcherProcess | Add-Member ScriptMethod WaitForExit { param($Milliseconds) $this.HasExited = $true; return $true } -Force
        Mock Open-NativeProcess { return $script:watcherProcess }
        Mock Get-Content { return '{"Status":"Stopped","Captures":0,"Message":"Stopped"}' } -ParameterFilter { $LiteralPath -like '*\state.json' }
        Stop-NativeTestHostTrace 'C:\logs'
        Assert-MockCalled Set-Content -Times 1 -Exactly -Scope It -ParameterFilter {
            $LiteralPath -eq 'C:\logs\native-host-trace-0123456789abcdef0123456789abcdef\stop.requested'
        }
        $script:watcherProcess.Killed | Should Be $false
    }
}
