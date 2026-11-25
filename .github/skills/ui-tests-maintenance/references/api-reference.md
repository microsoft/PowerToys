# UI-test API reference

Import `Microsoft.PowerToys.UITest` for the shared framework. See the
[framework guide](framework-guide.md) for session scope, selector behavior, and test lifecycle.

## Find elements and observe state

| API | Use |
|---|---|
| `Find<T>(by, timeoutMS)` / `Find(name)` | Find a required element. Use `Element.Find<T>` to scope a search under a container. |
| `Session.FindAll<T>(by, timeoutMS)` | Find matching elements and filter the result when names are ambiguous. |
| `Has<T>` / `HasOne<T>` | Probe for a match without treating its absence as a failed assertion. |
| `Session.WaitForElement(by, timeoutMS)` / `Session.WaitFor(predicate, timeoutMS)` | Wait for an element or condition. |
| `Element.WaitForProperty(...)`, `WaitForValue(...)`, `WaitForGone(...)` | Wait for the UIA state expected after an action. |
| `Element.GetValue()` | Read the control's value using its UIA patterns. |
| `Element.GetProperty(...)`, `IsEnabled`, `Displayed`, `Selected`, `AutomationId`, `HelpText` | Read live UIA state. `Displayed` reflects `!IsOffscreen`. |
| `Element.Name`, `ClassName`, `ControlType`, `X`, `Y`, `Width`, `Height` | Read the snapshot from the lookup. Re-find after navigation, movement, or layout changes. |
| `Session.Inspect(...)` | Inspect the live UIA tree as JSON. |

Use the typed wrappers `Button`, `CheckBox`, `ComboBox`, `Custom`, `NavigationViewItem`,
`NumberBox`, `Pane`, `RadioButton`, `Slider`, `Tab`, `TextBlock`, `TextBox`, `Thumb`,
`ToggleSwitch`, and `Window` where appropriate. For example, use `TextBox.SetText(value)`,
`ToggleSwitch.IsOn` / `Toggle(bool)`, and `ComboBox.SelectByText(text)` / `SelectedText`.
Use `Element` when a typed wrapper is unavailable or unnecessary.

## Act on controls

| API | Use |
|---|---|
| `Element.Invoke()` | Activate via UIA without moving the mouse. Prefer for ordinary control activation when available. |
| `Element.Click()` / `MouseClick()` / `DoubleClick()` | Exercise physical input. `Click` uses physical bounds and falls back to `Invoke` for zero bounds. `NavigationViewItem.Click()` uses `Invoke` for ordinary activation. |
| `Element.Drag(...)` / `DragTo(...)` / `KeyDownAndDrag(...)` | Drag from the element using its cached geometry; re-find after layout changes. |
| `Element.Focus()`, `ScrollIntoView()`, `Scroll(...)`, `ScrollToEdge(...)` | Focus or bring a control into view. |
| `KeyboardHelper.SendKeys(Key.LWin, Key.Shift, Key.C)` | Send a chord. For individual keys use `PressKey` and `ReleaseKey`, and release held keys in cleanup. |
| `MouseHelper.MoveTo`, `GetMousePosition`, `LeftClickAt`, `Drag`, `ScrollWheel` | Exercise physical mouse interaction with explicit coordinates or wheel detents. |
| `ClipboardHelper.GetText`, `Clear`, `SetText`, `WaitForText` | Read, write, or wait for clipboard text. The helper manages its own STA thread. |

## Windows, fixtures, and diagnostics

| API | Use |
|---|---|
| `Session.Attach(...)`, `Session.FromProcess(...)`, `WindowsFinder.WaitForWindowByApp(...)` | Bind to a module, process, or newly discovered window. |
| `WindowControl.WaitForForeground(...)`, `GetForegroundWindowInfo()` | Confirm the exact HWND owns foreground input, or inspect the current owner. |
| `WindowControl.TryCloseByApp(...)`, `TryKillProcessTreeByNameAndWait(...)` | Clean up an owned window or process tree; use specific names and predicates. |
| `WindowHelper.GetVisibleBounds(...)`, `IsWindowCloaked(...)`, `MonitorInfo.GetFromWindow(...)` | Check physical bounds, DWM visibility, and the window's monitor work area. |
| `Session.Screenshot(...)` / `TryScreenshot(...)` | Capture a window, element, or desktop for diagnostics. |
| `Session.ScreenshotVisibleWindow(...)` | Capture composed WinUI/WebView content from the visible desktop. Requires a foreground window session. |
| `WaitHelper.WaitForStable(...)` | Require consecutive matching samples from an authoritative state source. |
| `ExplorerControl`, `ExplorerShell`, `ShellMenu` | Manage Explorer windows, Shell selection and view mode, and context menus. See [Explorer and Shell tests](explorer-shell-tests.md). |
| `FileSystemAssert.AreFilesEqual(...)`, `AreDirectoryTreesEqual(...)` | Compare files or directory trees, including empty directories. |
| `SettingsConfigHelper.ConfigureGlobalModuleSettings(...)`, `UpdateModuleSettings(...)` | Seed module enablement or set up a module's own configuration. Do not use these as fallbacks for assertions about live Settings interactions. |
