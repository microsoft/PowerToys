# Agentic local-VM UI-test loop

This loop assumes the test project follows `Microsoft.PowerToys.UITest.Next`, builds as a
Microsoft.Testing.Platform executable, and has already passed the `ui-tests-migration` design and
CI-stability checks.

Use Windows 10 Enterprise LTSC 2021 as the default guest and run the full baseline there. If the
requirements explicitly include Windows 11 behavior, run only those checks again in a separate
Windows 11 VM after the Windows 10 pass.

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

The exchange must be below `<VmRoot>\shared` so the controller can map it to `\\host.lan\Data`:

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
  -VmRoot X:\PowerToysUiTestVm `
  -ExchangeRoot X:\PowerToysUiTestVm\shared\PowerToysUiTests\<Module> `
  -TestExecutable <Module>.UITests.Next.exe `
  -Filter 'Name=<focused-test>' `
  -BuildLabel (git rev-parse HEAD) `
  -PlanOnly
```

Check:

- `ExchangeRoot` in the request is a UNC under `\\host.lan\Data`.
- Test, product, winappcli, and .NET hashes are present.
- The filter uses `Name=`, `Name~`, `FullyQualifiedName~`, or `TestCategory=`.
- No password, token, or source path appears in the request.

## 4. Run one focused test

Use the default VM resource profile (4 vCPUs and 8 GB RAM) while creating and stabilizing tests. Do
not begin on the constrained profile: first prove the test and product behavior with sufficient CPU
and RAM.

```pwsh
pwsh .github\skills\ui-tests-local-vm\scripts\Invoke-LocalVmUiTest.ps1 `
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
- The guest UNC exchange is accessible.

Failure here is `BLOCKED`, not a test failure.

For an explicitly Windows 11-only check, point the same controller at a separate Windows 11 VM root
and exchange, use `-Platform x64Win11`, and apply a narrow filter. Do not run the ordinary suite only
on Windows 11, and do not reuse the Windows 10 volume as the Windows 11 guest.

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
- Zero selected tests/MTP exit code 8 is `BLOCKED`.
- Missing desktop, WinRM, share, archive, or status is `BLOCKED`.
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

## 7. Widen to the suite

Use a bounded category filter on the Windows 10 guest:

```pwsh
-Filter 'TestCategory=<Module>' -SuiteTimeout 45m -TimeoutMinutes 60
```

Report:

- Executed, passed, failed, and error counts.
- Exact pass rate.
- Root-cause groups, not only test names.
- Guest user/session/display and payload fingerprint.
- Export errors independently from assertion failures.

After reporting the Windows 10 baseline, run any Windows 11-specific subset against its independent
Windows 11 baseline and report that evidence separately.

Once the complete target suite is green, restart the same VM with `-ResourceProfile Constrained`
(1 vCPU and 4 GB RAM) and repeat the focused-to-suite progression. Keep the default-profile TRX as
the correctness baseline and classify failures that appear only under constrained resources
separately.

## 8. Confirm clean-profile behavior

A retained VM accumulates registry state, caches, thumbnail databases, WebView profiles, Settings,
and first-run suppressions. Choose one final confirmation based on risk:

- Restore a known stopped-volume snapshot.
- Create a new named volume and reinstall from the OEM baseline.

Do not call a retained run clean merely because the product archive was refreshed.

## Revision comparison

Hold VM volume, Windows build, display, account, tools, filter, and timeouts constant. Change only the
intentional test/product archive and record both fingerprints. For a clean-baseline comparison,
restore the same volume snapshot before each revision.
