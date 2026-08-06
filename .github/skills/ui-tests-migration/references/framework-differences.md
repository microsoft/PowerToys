# Framework differences: legacy vs `.Next`

The conceptual deltas you must internalize before porting. Read this once, end-to-end, then keep
[api-mapping.md](api-mapping.md) open for the mechanical lookups.

## At a glance

| Aspect | Legacy `Microsoft.PowerToys.UITest` | New `Microsoft.PowerToys.UITest.Next` |
|---|---|---|
| Folder | `src/common/UITestAutomation/` | `src/common/UITestAutomation.Next/` |
| Namespace | `Microsoft.PowerToys.UITest` | `Microsoft.PowerToys.UITest.Next` |
| Assembly | `Microsoft.PowerToys.UITest` | `Microsoft.PowerToys.UITest.Next` |
| Engine | WinAppDriver server on `http://127.0.0.1:4723` + Selenium/Appium | `winapp.exe` CLI (shell out, parse `--json`) |
| Driver object | `WindowsDriver<WindowsElement>`, `WindowsElement` | none — every call is a `winapp ui …` subprocess |
| 3rd-party deps | `Appium.WebDriver`, `Selenium.WebDriver`, … | none (MSTest only) |
| Element model | **stateful** — wraps a live `WindowsElement` | **stateless** — wraps a selector; every read/action re-shells out |
| Selector grammar | Selenium `By` (Name, ClassName, Id, **XPath**, **CssSelector**, AccessibilityId) | winappcli `By` (**Name=text**, **AccessibilityId**, **Slug**) — no XPath/CSS |
| Find scope flag | `bool global` parameter on every `Find` | no `global` param — session scope (`-w`/`-a`) decides reach |
| Mouse/keyboard | instance methods on `Session`/`UITestBase` (`MoveMouseTo`, `PerformMouseAction`, `SendKeys`) | **static** helpers (`MouseHelper`, `KeyboardHelper`, `ClipboardHelper`) |
| Run-time prereq | WinAppDriver installed + running | `winapp.exe` on PATH (or `WINAPP_CLI_PATH`) |
| Elevation | **Required** — harness launches the runner via `Verb="runas"`; a non-elevated host fails at launch | **Not required** — harness launches the runner non-elevated (works from a plain terminal) |
| Test runner | MSTest (VSTest) | MSTest via Microsoft.Testing.Platform (`EnableMSTestRunner`) |

## 1. The engine: winappcli, not WinAppDriver

The legacy harness spins up a WinAppDriver server and talks Selenium WebDriver to it. The `.Next`
harness has **no server and no session protocol** — `WinappCli.Invoke(...)` starts `winapp.exe`,
captures stdout/stderr/exit-code, and (for `--json` verbs) parses the envelope. Every `Find`,
property read, click, and key press is an independent process invocation.

Consequences you'll feel while porting:

- There is no long-lived "driver" to attach/dispose. `Session` is a lightweight value object holding a
  target flag (`-w <hwnd>` or `-a <app>`) and metadata. `Session.Cleanup()` is a no-op.
- "Is the CLI installed?" is checked once per run (`WinappCli.IsAvailable()` from `UITestBase`), and a
  missing CLI fails fast with an install hint — you don't manage that.
- Errors surface as non-zero exit codes + stderr, wrapped into MSTest `Assert` failures with a
  `winapp …  -> exit N; stderr: …` description. There are no `WebDriverException`/`NoSuchElementException`
  types to catch — use the `Has*`/`WaitFor*` probes instead of try/catch on Find.

## 2. Elements are stateless

Legacy `Element` wraps a live `WindowsElement`; properties like `Enabled`, `Text`, `Rect` read the
cached Selenium object. `.Next` `Element` wraps **only a selector** (a winappcli slug or text query)
plus the owning `Session`. The `ControlType`, `ClassName`, `Name`, `X/Y/Width/Height` fields are the
values captured **at `Find` time**; every *fresh* read (`IsEnabled`, `GetProperty(...)`, `GetValue()`)
shells out again via `winapp ui get-property`/`get-value`.

Porting implications:

- Cached geometry (`X`, `Y`, `Width`, `Height`) is a **snapshot**. If the UI moved since `Find`,
  re-find before using coordinates for a `Drag`/`MouseClick`.
