# Porting workflow

Two end-to-end playbooks. Pick the one matching your scenario (see SKILL.md "Pick your scenario").
Both assume you've read [framework-differences.md](framework-differences.md) and have
[api-mapping.md](api-mapping.md) open.

---

## Scenario A — Port existing legacy tests

Re-implement an existing `[Module].UITests` project (which references `UITestAutomation.csproj`) as a
new `[Module].UITests.Next` project (referencing `UITestAutomation.Next.csproj`), preserving every
test's **intent and assertions**.

### A0. Baseline the legacy suite first — ELEVATED (recommended)

Before porting, run the **legacy** suite once to learn its real local pass rate. **Run it elevated:**
the legacy harness launches PowerToys via `ProcessStartInfo { Verb = "runas" }`, so a non-elevated
test host can't complete the launch and **every test fails at startup with a misleading
`Win32Exception` cascade** — a false 0/N that looks like "the tests are broken" but is just the run
method. (This is exactly why VS Test Explorer passes them: VS runs as admin.) Don't conclude the
legacy suite is broken from a non-elevated run.

```pwsh
# 1. Build the legacy project (WinAppDriver-based, OutputType=Library).
tools\build\build.cmd -Path src\modules\<Module>\Tests\<Module>.UITests -Platform x64 -Configuration Debug

# 2. Run ELEVATED. Put the run in a .ps1 and launch it with -Verb RunAs (one UAC prompt) so the
#    harness's runas launch has an elevated host. The script should start WinAppDriver + run vstest:
#      $dll = "$PWD\x64\Debug\tests\<Module>.UITests\net10.0-windows10.0.26100.0\<Module>.UITests.dll"
#      Start-Process "C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe" -ArgumentList "127.0.0.1","4723"
#      vstest.console.exe $dll /Platform:x64 /InIsolation /Logger:"trx;LogFileName=legacy.trx" /ResultsDirectory:<dir>
#    Have the script write a DONE marker at the end; poll for it, then read the .trx.
Start-Process pwsh -Verb RunAs -ArgumentList "-NoProfile","-ExecutionPolicy","Bypass","-File","<runner>.ps1"
```

Knowing the baseline tells you which failures are pre-existing product/environment issues you should
NOT expect the port to fix. A measurement failure on a scaled (non-100%) display is usually DPI (see
[patterns-and-pitfalls.md](patterns-and-pitfalls.md) Pitfall 12): the ScreenRuler legacy suite scores
**4/5** elevated here (Bounds fails at 150% scale), while the `.Next` port scores **5/5** — its Exe
`app.manifest` makes it DPI-aware where the legacy `Library` project's manifest is silently ignored.

### A1. Inventory the source

- List every `[TestClass]` and `[TestMethod]` in the legacy project. Note `[TestCategory]` tags,
  `DataRow`s, and the base-ctor args (`scope`, `WindowSize`, `commandLineArgs`).
- List shared helpers (a `TestHelper`/`*Helpers` static class is common — ScreenRuler's
  `TestHelper.cs` is the canonical example). Decide per-helper whether to **port it**, **inline it**,
  or **drop it** (Selenium-only scaffolding usually drops).
- For each test, write a one-line statement of *what it asserts* (the behavior), independent of how the
  legacy harness did it. You're re-creating that behavior, not the Selenium calls.

### A2. Map the structure

| Legacy piece | `.Next` target |
|---|---|
| `[TestClass] FooTests : UITestBase` | same shape, `using Microsoft.PowerToys.UITest.Next;` |
| ctor `: base(PowerToysSettings, WindowSize.Large)` | `: base(PowerToysModule.PowerToysSettings, WindowSize.Large)` |
| ctor `commandLineArgs: new[]{ "--enable", "Foo" }` | `enableModules: new[]{ "Foo" }` (deterministic module baseline) |
| `TestHelper.InitializeTest(this, …)` | a private setup method, or rely on `UITestBase` hygiene + an explicit nav helper |
| `[TestMethod("Foo.Bar")]` | `[TestMethod]` + keep `[TestCategory("Foo")]` |

