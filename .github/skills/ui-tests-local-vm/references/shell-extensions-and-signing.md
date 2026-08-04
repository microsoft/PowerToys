# Shell extensions & the CI signing constraint

Read this before writing or verifying UI tests for any module with a **shell extension**
(context menu, preview handler, thumbnail provider, drag-drop handler). It is the knowledge that is
*not* obvious from the test framework and caused the most trial-and-error.

## The one fact that matters most

**CI PR-validation builds are UNSIGNED** (`codeSign:false`). Any test that depends on a
**sparse-MSIX-packaged** shell extension gets **0% on CI**, because the package cannot register
(`0x800B0100 TRUST_E_NOSIGNATURE`). A test that passes on your machine (where you self-signed or
installed a signed build) can therefore fail 100% on CI.

## Two shell-extension tiers

| Tier | Mechanism | Signing | Unsigned CI PR build |
|---|---|---|---|
| **Modern** (Win11 tier-1 context menu, `IExplorerCommand`) | sparse **MSIX** package | **required** | ❌ cannot register |
| **Classic** ("Show more options", Win10 default menu, most preview/thumbnail handlers) | **registry COM** (`HKCU\Software\Classes\...\SystemFileAssociations\<ext>\ShellEx\...`) | none | ✅ works |

**Rule:** drive the **signing-free surface** on CI — the classic COM menu, or launch the module exe
directly with the files (the PowerRename pattern; the exe often has a CLI/`FilesArgument`). Only
assert the modern/tier-1 surface when the package is *actually* registered, so it runs on
signed/official/installed builds only:

```csharp
private static bool ModernRegistered() =>
    new Windows.Management.Deployment.PackageManager()
        .FindPackagesForUser(string.Empty)
        .Any(p => p.Id.Name.Contains("<PackageName>", StringComparison.OrdinalIgnoreCase));
```

Then: `OpenContextMenu(useClassicMenu: !ModernRegistered())`, and gate modern assertions behind
`if (ModernRegistered())`.

## Debug vs Release — why local != CI

- The classic registry-COM handler is usually gated `#if defined(ENABLE_REGISTRATION) || defined(NDEBUG)`,
  so it is **compiled out of local Debug builds** and present only in CI **Release** (`NDEBUG`). To
  exercise the classic menu against a **local Debug** runtime, rebuild the extension DLL with
  `ENABLE_REGISTRATION` added to its `<PreprocessorDefinitions>`.
- The sparse `.msix` in build output is **unsigned**; registering it locally needs a self-signed cert
  whose subject == the package `Publisher` **and** that cert trusted (admin). CI does neither.
- Both handlers typically **self-gate on the module's enabled flag** at query time (classic
  `QueryContextMenu` returns `E_FAIL`, modern `GetState` returns `ECS_HIDDEN`), so the entry tracks
  the Settings toggle without re-registration.
- Modules register handlers at **enable time** (runtime), and an **already-running Explorer will not
  surface a freshly-registered handler until the shell restarts** — restart `explorer.exe` once after
  enabling (see PreviewPane / File Explorer add-ons tests).

## Reproduce CI's classic scenario on a local (signed) VM

1. Rebuild the extension DLL with `ENABLE_REGISTRATION` and deploy it into the guest runtime
   (`C:\PowerToysUiTestRun\PowerToys\WinUI3Apps\`).
2. Neutralize the sparse package so `enable()` cannot register it: rename its `.msix` and
   `Get-AppxPackage *<Package>* | Remove-AppxPackage -AllUsers`. This mirrors CI's unsigned failure.
3. The `ModernRegistered()` detection now returns false → the tests drive the classic menu exactly as
   CI does. Use `Invoke-GuestScript.ps1` for the guest-side steps.

## Slow-agent / cross-arch robustness

Shell-extension tests are especially prone to slow-agent and ARM64-only races that you often cannot
reproduce on a local VM (even constrained to 1 core, see customization.md — you cannot emulate ARM64
on an x64 host, so reason from the failure video/screenshot). The robustness patterns — re-select
before every attempt, retryable transient popups, verify fixtures reached disk, and slow-path
timeouts — are test design and live with the rest of the Explorer/shell test guidance in
[ui-tests-migration explorer-shell-tests.md](../../ui-tests-migration/references/explorer-shell-tests.md).
