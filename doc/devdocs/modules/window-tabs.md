# Window Tabs (development)

Related proposal: [PowerToys issue #10](https://github.com/microsoft/PowerToys/issues/10).

Window Tabs is an opt-in native module for grouping resizable application windows.
Enable it in Settings under Windowing & Layout. Hold Shift when starting a window
drag and drop its title bar on another window's title bar or tab strip. Windows
from different applications can share a group. Drag a tab along the strip to
reorder it, onto a different strip to transfer it, or outside the strip to detach.
Right-click a tab to detach it, close its application window, or ungroup all
windows. Middle-click closes a tab's application window. Closing posts WM_CLOSE;
the application retains control of save prompts and cancellation.

The default next/previous shortcuts are Win+Ctrl+PageDown and Win+Ctrl+PageUp.
Settings can change both shortcuts. While the strip has keyboard focus,
Ctrl+Tab / Ctrl+Shift+Tab switch tabs, Delete requests close, and Shift+Delete
detaches the selected tab. The strip uses the native tab control, including its
standard accessibility and overflow navigation.

## Design

`src/modules/WindowTabs/dllmain.cpp` implements PowertoyModuleIface. Runner loads
`PowerToys.WindowTabs.dll`; the service starts only when enabled. The service owns
a message-loop thread, two out-of-context WinEvent hooks and one native strip per
group. Hook callbacks post events; they never wait on application windows.
Group state lives exclusively on the service thread. Disable and destruction
stop that thread and restore original window placements before unloading the DLL.

Application windows stay top-level windows. No process injection, SetParent,
style mutation, keyboard injection, or elevation is used. Selecting a tab aligns
its window to the group's current rectangle and minimizes its siblings. Existing
taskbar and Alt+Tab entries remain available, including after a service crash.
Foreground events synchronize selection when a user activates a minimized member
through the shell. A property on each captured HWND prevents treating a recycled
handle as the original window.

Elevated, protected, inaccessible, hung, cloaked, fullscreen, owned, non-resizable,
tool, topmost, and PowerToys-process windows are excluded. A member that becomes
disabled by a modal dialog or stops responding is released. Unsupported windows
are left unchanged. This deliberately conservative eligibility policy needs
validation against actual desktop applications before expanding support.

The strip follows window position and DPI on a 500 ms timer. At a monitor's top
edge it occupies part of the title bar. Mixed-DPI placement, maximized windows,
virtual desktops, native snap interactions and accessibility need manual QA.
The module doesn't provide a single combined taskbar entry or replace native
application title bars. It is not a complete Groupy 2 replacement.

## Settings and sessions

The stable key is `WindowTabs`; its enabled flag defaults to false in both Runner
and the managed settings model. Existing general-settings and `powertoys` IPC
envelopes are reused. Native and managed property names are:

| Property | Shape | Default |
| --- | --- | --- |
| `restore_session` | `{ "value": true }` | true |
| `next_tab_shortcut` | HotkeySettings | Win+Ctrl+PageDown |
| `previous_tab_shortcut` | HotkeySettings | Win+Ctrl+PageUp |

Groups are stored in `%LOCALAPPDATA%\Microsoft\PowerToys\WindowTabs\session.json`.
The versioned JSON records ordered executable paths, window classes, titles and
the selected tab. It never records HWNDs or command lines. Saves replace the file
atomically. Input size, field sizes, group counts, tab counts, duplicate identities
and selection indices are validated. Invalid or unreadable sessions are ignored.
Titles and executable paths are local data and are not included in telemetry.

Restoration reconnects groups only when every saved member has exactly one
matching live window. Pending groups are retried as applications reopen; no
executables or documents are launched. Ambiguous identities and groups with
identical application/class/title tuples aren't restored. Window geometry and
document contents aren't persisted. Disabling restoration clears pending groups
and removes the session file when the service processes the setting.

## Validation

Build from each changed project directory with `tools\build\build.cmd`, after
`tools\build\build-essentials.cmd` has succeeded. Build
`src/modules/WindowTabs/UnitTests` before running WindowTabs.UnitTests.dll with
vstest.console.exe. Build Settings.UI.UnitTests and run its WindowTabs model and
view-model tests. The native tests cover selection, removal, ordering, size limits,
Unicode sessions, corrupted input, duplicate identities and schema validation.
The settings tests cover defaults, AOT serialization, persisted property names and
outgoing enabled/configuration IPC messages.

`FuzzingTest` follows the existing native libFuzzer/ASan project pattern and includes
OneFuzz configuration. It exercises both model invariants and untrusted session
input. Release x64 is the fuzz configuration; ARM64 is excluded from this target.
Debug runs a fixed seed. The separate `UnitTests/PortableModelTests.cpp` harness
can be compiled with a C++20 compiler and UBSan to check model transitions without
Windows dependencies; it does not validate the Win32 service or Settings UI.

Before review, exercise grouping, selection, tab dragging, close cancellation,
detachment, process crashes, module disable/enable, Runner exit/restart, corrupt
sessions and matching windows reopening in different orders. Use two monitors
with different DPI settings. Verify unsupported windows remain unchanged.

## Local test launcher

`Tests/WindowTabs.TestHost` loads the built module DLL through the normal module
interface and provides a visible Stop button. Its `--smoke-test` mode checks
service startup and shutdown. `--sample-notes` and `--sample-research` each open
a disposable window in a separate process for interactive testing. The launcher
and samples are test artifacts, outside the installer's root application folder.

On the development PC, x64 Debug builds of the module and launcher passed with
code analysis, all 12 native unit tests passed, and startup/shutdown passed.
The Release x64 ASan/libFuzzer target built and completed 1,000 seeded runs.
A saved two-window sample group was restored in the live launcher, and its Stop
button was exercised. Drag grouping, tab clicks, reordering and detachment still
need broader interactive testing, including real applications and mixed DPI.

## Outstanding integration gates

Visual Studio Build Tools and .NET SDK 10.0.403 are now installed on the development
PC. The full solution restore still fails with MSB4086 toolset-version errors in
Command Palette, Measure Tool and PowerRename projects. Settings UI builds and
managed Settings tests remain unverified.
Installer harvesting already includes DLLs in the root output directory and the
new DLL is listed for signing. Policy integration is pending clarification under
the repository's AGENTS.md rules; the module must not be released until its
Runner and Settings policy behavior is implemented and verified consistently.
Keep any pull request in draft until these gates are resolved.