- There is no `element.Rect` returning a live `Rectangle`. Use the cached `X/Y/Width/Height` ints, or
  re-find.
- Don't hold an `Element` across a navigation/relaunch and expect it to still resolve — re-find after
  the tree changes.

## 3. Selectors: `By.Name` / `By.AccessibilityId` / `By.Slug` only

The new `By` (in `By.cs`) is **not** Selenium's `By`. It has three kinds:

| `.Next` factory | Meaning | winappcli mechanic |
|---|---|---|
| `By.Name(text)` | case-insensitive substring search over Name/AutomationId | `winapp ui search "<text>"` |
| `By.AccessibilityId(id)` / `By.Id(id)` | stable `AutomationId` | search by id |
| `By.Slug(slug)` | a semantic slug printed by `inspect`/`search` (e.g. `btn-close-d1a0`) | direct slug selector |

There is **no** `By.XPath`, `By.ClassName`, or `By.CssSelector`. To port those:

- `By.ClassName("ToggleSwitch")` → use the typed wrapper (`Find<ToggleSwitch>(By.Name(...))`), which
  pins `ClassName` via `TargetClassName`. The wrapper's class filter replaces the ClassName selector.
- `By.XPath("//*[contains(@Name,'foo')]")` (the legacy `FindByPartialName`) → `By.Name("foo")` already
  does substring matching in winappcli, so a partial-name XPath usually collapses to a plain
  `By.Name`.
