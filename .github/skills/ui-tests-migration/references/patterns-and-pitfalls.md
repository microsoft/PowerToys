# Patterns & pitfalls

Adaptable recipes for the recurring PowerToys UI-test patterns, plus the gotchas that bite during a
`.Next` migration. **These are patterns, not a script** — every module differs; lift the shape, not
the literal strings. All snippets assume `using Microsoft.PowerToys.UITest.Next;` and a class deriving
from `UITestBase`.

## Recipe 1 — Navigate to a module's Settings page

Two common shapes. Prefer the NavigationView item by `AutomationId` when the module has one:

```csharp
// Stable: the left-nav item (a ListItem) by AutomationId. Expand the parent group first if needed.
// NavigationViewItem.Click() is a coordinate-free UIA invoke (the harness overrides it) — race-safe
// even as the FIRST interaction right after Settings opens, and immune to nav-pane scroll/overflow
// (a physical click there is silently dropped before the window is interactive; see Pitfall 19).
if (Session.Has(By.AccessibilityId("ScreenRulerNavItem"), 500) == false)
{
    Session.Find<NavigationViewItem>(By.AccessibilityId("SystemToolsNavItem")).Click(msPostAction: 500);
}
Session.Find<NavigationViewItem>(By.AccessibilityId("ScreenRulerNavItem")).Click(msPostAction: 500);
```

```csharp
// Dashboard utility-stack label that has no InvokePattern (the click is handled by the ancestor
// SettingsCard). A Name search may return several elements — disambiguate, then MouseClick (real
// mouse), not Click (UIA invoke), because the label itself isn't invokable.
var matches = Session.FindAll<Element>(By.Name("Color Picker"));
var label = matches.Where(m => m.ClassName.Equals("TextBlock", StringComparison.OrdinalIgnoreCase))
                   .OrderByDescending(m => m.X)   // rightmost = the utility-stack label
                   .First();
label.MouseClick(msPostAction: 800);
```

> Pitfall: a `By.Name("Color Picker")` substring search can match a quick-access tile, its label, the
> utility-stack label, and a `ToggleSwitch`. Use `FindAll` + a C# filter on `ClassName`/`ControlType`/
> coordinates instead of assuming a single hit.

## Recipe 2 — Toggle a module on/off and verify its process

```csharp
// The page-level enable switch. ToggleSwitch pins ClassName="ToggleSwitch", so the Name search
// won't grab a sibling Button with the same Name (e.g. a dashboard card).
var toggle = Find<ToggleSwitch>(By.Name("Color Picker"));
bool initial = toggle.IsOn;

toggle.Toggle(false);                                              // flips only if currently on
Assert.IsTrue(toggle.WaitForProperty("ToggleState", "Off", 5_000), "UI didn't flip to Off.");
Assert.IsTrue(WaitForProcess("PowerToys.ColorPickerUI", false, 10_000), "Process didn't exit.");

toggle.Toggle(true);
Assert.IsTrue(toggle.WaitForProperty("ToggleState", "On", 5_000), "UI didn't flip to On.");
Assert.IsTrue(WaitForProcess("PowerToys.ColorPickerUI", true, 10_000), "Process didn't start.");
// ... restore `initial` in a finally ...
```

```csharp
// Poll for process presence — no built-in, so keep a small helper (from the ColorPicker example).
private static bool WaitForProcess(string name, bool expected, int timeoutMS)
{
    var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
    while (DateTime.UtcNow < deadline)
    {
        if ((Process.GetProcessesByName(name).Length > 0) == expected) return true;
        Thread.Sleep(250);
    }
    return false;
}
```

> Process names are the `-a` names (no `.exe`): `PowerToys.ColorPickerUI`, `PowerToys.ScreenRuler`
> (actually `PowerToys.MeasureToolUI`), `PowerToys.FancyZonesEditor`, etc. — see `ModuleConfigData.cs`
> in the harness for the authoritative list.

## Recipe 3 — Read the activation shortcut from a `ShortcutControl`

