# API mapping cheat sheet (legacy → `.Next`)

Keep this open while editing. Left column is the legacy `Microsoft.PowerToys.UITest` API; right column
is the `Microsoft.PowerToys.UITest.Next` equivalent. "—" means no direct member; see the Notes.

## Namespaces & usings

| Legacy | `.Next` |
|---|---|
| `using Microsoft.PowerToys.UITest;` | `using Microsoft.PowerToys.UITest.Next;` |
| `using OpenQA.Selenium;` / `…Appium…` | *(delete — no Selenium/Appium)* |
| `[TestClass] : UITestBase` | `[TestClass] : UITestBase` *(same shape; different namespace)* |
| `using Microsoft.VisualStudio.TestTools.UnitTesting;` | *(unchanged)* |

## `UITestBase` (the base class)

| Legacy | `.Next` | Notes |
|---|---|---|
| `: base(PowerToysModule.PowerToysSettings)` | `: base(PowerToysModule.PowerToysSettings)` | Same enum name; **values differ** — see enum table below. |
| `: base(scope, WindowSize.Large)` | `: base(scope, WindowSize.Large)` | Same `WindowSize` enum. |
| `: base(scope, size, commandLineArgs: new[]{…})` | `: base(scope, size, enableModules: new[]{…})` | 3rd arg changed from launch args to a deterministic module-enable list. |
| `Session` (property) | `Session` (property) | Same name. Legacy is `required set`; `.Next` is `private set` (assigned by `TestInit`). |
| `Find<T>(by, timeoutMS, global)` | `Find<T>(by, timeoutMS)` | No `global` param (see framework-differences §4). |
| `Find(name)` / `Find<T>(name)` | `Find(name)` / `Find<T>(name)` | Same. |
| `Has<T>/HasOne<T>(by, …, global)` | `Has<T>/HasOne<T>(by, …)` | No `global`. |
| `FindByPartialName<T>(s)` | `Find<T>(By.Name(s))` | winappcli `By.Name` is already a substring match. |
| `FindByPattern<T>(regex)` | `Session.FindAll<T>(By.Name(...))` + C# `Regex` | No base helper; filter in C#. |
| `FindByClassName<T>(c)` | `Find<T>(By.Name(...))` with a typed wrapper | Wrappers pin ClassName; or `FindAll` + filter on `.ClassName`. |
| `SendKeys(Key[])` / `SendKeySequence(Key[])` | `KeyboardHelper.SendKeys(Key[])` | Static helper (also `Session.SendKeys` passthrough). |
| `MoveMouseTo(x,y)` | `MouseHelper.MoveTo(x,y)` | Static helper. |
| `GetMousePosition()` → `(int,int)` | `MouseHelper.GetMousePosition()` → `(int X,int Y)` | Static helper. |
| `IsWindowOpen(name)` | `WindowsFinder.ListByApp(proc).Count > 0` | Or `SessionHelper.IsRunning(scope)` for a process check. |
| `RestartScopeExe(enableModules?)` | `RestartScope(enableModules?)` | Returns the fresh `Session`. |
| `ExitScopeExe()` | *(automatic)* `sessionHelper.StopIfStarted()` in `TestCleanup` | Rarely needed manually. |

## `PowerToysModule` enum (values differ!)

| Legacy value | `.Next` value | Notes |
|---|---|---|
| `PowerToysSettings` | `PowerToysSettings` | Same. The default; drive most modules through it. |
| `FancyZone` | `FancyZonesEditor` | **Renamed.** |
| `Hosts` | `Hosts` | Same. |
| `Runner` | `Runner` | Same. |
| `Workspaces` | `Workspaces` | Same. |
| `PowerRename` | `PowerRename` | Same. |
| `CommandPalette` | `CommandPalette` | Same. |
| `ScreenRuler` | `ScreenRuler` | Same. |
| `LightSwitch` | `LightSwitch` | Same. |
| *(n/a)* | `ColorPicker` | New entry (overlay module — drive via the Settings scope). |

## `By` selectors

