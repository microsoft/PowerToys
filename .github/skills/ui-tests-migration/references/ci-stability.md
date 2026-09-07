# CI stability — get a port green in fewer iterations

The expensive gap in a UI-test migration is between **"passes on my box"** and **"green on the CI
agent."** Every round-trip through that gap is a push, a queue wait, and a log dig. This document
distills the failure modes that cause those round-trips into (1) a **mental model**, (2) **design
principles to bake in from the first commit**, and (3) a **pre-flight checklist** so the first CI run
is the *validation*, not the *discovery*.

Read this **after** [patterns-and-pitfalls.md](patterns-and-pitfalls.md) (it references those recipes
and pitfalls by number). The canonical worked example for everything below is the ScreenRuler port:
[ScreenRuler.UITests.Next/TestHelper.cs](../../../../src/modules/MeasureTool/Tests/ScreenRuler.UITests.Next/TestHelper.cs).

> **Why this matters for iteration count.** Almost every "flaky on CI, fine locally" failure traces to
> one of the root causes below. A dev box hides them (higher-res display, warmed caches, a
> profile that already dismissed first-run windows, a human not touching the mouse). If you design for
> them up-front, the first CI run tends to be green; if you don't, you rediscover them one push at a
> time.

---

## The core mental model: Win32 **windows** vs UIA **elements**

This single distinction drives the right tool choice for almost every interaction, and picking the
wrong layer is the #1 source of slow, racy CI failures.

| Layer | Sees | Cost / risk | Use it for |
|---|---|---|---|
| **Win32** (`WindowControl`, `WindowsFinder`) | **WINDOWS** — HWNDs: handle, class, title, rect, PID, visibility | Cheap, synchronous, attaches **no** UIA client | "Is it open? where/how big? is it visible? close/focus it?" — anything **window**-level |
| **UIA / winappcli** (`Find`, `GetProperty`, `Invoke`, `Inspect`) | **ELEMENTS** — XAML controls inside one window's content island (**not** child HWNDs) | Spins up a UIA client, walks a tree; can **race** a not-yet-ready window and **disturb** a live screen-capture | Reading element **state** (ToggleState/Name/Value), and **locating** a control's rect to act on it |

The operating rules that fall out of this:

- **Window-level question → Win32.** Presence, size, visibility, close, focus. Never ask winappcli
  "is this window up?" when `WindowControl.EnumerateProcessWindows(pids)` answers it synchronously
  without touching UIA.
- **Act on an element → locate once via UIA, then input via Win32.** Search for the control's
  rectangle **once**, then drive a real mouse/keyboard at that point. This is exactly what
  `Element.Click()` does (UIA `search` → `MouseHelper` click at the centre; falls back to a
  coordinate-free UIA invoke only when the control reports no on-screen size).
- **Read element state → UIA (unavoidable).** `ToggleState`, `Name`, `Value` have no Win32 equivalent.
- **NEVER walk a live/capturing window's UIA tree.** Attaching a UIA client and enumerating the tree
  (winappcli `list-windows` / `Inspect`) **disturbs a Windows.Graphics.Capture (WGC) session** and
  empties the very next frame. For a capture module, detect windows with Win32 `EnumWindows`, not UIA
  (Pitfall 18).

### Model the workflow as owned state boundaries

Before writing retries, make a table for every external boundary. Peek's stable workflow was:
`Explorer HWND → Shell selection/focus → hotkey → Peek HWND/title → renderer state → DWM pixels`.

| Boundary | Owner | Authoritative signal | Stability / recovery |
|---|---|---|---|
| Top-level window | Win32 | Exact expected HWND exists and owns foreground | Retry foreground; diagnose foreground PID/title/elevation |
| Explorer selection | Shell view | Exact selected path set plus focused path | Require consecutive samples; repair through `ExplorerShell` |
| Explorer layout | Shell view | Exact view mode and icon size | Set through `ExplorerShell`; verify item geometry |
| Shell extension | Explorer + provider | Provider log plus visible content | Drive through Explorer in the user's context; avoid test-host-only COM probes |
| Toggle hotkey | Runner/module | Any target HWND appeared | Stop resending once a window exists; wait for initialization |
| Renderer | Product automation peer | Product state is `Loaded`; loading UI is gone | Restart the process tree only after a bounded terminal failure |
| Visible output | DWM/compositor | Captured pixels match baseline | Capture composed desktop pixels; do not rewrite baselines first |

