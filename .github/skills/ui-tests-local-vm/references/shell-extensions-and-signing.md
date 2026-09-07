# Shell extensions & the CI signing constraint

Read this before writing or verifying UI tests for any module with a **shell extension**
(context menu, preview handler, thumbnail provider, drag-drop handler). It is the knowledge that is
*not* obvious from the test framework and caused the most trial-and-error.

## The one fact that matters most

**CI PR-validation builds are UNSIGNED** (`codeSign:false`). Any test that depends on a
**sparse-MSIX-packaged** shell extension gets **0% on CI**, because the package cannot register
(`0x800B0100 TRUST_E_NOSIGNATURE`). A test that passes on your machine (where you self-signed or
installed a signed build) can therefore fail 100% on CI.

## Preferred fix: sign the MSIX on CI and force-trust it

The workarounds below (driving the classic menu, launching the exe directly) let a test pass on an
unsigned build, but they *do not exercise the modern Win11 tier-1 context menu* — the surface real
users see. The faithful fix is to give CI a genuinely **signed** package plus a **test-only trusted
root**, so registration succeeds and the tests drive the real end-user workflow. This is legitimate
because the trust anchor is scoped to the test agent and asserts no security — it just makes Windows
treat the CI-built package as sideload-installable, exactly like a developer self-signing locally.

**Why signing (not Developer Mode) is required.** PowerToys registers each sparse package at
module-enable time with `PackageManager.AddPackageByUriAsync` (`src/common/utils/package.h`
`RegisterSparsePackage`) — a *packaged* deployment that demands a signature chaining to a trusted
root. Developer Mode / register-by-loose-manifest would only help if the product code called
`AddPackage -Register AppxManifest.xml`, which it does not. So the only route is: **sign the `.msix`
and trust the signer.**

**Mechanism** (all three steps must happen *before* the module is enabled):

1. Create a self-signed **code-signing** cert whose subject **exactly equals** the manifest
   `Publisher` — every PowerToys context-menu package and the CmdPal `PowerToysSparse.msix` use
   `CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US`. The private
   key is generated on the agent and never leaves it; nothing is committed.
2. **Force-trust** it via the **machine** stores: import the public cert into `LocalMachine\Root`
   **and** `LocalMachine\TrustedPeople` (a self-signed leaf is its own root, and AppX sideload
   consults TrustedPeople). These import silently, and the CI test agent is elevated (it installs
   machine-level), so it can write them. Do **not** import into `CurrentUser\Root` — the user Root
   store raises a CryptoAPI consent dialog that fails non-interactively (`UI is not allowed in this
   operation`), even when elevated. `CurrentUser\TrustedPeople` is silent and fine as an extra for
   per-user deployment; a non-elevated run that cannot write `LocalMachine\Root` cannot establish
   machine root trust silently.
3. `signtool sign /fd SHA256` every sparse `.msix` the product will register.

A ready-to-use, publisher-aware implementation ships with this skill:
[`.pipelines/signSparsePackages.ps1`](../../../../.pipelines/signSparsePackages.ps1). It reads each
package's manifest publisher, mints/reuses+trusts a matching cert, and signs only packages that are
not already validly signed (so real framework packages like VCLibs are left alone). Point it at
whichever tree hosts the packages:

```powershell
# buildNow (run-in-place) — sign the packages in the downloaded build tree:
.\.pipelines\signSparsePackages.ps1 -PackageRoot "$(Pipeline.Workspace)\$(TestArtifactsName)"

# installed (buildNowSlim / official) — sign after install, before the test enables the module.
# Machine install lands in %ProgramFiles%\PowerToys; per-user install in %LOCALAPPDATA%\PowerToys:
.\.pipelines\signSparsePackages.ps1 `
  -PackageRoot "$env:ProgramFiles\PowerToys","$env:LOCALAPPDATA\PowerToys" `
  -RequiredPackage 'ImageResizerContextMenuPackage.msix'

# local UI-test VM sideload — sign the deployed runtime:
.\.pipelines\signSparsePackages.ps1 -PackageRoot "C:\PowerToysUiTestRun\PowerToys"
```

**Where it is wired in CI.** This runs in `.pipelines/v2/templates/job-test-project.yml` after the
download/install steps and before **Run UI Tests**. It recursively searches the run-in-place artifact
and complete machine/per-user install roots. Windows 11/ARM64 Image Resizer, PowerRename, and
all-module jobs pass their context-menu MSIX names through `-RequiredPackage`, so missing, unsigned,
or untrusted setup fails at the prerequisite instead of surfacing later as a product-test failure.
Jobs that do not exercise either modern context menu keep signing best-effort:

