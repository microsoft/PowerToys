# Agentic local-VM UI-test loop

This loop assumes the test project follows `Microsoft.PowerToys.UITest.Next`, builds as a
Microsoft.Testing.Platform executable, and has already passed the `ui-tests-migration` design and
CI-stability checks.

Run the full suite twice: on a Windows 10 Enterprise LTSC 2021 guest first, then the same unfiltered
suite on a separate Windows 11 guest. Both must be green. Windows 11 is not a filtered follow-up -
tests with no Windows 11 content still fail there for shell, compositor, and timing reasons.

## 1. Build on the host

Build only; do not launch PowerToys or tests on the host when the task forbids it.

```pwsh
tools\build\build.cmd `
  -Path src\modules\<Module>\Tests\<Module>.UITests.Next `
  -Platform x64 -Configuration Debug

git rev-parse HEAD
```

Exit code 0 is required. Record the build label before packaging.

## 2. Create a lean exchange

The exchange can be any host folder. Keeping it under the scaffold's `shared` folder keeps VM assets
together and out of the repository; the controller mirrors it into the guest at
`C:\PowerToysUiTestExchange\<name>`:

```text
<VmRoot>\shared\PowerToysUiTests\<Module>\
|-- ui-tests.zip
|-- powertoys-runtime.zip
|-- winappcli.zip
|-- dotnet-runtime.zip
|-- product-overlay.zip                         # optional
|-- MicrosoftEdgeWebView2RuntimeInstallerX64.exe # optional
`-- LocalVmResults\
```

The controller writes each request and its durable evidence under `LocalVmResults`.

Package archive contents directly:

```pwsh
Compress-Archive -Path '<test-output>\*' `
  -DestinationPath '<exchange>\ui-tests.zip' -Force
Compress-Archive -Path '<product-runtime>\*' `
  -DestinationPath '<exchange>\powertoys-runtime.zip' -Force
```

**Build the product runtime in Release for any shell-extension test.** The runtime context-menu
registration for Image Resizer, File Locksmith, New+, and PowerRename is compiled behind
`#if defined(ENABLE_REGISTRATION) || defined(NDEBUG)`, so a **Debug** runtime silently omits it: the
module enables and logs normally, but the entry never appears in Explorer and menu assertions fail
with no obvious cause (for example, "Explorer did not show 'Resize with Image Resizer'"). CI ships
Release for this reason. If you must validate against a Debug runtime, rebuild only the affected
module DLL with `ENABLE_REGISTRATION` defined and overlay it via `product-overlay.zip`.

Use the repository-pinned winappcli build and a private .NET runtime matching the test executable.
Even when .NET 10 is installed in the VM baseline, the private runtime remains the default for
reproducibility and revision comparison.

The controller copies its bundled `templates/run-ui-tests.ps1`, computes per-component SHA-256
hashes, writes a run-specific request, and never maps the repository or build output directly into
Windows.

## 3. Validate the plan

Always run the first request with `-PlanOnly`:

```pwsh
pwsh .github\skills\ui-tests-local-vm\scripts\Invoke-LocalVmUiTest.ps1 `
  -VmName PowerToysUiTest-Win10 `
  -VmRoot X:\PowerToysUiTestVm `
  -ExchangeRoot X:\PowerToysUiTestVm\shared\PowerToysUiTests\<Module> `
  -TestExecutable <Module>.UITests.Next.exe `
  -Filter 'Name=<focused-test>' `
  -BuildLabel (git rev-parse HEAD) `
  -PlanOnly
```

Check:

- `GuestExchangeRoot` in the plan is a path under `C:\PowerToysUiTestExchange`.
- Test, product, winappcli, and .NET hashes are present.
- The filter uses `Name=`, `Name~`, `FullyQualifiedName~`, or `TestCategory=`.
- No password, token, or source path appears in the request.

## 4. Run one focused test

Use the default VM resource profile (4 vCPUs and 8 GB RAM) while creating and stabilizing tests. Do
not begin on the constrained profile: first prove the test and product behavior with sufficient CPU
and RAM.

Run the controller synchronously in the foreground and keep the active agent turn attached. It
requests automatic host sleep prevention, reports `HostSleepPrevented`, and returns only after
matching status/TRX evidence or a bounded controller failure. It cannot override manual sleep,
lid-close policy, reboot, or power loss.

```pwsh
pwsh .github\skills\ui-tests-local-vm\scripts\Invoke-LocalVmUiTest.ps1 `
  -VmName PowerToysUiTest-Win10 `
  -VmRoot X:\PowerToysUiTestVm `
  -ExchangeRoot X:\PowerToysUiTestVm\shared\PowerToysUiTests\<Module> `
  -TestExecutable <Module>.UITests.Next.exe `
  -Filter 'Name=<focused-test>' `
  -Platform x64Win10 `
  -BuildLabel (git rev-parse HEAD) `
  -DesktopWidth 1920 -DesktopHeight 1080 `
  -SuiteTimeout 15m -TimeoutMinutes 25
```

Before the guest runner starts, the controller dispatches a probe into the interactive account and
requires:

- User is the configured standard user and is not an administrator.
- Session ID is greater than zero.
- Explorer exists in that session.
- Display dimensions match the request, unless both are zero.
- The guest exchange folder is accessible.