PowerToys' `ShortcutControl` renders the current chord on its inner `EditButton`, exposing the readable
text (e.g. `"Win + Shift + C"`) via `AutomationProperties.HelpText`. `x:Name` reflects as the
`AutomationId` in WinUI when none is set, so:

```csharp
var editButton = Find<Button>(By.AccessibilityId("EditButton"));
string shortcutText = editButton.HelpText;            // "Win + Shift + C"
Key[] keys = ParseShortcutText(shortcutText);          // -> [LWin, Shift, C]
```

When the page has several shortcut controls, scope the search under the specific card first:

```csharp
var card = Session.Find<Element>(By.AccessibilityId("Shortcut_ScreenRuler"));
var editButton = card.Find<Element>(By.AccessibilityId("EditButton"));
```

```csharp
// Shortcut-string parser (ports verbatim from either example; note "win" -> Key.LWin).
private static Key[] ParseShortcutText(string s)
{
    var parts = s.Split(new[] { " + ", "+", " " }, StringSplitOptions.RemoveEmptyEntries);
    var keys = new List<Key>();
    foreach (var raw in parts)
    {
        var p = raw.Trim().ToLowerInvariant();
        Key? k = p switch
        {
            "win" or "windows" => Key.LWin,
            "ctrl" or "control" => Key.Ctrl,
            "shift" => Key.Shift,
            "alt" => Key.Alt,
            _ when p.Length == 1 && p[0] >= 'a' && p[0] <= 'z' => (Key)Enum.Parse(typeof(Key), p.ToUpperInvariant()),
            _ => null,
        };
        if (k.HasValue) keys.Add(k.Value);
    }
    return keys.ToArray();
}
```

## Recipe 4 — Fire a global hotkey reliably

The runner arms its low-level keyboard hook **asynchronously** after a module is enabled, so the very
first chord can be lost. Re-send with patient polling between attempts — and don't re-send too eagerly,
because for some modules re-sending hides/re-shows the target window:

```csharp
const int attempts = 3;
Session? overlay = null;
for (int i = 1; i <= attempts && overlay is null; i++)
{
    KeyboardHelper.SendKeys(keys);
    overlay = WindowsFinder.WaitForWindowByApp(
        "PowerToys.ColorPickerUI", w => w.Width < 300 && w.Height < 200, timeoutMS: 2_500);

    if (overlay is null)
    {
        MouseHelper.MoveTo(cx + 60, cy + 60);          // recovery nudge for cursor-following overlays
        overlay = WindowsFinder.WaitForWindowByApp(
            "PowerToys.ColorPickerUI", w => w.Width < 300 && w.Height < 200, timeoutMS: 2_500);
    }
}
Assert.IsNotNull(overlay, "Activation window did not appear after retries.");
```

> Only the runner's centralized hook can catch a global PowerToys hotkey, which is *why* tests launch
> through the Settings/runner scope. `KeyboardHelper.SendKeys` holds `LWin` via `keybd_event` while
> sending the rest through SendInput — pure injection doesn't reliably trigger `RegisterHotKey`.

## Recipe 5 — Inspect the clipboard around an action

```csharp
ClipboardHelper.Clear();
MouseHelper.LeftClick();                                            // the action that copies
string captured = ClipboardHelper.WaitForText(ignoredValue: string.Empty, timeoutMS: 3_000);
Assert.IsFalse(string.IsNullOrEmpty(captured), "Nothing was copied within 3s.");
```

`ClipboardHelper` already marshals to an STA thread and swallows contention errors — delete any legacy
hand-rolled STA wrapper.

## Recipe 6 — Discover overlay vs. editor windows from one process

```csharp
// Small overlay (transparent/topmost) — filter by size.
var overlay = WindowsFinder.WaitForWindowByApp(
    "PowerToys.ColorPickerUI", w => w.Width < 300 && w.Height < 200, timeoutMS: 2_500);

// Larger editor window from the SAME process.
var editor = WindowsFinder.WaitForWindowByApp(
    "PowerToys.ColorPickerUI", w => w.Width > 300 && w.Height > 300, timeoutMS: 10_000);

// Each returns a Session bound to that window; search within it:
var peer = overlay!.Find(By.AccessibilityId("ColorHexAutomationPeer"), timeoutMS: 2_000);
string hex = peer.Name;
```