PowerRename jobs also pass `PowerToys.exe` and `PowerToys.Settings.exe` through
`-RequiredAuthenticodeFile` on every platform. Release IPC accepts only a Microsoft-named signer
anchored in LocalMachine Root; unsigned PR binaries otherwise let the Settings toggle change
visually while the runner rejects the command as `not-microsoft-signed`. The same disposable-agent
test identity satisfies that authentication path without weakening the product policy. The job
records the exact thumbprint in a durable agent-work-folder marker. It processes stale markers before
signing, then removes the certificate from LocalMachine/User trust stores and CurrentUser\My
(including its private key) in an `always()` cleanup step. Failed cleanup keeps the marker for the
next job, so neither interruption nor agent reuse loses the recovery record.

```yaml
  - pwsh: |
      $roots = @(
        "$(Pipeline.Workspace)\$(TestArtifactsName)",
        "$env:ProgramFiles\PowerToys",
        "$env:LOCALAPPDATA\PowerToys")
      # Build the platform/module-specific arrays as shown in job-test-project.yml.
      $requiredPackages = @('PowerRenameContextMenuPackage.msix')
      $requiredAuthenticodeFiles = @('PowerToys.exe', 'PowerToys.Settings.exe')
      if ($requiredPackages.Count -gt 0 -or $requiredAuthenticodeFiles.Count -gt 0) {
        $signingArguments = @{
          PackageRoot = $roots
          CertificateMarkerPath = "$(Agent.WorkFolder)\PowerToysUiTestState\SigningCertificates.txt"
        }
        if ($requiredPackages.Count -gt 0) {
          $signingArguments.RequiredPackage = $requiredPackages
        }
        if ($requiredAuthenticodeFiles.Count -gt 0) {
          $signingArguments.RequiredAuthenticodeFile = $requiredAuthenticodeFiles
        }
        & "$(build.sourcesdirectory)\.pipelines\signSparsePackages.ps1" @signingArguments
      } else {
        try {
          & "$(build.sourcesdirectory)\.pipelines\signSparsePackages.ps1" `
            -PackageRoot $roots `
            -CertificateMarkerPath "$(Agent.WorkFolder)\PowerToysUiTestState\SigningCertificates.txt"
        }
        catch { Write-Host "##vso[task.logissue type=warning]Sparse MSIX signing skipped: $($_.Exception.Message)" }
      }
    displayName: "Sign sparse MSIX packages (test trust)"
```

**Prerequisite:** `signtool.exe`. The script finds it across PATH, any `Windows Kits` install (all
versions, plus the App Certification Kit). As a fallback it verifies and freshly extracts the exact
repository-pinned `Microsoft.Windows.SDK.BuildTools` `.nupkg` from the NuGet cache, or downloads that
same version when absent. It verifies the NuGet author/repository signatures and refuses to run a
`signtool.exe` without a valid Microsoft Authenticode signature. An agent without the SDK therefore
still works without trusting an unversioned or stale extracted tool. Verified end-to-end in a local Win11
VM — the unsigned package fails `Add-AppxPackage` / `AddPackageByUriAsync` with `0x800B0100`, and
after `signSparsePackages.ps1` signs it and the cert is force-trusted (`LocalMachine\Root` +
`TrustedPeople`) the same registration succeeds and the package appears in `Get-AppxPackage`.

**Caveat — CmdPal at install time.** The installer's custom action stages/registers
`PowerToysSparse.msix` *during* install (`installer/PowerToysSetupCustomActionsVNext/CustomAction.cpp`),
before any test-time signing step runs. Signing after install still covers every module that
registers at **enable** time (ImageResizer / PowerRename / FileLocksmith / NewPlus). If CmdPal's own
packaged registration is the thing under test on an installed build, the package must instead be
signed at **build** time (self-sign in the build stage and publish the public `.cer` for the test
stage to trust) — the run-in-place `buildNow` path avoids this because nothing registers until the
test enables it.

With this in place a test can drive the modern surface directly on CI. `ModernRegistered()` (below)
becomes a portability guard for *unsigned* environments rather than a reason to avoid the modern menu.

## Two shell-extension tiers

| Tier | Mechanism | Signing | Unsigned CI PR build |
|---|---|---|---|
| **Modern** (Win11 tier-1 context menu, `IExplorerCommand`) | sparse **MSIX** package | **required** | ❌ cannot register |
| **Classic** ("Show more options", Win10 default menu, most preview/thumbnail handlers) | **registry COM** (`HKCU\Software\Classes\...\SystemFileAssociations\<ext>\ShellEx\...`) | none | ✅ works |

**Rule:** you have two options on an unsigned CI build. **(A, preferred)** sign the modern package
and force-trust it (see *Preferred fix* above) so the test drives the real Win11 tier-1 menu.
**(B, fallback)** drive the **signing-free surface** — the classic COM menu, or launch the module exe
directly with the files (the PowerRename pattern; the exe often has a CLI/`FilesArgument`). If you
take the fallback, only assert the modern/tier-1 surface when the package is *actually* registered,
so it runs on signed/official/installed builds only:

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
