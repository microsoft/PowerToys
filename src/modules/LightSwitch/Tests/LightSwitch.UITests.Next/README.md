# Light Switch UI tests

This `.Next` suite retains the five legacy scenarios: theme shortcut and module
lifecycle, fixed-hour edits, sunrise/sunset offsets, manual coordinates, and real
Windows geolocation. The legacy `LightSwitch.UITests` project remains unchanged.

## Prerequisites and side effects

Run UI cases only on a **disposable, isolated, unlocked test desktop**, not a
working developer machine. Do not use the mouse or keyboard during execution.
The suite stops PowerToys, Settings, and the Light Switch service, enables only
Light Switch, changes its settings file, and changes the actual user's Windows
system/app themes and `ColorPrevalence`. It restores the original module settings
and theme values; the harness restores its global settings snapshot. Cleanup
cannot guarantee restoration if the test host is forcibly terminated.

The suite can accept **Yes** on the real Windows location-consent prompt.
**Windows location access may remain enabled after the run.** This is intentional;
location permission is not part of the theme/settings snapshot. Use an isolated
test account and machine where granting this permission is acceptable.

Required runtime setup:

- .NET 10 Windows Desktop Runtime, the repository-pinned winappcli, and a complete
  matching PowerToys runtime, including `WinUI3Apps` and `LightSwitchService`.
  Building this test project does not build the product.
- English Windows display language and English PowerToys Settings language
  (or its system-default setting). Use the same English regional formats for
  Settings and the test process; both 12-hour and 24-hour formats are supported.
  The class preflight checks the test cultures and `language.json` override.
- Working Windows location services and a real location provider for
  `TestGeolocationUpdate`. Missing permission or provider is a failure, never a
  skip or a manual-coordinate fallback.
- Compatible input integrity levels. CI runs in its elevated interactive account;
  ordinary local UI cases also support a non-elevated test desktop after an
  administrator provisions any required machine-level location prerequisites.
  `.pipelines\configureLightSwitchLocation.ps1` owns that CI prerequisite setup,
  not the test executable. Release Runner/Settings additionally require the
  existing authenticated Settings IPC companion-signing setup.

## Build and run

From the repository root, build with the existing repository tooling:

```powershell
tools\build\build.cmd -Path src\modules\LightSwitch\Tests\LightSwitch.UITests.Next -Platform x64 -Configuration Debug
$exe = '.\x64\Debug\tests\LightSwitch.UITests.Next\net10.0-windows10.0.26100.0\LightSwitch.UITests.Next.exe'
& $exe --filter 'TestCategory=LightSwitch' --report-trx --results-directory .\TestResults\LightSwitch
```

Use `-Platform ARM64` and the corresponding output directory for ARM64.
`--filter 'FullyQualifiedName~TestLightSwitchShortcut'` selects the focused
lifecycle/shortcut scenario. `--filter 'TestCategory=Unit'` runs only the pure
helper regressions without starting PowerToys, touching the registry, or using
the interactive desktop.

On UI failure, screenshots and state/UIA/settings evidence are captured before
cleanup; pipeline runs additionally capture recordings and product logs.