The probe and test tasks execute under the provisioned PowerShell 7 `pwsh.exe`. The narrow
PowerShell Direct control channel remains inbox Windows PowerShell 5.1; no remoting endpoint or
firewall rule is enabled for PS7.

Failure here is `BLOCKED`, not a test failure.

Iterate on Windows 10 first; it is the faster loop. Point the same controller at the separate
Windows 11 VM and exchange with `-Platform x64Win11` for the second pass. Do not reuse the Windows 10
guest disk as the Windows 11 guest.

## 5. Parse evidence

Each run writes:

```text
LocalVmResults\localvm-<timestamp-guid>\
|-- controller-plan.json
|-- request.json
|-- desktop-probe.ps1
|-- desktop-probe.json
|-- progress.json
|-- status.json
|-- local-vm-ui-tests.log
`-- TestResults\
    |-- <suite>.trx
    `-- <logs, screenshots, recordings, attachments>
```

The controller prints scalar TRX counters and per-test outcomes. Read both `status.json` and TRX:

- Assertion-bearing TRX failures are `FAIL`.
- Skipped, inconclusive, or otherwise `NotExecuted` tests are `FAIL`; require `total > 0` and
  `executed == total` even when the test process exits 0.
- Zero selected tests/MTP exit code 8 is `BLOCKED`.
- Missing desktop, control channel, archive, or status is `BLOCKED`.
- A guest task that never starts or exits without matching status is `BLOCKED` immediately; do not
  wait out the full suite timeout or accept task exit as completion.
- Proven display/profile/compositor differences are `ENVIRONMENT`.
- An N/M pass rate proves the execution loop ran, even when the task did not ask to stabilize tests.

Do not modify an already stabilized suite merely because the local VM differs from CI. Report the
pass rate and group failures by controlling boundary first.

## 6. Iterate incrementally

After changing tests or product code:

1. Build the touched project to exit code 0.
2. Replace only the corresponding archive.
3. Rerun the same focused filter with `-ReuseStagedPayload`.
4. Confirm `RefreshedComponents` contains only the changed component.
5. Widen only after the focused behavior is understood.

```pwsh
pwsh .github\skills\ui-tests-local-vm\scripts\Invoke-LocalVmUiTest.ps1 `
  <same parameters> `
  -ReuseStagedPayload
```

The guest manifest persists under `C:\PowerToysUiTestRun`. Unchanged tests/product/winappcli/.NET
are not extracted again. WebView2 and other baseline tools remain installed.

The VM stays running after each run. Use `-SkipStart` when it is already healthy, and
`-StopVmAfterRun` only when no further iteration is expected.

## 7. Widen to the suite, on both guests

Use a bounded category filter, first on the Windows 10 guest:

```pwsh
-Filter 'TestCategory=<Module>' -SuiteTimeout 45m -TimeoutMinutes 60
```

Report:

- Executed, passed, failed, and error counts.
- Exact pass rate.
- Root-cause groups, not only test names.
- Guest user/session/display and payload fingerprint.
- Export errors independently from assertion failures.

Then run the **same** filter against the Windows 11 guest with `-Platform x64Win11` and report that
evidence separately. Narrowing the Windows 11 run to Windows 11-specific tests does not satisfy this
step. The module is done only when both suites are fully green; a Windows 10 pass with an unrun or
red Windows 11 suite is an incomplete result, not a success.

Once the complete target suite is green, stop the guest, then let the controller restart it with the
`Constrained` profile (1 vCPU and 4 GB RAM). Pass the guest's config explicitly when one VM root owns
multiple guests:

```pwsh
pwsh <VmRoot>\Stop-LocalVm.ps1 -ConfigPath <VmRoot>\vm.config.win10.psd1

pwsh .github\skills\ui-tests-local-vm\scripts\Invoke-LocalVmUiTest.ps1 `
  -VmName PowerToysUiTest-Win10 `
  -ConfigurationPath <VmRoot>\vm.config.win10.psd1 `
  -ResourceProfile Constrained `
  -VmRoot <VmRoot> -ExchangeRoot <exchange> `
  -TestExecutable <Module>.UITests.exe `
  -Filter 'TestCategory=<Module>' -Platform x64Win10 `
  -ReuseStagedPayload
```

`Invoke-LocalVmUiTest.ps1` rejects a config whose `VmName` does not match, records the resource
profile in the request/result, and passes it to `Start-LocalVm.ps1`. Resource changes only apply
while the VM is off; stopping first is therefore required. Repeat separately on Windows 11. Keep the
default-profile TRX as the correctness baseline and classify failures that appear only under
constrained resources separately.

## 8. Confirm clean-profile behavior

A retained VM accumulates registry state, caches, thumbnail databases, WebView profiles, Settings,
and first-run suppressions. Choose one final confirmation based on risk:

- Restore the baseline checkpoint with `Reset-LocalVm.ps1 -Restore`. A standard checkpoint includes
  memory, so this returns to the captured logged-on desktop in seconds.
- Rebuild the guest from media with `New-UiTestVm.ps1 -Force` when the checkpoint itself is suspect.

Do not call a retained run clean merely because the product archive was refreshed.

## Revision comparison

Hold the guest, Windows build, display, account, tools, filter, and timeouts constant. Change only the
intentional test/product archive and record both fingerprints. For a clean-baseline comparison,
restore the same checkpoint before each revision.