### A3. Re-implement each test, method by method

For each legacy method:

1. **Translate the selectors** first (the highest-risk part). Replace `By.XPath`/`By.ClassName` per
   [framework-differences.md §3](framework-differences.md). Prefer `By.AccessibilityId` — open the
   module's XAML and find the `x:Name`/`AutomationProperties.AutomationId` the control exposes.
2. **Translate the actions** with [api-mapping.md](api-mapping.md). The frequent ones:
   - `element.Click(msPreAction: N, …)` → if you relied on the pre-delay, add `Thread.Sleep(N)` then
     `element.Click(msPostAction: …)` (`.Next` `Click` has no `msPreAction`).
   - A click on a non-invokable element (TextBlock/ListItem whose ancestor handles it) →
     `element.MouseClick(...)`.
   - Selenium `Actions` drags → `element.Drag(...)` / `MouseHelper.Drag(...)`.
   - `testBase.SendKeys(...)` / `Session.PerformMouseAction(...)` → `KeyboardHelper.*` / `MouseHelper.*`.
3. **Translate the waits.** Replace hand-rolled `while (DateTime.Now < end) { … Task.Delay(...) }`
   poll loops with the built-ins: `element.WaitForProperty("ToggleState","On",t)`,
   `element.WaitForValue(...)`, `Session.WaitForElement(by,t)`, `Session.WaitFor(() => …, t)`, or
   `ClipboardHelper.WaitForText(...)`. Keep a custom poll only when you're polling something with no
   built-in (e.g. `Process.GetProcessesByName(...)` — see the ColorPicker `WaitForProcess` helper).
