---
name: ui-tests-migration
description: "Create and stabilize PowerToys UI suites using the existing Microsoft.PowerToys.UITest framework and winappcli. Use for new UITest projects, flaky CI, Settings IPC and signing, Explorer/Shell selection, preview and thumbnail handlers, hotkeys, foreground ownership, and WinUI/WebView visual assertions. Covers test design, Hyper-V validation and the required CI handoff. WinAppDriver/Selenium migration guidance is historical and applies only to older branches that still contain the legacy framework."
license: Complete terms in LICENSE.txt
---

# PowerToys UI-test development

Create and stabilize UI tests using `Microsoft.PowerToys.UITest`, the **winappcli** framework
in `src/common/UITestAutomation/`. The framework shells out to `winapp.exe` and parses its JSON.
It does not use WinAppDriver, Selenium, or Appium.

The WinAppDriver harness and its test projects were removed from `main`. The legacy comparisons
linked below are historical guidance for older branches, not scaffolding for current tests.

## When to use this skill

Use this skill when the task is to:

- **Extend or stabilize** an existing `[Module].UITests` project using the shared framework.
- **Create a new** `[Module].UITests` project for a module with **no** UI tests by reading the
  module's human test **sign-off markdown** (e.g. `ColorPickerUITest.md`) and turning each manual
  checklist item into an automated test.
- **Validate a new or migrated suite in a local Windows VM** through an unattended
  build/package/deploy/run/TRX/diagnose loop. Use a retained VM for fast iteration and a restored
  baseline checkpoint when clean-profile behavior matters.

This skill covers project scaffolding, PowerToys test recipes, and the build and validation loop.

## End-to-end completion contract

A request to **create, migrate, or stabilize UI tests** includes the complete delivery loop:
implement -> build -> full local VM matrix -> commit and push -> scoped CI -> terminal results.
Local success is a handoff, **not completion**. After the full default and constrained suites pass,
invoke [ui-tests-pipeline-ci](../ui-tests-pipeline-ci/SKILL.md) automatically; do not wait for a
separate "push" or "run CI" request. Keep commit/push and CI validation as open TODOs from the start.

Respect an explicit local-only/no-push/no-CI request. A read-only investigation, VM setup, or request
to run an existing suite locally does not authorize publishing changes. CI remains Microsoft
FTE-only and requires the pipeline skill's successful access preflight; report an exact blocker
when unavailable, never call local-only evidence CI-validated. The pipeline skill owns publication,
queueing, completion watching, and the three-run stabilization limit.

> **Reference implementations — read the examples relevant to the suite.**
> - **End-to-end flow**: [ColorPickerEndToEndTests.cs](../../../src/modules/colorPicker/ColorPicker.UITests/ColorPickerEndToEndTests.cs)
>   — full end-to-end scenario (navigate Settings → toggle module → read shortcut → fire hotkey →
>   read overlay → click-capture → inspect editor), driven entirely through `winappcli`.
> - **Cross-window input**: the ScreenRuler suite lives in
>   [ScreenRuler.UITests/TestHelper.cs](../../../src/modules/MeasureTool/Tests/ScreenRuler.UITests/TestHelper.cs)
>   + 5 test classes. It is the canonical port reference — cross-window toolbar discovery via
>   `Session.FromProcess`, a DPI-aware `app.manifest`, cursor centering, and patient hotkey
>   activation are all there because real runs needed them (see
>   [references/patterns-and-pitfalls.md](references/patterns-and-pitfalls.md)).
> - **Stateful/visual reference (validated 15/15 across Win10 x64, Win11 x64, and ARM64)**:
>   [PeekFilePreviewTests.cs](../../../src/modules/peek/Peek.UITests/PeekFilePreviewTests.cs)
>   demonstrates stable Explorer Shell selection, toggle-hotkey activation, process-preserving
>   pinning tests, renderer readiness, and composed WinUI/WebView visual baselines.
> - **Explorer/Shell-extension reference (validated across x64 and ARM64 CI)**:
>   [FileExplorerAddonsTests.cs](../../../src/modules/previewpane/PreviewPane.UITests/FileExplorerAddonsTests.cs)
>   demonstrates class-scoped runner reuse, one-time Shell restart, state-aware Preview pane
>   activation, exact Shell selection, deterministic icon sizes, provider-log readiness, and
>   failure media captured before Explorer teardown. Read
>   [references/explorer-shell-tests.md](references/explorer-shell-tests.md) before testing Explorer.

## Required reads (in order)