> Use `ListByApp`/`WaitForWindowByApp` (process-filtered), **not** `ListAll`, for windows that expose
> their name only via UIA (no Win32 title) — the unfiltered list drops them.

## Recipe 7 — Walk a window's UIA tree (when there's no single selector)

```csharp
var tree = editor.Inspect(depth: 12);                  // JsonElement: { windows:[{ elements:[{type,name,value,children}] }] }
var values = new List<(string Type, string Name, string Value)>();
WalkElements(tree, values);                             // recursive walk (see ColorPicker example)
bool found = values.Any(v =>
    v.Name.Contains(captured, StringComparison.OrdinalIgnoreCase) ||
    v.Value.Contains(captured, StringComparison.OrdinalIgnoreCase));
Assert.IsTrue(found, $"'{captured}' not found in editor tree.");
```

Use this when a value can appear in any of several controls (e.g. ColorPicker's editor renders the
captured color in whichever format control matches) and you only need "it's somewhere in the tree".

## Recipe 8 — Read a value the UIA Name hides

When `AutomationProperties.Name` overrides the UIA Name with a friendly label (e.g. a color *name*
instead of its HEX), `GetValue()` still reads the underlying Text/Value binding:

```csharp
string displayed = Find<TextBlock>(By.AccessibilityId("SomeLabel")).GetValue();   // the real text, not the Name
```

## Recipe 9 — Enable ONLY the module under test (deterministic, faster, isolated)

Pass `enableModules` to the base ctor so exactly those modules are on before launch — and for a
single-module suite, pass **just the one you're testing**. `ConfigureGlobalModuleSettings` enables the
named modules and **disables every other one**, so the runner boots only what you need:

```csharp
// All five ScreenRuler test classes do this; ColorPicker too. The key is the settings.json
// "enabled" name (note spaces, e.g. "Measure Tool", "PowerToys Run") — see the enabled section of
// %LocalAppData%\Microsoft\PowerToys\settings.json or ModuleConfigData.
public MyTests() : base(PowerToysModule.PowerToysSettings, enableModules: new[] { "Measure Tool" }) { }
```

Why it's worth doing on every per-module suite:

- **Faster on a fresh profile (CI).** The runner's `start_enabled_powertoys` phase starts each enabled
  module; on a clean CI profile that's ~15 default-on modules (~10s). Enabling one cuts that to ~1s
  (~9s saved per cold start). *(The hotkey register/unregister loop runs over all modules regardless,
  so it's unchanged — the win is the start phase.)* Locally it's timing-neutral.
- **Isolated + deterministic.** No other module's global hotkey, overlay, or tray behavior can
  interfere with your gesture, and the test starts from a known on/off baseline instead of whatever
  `settings.json` happened to hold.

It's compatible with tests that toggle the module themselves (e.g. ColorPicker toggles OFF→ON to check
the process lifecycle) — the module just starts already-enabled.

For a per-module *setting* (not just enable/disable), edit the module's own settings file before launch:

```csharp
SettingsConfigHelper.UpdateModuleSettings(
    "ColorPicker",
    defaultSettingsContent: "{}",
    settings => settings["copiedColorRepresentation"] = "HEX");
```

## Recipe 10 — Drive controls that live in a *different* window (process-scoped session)

A module's toolbar / overlay / editor is a separate window from Settings. The legacy `global: true`
Find reached into it implicitly; in `.Next` bind a session to that **process** and search there.
`Session.FromProcess` uses the `-a` (process) scope, so it resolves a control across whichever of the
process's windows owns it — ideal for a toolbar that may be one of several windows.

```csharp
// Screen Ruler's toolbar buttons live in PowerToys.MeasureToolUI, NOT the Settings window.
var ruler = Session.FromProcess("PowerToys.MeasureToolUI", PowerToysModule.ScreenRuler, timeoutMS: 5_000);
ruler.Find<Element>(By.AccessibilityId("Button_Spacing"), 15_000).Click();
```

