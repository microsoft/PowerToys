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
feature enablement, reboot, UAC prompt or security-policy change. The opt-in Debug
pilot now explicitly attempts modern client setup **before** `Prepare`; installation
is not a fallback hidden in the tests. Win10 skips this step and keeps Legacy.
Scoped firewall rules, exact run identities, protected staging, standard-user
dispatch and the test's own prerequisite gates are unchanged.

The elevated `mwbSandboxExperiment.ps1 -Mode Prepare` step captures read-only
`prerequisite-admin.json` before artifact, feature or backend gates: feature/OS
state, all-users registrations (SID and install state), provisioned packages,
and an explicit-SID inventory for the interactive test account. Provisioned packages are
matched using DISM's `DisplayName` and full publisher-qualified `PackageName`,
not a bare name compared to `PackageName`. Query failures remain distinct from
confirmed absence.

The elevated report never executes another user's alias or claims user readiness.
The modern test records its actual Limited interactive identity, package and
alias checks, and the existing bounded `wsb --version` probe when reached.
The pipeline collects `mwb-preflight-<invocation-guid>\winapp-prerequisites.json`
recursively beneath the exact job's `ui-<job-prefix>` launcher directory, even
when fixture initialization fails before creating its ordinary run directory or
TRX. It verifies invocation/run identities and retains every current-job report
in the published `prerequisite-user.json`; other jobs are never searched.

An `always()` artifact step publishes only the allowlisted reports and a text
summary from `$(Common.TestResultsDirectory)\mwb-prerequisites-$(System.JobId)`.
Missing reports become explicit `NotChecked` placeholders. Query failures retain
stable codes and HRESULTs, not raw exception text, and never imply package
absence. User-report collection strips unapproved fields and private paths;
raw initializer logs/markers, process arguments and authentication state remain
private. Other-user registrations, staged-only, provisioned-only, and absent
inventories remain distinguishable. Both reports must be considered
before diagnosing image installation versus account registration. See
`src/modules/MouseWithoutBorders/MouseWithoutBorders.UITests/README.md` for the
full field list; collecting these diagnostics never itself installs, elevates, or enables features.

## Explicit modern client setup (Debug MWB pilot only)

`Install-MwbSandboxClient.ps1` uses an already elevated controller to stage a
read-only request and scripts, then dispatches a priority-4, `Interactive`,
`Limited` scheduled task to the **exact currently logged-on account** (the pool's
ShineTest account, including its actual suffix/SID). The worker rejects SYSTEM,
elevation, session zero, another account, and a missing Explorer desktop. There
are no passwords, UAC prompts, feature enablement, reboots, or Store/policy edits.

An already healthy current-user package from family
`MicrosoftWindows.WindowsSandbox_cw5n1h2txyewy`, a reparse-point `wsb.exe` alias,
and a successful, zero-exit `wsb --version` make the step a no-op. Otherwise:

* An existing Store/System-signed, non-development, healthy package may be
  [registered with `Add-AppxPackage -Register -DisableDevelopmentMode`](https://learn.microsoft.com/powershell/module/appx/add-appxpackage#example-3-add-a-disabled-app-package-in-development-mode).
  Exact family/full name, manifest identity, WindowsApps location, OS ownership,
  non-writable ACLs and absence of reparse points are checked. No downloaded or
  arbitrarily unpacked manifest is accepted.
* For an absent package, Microsoft's
[upgrade instructions](https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-versions#upgrading-to-the-newer-version)
  describe first launch through Start on Windows 11 24H2 KB10D or later, with the
  feature already enabled and Internet access to Microsoft Store/Windows Update.
  The usual update duration is 30 seconds–2 minutes. The automation invokes the
  Microsoft-signed inbox `WindowsSandbox.exe` with a fresh
  [documented `.wsb` configuration](https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file),
  only after verifying that this is the installed `.wsb` open association.
  **The docs do not promise an unattended install switch or that every image's
  first-launch updater honors a `.wsb` launch.** This path requires live validation
  on the selected pool image; registration/readiness, not launcher exit, proves
  completion. No guessed Store URLs, third-party mirrors or signature bypasses
  are used.

Setup refuses an existing Sandbox desktop/instance before mutation. A first-launch
guest receives only a job-private output mapping, writes the job GUID, then shuts
**itself** down via its configured logon command. No user Sandbox is adopted or
stopped. Setup samples client descendants during installation commands, readiness
polling and cleanup, retaining process handles rather than reopening PIDs to stop
them. Ownership requires the actual parent PID chain, births within each retained
parent's lifetime (including its exit time), the launcher's session, matching
snapshot/handle birth times, and exact Microsoft-signed System32 or verified
Store/System package executable paths. Only `WindowsSandbox.exe`,
`WindowsSandboxClient.exe` and `WindowsSandboxRemoteSession.exe` are eligible;
a server broker, system host, vmmem or mere post-launch appearance never qualifies.
Missing ancestry, inaccessible identity or unverified paths fail closed.

The elevated controller verifies WindowsApps ownership/ACLs and the Store package
manifest, then publishes an administrator-owned, read-only, run-bound package
proof in private staging. The Limited worker uses that proof for registration and
client image allowlisting; it does not need permission to read WindowsApps ACLs.
The inbox launcher's session is inherited from the verified worker, since PID-based
session queries can fail after that short-lived launcher exits.

After the **correct guest GUID and an empty provider inventory**, cleanup may
terminate retained, verified descendant client handles. The original owned
launcher remains subject to its existing bounded cleanup. Success still requires
both global desktop absence and an empty provider inventory after termination;
neither a hidden window nor an owned process exit replaces these gates.
Unconfirmed cleanup fails the step and
blocks `Prepare`, even if the package became ready. Store-managed background
updates are not cancelled. A leftover instance requires image/operator inspection,
not an automatic broad cleanup.

Installation polling is bounded to 300 seconds, registration to 120 seconds,
and version probes to 15 seconds. Cold first launch and guest self-shutdown
share an eight-minute budget: package registration can finish before the guest
reaches its logon command, so early readiness must not shorten that budget. The
controller has a 600-second task deadline with a 630-second scheduler limit;
the pipeline step has 12 minutes. Child output draining is separately bounded
(including inherited pipes). These bounds do not increase the test/CLI performance
envelopes. Scheduler completion and exit code must corroborate the worker report.
The task is unregistered and its protected per-job staging/DACL grants removed.

`client-setup.json` contains allowlisted before/after current-user readiness,
action, errors (codes/HRESULT only), guest/launcher cleanup and dispatch cleanup.
Cleanup failures distinguish remaining desktops, remaining instances, and provider
query failures. `Cleanup.RemainingProcesses` carries at most 64 remaining detection
records: allowlisted `Name`, numeric `PID`, `ParentPID`, `SessionId`, UTC `StartTime`
(or null when unavailable), and `VerifiedOwned`. This is detection evidence, not
permission to adopt those processes; full image paths and retained handles stay
private, and command lines are never queried. A setup failure also captures `client-setup-failure.png` from the
Limited user's desktop; capture failures are reported explicitly.
`client-setup-admin-before.json` and `client-setup-admin-after.json` contain the
separate schema-3 inventory. These are in the existing `always()` prerequisite
artifact even when installation fails before `Prepare` or no TRX exists.
An unreached setup produces a `NotChecked` placeholder. Process command lines,
provider output, package paths, Store logs and authentication state are not published.

On a disposable CI-equivalent **VM**, after ensuring there is no user Sandbox and
logging in the standard test account, run from the elevated agent identity:

```powershell
& .\.pipelines\Install-MwbSandboxClient.ps1 -Platform x64Win11 `
  -RunId ([guid]::NewGuid()) -ResultsDirectory C:\MwbSetupValidation
```

Check both zero task exit and `Status=Ready`, identity, before/after state, and
cleanup in `client-setup.json`; repeat with a new run GUID to verify `Action=None`.
Then run the unchanged MWB prerequisite/test flow. ARM64 uses `-Platform arm64`.
Do not run this installation/guest-launch validation on a physical workstation.

Focused inert Pester 3.4 coverage lives in
`tests\winappCliRelease.Tests.ps1`, `tests\mwbSandboxCiPreparation.Tests.ps1`,
`tests\mwbSandboxExperiment.Tests.ps1`, `tests\mwbReadyToRun.Tests.ps1`, and
`tests\mwbSandboxClientSetup.Tests.ps1`.
Preparing dependencies does not authorize queueing CI or running product UI.