For each boundary, name: **action, owner, signal, stable-sample count, retry semantics, and reset
scope**. A wait without these fields usually becomes an arbitrary sleep or a destructive retry.

---

## Principle 1 — Assert on an **authoritative signal**, retry until true (not a fixed sleep)

A `Thread.Sleep(500)` *guesses* how long a step takes; a CI agent can be 10× slower, so the guess is
either flaky (too short) or wasteful (too long). Instead, name the **one observable fact** that proves
the step happened and **poll for it to a deadline**:

| Step | Authoritative signal | How to read it |
|---|---|---|
| Module enabled | its process is running | `Process.GetProcessesByName(name).Length > 0` |
| Tool / overlay engaged | the overlay **window** exists | Win32 `WindowControl.EnumerateProcessWindows` (Pitfall 18) |
| Measurement taken | clipboard is non-empty | `ClipboardHelper.WaitForText` |
| Page navigated | the target control is present | `Session.Has(By.AccessibilityId(...))` |

Retry the **whole interaction** (press → check signal) until the signal is true or the deadline
elapses — don't press once and hope. Reference: `SelectToolAndVerify` presses the toolbar button and
re-checks `IsMeasureOverlayPresent()` on a 25 s deadline; `MeasureWithRetry` re-runs the gesture while
the clipboard is empty. Both adapt to a slow agent for free.

> Corollary: **fail with the signal in the message.** `Assert.Fail("overlay never appeared after N
> attempts")` tells you *which* signal missed on CI; `Assert.IsTrue(x)` tells you nothing.

### Observed once is not necessarily stable

Explorer selection, foreground, window bounds, and renderer state can briefly match and then regress
while deferred UI initializes. Use `WaitHelper.WaitForStable` when readiness must survive several
samples. A mismatch resets the count and may run a recovery action. Keep the observation structured
so timeout diagnostics can report the last state rather than only `false`.

Classify every retry before implementing it:

- **Idempotent**: setting text, selecting an exact Shell item, bringing a known HWND forward. Safe to
  repeat while the signal is false.
- **Toggle**: activation hotkeys, pin buttons, toggle buttons. Re-read state before repeating; once
  any target window appears, do not resend a show/hide chord.
- **Destructive/resetting**: killing a process, recreating WebView2, restarting capture. Use only after
  patient in-place readiness has failed, because the reset discards progress and state.

---

## Principle 2 — The input-method decision: UIA **invoke** vs physical **click**

Two ways to activate a control, with different failure modes:

- **Physical click** (`Element.Click()` → UIA locates the rect, Win32 clicks its centre). Use for
  **real interactions** that need genuine on-screen, foreground input: drags, clicks on a
  capture surface, anything that measures cursor position. Requires the control **visible** and the
  window **interactive-for-mouse-input**.
- **Coordinate-free UIA invoke** (`Element.Invoke()` → InvokePattern → Toggle → Select → Expand).
  Use where a real cursor move is undesirable or the on-screen point is unreliable: **navigation
  items** (they live in a scrollable pane / overflow "…" menu, so they can report a size yet sit
  off-viewport), the **first interaction** right after a window appears, and zero-bounds/off-screen
  controls.

**The iteration-burning trap: the first interaction after a window appears.** A window's UIA tree
exists a moment *before* the window is interactive for mouse input. A physical click that lands in that
window is **silently dropped** — flaky, and only on slower agents. Navigation is almost always the
first interaction, so it must not depend on a physical click.

The harness bakes this in so you get it for free: `NavigationViewItem.Click()` is overridden to a
coordinate-free invoke, and `Element.Click()` falls back to invoke for zero-bounds controls. **So keep
navigating with `Find<NavigationViewItem>(By.AccessibilityId(...)).Click()`** (Recipe 1) — it's
race-safe under the hood. Only
reach for a raw `MouseClick`/manual `MouseHelper` when the interaction genuinely needs real mouse input
(and by then the window has settled).

**The second trap: a background-launched window comes up *behind* the foreground.** A physical click
lands on whatever window is **topmost at those pixels** — not necessarily your target. When a module's
overlay/toolbar is shown by a *background* process (the runner, reacting to a hotkey) while another
window holds the foreground, Windows' **foreground lock** puts it *behind* that window — it's present,
`IsWindowVisible`-true, and un-cloaked, yet occluded. A coordinate click then hits the covering window
and looks **exactly** like the interactivity race, but it's occlusion. This is a prime "passes local,
flakes on CI" cause: on CI the Settings window used to enable the module is still foreground when the
overlay appears. The harness guards against it — `Element.Click()` calls `Session.EnsureForeground()`
first. `WindowControl.TryBringToForeground` is best-effort; use `WaitForForeground` when exact
ownership is required and inspect `GetForegroundWindowInfo()` on failure. UIA `Invoke` is immune to
occlusion because it never touches coordinates.

