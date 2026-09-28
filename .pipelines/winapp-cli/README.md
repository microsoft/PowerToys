# Official winapp CLI dependency

`release.json` is the single pin for the public Microsoft winapp CLI release used
by both `InstallWinAppCli.ps1` (the ordinary UI-test harness) and
`New-MwbSandboxCiArtifact.ps1` (the opt-in MWB experiment).
`WinAppCli.Common.ps1` downloads the architecture-specific ZIP from the immutable
release tag, verifies its published SHA256 **before extraction**, and rejects
changed layouts, native architectures or executable versions. Both callers
default to x64 and support native ARM64.

The v0.7.0 ZIPs contain six root files: `winapp.exe`, `libHarfBuzzSharp.dll`,
`libSkiaSharp.dll`, and their three PDBs. The deployed closure is the three native
binaries, not the debugging symbols. Their PE headers must have the requested
machine type and no managed CLR directory. No source checkout, private patch,
NativeAOT build, Visual Studio toolchain or dedicated CLI SDK is involved.
To update the dependency, review the official release assets, archive layout and
runtime closure; update the tag/version, both asset hashes and tests together.

## MWB build and test separation

The gated Debug build job publishes the ordinary product/test artifact unchanged,
adding `mwb-sandbox-ci` with `manifest.json`, `runtime.zip`,
`runtime.zip.manifest.json`, and `winapp-cli` containing the portable binaries.
The version-2 manifest records the PowerToys source revision, architecture, Debug
configuration, release repository/tag/version/asset/SHA256, and every deployed
file hash. Old private-preview manifests are deliberately rejected.

The lean runtime still includes Runner, MWB, helper, Settings and Quick Access.
Optional ReadyToRun compilation works only on private staging using the exact
`Microsoft.NETCore.App.Crossgen2.win-x64` package matching the **actual product
CLR**, including the ARM64 target JIT when appropriate. The normal build SDK
restores this package only if missing; it is independent of winapp CLI.
Compiler/runtime versions and commits, unchanged Debug attributes and IL, and
identical assembly copies remain verified. No SDK/compiler is needed on test
agents. Preparation output/work directories must be new and outside both the
source checkout and original product.

Test preparation verifies release and runtime provenance, then extracts the host
from the same archive sent to the guest. Runtime and CLI files are staged in an
administrator-owned, read-only run directory and rehashed before dispatch.
The standard harness and protected MWB CLI use separate paths but the same
official dependency pin. No product or UI is launched during build preparation.

## Windows Sandbox is a separate prerequisite

**winapp CLI is not the `MicrosoftWindows.WindowsSandbox` client package.**
Downloading this release does not install/register that Windows package or
enable the Sandbox feature. Win10 x64 keeps Legacy; Win11 x64/ARM64 require the
modern backend on 24H2 or newer. An Enabled feature alone is insufficient: the
Limited interactive test user must have the modern Sandbox package registered
and a trusted, responsive `wsb` provider.

Missing prerequisites remain `BLOCKED_INFRASTRUCTURE`, never a Legacy fallback,
feature enablement, reboot, client installation, UAC prompt or security-policy
change. Scoped firewall rules, exact run identities, protected staging, standard
user dispatch and bounded owned recovery are unchanged.

The elevated `mwbSandboxExperiment.ps1 -Mode Prepare` step always captures and
publishes a read-only `mwb-prerequisite-report.json` diagnostic (feature state,
an all-users/provisioned Sandbox package inventory, and the exact interactive
test user's package/alias/bounded-`wsb --version` state) before any prerequisite
throw, distinguishing a confirmed-absent result from a query failure. See
`src/modules/MouseWithoutBorders/MouseWithoutBorders.UITests/README.md` for the
full field list; this never itself installs, elevates, or enables features.

Focused inert Pester 3.4 coverage lives in
`tests\winappCliRelease.Tests.ps1`, `tests\mwbSandboxCiPreparation.Tests.ps1`,
`tests\mwbSandboxExperiment.Tests.ps1`, and `tests\mwbReadyToRun.Tests.ps1`.
Preparing dependencies does not authorize queueing CI or running product UI.
