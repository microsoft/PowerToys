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
> one of five root causes below. A dev box hides all of them (higher-res display, warmed caches, a
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
navigating with `By.AccessibilityId(...).Click()`** (Recipe 1) — it's race-safe under the hood. Only
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
first, which raises the target with the foreground-lock-defeating `AttachThreadInput` dance
(`WindowControl.TryBringToForeground`) before the real click, and still falls back to `Invoke()` if the
raise doesn't take. Diagnose it via winappcli's `isForeground` flag on `list-windows`; UIA `invoke` is
immune because it never touches coordinates.

**Elevation must match.** Injected input — a synthetic hotkey *or* a real click — from a process at a
*different* integrity level than the PowerToys runner is blocked by UIPI: a non-elevated host can't
drive an elevated runner, and an elevated host's foreground window blocks the non-elevated runner's
hook. Run the test host at the **same** elevation as the runner (the `.Next` harness launches the
runner non-elevated, so run the tests non-elevated too).

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

---

## Principle 5 — Everything on-screen, DPI-correct, from a clean profile

The whole "passes local, fails CI" cluster is environment differences a dev box papers over. Each has
a one-time fix; do them all up-front:

| Difference (CI vs local) | Symptom | Fix (bake in once) |
|---|---|---|
| **DPI** — CI often 100%, dev often 125–150% (or vice-versa) | coordinate tests off by the scale factor (`150 × 149` for a 100px drag) | `app.manifest` with `PerMonitorV2`, wired in the csproj (Pitfall 12) |
| **Off-screen** — same-size 1920×1080 agent, a resized window keeps its old top-left | gesture lands off-screen → empty result | anchor to `ScreenCenter()`, move in steps; harness centers+clamps `WindowSize` presets (Pitfall 16, Recipe 11) |
| **Fresh profile** — OOBE / "what's new" window, centered + topmost | centre-screen gesture hits *that* window | harness `PreTestHygiene` calls `SettingsConfigHelper.SuppressFirstRunExperience()` (Pitfall 17) |
| **Cursor position** — undefined at test start | gesture anchored to current cursor drifts off-screen | park at `ScreenCenter()`, never anchor to `GetMousePosition()` (Recipe 11) |
| **Cold runner** — ~15 default modules start on a clean profile | slow start, cross-module hotkey/overlay interference | enable **only** the module under test via the base ctor (Recipe 9) |

---

## Pre-flight CI-stability checklist

Tick these **before** the first CI push. Each maps to a principle/recipe above; skipping one is a
likely extra CI iteration.

```markdown
- [ ] app.manifest (PerMonitorV2) wired into the csproj — any coordinate-exact test (P5 / Pitfall 12)
- [ ] Base ctor enables ONLY the module under test (P5 / Recipe 9)
- [ ] First-run/what's-new suppression confirmed for capture & coordinate modules (P5 / Pitfall 17)
- [ ] Gestures anchored to ScreenCenter(), cursor moved in steps, never to the current cursor (P5 / Recipe 11)
- [ ] Navigation & the first interaction go through By.AccessibilityId(...).Click() (invoke under the hood) (P2 / Recipe 1)
- [ ] Window/overlay presence via WindowControl/WindowsFinder (Win32) — never a UIA walk of a live-capture window (mental model / P3 / Pitfall 18)
- [ ] Every wait polls an authoritative signal to a deadline — no bare Thread.Sleep standing in for "wait until ready" (P1)
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