**Integrity boundaries can make foreground activation impossible.** `AttachThreadInput` does not
override UIPI. A visible elevated helper console can permanently block a non-elevated Explorer or
module window. Pipeline helpers must start hidden (the shared WinAppDriver uses `-WindowStyle Hidden`).
Log the foreground process, title, and elevation before adding more retries. Match the test host and
runner integrity where possible; modules configured to run non-elevated still require their own
foreground handoff.

"Start hidden" means **hidden at process creation**, not enumerate-and-hide afterward. The latter is
a time-of-check/time-of-use race: a shell-launched console can be created after the hide pass and own
foreground just as the target opens. For same-integrity direct children, use `UseShellExecute=false`
plus `CreateNoWindow=true`. If an elevated host must ask Explorer to create a medium-integrity helper,
do not route it through `.cmd`/`start /b`; use a non-activating launcher such as
`WScript.Shell.Run(..., 0, False)` with an encoded command. Smoke-test the launcher independently:
the helper must establish its readiness precondition while exposing no main window and never becoming
the foreground PID.

**Require foreground only when the interaction requires it.** Explorer context menus, SendInput,
coordinate clicks, and drags need stable ownership because focus or z-order changes the operation.
Coordinate-free UIA search/invoke does not. For those flows, an exact-HWND assertion can be a false
negative when WinUI recreates its top-level window or the scheduled interactive host observes
`GetForegroundWindow()==0`; use process/window presence plus the authoritative UIA-ready element,
while keeping foreground activation best-effort and diagnostic. Let the interaction boundary decide —
do not globally weaken strict Explorer or physical-input checks.

---

## Principle 3 — Screen-capture (WGC) modules: cold-start + don't disturb the session

Any module built on **Windows.Graphics.Capture** — Screen Ruler spacing, Magnifier, Text Extractor,
Color Picker's zoom, screenshot tools — shares three facts that a warm dev box hides:

1. **First-frame cold-start.** The first captured frame is instant when warm but can take **several
   seconds** on a cold/headless CI agent. A gesture that reads the result too early gets **nothing**.
2. **Per-process, no cross-test warming.** Each test spawns its own module process = its own capture
   session = its own cold-start. There is no "the previous test warmed it up."
3. **A UIA tree-walk of the live window disturbs/empties it** (the mental-model rule above).

The resilient shape (see `PerformSpacingToolTest` / `MeasureWithRetry` / `ReengageTool` /
`IsMeasureOverlayPresent`, and Recipe 12):

- **Detect** the overlay/window via **Win32 EnumWindows**, never winappcli `list-windows`/`Inspect`.
- **Retry the gesture IN PLACE** (same session) to give the first frame time — do **not** close/reopen
  between attempts (that *resets* the cold-start every time).
- If in-place retries still yield nothing, **RE-ENGAGE ONCE** (tear the session down and recreate it)
  to recover a genuine *stall* — but only once, after a generous in-place window. Re-engaging on every
  attempt is the classic mistake that never recovers.

---

## Principle 4 — Guard state-toggling controls on their current state

A `ToggleSwitch` / toolbar `ToggleButton` flips **relative to its current state**, so a blind "press
to select" can *deselect* an already-engaged control — and an innocent retry can toggle it back off.
**Read the state first** and only press when it's wrong: press when `ToggleState == "Off"` to select,
`"On"` to deselect. `ToggleSwitch.Toggle(bool)` already does this; for a raw toolbar `ToggleButton`,
guard on `GetProperty("ToggleState")` yourself (as `SelectToolAndVerify` / `ReengageTool` do). This is
also why a *retry loop* around a toggle is dangerous unless it re-reads state each pass.

The same state rule applies to global hotkeys that toggle a window. Revalidate the input source,
send the chord once, wait for any target HWND, then wait for expected title/content without
resending. If initialization reaches a terminal timeout, stop the full process tree and begin a
fresh attempt.

---

## Principle 5 — Match process lifecycle to scenario state