> **Process name ≠ window title.** The Measure Tool's window *title* is `"PowerToys.ScreenRuler"`, but
> the *process* name winappcli's `-a` flag needs is `"PowerToys.MeasureToolUI"`. The authoritative
> process names are in the harness's `ModuleConfigData.cs`.

## Recipe 11 — Center the cursor before a coordinate measurement

```csharp
var size = System.Windows.Forms.SystemInformation.PrimaryMonitorSize;  // PHYSICAL px when DPI-aware (Pitfall 12)
int cx = size.Width / 2, cy = size.Height / 2;
MouseHelper.MoveTo(cx, cy);                                  // park at a known on-screen spot
MouseHelper.Drag(cx - 50, cy - 50, cx + 49, cy + 49);       // 100x100 box centred on screen
```

Never anchor a gesture to the *current* cursor (`GetMousePosition() + 200`) — the cursor can be
anywhere (often near the bottom edge after a toolbar pops up), pushing the gesture off-screen and
producing a wrong/empty measurement. `System.Windows.Forms` flows transitively from the harness
(`UseWindowsForms=true`), so you can call `SystemInformation` without adding a reference.

**Move in steps so the overlay tracks the cursor.** A coordinate gesture must land on-screen, and the
module's overlay needs to see the cursor *move* before the click — a single `SetCursorPos` can land
without a tracked move, leaving the measurement empty. Park at a known on-screen point (screen-centre)
and move in a couple of steps:

```csharp
var (cx, cy) = ScreenCenter();
MouseHelper.MoveTo(cx - 60, cy - 60);  // first move...
Thread.Sleep(200);
MouseHelper.MoveTo(cx, cy);            // ...then settle on the target so the overlay is tracking
Thread.Sleep(400);
MouseHelper.LeftClick();               // or Drag(...) for a free-form box
```

## Recipe 12 — Drive a screen-capture module (cold-start + Win32 overlay presence)

Modules built on Windows.Graphics.Capture (Screen Ruler spacing, Magnifier, Text Extractor, screenshot
tools) need three habits a warm dev box hides — see [ci-stability.md](ci-stability.md) Principle 3. The
ScreenRuler port is the reference (`SelectToolAndVerify` / `MeasureWithRetry` / `ReengageTool` /
`IsMeasureOverlayPresent`).

```csharp
// 1) Detect the capture overlay with Win32 EnumWindows — NOT winappcli list-windows/Inspect, which
//    attaches a UIA client and empties the live capture (Pitfall 18).
private static bool IsOverlayPresent(string processName)
{
    var pids = Process.GetProcessesByName(processName).Select(p => p.Id).ToList();
    return WindowControl.EnumerateProcessWindows(pids)
        .Any(w => w.ClassName.Contains("OverlayWindow", StringComparison.OrdinalIgnoreCase));
}

// 2) Engage the tool, then RETRY until the authoritative signal (the overlay window) confirms — the
//    tool's UIA tree exists before the window is interactive, so an early press is silently dropped.
var deadline = DateTime.UtcNow.AddSeconds(25);
while (!IsOverlayPresent(proc) && DateTime.UtcNow < deadline)
{
    var tool = ruler.Find<Element>(By.AccessibilityId("Button_Spacing"), 8_000);
    if (tool.GetProperty("ToggleState") != "On") tool.Click(msPostAction: 300);  // guard: don't toggle it off
    MouseHelper.MoveTo(cx, cy); Thread.Sleep(500);                                // leave the toolbar onto the surface
}

// 3) Measure, retrying IN PLACE while the clipboard is empty (the WGC first frame cold-starts). If the
//    in-place retries yield nothing, re-engage ONCE (toggle off+on = a fresh capture session) to
//    recover a genuine stall — never close/reopen every attempt (that resets the cold-start).
string result = MeasureWithRetry(() => { MouseHelper.MoveTo(cx, cy); MouseHelper.LeftClick(); }, maxAttempts: 5);
```