| Legacy | `.Next` | Notes |
|---|---|---|
| `By.Name("x")` | `By.Name("x")` | winappcli = case-insensitive **substring** over Name/AutomationId. |
| `By.AccessibilityId("Id")` | `By.AccessibilityId("Id")` | **Preferred.** Also `By.Id("Id")`. |
| `By.Id("Id")` | `By.Id("Id")` / `By.AccessibilityId("Id")` | Same intent. |
| `By.ClassName("C")` | *(none)* | Use a typed wrapper, or `FindAll` + filter on `.ClassName`. |
| `By.XPath("//*[contains(@Name,'x')]")` | `By.Name("x")` | Substring search covers `contains(@Name)`. |
| `By.XPath("//*[@Name='x']")` | `By.Name("x")` (+ C# exact filter if needed) | |
| `By.XPath` (structural axes) | scoped `element.Find<T>(By.…)` or `FindAll` + C# filter | No XPath engine. |
| `By.CssSelector(...)` | *(none)* | Re-express as above. |
| *(n/a)* | `By.Slug("btn-x-1a2b")` | Direct slug from `inspect`/`search` output. |

## `Element` — properties

| Legacy | `.Next` | Notes |
|---|---|---|
| `Name` | `Name` | `.Next` is cached at Find time; re-find for fresh. |
| `ClassName` | `ClassName` | Cached. |
| `ControlType` | `ControlType` | Cached. |
| `Text` | `GetValue()` | TextPattern→ValuePattern→Selection→Name fallback. |
| `Enabled` | `IsEnabled` | Live read via `get-property`. |
| `Displayed` | `Displayed` (== `!IsOffscreen`) | Live read. |
| `Selected` | `Selected` | Live read (`IsSelected`). |
| `AutomationId` | `AutomationId` | Live read. |
| `HelpText` | `HelpText` | Live read (used for `ShortcutControl` text). |
| `Rect` → `Rectangle?` | `X`, `Y`, `Width`, `Height` (ints) | Cached snapshot; re-find if UI moved. |
| `GetAttribute("P")` | `GetAttribute("P")` / `GetProperty("P")` | Both live-read one UIA property. |

## `Element` — actions

| Legacy | `.Next` | Notes |
|---|---|---|
| `Click(rightClick=false, msPreAction=500, msPostAction=500)` | `Click(rightClick=false, msPostAction=200)` | **No `msPreAction`.** Uses UIA invoke (falls back to toggle/select/expand); `rightClick` → `click --right`. Add an explicit `Thread.Sleep` before if you relied on `msPreAction`. |
| `Click()` on a non-invokable element (TextBlock/ListItem) | `MouseClick(msPostAction=200)` | Real mouse simulation — use when the click is handled by an ancestor (the ColorPicker utility-stack label pattern). |
| `DoubleClick()` | `DoubleClick(msPostAction=200)` | Real mouse double-click. |
| Selenium `Actions` drag | `Drag(offsetX, offsetY, steps=10)` / `DragTo(target)` | Win32 mouse; uses cached center. |
| `Actions` key-down + drag | `KeyDownAndDrag(key, targetX, targetY, steps)` | Modifier-drag (FancyZones merge, tab tear-off). |
| `ReleaseKey(key)` | `KeyboardHelper.ReleaseKey(key)` | |
| `SetText`/`Clear`+`SendKeys` (TextBox) | `TextBox.SetText("v")` | `winapp ui set-value`. |
| `element.Find<T>(by)` | `element.Find<T>(by)` | Scoped search under the element. |
| `ScrollIntoView()` | `ScrollIntoView()` | Same. |
| — | `Scroll(ScrollDirection)`, `ScrollToEdge(toBottom)` | New scroll verbs. |
| — | `Focus()` | `winapp ui focus`. |
| — | `WaitForProperty(p, v, t)`, `WaitForValue(v, contains, t)`, `WaitForGone(t)` | Built-in waits (replace manual poll loops). |

## `Session`

| Legacy | `.Next` | Notes |
|---|---|---|
| `Find<T>(by, t, global)` / `Find(name)` | `Find<T>(by, t)` / `Find(name)` | No `global`. |
| `FindAll<T>(by, t, global)` | `FindAll<T>(by, t)` | No `global`; polls until found or timeout. |
| `Has`/`HasOne`/`Has<T>` | `Has`/`HasOne<T>`/`Has<T>` | Same intent. |
| `Attach(PowerToysModule)` / `Attach(windowName)` | `Session.Attach(module, size?)` / `Session.FromProcess(app)` / `WindowsFinder.WaitForWindowByApp(...)` | Re-bind to another window/process. |
| `SendKeys(Key[])` / `SendKey(key, …)` | `Session.SendKeys(Key[])` or `KeyboardHelper.SendKeys(Key[])` | Prefer the static helper. |
| `MoveMouseTo(x,y, …)` | `MouseHelper.MoveTo(x,y)` | Static. |
| `PerformMouseAction(MouseActionType.LeftClick)` | `MouseHelper.LeftClick()` | See action map below. |
| `SetMainWindowSize(size)` | `WindowHelper.SetWindowSize(hwnd, size)` | `hwnd = new IntPtr(Session.WindowHandle)`. |
| `MainWindowHandler` (`IntPtr`) | `WindowHandle` (`long`) / `WindowHandleArg` (string) | |
| — | `Inspect(depth, interactive, …)` → `JsonElement` | `winapp ui inspect --json` tree (the ColorPicker editor walk). |
| — | `WaitForElement(by, t)`, `WaitFor(Func<bool>, t)` | Built-in waits. |
| — | `Screenshot(path, element?, captureScreen?)` / `TryScreenshot(...)` | |

### `MouseActionType` → `MouseHelper`

| Legacy `PerformMouseAction(...)` | `.Next` |
|---|---|
| `MouseActionType.LeftClick` | `MouseHelper.LeftClick()` |
| `MouseActionType.RightClick` | `MouseHelper.RightClick()` |
| `MouseActionType.LeftDown` / `LeftUp` | `MouseHelper.LeftDown()` / `LeftUp()` |
| `MouseActionType.RightDown` / `RightUp` | `MouseHelper.RightDown()` / `RightUp()` |
| (scroll) | `MouseHelper.ScrollUp()` / `ScrollDown()` / `ScrollWheel(amount)` |
| (drag) | `MouseHelper.Drag(fromX, fromY, toX, toY, steps)` |

## Static helpers (new — no instance equivalent)

| Need | `.Next` helper |
|---|---|
| Send a key chord (incl. global Win-key hotkeys) | `KeyboardHelper.SendKeys(Key.LWin, Key.Shift, Key.C)` |
| Hold/release a key | `KeyboardHelper.PressKey(key)` / `KeyboardHelper.ReleaseKey(key)` |
| Move cursor / read cursor | `MouseHelper.MoveTo(x,y)` / `MouseHelper.GetMousePosition()` |
| Click at the current/again a point | `MouseHelper.LeftClick()` / `LeftClickAt(x,y)` / `RightClick()` / `DoubleClick()` |
| Read clipboard | `ClipboardHelper.GetText()` |
| Clear clipboard | `ClipboardHelper.Clear()` |
| Set clipboard | `ClipboardHelper.SetText("v")` |
| Wait for clipboard to change | `ClipboardHelper.WaitForText(ignoredValue, timeoutMS)` |
| Seed module on/off baseline | `SettingsConfigHelper.ConfigureGlobalModuleSettings("ColorPicker", …)` |
| Edit a module's own settings.json | `SettingsConfigHelper.UpdateModuleSettings(name, default, json => {…})` |

> The legacy `TestHelper.ClearClipboard`/`GetClipboardText` STA-thread wrappers are replaced by
> `ClipboardHelper` (which already runs on an STA thread internally). Delete the hand-rolled STA code.

## Element wrappers (`Find<T>`)

| Wrapper | Legacy | `.Next` | Notes |
|---|---|---|---|
| `Element` | ✅ | ✅ | Base. |
| `Button` | ✅ | ✅ | |
| `CheckBox` | ✅ | ✅ | |
| `ComboBox` | ✅ | ✅ | `.Select(item)` / `.SelectByText(text)` / `.SelectedText`. |
| `RadioButton` | ✅ | ✅ | |
| `Slider` | ✅ | ✅ | |
| `Tab` | ✅ | ✅ | |
| `TextBlock` | ✅ | ✅ | |
| `TextBox` | ✅ | ✅ | `.SetText(v)` / `.Value`. |
| `ToggleSwitch` | ✅ | ✅ | `.IsOn` / `.Toggle(bool)`. Pins `ClassName="ToggleSwitch"`. |
| `Thumb` | ✅ | ✅ | |
| `NavigationViewItem` | ✅ | ✅ | UIA `ListItem`. |
| `Pane` | ✅ | ✅ | |
| `Custom` | ✅ | ✅ | UIA `Custom` (FancyZones zones, Workspaces canvas). |
| `Window` | ✅ | ✅ | |
| `Group` | ✅ | ❌ | Use `Find<Element>` or add a wrapper. |
| `HyperlinkButton` | ✅ | ❌ | Use `Find<Button>` (it's a Button under UIA) or add a wrapper. |

## `Key` enum

Both frameworks expose a `Key` enum. The `.Next` `Key` (in `KeyboardHelper.cs`) uses `LWin` (not
`Win`). When porting a shortcut parser, map `"win"`/`"windows"` → `Key.LWin`. Letters `A`–`Z`,
digits `Num0`–`Num9`, `F1`–`F12`, and the usual `Ctrl/Shift/Alt/Esc/Enter/Tab/Space/Arrows` are all
present.
