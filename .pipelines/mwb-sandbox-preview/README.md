# Private MWB Sandbox CI preparation

This dependency is scoped to the opt-in Mouse Without Borders Sandbox experiment.
It does not replace `.pipelines\InstallWinAppCli.ps1` or the stable CLI used for
local UI automation inside either endpoint.

## Gate and artifact

The automation pipeline accepts the experiment only with `buildSource=buildNow`,
`buildPlatforms=[x64]`, exactly `uiTestModules=[MouseWithoutBorders.UITests]`, and
`useLatestWebView2=false`. The build uses Debug, builds tests, and disables
installers. Ordinary callers retain their existing defaults.

After the original product/tests are staged, the **build job** calls
`New-MwbSandboxCiArtifact.ps1 -ReadyToRun`. The existing `build-x64-Debug` artifact
keeps its original `x64\Debug\x64\Debug` product and test tree, adding:

```text
mwb-sandbox-ci\
  manifest.json
  runtime.zip
  runtime.zip.manifest.json
  preview\
    winapp.exe
    libHarfBuzzSharp.dll
    libSkiaSharp.dll
```

The runtime archive comes from the existing lean dependency-closure packager:
Runner, MWB, helper, Settings, Quick Access and their managed/native/resources
dependencies. ReadyToRun compilation operates only on private staging. Its
compiler is the exact `Microsoft.NETCore.App.Crossgen2.win-x64` NuGet package
version matching the **actual built CoreCLR**, reusing an already-restored SDK
compiler or restoring it using `dotnet restore` against nuget.org only when
missing. The packager verifies the compiler and adjacent native JIT
libraries against the runtime's file versions/commit, preserves Debug attributes
and IL, and checks identical assembly copies. No compiler or SDK is needed on the
test agent. The helper's `-ReadyToRun` switch is explicit; the packager's defaults
are unchanged.

## Frozen preview source

`source.json` pins the public upstream commit, SDK/runtime and informational
version. `firewall-query.patch` is the single performance change plus its upstream
unit tests: enumerate ActiveStore application filters once, then resolve only
exact program matches. Rule scope, stores, privileges, authentication, and error
handling are unchanged.

The build fetches only that commit with interactive Git authentication disabled,
verifies `FETCH_HEAD` and the patch SHA-256, checks/applies the patch and audits its
changed paths. Repository-local and per-command `core.longpaths=true` cover the
entire checkout, including deeply nested test assets. The audit rejects stderr
even with exit code zero; unreadable files are never ignored. Command logs retain
both streams, but parsed commit/version/path values come only from stdout.
A private, isolated .NET SDK and the build agent's x64 Visual Studio
NativeAOT toolchain publish the three portable native files. Neither the CLI nor
PowerToys is launched during preparation. Source/build work stays outside the
PowerToys checkout and published bundle. The manifest retains source/patch,
SDK/runtime/Visual Studio provenance and hashes of every portable/runtime file;
preparation logs are published with the ordinary build logs.

The exact SDK selects its servicing runtime. No global `RuntimeFrameworkVersion`
override is passed: that property would also override the Windows SDK framework
reference and cause conflicting `Microsoft.Windows.SDK.NET.Ref` downloads.
After a successful publish, preparation verifies the selected Windows x64
`project.assets.json` targets for the CLI and both UI Automation projects.
.NETCore runtime packs, the NativeAOT runtime pack, and both ILCompiler packages
must match the runtime pin. The manifest records observed versions, compiler
package integrity metadata and assets-file hashes before portable files are
published. Windows SDK reference versions remain independent; NU1505 and other
warnings are not suppressed.

This is a source-build dependency, **not** a reference to a private uploaded ZIP.
The resulting executable's hash is recorded per build; it need not be
byte-identical across Visual Studio toolchain versions.

## Test-host trust and prerequisites

Preparation verifies the bundle against the current PowerToys source revision,
the tracked preview pin, and every archive/portable hash. The guest receives the
original runtime archive. The host is extracted from that **same archive** into
an administrator-owned run directory, read-only to the interactive test user.
Each host file is checked against the inventory again before dispatch. Original
product output is never overwritten or used as a runtime fallback.

The Win10 job selects Legacy. The Win11 job selects WinApp and requires Windows
11 24H2 or newer. An Enabled Sandbox feature is necessary but insufficient: the
modern backend checks per-user package registration, the trusted `wsb` alias,
preview schema, and a bounded `wsb --version` response as the Limited interactive
user. Missing prerequisites produce `BLOCKED_INFRASTRUCTURE`, never a Legacy
fallback, feature enablement, reboot, client installation, or UAC prompt.
Standard-user Priority 4 dispatch and existing scenario/lease/endpoint timeouts
and owned recovery are unchanged. Raw client command lines and authentication
state must not be logged or published.

Tests: `.pipelines\tests\mwbSandboxCiPreparation.Tests.ps1` and the existing
`mwbSandboxExperiment.Tests.ps1` use Pester 3.4 with inert fixtures/mocks. They
do not download/build tools, mutate Windows, or execute product/UI scenarios.
Pipeline queue authorization and local full-suite signoff are separate gates;
preparing these files does not authorize a run.