Closing a window, waiting for input idle, and terminating a process tree are different operations.
Some state lives only in a long-running process. Peek's pinned geometry must preserve that process,
while an explicitly unpinned reopen is safer with a fresh process. Encode this as a lifecycle matrix
per scenario; use `TryKillProcessTreeByNameAndWait` only where state should be discarded.

**Restarting PowerToys is not a way to apply settings.** Modules watch their own `settings.json` and
hot-reload it, so seeding the file is enough. A per-test `RestartScope()` on top of the base class's
launch starts the runner twice per test — pure runtime — and worse, it converts "the user changed a
setting while the module ran" into "the module started with that setting", hiding live
reconfiguration defects. Restart only when the restart is the behaviour under test (state surviving a
restart), when the enabled-module set changes, or to recover from a terminal failure. See
[patterns-and-pitfalls.md](patterns-and-pitfalls.md) Recipe 17.

---

## Principle 5a — Keep module lifecycle tests on real Release Settings IPC

The Runner is the server for the Settings named pipe and owns module enable/disable lifecycle. Since
the security hardening in #49527, a **Release** Runner accepts `PowerToys.Settings.exe` only when the
client is in the Runner-relative `WinUI3Apps` directory, has the allow-listed basename and matching
file version, and carries an intact Microsoft Authenticode signature chaining to `LocalMachine\Root`.
Debug builds relax only the signature check; directory, basename, and version checks remain active.

This creates a deceptive CI symptom:

| Environment | Expected behavior |
|---|---|
| Local Debug Runner + unsigned Settings | Direct Settings lifecycle works because `_DEBUG` compiles out only the signature requirement. |
| Installed Microsoft-signed Release build | Direct Settings lifecycle works through the production authentication path. |
| Unsigned PR Release build without test setup | The switch may visibly change, but the Runner rejects the client before dispatch/persistence; module events/processes remain alive. |

Do not treat an older green Settings-toggle test as proof that unsigned Release IPC is healthy. It
may predate #49527, run against Debug, install officially signed bits, or assert only `ToggleState`
without checking a Runner-owned effect. Establish the build configuration, signing path, and runtime
assertion before comparing suites.

The UIA `ToggleState` is therefore **not** proof that enable/disable reached the Runner. A lifecycle
test must assert both the switch state and the immediate product effect: process start/exit, named
event availability, window/shortcut behavior, registration, or another module-owned signal.

### Diagnose before changing tests

When several Settings-driven lifecycle tests fail together in Release CI while feature tests pass:

1. Read the attached `RunnerLogs\runner-log_*.log` before editing code.
2. Classify the exact rejection:

| Runner reason | Route |
|---|---|
| `not-microsoft-signed` for the expected `PowerToys.Settings.exe` path | Missing CI companion-signing opt-in. Fix pipeline setup, not the test. |
| `bad-directory`, `bad-basename`, or `version-mismatch` | Packaging/layout/version defect. Signing alone is not the fix. |
| No authentication rejection | Continue ordinary test/product diagnosis; do not assume IPC authentication. |

The authentication source of truth is `src/runner/settings_window.cpp` plus
`src/common/interop/pipe_caller_auth.cpp`.

### Reuse the existing CI mechanism

Do not create a second signing script or a module-specific test bypass. The UI Test Automation
pipeline already has the supported mechanism:

- `.pipelines/signSparsePackages.ps1 -RequiredAuthenticodeFile` signs unpackaged companion binaries
  with a disposable test certificate whose subject matches the Microsoft publisher expected by the
  Release verifier. It imports the test root into `LocalMachine\Root`/`TrustedPeople`, validates the
  signatures, records the certificate marker, and removes trust/private keys in pipeline cleanup.
- `.pipelines/v2/templates/job-test-project.yml` computes `$requiresAuthenticatedSettingsIpc` from
  the selected `uiTestModules` and passes both `PowerToys.exe` and `PowerToys.Settings.exe` as required
  Authenticode files. Its package roots cover the run-in-place artifact, machine-level install, and
  per-user install.

For a new selected suite that drives module enable/disable through Settings:

1. Add the exact project family to the existing `$requiresAuthenticatedSettingsIpc` selection
  condition. Preserve existing project families and all-module behavior.
2. Keep both companion filenames in the shared required-file list. Do not copy the signing block.
3. Preview the pipeline and require the signing branch in every requested platform/install-mode job.
4. In CI logs, require `Successfully signed` and `Verified required Authenticode file(s)` for both
  Runner and Settings before accepting the run.