- `By.XPath("//*[@Name='exact']")` → `By.Name("exact")` (winappcli substring-matches; if you need to
  disambiguate, `FindAll` then filter in C# on `m.Name == "exact"`).
- Complex structural XPath (parent/child axes) → there is no direct equivalent. Re-express as: find the
  container by id, then `container.Find<T>(By.…)` (scoped search), or `Session.FindAll<T>` + a C#
  `Where(...)` on the cached `ControlType`/`ClassName`/`Name`/coordinates. The ColorPicker example
  does exactly this (`FindAll<Element>(By.Name("Color Picker"))` then `.OrderByDescending(m => m.X)`).

**Prefer `By.AccessibilityId`.** When porting, if a legacy test used a fragile `By.Name` or XPath, check
the module's XAML for an `x:Name`/`AutomationProperties.AutomationId` and switch to `By.AccessibilityId`
— it's the most stable selector and what the new examples favor.

## 4. No `global` parameter — session scope decides reach

Legacy `Find<T>(by, timeoutMS, global)` had a `global` bool to widen the search beyond the current
window. `.Next` `Find<T>(by, timeoutMS)` has **no** `global` param. Instead, the **session scope**
governs reach:

- **Window scope** (`-w <hwnd>`, the default from `UITestBase`/`SessionHelper.Init`): searches within
  one window. Use when a process owns several windows and you must pin one (Settings vs. its
  `PopupHost`; ColorPicker overlay vs. editor).
- **Process scope** (`-a <name|pid>`, via `Session.FromProcess(...)`): searches all of a process's
  windows; every call re-resolves, so it transparently survives window replacement (re-navigation,
  page swaps, dropdown popups in a separate `PopupHost`). Closest analog to the legacy `global: true`.

To reach a **different** window (e.g. an editor/overlay the module just spawned), don't pass a flag —
discover it with `WindowsFinder`/`WindowControl` (see §6) and get a new `Session` bound to it.

## 5. Lifecycle, hygiene, and module pre-enablement (`UITestBase`)

Both bases run `[TestInitialize]`/`[TestCleanup]`, but the `.Next` base centralizes things the legacy
tests often did by hand:

- **Constructor:** `UITestBase(PowerToysModule scope = PowerToysSettings, WindowSize size = UnSpecified, string[]? enableModules = null)`.
  - `scope` — which module/window to drive. **Most module tests use `PowerToysModule.PowerToysSettings`**
    and drive the utility *through* the Settings UI + its activation hotkey, because the **runner**
    (`PowerToys.exe`) owns module toggles and the centralized keyboard hook. Launching a module's UI
    exe standalone bypasses that and the hotkey never fires.
  - `size` — applied after the window appears; `UnSpecified` maximizes (deterministic on CI). Maps to
    the legacy `WindowSize` ctor arg.
  - `enableModules` — when non-null, exactly these modules are enabled (others disabled) in the global
    `settings.json` **before** launch. This is the deterministic replacement for the legacy
    `commandLineArgs`/`StartExe(enableModules)` pattern. The names are the keys under `"enabled"` (e.g.
    `"ColorPicker"`, `"FancyZones"`, `"Measure Tool"`).
- **Pre-test hygiene** runs automatically: `Win+M` (minimize all) → `Esc` → kill stale PowerToys
  processes (`StaleProcessNames`, overridable). You usually delete the legacy test's manual
  `CloseOtherApplications`/`Win+M` calls.
- **Teardown** stops only what the base launched (`StopIfStarted()`), so you rarely need a manual
  process-kill in `[TestCleanup]`. (Per-test cleanup of *spawned* windows — an overlay/editor the test
  popped — is still the test's job; use `WindowControl.TryCloseByApp` in a `finally`.)
- **`RestartScope(enableModules?)`** replaces the legacy `RestartScopeExe` — re-seeds modules,
  kills + relaunches, reapplies size, returns the fresh `Session`.
- **Class-shared window:** override `protected bool ReuseScopeAcrossTests => true;` to launch once per
  class and reuse the window across `[TestMethod]`s (skips per-test hygiene/relaunch). Use for smoke
  suites with many cheap cases against one window. Default is per-test isolation.

## 6. Multi-window discovery

The legacy harness used `Session.Attach(module|windowName)` to switch the driver to another window.
`.Next` discovers windows with two static helpers:

- **`WindowsFinder`** (read/wait): `ListByApp(appNameOrPid)`, `ListAll()`,
  `WaitForWindowByApp(app, predicate, timeoutMS)`, `WaitForWindowByTitle(...)`,
  `WaitForWindowByProcess(...)`. Returns `WindowInfo` (hwnd/title/process/size/className) and, for the
  `WaitFor*` variants, a ready-to-use `Session` bound to that window. This is how the ColorPicker test
  finds the overlay (`Width<300 && Height<200`) vs. the editor (`Width>300 && Height>300`) from the
  same `PowerToys.ColorPickerUI` process.
- **`WindowControl`** (tolerant cleanup): `TryCloseByApp(app[, predicate])`, `TryFocusByApp`,
  `TryKillProcessByName` (exact), `TryKillProcess` (substring), `SafeCloseAndFocus`. Every method
  swallows exceptions and returns a bool — designed for `finally` blocks so cleanup never masks the
  real failure.

Note: unfiltered `WindowsFinder.ListAll()` drops windows with no Win32 title (e.g. the ColorPicker
editor exposes its name only via UIA). **Use `ListByApp`/`WaitForWindowByApp` with a process filter**
for those.

## 7. What `.Next` does NOT (yet) provide

When a legacy test relies on one of these, adapt rather than expecting a drop-in:

- **`By.XPath` / `By.CssSelector` / `By.ClassName`** — none exist (see §3).
- **`FindByPattern` / regex Name matching** as a base helper — re-express with `FindAll<T>(By.Name(...))`
  + a C# `Regex`/`Where` on the cached `Name` (the legacy base's `FindByNamePattern` shows the shape).
- **`Group`, `HyperlinkButton` wrappers** — the legacy `Element/` set has them; `.Next` doesn't.
  Use `Find<Element>` (or `Find<Button>` for a hyperlink button, which is a Button under UIA), or add a
  tiny wrapper subclass mirroring `Button.cs`/`NavigationViewItem.cs` if you need the type.
- **`element.Text` / `element.Rect` / `element.Enabled`** (legacy names) — use `GetValue()` /
  `X,Y,Width,Height` / `IsEnabled` (see [api-mapping.md](api-mapping.md)).
- **Instance `Session.SendKeys`/`MoveMouseTo`/`PerformMouseAction`** — exist as a thin `Session.SendKeys`
  passthrough, but prefer the static `KeyboardHelper`/`MouseHelper`.

If a genuinely missing capability blocks a port, add it to the `.Next` harness in a small, focused way
that mirrors the existing file style (one wrapper class, or one static helper method) — and call it out
to the user. Don't pull in a NuGet package.