> Each test spawns its own module process = its own capture session = its own cold-start; there is no
> cross-test warming, so every capture test must tolerate the first-frame delay on its own.

---

## Pitfalls

1. **`Click` has no `msPreAction` in `.Next`.** Legacy `Click(msPreAction: 1000, msPostAction: 2000)`
   → `Thread.Sleep(1000); el.Click(msPostAction: 2000);`. Forgetting the pre-delay causes flaky clicks
   on slow-rendering pages.
2. **`Click` (invoke) vs. `MouseClick` (real mouse).** `Click` uses UIA InvokePattern (and falls back
   to Toggle/Select/Expand). For elements with **no** invoke pattern (TextBlocks, list labels, headers
   whose ancestor handles the click), `Click` silently does nothing useful — use `MouseClick`.
3. **`By.Name` is a substring match and may return many hits.** Always `FindAll` + filter when the
   name isn't unique. Prefer `By.AccessibilityId`.
4. **No `global` parameter.** If a legacy `Find(by, t, global: true)` reached into a popup/other
   window, switch the session scope (`Session.FromProcess`) or discover the window via `WindowsFinder`.
5. **`PowerToysModule.FancyZone` was renamed to `FancyZonesEditor`.** Update the enum value.
6. **Don't launch overlay/utility module exes standalone.** Drive `ColorPicker`/`LightSwitch`/etc.
   through the `PowerToysSettings` scope so the runner owns the hotkey and toggles; a standalone exe
   has no runner behind it.
7. **`System.Threading.Timer` is ambiguous** in this harness (WinForms is referenced and also defines
   `Timer`). Fully-qualify if you add one. (Rare in tests, common if you port harness-level code.)
8. **Cached element geometry is a snapshot.** Re-`Find` before using `X/Y/Width/Height` for a
   drag/mouse-click if the UI moved since the lookup.
9. **Restore state you change.** Toggles, settings.json edits, and clipboard contents must be restored
   in a `finally` so a failure mid-test doesn't poison the next one. Make cleanup tolerant
   (`WindowControl.Try*`) so it never masks the real failure.
10. **First-build/NuGet errors** → run `tools\build\build-essentials.cmd` once before the per-project
    build (or `dotnet restore <csproj> -p:Platform=x64`). A missing `project.assets.json` shows up as
    `NETSDK1004`. Missing `Common.Dotnet.CsWinRT.props` import → CI's `verifyCommonProps.ps1` fails;
    the template already includes it.
11. **`winapp.exe` missing at run time** is expected on a headless agent — the project still *builds*.
    Don't treat a missing-CLI run failure as a migration defect; report build-clean + ready-to-run.
12. **Coordinate-exact tests need an `app.manifest` with `PerMonitorV2`.** Without it the test host is
    DPI-unaware, so `MouseHelper`'s `SetCursorPos`/`GetCursorPos` coordinates are virtualized by the
    display scale and stop matching winappcli's PHYSICAL-pixel bounds. On a 150% display a 99px drag
    measured as ~149px (Screen Ruler reported `150 x 149` instead of `100 x 100`). Copy the manifest
    from the module's legacy UITests project (or [templates/app.manifest](../templates/app.manifest))
    and add `<ApplicationManifest>app.manifest</ApplicationManifest>` to the csproj. Regex-only
    assertions (e.g. `\d+ x \d+`) don't notice the scale — only exact-value tests fail, which makes
    this easy to miss.
    **Why the legacy project's manifest doesn't save it:** a legacy `OutputType=Library` test runs
    inside `testhost.exe` (vstest), whose manifest — not the test DLL's — governs DPI awareness, so the
    legacy `app.manifest` is silently ignored and its coordinate-exact tests can't be DPI-correct on a
    scaled display (the ScreenRuler legacy Bounds test fails `150 x 149` even *with* its manifest). A
    `.Next` project is an `OutputType=Exe` (MTP), so ITS manifest applies to its own process — which is
    why adding the manifest actually fixes the port, and can make it pass where the legacy can't.