5. Keep the tests unchanged in shape: drive the real switch, verify `ToggleState`, then assert the
  runtime effect directly.

**Forbidden workaround:** never make a lifecycle test pass by writing the global `enabled` map and
restarting PowerToys after `not-microsoft-signed`. That tests startup from seeded state, not the user
workflow, hides a broken Settings-to-Runner contract, and duplicates infrastructure the pipeline
already provides. Never weaken or compile out the Release authentication policy for UI tests.

---

## Principle 6 — Everything on-screen, DPI-correct, from a clean profile

The whole "passes local, fails CI" cluster is environment differences a dev box papers over. Each has
a one-time fix; do them all up-front:

| Difference (CI vs local) | Symptom | Fix (bake in once) |
|---|---|---|
| **DPI** — CI often 100%, dev often 125–150% (or vice-versa) | physical clicks miss or drags are scaled (`150 × 149` for a 100px drag) | every `UITestAutomation.Next` test Exe embeds `app.manifest` with `PerMonitorV2` (Pitfall 12) |
| **Off-screen** — same-size 1920×1080 agent, a resized window keeps its old top-left | gesture lands off-screen → empty result | anchor to `ScreenCenter()`, move in steps; harness centers+clamps `WindowSize` presets (Pitfall 16, Recipe 11) |
| **Fresh profile** — OOBE / "what's new" window, centered + topmost | centre-screen gesture hits *that* window | harness `PreTestHygiene` calls `SettingsConfigHelper.SuppressFirstRunExperience()` (Pitfall 17) |
| **Cursor position** — undefined at test start | gesture anchored to current cursor drifts off-screen | park at `ScreenCenter()`, never anchor to `GetMousePosition()` (Recipe 11) |
| **Cold runner** — ~15 default modules start on a clean profile | slow start, cross-module hotkey/overlay interference | enable **only** the module under test via the base ctor (Recipe 9) |

---

## Principle 7 — Trace every action with a timestamp so a hang shows where it stuck

An assertion failure gives you a stack trace; a **hang or CI timeout does not** — the process is
killed with no exception and the recording only shows a frozen window, so you cannot tell *which* step
blocked. Emit a **timestamped line before every meaningful UI action**: on CI the **last line before
the kill** names the stuck step, and the gap between two lines shows *which* step was slow (a classic
context menu that took 15 s is obvious from the timestamps, with no profiler).

```csharp
// Last line before a CI timeout = the step that hung; gaps between lines = the slow step.
private void Step(string message) =>
    TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
```

```csharp
Step($"Opening Explorer at '{folder}'");
var explorer = OpenExplorer(folder);
Step("Selecting fixture");
SelectFiles(explorer, fixture);
Step("Opening context menu");
var menu = OpenContextMenu(explorer);          // if this blocks, the trail ends on this line
Step($"Invoking '{ContextMenuCaption}'");
```

- **Log *before* the blocking call, not after** — a line printed after the action can never appear for
  the action that hung.
- **`TestContext.WriteLine`, not `Console.WriteLine`** — MTP captures it into the TRX
  `<Output><StdOut>` attributed to the test, so it reaches the CI test log and the failure attachment.
- **UTC, millisecond precision** — read per-step durations straight from adjacent lines.
- **Name the target** (file, window, control, awaited signal), not just the verb.
- **One line per external interaction** (open / select / menu / invoke / wait-for-window), so the trail
  reads as the workflow, not noise.

This complements Principle 1 (a signal-bearing `Assert.Fail` says *what* was missing) and a rich
one-shot failure dump (Peek's `GetActivationDiagnostics`: foreground PID/title/elevation, process and
window inventory): the trail says *where* it stuck, the dump says *why*.

---

## Composed visuals: HWND, renderer, and pixels are separate

`PrintWindow` can omit WinUI/WebView2/compositor content. Use `Session.ScreenshotVisibleWindow`, which
requires exact foreground ownership and captures DWM extended-frame screen pixels. The capture helper
temporarily raises the target topmost and restores its prior state so another window cannot contaminate
the frame. `VisualAssert` retries pixel comparison because `Loaded` may precede the final composed
frame. Before changing a baseline or similarity threshold, verify title, renderer state, theme,
platform, foreground HWND, z-order, dimensions, and captured content.

---

## Pre-flight CI-stability checklist

