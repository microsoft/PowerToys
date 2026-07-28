# Agentic UI-test loop

This workflow assumes the UI-test project uses `Microsoft.PowerToys.UITest.Next`, produces a
Microsoft.Testing.Platform executable, and already builds to exit code 0.

## 1. Build first

Build the product and focused test project with the repository scripts. Do not package a dirty or
unknown output folder without recording its commit/build label.

```pwsh
tools\build\build.cmd `
  -Path src\modules\<Module>\Tests\<Module>.UITests.Next `
  -Platform x64 `
  -Configuration Debug

git rev-parse HEAD
```

Use the output under `x64\Debug\tests\<Module>.UITests.Next\...`. Hash the compiled test DLL or
archive when comparing revisions; .NET apphost EXE hashes can be identical across builds.

## 2. Create a lean exchange

Use a dedicated directory outside the repository:

```text
C:\Temp\PowerToysSandbox\<Module>\
|-- run-ui-tests.ps1
|-- ui-tests.zip
|-- powertoys-runtime.zip
|-- winappcli.zip
|-- dotnet-runtime.zip
|-- MicrosoftEdgeWebview2Setup.exe   # optional
|-- product-overlay.zip              # optional revision overlay
`-- SandboxResults\                  # host-persistent output
```

Copy [../templates/run-ui-tests.ps1](../templates/run-ui-tests.ps1) as `run-ui-tests.ps1`.

Why archives instead of thousands of mapped files:

- Dynamic sharing and guest login stay fast.
- Product/tests run from guest-local storage, not SMB-backed paths.
- One archive hash identifies the exact tested payload.
- The exchange contains no source tree, package cache, or unrelated user files.

## 3. Stage prerequisites

### Product and tests

Package the **contents** of the product runtime root and test output root so extraction creates the
expected layout directly:

```pwsh
Compress-Archive -Path '<test-output>\*' -DestinationPath '<exchange>\ui-tests.zip'
Compress-Archive -Path '<product-root>\*' -DestinationPath '<exchange>\powertoys-runtime.zip'
```

`POWERTOYS_INSTALL_DIR` points at the extracted product root. It must contain `PowerToys.exe`,
`WinUI3Apps\PowerToys.Settings.exe`, and the module binaries required by the test.

For a historical comparison, keep the common runtime base constant and stage only the historical
tests plus a `product-overlay.zip` containing changed product files. Record the exact commit in
`BuildLabel` and verify archive hashes before launch.

### winappcli

Use the same pinned winappcli version as the UI-test pipeline. `.pipelines/InstallWinAppCli.ps1`
contains the repository's download/hash policy. Package its extracted directory as `winappcli.zip`.

### Private .NET runtime

Stage the runtime required by the generated test app as `dotnet-runtime.zip`; the guest script sets
`DOTNET_ROOT` and prepends it to `PATH`. Keep SDKs and unrelated packs out of the archive.

### WebView2

WebView/Monaco tests need a runtime that a clean Sandbox may not contain. Stage the signed repository
bootstrapper:

```pwsh
Copy-Item `
  installer\PowerToysSetupVNext\WebView2\MicrosoftEdgeWebview2Setup.exe `
  '<exchange>\MicrosoftEdgeWebview2Setup.exe'
```

Pass `-InstallWebView2`. The guest installs it silently before the test. Do not map the host's
installed WebView2 directory; it can exceed 800 MB and delay or prevent guest login.

## 4. Run

```pwsh
pwsh .github\skills\windows-sandbox-ui-tests\scripts\Invoke-SandboxUiTest.ps1 `
  -ExchangeRoot '<exchange>' `
  -TestExecutable '<Module>.UITests.Next.exe' `
  -Filter 'TestCategory=<Module>' `
  -Platform x64Win11 `
  -BuildLabel (git rev-parse HEAD) `
  -InstallWebView2 `
  -SuiteTimeout 2h `
  -TimeoutMinutes 150
```

The controller:

1. Refuses overlapping Sandboxes.
2. Launches `Microsoft.Windows.Containers.Sandbox` through Start.
3. Waits for the new environment ID and `ExistingLogin` readiness.
4. Dynamically shares the exchange as `C:\SandboxExchange`.
5. Creates a run-specific request and dispatches the guest runner hidden.
6. Streams progress while waiting for `status.json`.
7. Parses the TRX counters.
8. Stops the exact Sandbox in `finally`.

MSTest filters need an explicit property. Use `Name=...`, `Name~...`,
`FullyQualifiedName~...`, or `TestCategory=...`; a bare display name can select zero tests.
The guest template defensively treats a bare value as `Name=<value>`, but verify the exact selection
before packaging so a typo does not cost a Sandbox cycle:

```pwsh
& '<test-output>\<Module>.UITests.Next.exe' --list-tests --filter 'Name=<display-name>'
```

Pass module-specific child process names through `-CleanupProcess`; the template always stops the
PowerToys runner, Settings, and winapp, then stops the additional names before exporting results.

## 5. Result contract

Each run writes:

```text
SandboxResults\<timestamp-guid>\
|-- request.json
|-- progress.json
|-- status.json
|-- sandbox-ui-tests.log
`-- TestResults\
  |-- sandbox-ui-tests.trx
  `-- <MSTest artifacts and failure attachments>
```

`status.json` includes:

- `Status`: `PASS` or `FAIL` from the test process exit code.
- `ExitCode`.
- `Error`: provisioning/runner exception, if any.
- `BuildLabel`, `Filter`, `Platform`, and `RunId`.
- Guest user/session/OS and start/completion timestamps.

Always read the TRX for per-test outcomes and failure messages. The process exit code alone cannot
distinguish assertion failures, zero tests, global timeout, or infrastructure errors.
Classify zero tests/MTP exit code 8 as `BLOCKED`, not a product failure. Classify a nonzero run with
assertion-bearing TRX results as `FAIL`.

## 6. Iterate with discipline

1. Run one deterministic test.
2. Identify the first authoritative signal that failed.
3. Make the smallest test/framework change.
4. Rebuild and recreate the test archive.
5. Rerun the focused test in a fresh Sandbox.
6. Widen to the module suite only after the focused check passes.

Choose timeout budgets from the selected scope. Keep the host deadline larger because it includes
Sandbox startup, archive extraction, prerequisite installation, test execution, and result export.

| Scope | Guest `SuiteTimeout` | Host `TimeoutMinutes` |
|---|---:|---:|
| One focused test | `15m` | `25` |
| One module suite | `45m` | `60` |
| Broad project/all selected UI tests (defaults) | `2h` | `150` |

These are starting budgets, not hard limits. Increase both for a known longer project run; the host
parameter accepts up to 1440 minutes. Shorten both during focused iteration so a real hang fails
promptly.

## 7. Compare revisions fairly

Hold these inputs constant:

- PowerToys runtime base, except the intentional overlay.
- winappcli, .NET runtime, WebView2 provisioning.
- Sandbox launch route, display, platform value, filter, and timeouts.
- Test assets and visual thresholds, unless the revision intentionally changes them.

Change only the test/product archives under comparison. Record commit labels and hashes in the result
folder. Report pass rate plus root-cause groups; do not compare only elapsed time or process exit code.