1. **This `SKILL.md`** — the naming rules, workflow, and build/validate loop.
2. **[references/framework-differences.md](references/framework-differences.md)** — winappcli engine, stateless elements, selector
   grammar (no XPath/CssSelector), session scopes (window vs process), lifecycle/hygiene/module
   pre-enablement, multi-window discovery, and framework API limits.
3. **[references/api-mapping.md](references/api-mapping.md)** — the line-by-line cheat sheet:
   namespaces, `By`, `Element` actions/properties, `Session`, `UITestBase`, the static
   Keyboard/Mouse/Clipboard helpers, and the element-wrapper catalog. Keep this open while editing.
4. **[references/project-setup.md](references/project-setup.md)** — csproj scaffold, naming/placement
   rules, `.slnx` registration, and how to build and run a UI-test project. Uses the
   [templates/](templates/) starter files.
5. **[references/porting-workflow.md](references/porting-workflow.md#scenario-b--greenfield-from-a-human-sign-off-markdown)** —
   author tests from a human sign-off checklist. Use Scenario A only as historical migration guidance.
6. **[references/patterns-and-pitfalls.md](references/patterns-and-pitfalls.md)** — adaptable recipes
   for the recurring PowerToys patterns (toggle a module + verify its process, read the activation
   shortcut from a `ShortcutControl`, fire a global hotkey reliably, inspect the clipboard, discover
   overlay/editor windows) and the gotchas that bite during migration.
7. **[references/explorer-shell-tests.md](references/explorer-shell-tests.md)** — required for tests
  involving Explorer, preview handlers, thumbnail providers, Shell selection, view modes, or Shell
  restarts. Covers lifecycle boundaries, authoritative signals, and failure evidence.
8. **[references/ci-stability.md](references/ci-stability.md)** — the CI-stability capstone: the
  Win32-window vs UIA-element mental model, state-boundary worksheet, stable-sample waits, retry
  semantics, foreground/integrity constraints, process lifecycle, composed visual capture, and a
  **pre-flight checklist** to apply BEFORE the first CI push. It also covers Release Runner/Settings
  IPC authentication, the existing CI companion-signing mechanism, and why a visible Settings toggle
  must never be rescued with a settings-file/restart fallback. Read this to spend one CI iteration
  instead of six.
9. **[ui-tests-local-vm](../ui-tests-local-vm/SKILL.md)** — the live desktop execution loop:
  scaffold or reuse a persistent Hyper-V VM, run as a true standard user, refresh only
  changed payloads, iterate through durable TRX/evidence, and restore or recreate the baseline for
  clean-profile validation.
10. **[ui-tests-pipeline-ci](../ui-tests-pipeline-ci/SKILL.md)** — the mandatory post-local handoff
  for implementation tasks: preflight, scoped commit/push, exact-revision CI, and terminal sign-off.

## Choose the project

Extend the module's existing UI-test project if it has one. Otherwise, use the module's manual
sign-off checklist to create `[Module].UITests` under `src/modules/[Module]/Tests/[Module].UITests/`.
If the module uses a different test layout, match its convention. If you cannot find a checklist or
test spec, ask for one. See [project setup](references/project-setup.md).

## High-level workflow

Create a TODO list and work top-to-bottom. Each step links to the reference that drives it.

```markdown
- [ ] 1. Identify the module and whether to extend or create its suite
- [ ] 1a. Read the module's developer docs — `doc/devdocs/modules/<module>.md` (if the exact file is
        missing, search `doc/devdocs/`, including `doc/devdocs/common/`) — to learn its
        development-cycle specifics BEFORE writing tests: how its shell extensions / context menus
        register, whether they need a **Release** build (`NDEBUG`) or a **signed** sparse MSIX package,
        and any Explorer-restart or first-run needs. Skipping this produces opaque failures — e.g. a
        context-menu entry never appears because a Debug build compiles registration out, or an
        unsigned `.msix` fails to register (`0x800B0100`).
- [ ] 2. Read the relevant reference examples end-to-end
- [ ] 3. Inventory the source:
        • For an existing suite, identify the relevant test methods and shared helpers
        • For a new suite, read the module's sign-off markdown and list the checklist items
  • For each workflow → list every external boundary (runner, Explorer, HWND, renderer,
    compositor, child process) and its authoritative ready signal
        — references/porting-workflow.md
- [ ] 4. Internalize the deltas — references/framework-differences.md
- [ ] 5. If needed, scaffold a new project (csproj + PerMonitorV2 app.manifest from templates,
  register in .slnx)
        — references/project-setup.md
- [ ] 6. Implement or update tests using recipes from references/patterns-and-pitfalls.md
- [ ] 6a. If Explorer/Shell is involved, apply references/explorer-shell-tests.md
- [ ] 7. Apply the CI-stability checklist BEFORE building — references/ci-stability.md
  (stable authoritative signals, retry classification, foreground/integrity, lifecycle reset
  scope, non-activating helper processes, composed capture, DPI manifest, single-module enable,
  first-run suppression)
- [ ] 7a. If a test changes a module's enabled state through Settings, keep the real Settings UI +
  immediate runtime assertion. Verify the selected UITest project is covered by the existing
  `$requiresAuthenticatedSettingsIpc` companion-signing path in
  `.pipelines/v2/templates/job-test-project.yml`; never add a test-side settings/restart fallback
  for Release CI — [references/ci-stability.md](references/ci-stability.md#principle-5a--keep-module-lifecycle-tests-on-real-release-settings-ipc)
- [ ] 8. Build the new project to exit code 0 — this SKILL.md "Build & validate"
- [ ] 9. Run one deterministic test in the local VM and diagnose the first failure
      — ../ui-tests-local-vm/SKILL.md
- [ ] 10. Rerun the focused test after each fix, then widen to the complete module suite with bounded
  timeouts; parse TRX and verify durable evidence export
- [ ] 11. If the local VM is unavailable or unsupported, run on another live desktop or report the exact
   environmental blocker; do not silently stop at compile validation
- [ ] 12. Complete the full default and Constrained suites on both guest OSes, plus applicable
   architecture builds/guests; preserve counts, payload hashes, and evidence
- [ ] 13. Invoke ui-tests-pipeline-ci, pass its access preflight, commit only task-owned changes,
   and push the feature branch; record the exact SHA
- [ ] 14. Preview and queue scoped CI, persist its build ID, watch through completion, and diagnose/retry
   within the three-run ceiling; finish only on verified terminal success or an explicit blocker
```

## Build & validate

The framework needs `winapp.exe` only at **run** time, not build time — the project has zero
managed dependency on the engine. So you can always compile-verify a migration even on an agent with
no winappcli installed.

```pwsh
# 0. FIRST build of a brand-new project: restore so the assets file exists, otherwise the build
#    fails with NETSDK1004 "Assets file ... project.assets.json not found".
dotnet restore src\modules\<Module>\Tests\<Module>.UITests\<Module>.UITests.csproj -p:Platform=x64
#    (Equivalently, run tools\build\build-essentials.cmd once at the start of the session.)

# 1. Build just the new test project (fast inner loop). Prefer the repo build script.
tools\build\build.cmd -Path src\modules\<Module>\Tests\<Module>.UITests -Platform x64 -Configuration Debug
#    Exit code 0 = success; non-zero = failure. On failure read the errors log under the repo root:
#    artifacts\logs\<Module>.UITests\build.<Configuration>.<Platform>.errors.log
#    Do not substitute `dotnet build` when UITestAutomation's COM references are in the graph:
#    .NET SDK MSBuild cannot run ResolveComReference and fails with MSB4803. Use the repo script or
#    Visual Studio's full-framework MSBuild.exe; use `dotnet restore` only to create project.assets.json.

# 2. Run (needs a live desktop). A UI-test project is a Microsoft.Testing.Platform Exe — run the
#    produced exe directly with a TRX report; filter to one test/category for a tight loop.
$exe = "<repo>\x64\Debug\tests\<Module>.UITests\net11.0-windows10.0.26100.0\<Module>.UITests.exe"
& $exe --filter "TestCategory=<Cat>" --report-trx --report-trx-filename run.trx --results-directory <dir>
#    --filter accepts "TestCategory=X" or "FullyQualifiedName~Y"; omit it to run everything.
#    Exit 0 = all passed. Parse the .trx for per-test outcomes + failure messages.
```

- **Default to persistent local VM validation —
  [ui-tests-local-vm](../ui-tests-local-vm/SKILL.md).** It keeps the interactive desktop and staged
  tools, refreshes only changed archives, and returns durable status/TRX/evidence. Do not
  modify stabilized tests merely to improve a VM-specific pass rate when the task only asks whether
  the execution loop works. Finish clean-profile claims from a restored known baseline or a fresh
  named VM volume.
- **Design for CI stability up-front — [references/ci-stability.md](references/ci-stability.md).**
  Before the first push, walk its pre-flight checklist (authoritative-signal retries instead of fixed
  sleeps, navigation via UIA invoke, interaction-scoped foreground checks, non-activating helpers,
  Win32 window/overlay detection, screen-capture cold-start handling, DPI manifest, single-module
  enable, first-run suppression). Most "passes local, fails CI" loops come from skipping one of
  these; applying them proactively is how you spend one CI iteration instead of six.
- **Run it in a loop: write → build → run → diagnose → repeat.** UI tests surface environment-real
  failures (DPI scaling, cursor position, hotkey-arming races) that only a live run reveals. Start
  with one deterministic test (e.g. the activation/toggle test), get it green, then widen.
- **Diagnose from the artifacts, not from the assertion message.** Every failed test attaches a
  desktop screenshot, and in pipeline mode an MP4 of the run. **Open them before forming any theory**
  — especially before concluding the product is broken. An assertion can only say "found 0 rows"; the
  screenshot says whether the list was empty or whether your selector was wrong. This is the single
  highest-leverage habit in the agentic loop: skipping it cost ~8 iterations and a confident but
  entirely wrong product-defect report on File Locksmith (see
  [references/patterns-and-pitfalls.md](references/patterns-and-pitfalls.md) Pitfall 26). If there is
  no video, find out why rather than proceeding blind — the harness now prints the reason (a clean
  Windows image without the Visual C++ redistributable cannot load the native encoder).
- **Always** build to exit code 0 before proceeding to live validation. Fix every compile error — do
  not leave `// TODO: port this` stubs that break the build.
- Running the tests requires a **live interactive desktop** plus `winapp.exe`
  (`winget install Microsoft.winappcli`, or set `WINAPP_CLI_PATH`). The whole PowerToys runner is
  launched by the harness (`PowerToys.exe --open-settings`) — you should see the Settings window
  appear. If no supported live desktop is available, report **local validation blocked**, along with
  the build result and checklist coverage. Compile-only evidence does not complete an implementation
  task or satisfy the CI local gate.
- New `.csproj` files under `src/` MUST `<Import Project="$(RepoRoot)src\Common.Dotnet.CsWinRT.props" />`
  right after `<Project Sdk=...>` (CI audits this). The template already does.

## What NOT to do

- **Do NOT change product behaviour.** The one sanctioned product edit
  is adding **`AutomationProperties.AutomationId`** to a control that is otherwise unaddressable — an
  icon-only button whose label lives in a tooltip has no UIA Name at all, and the alternative is a
  brittle coordinate click. Use `AutomationProperties.AutomationId`, never `x:Name` (which also emits
  a code-behind field); see [references/patterns-and-pitfalls.md](references/patterns-and-pitfalls.md)
  Recipe 16. Anything larger — a hidden automation-peer TextBlock, a new property, a state string —
  must be flagged for the user instead. (ColorPicker's `ColorHexAutomationPeer` hook is a documented,
  pre-existing exception — see its class remarks.)
- **Do NOT port the legacy plumbing literally.** No Selenium `Actions`, no `WindowsDriver`/`WindowsElement`,
  no `By.XPath`/`By.CssSelector`, no `:4723`. Map them to the winappcli idioms in
  [references/api-mapping.md](references/api-mapping.md).
- **Do NOT restore the legacy harness.** Reference the current winappcli-based
  `src/common/UITestAutomation/UITestAutomation.csproj`.
- **Do NOT invent assertions** for a vague sign-off item. If a checklist line has no observable
  pass/fail signal, implement what you can and leave a clearly-marked `TestContext.WriteLine` note
  (or skip with an explanation) rather than asserting on something you can't actually read.
- **Do NOT introduce new third-party NuGet dependencies.** Use the helpers the framework already ships.
- **Do NOT retry a toggle hotkey blindly.** Once any target window appears, wait for initialization;
  resending the chord may close a healthy window. Restart only after a terminal readiness failure.
- **Do NOT replace or weaken visual baselines before proving capture is correct.** Foreground HWND,
  DWM z-order, composed WebView content, theme, and platform are separate failure sources.
- **Do NOT work around a Release Runner `not-microsoft-signed` Settings rejection in test code.** Do
  not seed the global enabled map, restart PowerToys, bypass authentication, or weaken the lifecycle
  assertion. Reuse the pipeline's existing Runner/Settings companion-signing mechanism; see
  [references/ci-stability.md](references/ci-stability.md#principle-5a--keep-module-lifecycle-tests-on-real-release-settings-ipc).

## What is NICE to do

- **Improve the framework when a helper is demonstrably reusable.** Prefer small composable APIs
  (`WaitHelper`, `WindowControl`, `ExplorerShell`) over module-specific mega-helpers. Keep product
  semantics such as Peek pin-state preservation in the module test.