13. **Anchor coordinate gestures to the screen centre, not the current cursor** (Recipe 11). This is
    the #1 cause of "measurement is wrong/empty" — the cursor drifts to the bottom edge after a
    toolbar appears.
14. **Global-hotkey activation is racy right after enabling a module.** The runner arms its keyboard
    hook asynchronously, so the first chord is easily lost. Settle ~1.5s after the toggle, then
    re-send the chord and poll for the window, for several attempts (SKILL Recipe 4; the ScreenRuler
    `SendShortcutUntilVisible` helper is the reference).
15. **Per-test cold relaunch amplifies flakiness.** By default each `[TestMethod]` kills + relaunches
    the runner, so every test pays the startup + hook-arming cost. For a suite of cheap cases against
    one page, consider `ReuseScopeAcrossTests => true` (one launch per class). Content-dependent
    measurements (spacing edge-detection) also vary with what's under the cursor — assert on **format**
    (regex) unless the gesture is content-independent (a free-form drag like Bounds), where an exact
    value is safe.
16. **Coordinate gestures break when the window/cursor is off-screen — and it only shows on CI.** A
    `WindowSize` preset that resized but kept its old top-left could push the Settings window (and the
    measurement area) partially off a same-sized 1920×1080 CI display, so the gesture landed off-screen
    and nothing was captured (empty clipboard). It passed **locally** only because a higher-res dev
    display left everything on-screen — so don't trust a local pass for coordinate tests. The harness
    now **centers and clamps** `WindowSize` presets to ~90% of the display, keeping the window fully
    on-screen; anchor gestures to `ScreenCenter()` (always on-screen) and move in steps (Recipe 11).
    You do **not** need to minimize or move the covering window — an overlay module like the Measure
    Tool captures the gesture even with the Settings window underneath (verified); the failure was the
    off-screen position, not the window covering the centre.
17. **The first-run "Welcome to PowerToys" / "What's new" window appears on a fresh profile (CI) and
    eats centre-screen gestures.** On a clean profile the runner opens the OOBE (Welcome) or SCOOBE
    (what's-new) window — **centered and topmost** — so a coordinate measurement at screen-centre lands
    on it instead of the module overlay (empty clipboard). It never shows on a dev box because your
    profile already marked them seen — the *same* local-passes/CI-fails trap as Pitfall 16, and the
    hardest to spot because the runner log still shows the hotkey firing and the module activating. The
    harness now suppresses both in `PreTestHygiene` via
    `SettingsConfigHelper.SuppressFirstRunExperience()` (seeds `oobe_settings.json`
    `openedAtFirstLaunch=true` + `settings.json` `show_whats_new_after_updates=false`, mirroring the
    runner's own gating). If you drive coordinate gestures and see "passes local, empty result on CI",
    suspect a stray fresh-run window first.
18. **Never walk a live screen-capture window's UIA tree.** winappcli `list-windows` / `Inspect` (and
    any `Find` that enumerates the overlay) attaches a UI Automation client, and walking the tree
    **disturbs a Windows.Graphics.Capture session** — the very next frame comes back empty, so the
    measurement is blank with no error. Detect capture overlays with Win32
    `WindowControl.EnumerateProcessWindows` (by class/title) and read the result from the **clipboard**,
    not the overlay's UIA. (Reading UIA on a *non*-capture window is fine.) See Recipe 12 and the
    Win32-window vs UIA-element mental model in [ci-stability.md](ci-stability.md).
19. **A physical click on the FIRST interaction after a window appears is racy.** A window's UIA tree
    exists a moment before the window is interactive-for-mouse-input, so a real click that lands early
    is silently dropped — flaky, and only on slower agents (it cost a Win10-only "NavigationViewItem
    not found" until navigation was moved to UIA invoke). Navigation is almost always the first
    interaction: activate a `NavigationViewItem` with `By.AccessibilityId(...).Click()` (the harness
    routes it to a coordinate-free UIA invoke), not a raw `MouseHelper`/`MouseClick`. See
    [ci-stability.md](ci-stability.md) Principle 2.