4. **Translate cleanup.** Delete manual `CloseOtherApplications`/`Win+M` (the base does hygiene). For
   windows the *test* spawned (overlay/editor), close them in a `finally` with
   `WindowControl.TryCloseByApp("PowerToys.<Module>UI")`. Restore any toggle you flipped to its
   initial state in a `finally` (see the ColorPicker example's nested `finally`).
5. **Keep the assertions identical in spirit** — same things checked, same pass/fail meaning.

### A4. Port shared helpers thoughtfully

- Start from [../templates/TestHelper.cs](../templates/TestHelper.cs) — it already implements the
  common building blocks (navigate, toggle + verify process, read shortcut, discover/activate/close
  the module window, clipboard, screen-center) with the right `.Next` idioms; map your legacy helper's
  module-specific bits onto it rather than translating Selenium scaffolding line-by-line.
- A static `TestHelper` is fine to keep, but re-point it at the new APIs. Drop members that only
  existed to work around Selenium (manual `Session.Attach` dances, STA-clipboard wrappers → use
  `ClipboardHelper`).
- Shortcut-string parsing helpers (`ParseShortcutText` turning `"Win + Shift + C"` into `Key[]`) port
  almost verbatim — just map `"win"` → `Key.LWin`. Both examples include this parser; reuse it.

### A5. Validate (write → build → run → iterate)

Build to exit 0 (see [project-setup.md §5](project-setup.md)). Then map each new `[TestMethod]` back
to the legacy method it replaces and confirm none were dropped. On a live desktop, **run in a loop**:
start with one deterministic test (the activation/toggle test), get it green, then widen to the whole
suite. UI runs expose environment-real failures that only show up live — DPI scaling, cursor drift,
and hotkey-arming races (all hit during the ScreenRuler port; see
[patterns-and-pitfalls.md](patterns-and-pitfalls.md) Pitfalls 12–15). Diagnose each from the TRX
failure message + the auto-captured failure screenshots, fix, and re-run — don't just re-run hoping
for a different result.

---

## Scenario B — Greenfield from a human sign-off markdown

The module has **no** automated UI tests. Build a new `[Module].UITests` project (no `.Next` suffix)
whose tests come from the module's **manual test sign-off** document — the human checklist QA runs
before a release. `ColorPickerUITest.md` is the archetype:

```text
* Enable the Color Picker in settings and ensure that the hotkey brings up Color Picker
- [] Change `Activate Color Picker shortcut` and check the new shortcut is working
- [] Try all three `Activation behavior`s
- [] Change `Color format for clipboard` and check if the correct format is copied
...
```

### B1. Find the sign-off doc

- Look in the module's folder and its `Tests/`/`UITests/` subfolders for a `*.md` describing manual
  test steps (often `<Module>UITest.md`, `<Module>Test.md`, or a section in the module README).
  Search the repo for the module name + "test"/"checklist" if it's not obvious.
- If there's genuinely no doc, **ask the user** for the test spec rather than inventing coverage.

### B2. Turn each checklist item into a test intent

For every bullet, write down: **trigger → observable signal → assertion**. Classify each item by how
the new harness can drive it (see [patterns-and-pitfalls.md](patterns-and-pitfalls.md) for the
recipes):

| Checklist phrasing | Drive technique | Observable signal |
|---|---|---|
| "Enable X in settings; module runs" | toggle the page switch | `WaitForProcess("PowerToys.<M>UI", true)` |
| "Hotkey brings up X" | read shortcut from `ShortcutControl`, `KeyboardHelper.SendKeys(...)` | the module's window/overlay appears (`WindowsFinder.WaitForWindowByApp`) |
| "Change shortcut and it works" | set the new shortcut (UI or settings.json), fire it | window appears for the new chord |
| "Change format/option and output matches" | flip the setting, perform the action | clipboard/value matches (`ClipboardHelper.WaitForText`) |
| "Value is shown in the UI" | read it | `element.GetValue()` / `.Name` / `.HelpText` equals expected |
| "Select/remove item from a list" | `Find`+`Click` the item | list count / selection changes |
| "Check logs for errors" | *(usually not automatable)* | note as out-of-scope; don't fake an assertion |

### B3. Group items into test methods

- One `[TestMethod]` per coherent scenario, not necessarily one per bullet — several related bullets
  (enable → read shortcut → activate → capture → verify) often belong in one end-to-end flow, exactly
  like `ColorPickerEndToEndTests.NavigateReadShortcutActivateAndCapture`.
- Add `[TestCategory("<Module>")]` so the suite is filterable.
- Drive **through the Settings scope** (`base(PowerToysModule.PowerToysSettings)`) for overlay/utility
  modules so the runner owns the hotkey and toggles — don't launch the module exe standalone.

### B4. Make the UI observable (flag, don't fix)

Sign-off docs assume a human's eyes. Some signals aren't UIA-readable (a transparent overlay's
displayed HEX, a canvas color). If an assertion needs a hook the product doesn't expose:

- First try the existing readouts: `GetValue()` (reads the Text binding even when
  `AutomationProperties.Name` overrides the UIA Name), `Inspect(...)` tree walks, clipboard, window
  geometry.
- If there's truly no signal, **flag it to the user** that a small test-only UIA hook is needed (like
  ColorPicker's hidden `ColorHexAutomationPeer` TextBlock — `Visibility=Visible, Opacity=0`, bound to
  the same source). Do **not** add such a hook to product code yourself without sign-off; describe it
  and let the user decide.

### B5. Validate

Build to exit 0. List each checklist item and the `[TestMethod]` (or `TestContext.WriteLine` note)
that covers it, and explicitly call out any items left as manual-only (e.g. "check logs for errors").

---

## Both scenarios — definition of done

- [ ] New project builds to **exit code 0**, referencing `UITestAutomation.Next.csproj` only.
- [ ] No Selenium/Appium/`WindowsDriver`/`By.XPath`/`:4723` left anywhere.
- [ ] Registered in `PowerToys.slnx` with the `*|ARM64`/`*|x64` platform block.
- [ ] (A) Every legacy `[TestMethod]` has a `.Next` counterpart; the legacy project is untouched.
- [ ] (B) Every actionable sign-off item maps to a test or is explicitly noted as manual-only.
- [ ] Toggles/settings the test changes are restored in a `finally`; spawned windows are closed.
- [ ] No product-code edits (or any needed UIA hook is flagged to the user, not silently added).