Tick these **before** the first CI push. Each maps to a principle/recipe above; skipping one is a
likely extra CI iteration.

```markdown
- [ ] app.manifest (PerMonitorV2) wired into every UITestAutomation.Next test Exe, including greenfield projects without a .Next suffix (P6 / Pitfall 12)
- [ ] Base ctor enables ONLY the module under test (P6 / Recipe 9)
- [ ] First-run/what's-new suppression confirmed for capture & coordinate modules (P6 / Pitfall 17)
- [ ] Gestures anchored to ScreenCenter(), cursor moved in steps, never to the current cursor (P6 / Recipe 11)
- [ ] Navigation & the first interaction use Find<NavigationViewItem>(By.AccessibilityId(...)).Click() (invoke override) (P2 / Recipe 1)
- [ ] Window/overlay presence via WindowControl/WindowsFinder (Win32) — never a UIA walk of a live-capture window (mental model / P3 / Pitfall 18)
- [ ] Every wait polls an authoritative signal to a deadline — no bare Thread.Sleep standing in for "wait until ready" (P1)
- [ ] Multi-part readiness uses consecutive stable samples and reports the last structured observation (P1)
- [ ] Every meaningful UI action is preceded by a timestamped TestContext.WriteLine so a hang/timeout shows the stuck step (P7)
- [ ] Every retry is classified as idempotent, toggle, or destructive; toggle hotkeys stop after any target HWND appears
- [ ] Exact foreground requirements use `WaitForForeground`; failures record foreground PID/title/elevation
- [ ] Pipeline helper processes have no visible foreground-capable windows; detached consoles start hidden
- [ ] Process lifecycle is explicit per scenario: close/preserve/input-idle/process-tree restart
- [ ] Module settings are seeded and hot-reloaded, not applied by relaunching PowerToys (P5 / Recipe 17)
- [ ] Settings-driven module lifecycle tests assert both ToggleState and the immediate Runner-owned
  effect; selected suites are covered by `$requiresAuthenticatedSettingsIpc` and reuse
  `RequiredAuthenticodeFile` for Runner + Settings (P5a / Recipe 2)
- [ ] Renderer readiness is separate from window/title readiness; composed visuals use visible DWM capture
- [ ] Explorer-driven tests verify exact selected paths and focused path via `ExplorerShell` (Recipe 13)
- [ ] Explorer view mode/icon size is set through `ExplorerShell`, then independently verified by item geometry
- [ ] Shell handlers are activated by Explorer; readiness requires provider logs plus visible output
- [ ] Derived cleanup captures failure artifacts before closing the window that explains the failure
- [ ] Capture modules: in-place gesture retry + single re-engage; overlay detected via Win32 (P3 / Recipe 12)
- [ ] Toggle/ToggleButton presses guarded on the current ToggleState (P4)
- [ ] Clipboard via ClipboardHelper (STA + retry); no hand-rolled STA wrapper (Recipe 5)
- [ ] All mutated state restored in a finally; cleanup uses WindowControl.Try* so it never masks the real failure (Pitfall 9)
- [ ] Content-dependent measurements assert on FORMAT (regex); exact values only for content-independent gestures (Pitfall 15)
```

---

## Local vs CI — why a local pass is not proof

A green local run tells you the code **compiles and the logic executes**; it does **not** tell you the
test is CI-stable, because your box differs from the agent on all four axes at once:

- **Higher-res display** → everything stays on-screen (hides off-screen gestures, Pitfall 16).
- **Warmed profile** → OOBE/what's-new already dismissed (hides Pitfall 17), caches hot (hides WGC
  cold-start, P3).
- **Faster machine** → the not-yet-interactive-window race (P2) and hook-arming race (Pitfall 14)
  rarely trigger.
- **A human at the keyboard** → *you might touch the mouse.* A real mouse drag (Bounds-style) is
  corrupted by any physical cursor movement mid-gesture — a wrong box size that looks like a bug but is
  just interference. CI has no human, so this is local-only noise; **don't touch input during a local
  run.**

Practical local discipline: treat local as the fast **compile + logic** loop, and CI as the real gate.
**Don't over-run the suite locally** — for modules that kill/relaunch their process each test (e.g. the
Measure Tool), repeated runs can wedge Win32 input injection until the desktop session is reset
(`Win32Exception` on `SendInput`/`SetCursorPos`); that's an environment artifact, not a code defect,
and never happens on a fresh CI agent.
