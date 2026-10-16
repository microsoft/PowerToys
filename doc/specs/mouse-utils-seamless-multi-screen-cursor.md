# Mouse Utilities – Seamless Multi-Screen Cursor

Status: draft proposal
Tracking issue: microsoft/PowerToys#51371
Related: Mouse Utilities – CursorWrap (`src/modules/MouseUtils/CursorWrap`), prior art [LittleBigMouse](https://github.com/mgth/littlebigmouse)

## 1. Overview

Windows arranges monitors in a single virtual desktop measured in **pixels**. When the cursor crosses a shared
border, Windows keeps the pixel coordinate along that border. Monitors with different resolution, physical size or
physical offset therefore make the cursor **jump** when it crosses: it leaves one screen at one physical spot and
enters the other screen somewhere else.

Example (layout like the one below): monitor 3 is a 27" 4K panel (≈0.156 mm per pixel), monitor 2 above it is a
24" 1080p panel (≈0.277 mm per pixel).

```
            ┌───────────┐
            │     2     │        ┌─────┐
            │  1920 px  │        │  1  │
      ┌─────┴───────────┴────────┴─────┴┐
      │               3                 │
      │            3840 px              │
      └─────────────────────────────────┘
```

* Windows thinks monitor 2 covers 50 % of monitor 3's top edge. Physically it covers ≈89 %.
* At best there is one point where crossing is correct. The further the cursor is from that point, the bigger the
  jump: 1000 px away on monitor 3, the cursor lands ≈120 mm away from where the user expects it.

Seamless Multi-Screen Cursor is a new **Mouse Utilities** module. It lets the user calibrate each shared border with
a simple visual overlay, then remaps the cursor when it crosses so movement is physically continuous.

## 2. Goals / Non-goals

### Goals

* Physically continuous cursor movement across every calibrated border between two monitors.
* Calibration that needs no measuring and no typing of sizes. The user only drags lines until they line up visually.
* A per-border on/off switch.
* Calibration survives re-arranging monitors in Windows display settings and resolution changes, as long as the
  monitors do not move physically.
* No added latency for normal movement inside one monitor.
* Coexist with CursorWrap, Mouse Jump, FancyZones and games.

### Non-goals (v1)

* Equal physical pointer **speed** on all monitors (cursor speed in mm/s differs between monitors).
* Equal physical cursor **size** on all monitors.
* Diagonal crossing through corner gaps (LittleBigMouse "Cross" mode).
* Bezel-gap compensation (making the cursor "travel" through the bezel).
* Changing the Windows display arrangement.
* Monitors that do not face each other in the Windows layout (touching only at a corner, diagonal, or with another
  monitor in between). Monitors that face each other across a gap can be glued (§5.2).

## 3. Scenarios

1. A laptop (high-DPI, small) is docked under a large 1080p monitor. Moving the cursor up from the laptop exits at the
   same physical spot on the big monitor.
2. A 4K and a 1440p monitor of different sizes sit side by side, with bottoms aligned on the desk but not in Windows.
   Crossing left/right keeps the cursor at the same height.
3. A user rearranges the monitors in Windows display settings (pixel offsets change). The calibration still applies;
   only a physical move of a monitor needs recalibration.
4. A user does not want remapping between a TV and the main monitor. They switch that border off; the other borders
   stay seamless.
5. A game confines the cursor (`ClipCursor`) or hides it for mouse-look. The module does not interfere.

## 4. Model

### 4.1 Why one alignment is not enough

Along a border, the mapping from monitor A to monitor B is linear:

```
uB = offset + s · uA
```

* `u` is the pixel coordinate along the border (x for a horizontal border, y for a vertical one).
* `s` is the ratio of pixel pitches, `mmPerPixel(A) / mmPerPixel(B)`. In the example above, 0.156 / 0.277 ≈ 0.56.
* `offset` follows from where the monitors physically sit relative to each other.

One aligned pair of lines gives one equation and fixes only the offset. It leaves `s` unknown, so the jump still grows
with distance from the aligned point. Two aligned pairs give two equations, which fix both values.

### 4.2 Calibration data per border

The user aligns **two marker pairs** per border. Pair k is a point `(uA_k, uB_k)` that is at the same physical
position on both monitors:

```
s  = (uB_2 − uB_1) / (uA_2 − uA_1)
f(uA) = uB_1 + s · (uA − uA_1)          // A → B
g(uB) = uA_1 + (uB − uB_1) / s          // B → A, exact inverse of f
```

Validation:

* `s > 0`: the markers must keep the same order on both monitors.
* `s` is within a plausible range, for example `[0.2, 5]`.
* The two pairs are far enough apart. Precision depends on their distance L: misplacing each marker by δ mm gives a
  position error of ≈`2·δ·D / L` at distance D. With δ = 1 mm and L = D = 300 mm, the error is ≈2 mm. The overlay
  warns when L covers less than 30 % of the span both monitors physically share. The warning compares against the
  shared span, not the monitor edges: a very wide monitor next to a narrow one would otherwise always warn.

### 4.3 Storage is relative to each monitor

Marker positions are stored as a **fraction of each monitor's edge length**, keyed by stable monitor identity:

```
a_k = (uA_k − A.edgeStart) / A.edgeLength
b_k = (uB_k − B.edgeStart) / B.edgeLength
```

This makes the calibration independent of:

* Where the monitors sit in the Windows arrangement. Moving monitor 2 sideways in display settings does not move it
  on the desk.
* Resolution changes with the same aspect ratio.

The runtime maps fractions back to virtual-desktop pixels each time the topology is rebuilt (§6.6).

A border record applies only while both monitors are connected, keep the orientation (landscape or portrait) they
had at calibration, and still either **share a border on the same side** in the Windows layout, or face each other
across a gap on that side with no monitor in between. In the second case the record acts as a glued pair, even when it
was calibrated while the monitors touched. Otherwise it is ignored (Windows default behavior) and kept on disk for
when the configuration returns. Of several records for the same monitor pair, only the first usable one applies.

## 5. Calibration overlay

### 5.1 Entry point

* Settings → Mouse Utilities → Seamless Multi-Screen Cursor → **Calibrate monitors** button.
* Command Palette → **Calibrate Seamless Multi-Screen Cursor**. It sets the named event `SEAMLESS_CURSOR_CALIBRATE_EVENT`
  (`src/common/interop/shared_constants.h`), which a listener thread in the module waits for.
* The button sends a custom action (`call_custom_action`) to the module, which opens the overlay.
* While the overlay is open, remapping is suspended, unless **Try while adjusting** is on (§5.3). Calibrating again
  while it is open brings it to the front.
* If Windows cannot tell the monitors' device paths (for example in a remote session), the overlay says so and offers
  only Close: nothing saved could ever apply.
* While the module is disabled, the custom actions and the named event are ignored, and Command Palette does not list
  the command. Right after an upgrade, until PowerToys first saves its settings, Command Palette can still list it, as
  it does for every module.
* All overlay strings are in the module's `Resources.resx`, converted to its `.rc` file at build time, so they are
  translated like the strings of other modules.

### 5.2 Layout

* There is one full-screen, top-most overlay window per monitor. Each one shows a nearly opaque dark background and
  the monitor's name and resolution at 30 % of its height; on the primary monitor they sit right above the panel.
* Every pair of monitors that shares part of a border in the Windows layout is a **border**. Each border gets its own
  color from a fixed palette that colorblind users can tell apart.
* Each border has **two marker pairs**. A marker:
  * is a thick bar at a right angle to the border and touches it;
  * is about 10 % of the monitor's height long (or width, for a vertical border) and about 24 px wide, scaled by the
    monitor's DPI;
  * has a thin center line in a contrasting color, for precise alignment;
  * carries a small label "1" or "2" to show which pair it belongs to.
* Markers move only along their own monitor's edge. A marker can be anywhere on its monitor's edge, not only on the
  part the Windows layout shares, because what matters is physical overlap.
* Each border has a small on/off switch in the border's color, next to the border: "Smooth crossing: On/Off". It
  sits on the primary monitor when the border touches it, otherwise on the first monitor of the pair. When a border
  is off, its markers are dimmed and not editable.
* **Glue.** Windows sometimes cannot place two monitors side by side, for example two monitors standing on a wide one,
  where the layout snaps both to the wide monitor and leaves a gap between them. Such pairs are offered for gluing:
  * A pair qualifies when one monitor lies entirely beyond an edge of the other, their ranges along that edge
    overlap, and no other monitor lies in between (`FindGlueCandidates`). Diagonal pairs do not qualify.
  * Both monitors show a "Glue to <other monitor>: On/Off" switch with a glue tube icon next to the facing edge, off by
    default. Either switch glues or unglues the pair, and each names the other monitor, so it is clear which two
    monitors are meant.
  * Once glued, the pair gets two marker pairs and is calibrated like a shared border. Ungluing keeps its lines: the
    pair is saved switched off, so gluing it again brings them back. A pair that was never glued, stored or adjusted
    is not saved, and Reset forgets the stored lines of every glue pair on screen. The record is marked as a glue
    record, so if those monitors later share a border, the overlay shows that border on, with those lines, and it
    takes effect once saved. A record keeps its kind (glue record or not) until its switch is used or Reset, so saving other
    borders never turns a shared border that was switched off into an unglued pair.
* An instruction panel on the primary monitor shows the instructions, the key hints, the **Try while adjusting**,
  **Pointer trail**, **Edge map** and **Stop at empty edges** switches, and the Cancel, Reset and Save buttons. The
  switches sit in rows of related pairs, trying out (Try while adjusting, Pointer trail) above the edges (Edge map,
  Stop at empty edges), or one per row on a monitor too narrow for a pair. The instructions explain how to align the lines
  straight across the bezel, to put the two pairs far apart, Try while adjusting, switching smooth crossing off, and
  gluing. With no border to calibrate, the panel says so and offers only Close.
  * The panel stays clear of the lines along the edges and of the switches on the top and bottom edges. On a monitor
    too small for that, it gives up the room for the lines and the issue text first, then for the switches, but keeps
    itself and the caption on screen.
  * A switch that the panel or the caption covers moves along its edge, clear of them and of the other switches, and
    on a side edge clear of the top and bottom switch rows. Switch positions depend only on the monitor layout, so
    they are laid out once when the overlay opens.
  * The panel grows when its texts need more room than usual, for example with a long translation or a narrow
    panel; the texts are measured when the overlay opens, and each is clipped to its place.
  * The key hints are separate strings, divided by dots. They are arranged in rows for the panel's width, and a hint
    that does not fit on a row starts the next one, so a key is never separated from its description.
  * When even that leaves a switch or its issue text under the panel, or the full panel does not fit on the monitor,
    the panel turns compact: it leaves out the instructions and the edge map legend, keeps the title, the key hints,
    its switches and buttons, and the switches are laid out again.
  * Switches are drawn above the panel and take clicks first, the one drawn on top first, so they stay usable even
    where nothing fits; lines the panel covers cannot be grabbed through it. In very crowded layouts a moved switch
    can still cover the issue text of another one.
  * The monitor's name and resolution sit right above the panel.

### 5.3 Interaction

* Drag a marker with the mouse.
* Click a marker to select it. The arrow keys move it 1 px; Shift + arrow moves it 10 px.
* Tab (Shift + Tab backwards) moves the keyboard focus through the markers, then each border's switch, then the
  panel's switches and buttons; Space presses the focused switch or button, which shows a white ring. Clicking with
  the mouse removes the ring.
* Enter saves and Esc cancels; repeats of a held key are ignored. With no border to calibrate, Enter closes. If
  saving fails, the overlay stays open and says so, so the alignment is not lost.
* A display change closes the overlay without saving, because the layout it shows is stale.
* Saving replaces only the records of the borders on screen, for the orientation the monitors have now. Records of
  monitor pairs that are not side by side right now, of another edge or of another orientation are kept. If
  `calibration.json` cannot be parsed, it is copied to `calibration.json.bak` before the save replaces it. While another
  program holds the file, or if that copy fails, the save fails and says so, rather than lose calibrations for good.
  When the overlay opens, it waits about a second for a file another program holds. If it still cannot read it, the
  borders show predicted lines, the panel says at once that saving is not possible and dims Save, and Save keeps the
  stored lines; opening the overlay again tries again.
* Thin dashed lines on each side of a border show where the other monitor physically ends, computed from the lines as
  placed now. Depending on the overlap, none, one or two of them fall on a monitor's edge. They are drawn below every
  marker.
* **Edge map** (a switch in the panel, off by default, remembered in `overlay.json`) colors each edge with what a move
  across it does under the lines as placed now: the border's color where the pointer crosses, red where it stops (a
  wall, or on an edge with a calibrated border, a part with no monitor across in either the calibration or the Windows
  layout), nothing where Windows decides by its own layout (uncalibrated or switched-off borders, and edges without a
  calibrated border). The engine is rebuilt from
  the current lines for this (`Engine::EdgeParts`), so the map follows every drag. With CursorWrap enabled, red outer
  edges wrap instead of stopping.
* **Try while adjusting** (a switch in the panel, on by default, remembered in `overlay.json`) lets the user move the
  pointer across borders with
  the lines as currently placed, before saving:
  * the overlay sends the current lines to the hook thread, which rebuilds the engine from its cached monitor list;
  * remapping pauses while a line is dragged and catches up when the drag ends, so a drag never moves the cursor;
  * closing the overlay reloads `calibration.json`, so an unsaved preview never outlives the overlay.
  Suspend, preview and reload requests are applied on the hook thread in the order they were made.
* **Pointer trail** (a switch in the panel, off by default, remembered in `overlay.json`) leaves glowing dots behind
  the pointer, so a straight move across a border shows whether the crossing is straight too:
  * dots every 7 px along the path (at 100 % scaling, more apart on scaled displays), so a fast move leaves no gaps;
    each shrinks, turns from white to blue and fades out within 1.2 s, inside a soft glow;
  * the trail is drawn under the lines, switches and panel, and none is left while a line is dragged;
  * a move onto another monitor or a jump starts a new piece of trail instead of connecting it. A jump is a move of
    more than half the monitor's width sideways or half its height up or down, such as a CursorWrap wrap, also on a
    very wide monitor;
  * a CursorWrap wrap also draws a fading line across every monitor that the lines link to the one left through
    borders across the wrap direction: horizontal for a wrap at a left or right edge, vertical for one at a top or
    bottom edge, at the same physical height or position (`Engine::MapJump`). With Try while adjusting on, the
    pointer lands on the line when the wrap kept the physical position. A wrap is recognized as a move against its
    direction by more than half a monitor, into the outer quarter of the opposite side of the same or another monitor
    (mouse moves arrive merged while the overlay draws, so after a fast wrap the first one seen can be well inside).
    The line's height comes from the last position seen before the wrap, so after a fast diagonal move it can be a few
    pixels off the point where CursorWrap measured;
  * any change that affects crossings clears the whole trail and the wrap lines at once: moving a line, a border's
    switch, Reset, Try while adjusting and Stop at empty edges;
  * a 16 ms timer runs only on monitors with dots left to fade.
* When a pair-1 marker moves, the matching pair-2 marker is re-predicted from the EDID scale (§5.4), until the user
  moves pair 2 directly. If EDID is correct, pair 2 is already aligned and the user only checks it. Borders loaded
  from a saved calibration do not re-predict. Reset predicts every border again, turns every border on and unglues
  glued pairs.
* When a border is invalid (§4.2), its markers turn red and a short reason appears. Save is still possible: the module
  ignores invalid borders and keeps their data for the next session.

### 5.4 Initial marker positions (EDID prefill)

* Pixel pitch per monitor comes from the physical size Windows reports (`GetDeviceCaps(HORZSIZE/VERTSIZE)`,
  `PhysicalPitch` in `Monitors.cpp`).
* Sanity check: the size must be at least 100 mm wide and 50 mm high, and its aspect ratio must be within 5 % of the
  pixel aspect ratio.
  If it fails, assume equal pitch (`s = 1`). TVs, projectors and some panels report wrong or empty sizes.
* With no saved calibration, assume the monitors are physically centered on the middle of the shared pixel segment.
  * Place pair 1 at 25 % of the shared physical span and pair 2 at 75 %.
  * Compute the B-side positions from the predicted `s`.
* With a saved calibration, show the saved positions.

### 5.5 Hint animation

On open, the first border plays a short loop, if it is on (an unglued pair is off):

A ghost marker slides from an offset position until it lines up with its partner. The loops alternate between pair 1
on monitor B, sliding back, and pair 2 on monitor A, sliding forward, so both monitors and both pairs are shown. Four
loops play, or fewer if the first drag, Tab or switch click comes earlier. A 16 ms timer runs only while the
animation plays; otherwise the overlay redraws only when something changes, which during a drag is every mouse move.

## 6. Runtime engine

### 6.1 Threads

* **Hook thread:**
  * owns the `WH_MOUSE_LL` hook, a hidden window for display-change notifications, and the active edge tables;
  * does nothing else, so mouse latency stays low;
  * follows the same pattern as CursorWrap's hook thread (`CursorWrap/dllmain.cpp`).
* **Overlay thread:** created only while calibrating, so a busy renderer can never delay the hook.
* PowerToys.exe is Per-Monitor V2 DPI aware. Hook points, `rcMonitor`, `GetPhysicalCursorPos` and
  `SetPhysicalCursorPos` are all in physical pixels.
  Windows scaling percentages do not affect the engine.

### 6.2 Edge tables

When the topology is rebuilt, every edge of every monitor gets a sorted list of segments:

```
Segment { start, end, target, map, wall, outer }   // [start, end) along the edge; map: the BorderMap f
```

Built from each enabled, valid border record (A on this edge, B across it), whose monitors either share that border
or are glued across a gap. Windows does not connect glued monitors at all, so rule 2 never applies to them; rule 3
still applies on the rest of a glued edge. For each `u` on A's edge:

1. **Physical neighbor.** If `f(u)` falls inside B's edge range, the target is `f(u)`. This covers parts that the
   Windows layout shows as an outer edge but where B is physically adjacent.
2. **Off-target (wall).** With `stop_at_empty_edges` on (the default; with it off, the cursor goes to B's nearest
   corner instead): if Windows shares the border at `u` but `f(u)` falls outside B, nothing is physically across,
   so the cursor stops at the edge of A, as at the edge of a physical screen. Movement along the edge is kept, so the
   cursor slides along it. Through a corner, a wall on the edge crossed first does not hide a real crossing of the
   other edge. No other PowerToy acts here: these parts are inner edges for Windows, while CursorWrap and Mouse Without
   Borders act only on outer desktop edges, and Mouse Jump and Find My Mouse move the cursor directly.
3. **Outer wall.** Only with `stop_at_empty_edges` on: if nothing is across in the calibration and Windows connects no
   monitor there either, the cursor
   stops too. Windows would also stop it, unless its "Ease cursor movement between displays" setting moves it to a
   nearby monitor, which contradicts the calibration. CursorWrap and Mouse Without Borders act at such outer desktop
   edges, so the hook passes the move to the other hooks first (`CallNextHookEx`) and stops the cursor only if none of
   them handled it. This applies only on edges that have at least one calibrated border.
4. **Otherwise** there is no segment and Windows behaves as usual. This covers uncalibrated or disabled borders that
   Windows connects, and every edge without a calibrated border.

If two calibrated neighbors claim the same `u` under rule 1 (calibration says they overlap physically), the
neighbor whose `f` at the middle of the segment is nearer the center of its edge, relative to that edge's length,
wins.

### 6.3 Crossing detection (hot path)

For each `WM_MOUSEMOVE` with hook point `p`, using cached source monitor A (the monitor of the previous point `q`):

1. **Fast path.** The hook reads the cursor position (`GetPhysicalCursorPos`) and applies the cheap exclusions
   (§6.5). If `p` is inside A, it calls `CallNextHookEx`.
2. Otherwise, find the edge of A that the segment `q→p` crosses first. At a corner, take the edge with the smaller
   intersection parameter first:
   * if Windows connects a monitor there and the calibration has no segment for it, Windows handles the move. Only
     after a wall on the edge crossed first, when `p` lies beyond the corner in a calibrated monitor or in no monitor,
     the cursor goes straight across that edge into the connected monitor instead, and stays inside it however far
     the move overshoots. After an outer wall, other hooks
     act first, as at outer desktop edges;
   * if it has no segment and Windows connects nothing there, try the other edge. If that edge stops the cursor, other
     hooks act first, as at an outer wall;
   * a wall on the first edge does not hide a crossing of the other edge.
3. Compute the crossing coordinate `u_c` on that edge. Pixels map through their centers, so `u_c` is measured on the
   line from `q + 0.5` to `p + 0.5`. Look up its segment. If none, pass through.
4. Apply the remaining exclusions (§6.5). If excluded, pass through.
5. Compute the target:
   * Along the edge: `uB = f(u_p + 0.5)`, clamped to B's facing edge, where `u_p` is where the move ends rather than
     the crossing point `u_c`, so diagonal movement stays continuous. `u_c` only selects the segment.
   * Across the edge: B's facing edge, moved inward by `round(d · s)`. `d` is how far `p` passed A's edge, in A's
     pixels. This keeps fast movements continuous. The result is clamped to stay inside B.
6. Call `SetPhysicalCursorPos(target)`, update cached A = B, and return `1` to swallow the original move.

Notes:

* **Outer edges.** Rule 1 segments on outer edges need the hook point to pass beyond the edge. It does: Windows
  clamps the cursor to the desktop only after the hook runs (§13).
* **Source monitor.** The source monitor comes from `GetPhysicalCursorPos` inside the hook, which still returns the position
  before the move. This stays correct after another tool moves the cursor with `SetCursorPos`, which the hook never
  sees.
* **Round trip.** `g` is the exact inverse of `f`, so crossing back at the same physical spot returns within ±1 px.
* **Bounce protection.** The target is always strictly inside B. A crossing needs `p` strictly outside the cached
  monitor, so a zero-length move after a jump never triggers another one.
* **Fast moves.** Each event makes at most one jump. Anything left over is handled on the next event.

### 6.4 Coexistence with CursorWrap ("Seamless wins")

* Both modules install low-level mouse hooks in PowerToys.exe. The most recently installed hook runs first, so the
  order depends on which module was enabled last.
* A conflict exists only on rule-1 segments that lie on outer edges, including both edges of a glued pair.
  CursorWrap would wrap the cursor there.
* Mechanism:
  * After every rebuild, Seamless publishes an immutable copy of its engine as `std::shared_ptr<const Engine>`, with
    atomic load and store (`EdgeClaims.cpp`). It publishes `nullptr` while its hook does not remap: when nothing
    is calibrated, when installing the hook failed, while remapping is suspended for the calibration overlay, from a
    display change until the rebuild, and when the module stops. Otherwise neither module would act on a claimed
    edge.
  * It exports `extern "C" bool SeamlessCursor_IsEdgeClaimed(RECT monitor, int edge, int along)`. It returns `true`
    when the engine has a crossing (not a wall) on that edge of that monitor at that coordinate, and `false` without a
    published engine. Edges are numbered left, right, top, bottom from 0, as in CursorWrap's `EdgeType`.
  * CursorWrap looks the exports up once with
    `GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_PIN, L"PowerToys.SeamlessCursor.dll")` + `GetProcAddress`.
    Once is enough: the runner loads every module DLL before it enables any of them. The lookup pins the DLL,
    because on exit or restart the runner may unload it while CursorWrap's hook still runs. Without the DLL (for
    example under ModuleLoader), nothing is claimed.
  * In `MonitorTopology::IsOnOuterEdge`, CursorWrap drops every candidate edge that Seamless claims at the cursor
    position. It asks only when the cursor is at an outer edge.
  * Only crossings are claimed. Where Seamless merely stops the cursor (rules 2 and 3), CursorWrap may still wrap it;
    for outer walls the hook lets the other hooks act first, so the result does not depend on the hook order either.
  * Result: whichever hook runs first, Seamless handles the claimed segments and CursorWrap handles everything else.
  * At a corner where a wall meets an outer edge, CursorWrap picks its monitor from the requested point, which can be
    the monitor across the wall, so it may wrap on that monitor. The same happens whenever CursorWrap's hook runs
    first. Accepted for v1.
  * **Physical wraps.** Whenever CursorWrap wraps to another monitor, it asks
    `SeamlessCursor_MapWrap(RECT from, int edge, int along, RECT to, int* mapped)` for the coordinate on the target
    monitor at the same physical position. This helps monitors of different pixel size and monitors whose pixel rows do
    not line up physically. When the position lies off the monitor CursorWrap picked by pixel coordinate, CursorWrap
    takes the outermost of its other outer edges of that side where the position lands, which can be the source
    monitor's own far edge (a glued pair across a wide gap); when none fits, the pointer stays at the nearer end of the
    picked edge. Wraps within one monitor are left as they are. The result is kept two
    pixels clear of the ends of the target edge, which CursorWrap would take for the adjacent edge.
    Seamless follows the chain of calibrated
    borders with the fewest borders, on edges of the same orientation as the wrap edge: borders on left and right edges
    give the height for a horizontal wrap, borders on top and bottom edges the horizontal position for a vertical
    wrap. Borders of the other orientation cannot tell it, because the gap between the screens (bezels) is unknown.
    Without such a chain, while nothing is published, or with the `map_cursor_wrap` setting off, CursorWrap keeps
    the coordinate as before. Turning the setting off leaves the claimed edges (above) in place.
* The export was chosen over named shared memory: both modules run in PowerToys.exe (§6.1), so an in-process call
  is enough and needs no versioning or cross-process synchronization.

### 6.5 Exclusions (pass through unchanged)

* **Another application has confined the cursor.** `GetClipCursor` returns something other than the virtual-screen
  rectangle. This covers games and some drag operations.
* **The cursor is hidden.** `GetCursorInfo` has no `CURSOR_SHOWING`, for example mouse-look in games.
* **Pen or touch input.** The test is `(dwExtraInfo & 0xFFFFFF00) == 0xFF515700` (`MI_WP_SIGNATURE`). These are
  absolute pointing devices, so remapping would fight them.
* **Injected jumps.** Moves flagged `LLMHF_INJECTED` that land at least 128 px from the cursor along either axis.
  Automation and remote-control tools jump straight to the point they want, and remapping would move their clicks off
  target. Smaller injected moves are still remapped: some precision touchpads deliver injected events, as found in
  Auto Hide Cursor #50604 / #50803.
* The pen/touch and injected-jump checks are cheap and run on every move, before the engine. The `ClipCursor` and
  hidden-cursor checks run only when the engine remaps a move, so they cost nothing on the hot path.

### 6.6 Topology changes

* Similar to CursorWrap:
  * a hidden top-level window receives `WM_DISPLAYCHANGE`, `WM_SETTINGCHANGE(SPI_SETWORKAREA)` and
    `WM_DEVICECHANGE`. All of these are broadcast to top-level windows, so no device-notification registration is
    needed;
  * a 500 ms debounce timer delays the rebuild;
  * the rebuild runs on the hook thread.
* `WM_DEVICECHANGE` starts the timer only for `DBT_DEVNODES_CHANGED`. While a rebuild is pending after a display
  change, device and work-area notifications do not restart the timer, so they cannot postpone it indefinitely.
* From `WM_DISPLAYCHANGE` until the rebuild, the engine still has the old monitor rectangles. Meanwhile the hook
  passes every move through and the claimed-edges snapshot is withdrawn.
* While the overlay tries its lines (§5.3), rebuilds use those lines instead of `calibration.json`, until the overlay
  closes. Otherwise they use the calibration read when the hook thread starts and on every Reload (whenever the
  overlay closes, or after a reset); display and device changes do not read the file again, because mouse input waits
  while a rebuild runs.
* If Windows cannot tell the monitors' device paths (for example while the session is locked), no calibration would
  match. The layout then counts as stale, as after a display change, and the failure is logged once. The rebuild is
  retried after 5 s, then less and less often, up to once a minute. A calibration file that cannot be read (another
  program holds it) is read again on the following rebuilds, retried the same way; meanwhile the calibration read
  before stays in use. A file that cannot be parsed counts as empty and is not read again before the next Reload;
  saving from the overlay first copies it to `calibration.json.bak`. Unlocking the session triggers a rebuild 500 ms
  later when one is still pending. The hook thread never waits for a file another program holds, because mouse input
  would wait too.
* The rebuild:
  1. enumerates monitors, without their physical size, which only the overlay needs;
  2. resolves monitor identity (§7.2);
  3. matches border records against borders the Windows layout shares;
  4. builds the edge tables;
  5. publishes the claimed-edges snapshot.
* Mirrored monitors (identical rectangles) never share a border with each other.

### 6.7 Robustness

* The hook callback does O(1) work plus at most a few segment lookups. It never allocates, logs or locks.
* Windows silently removes a low-level hook that keeps exceeding `LowLevelHooksTimeout`. Every 2 s a watchdog timer
  checks whether the cursor moved while the hook saw no input, and if so reinstalls the hook. LittleBigMouse does the
  same. While an elevated window has focus and PowerToys is not elevated, the hook gets no input either, so the
  watchdog reinstalls it repeatedly; only the first reinstall is logged. If installing the hook fails, the watchdog
  retries; only the first failure is logged.
* With no usable calibrated border, the hook is not installed at all.
* Only the module stops the hook thread. A `WM_CLOSE` from outside (for example `taskkill` without `/f`) is ignored,
  so it cannot leave the module enabled without remapping.
* Elevated windows: same limitation as other Mouse Utilities. When an elevated window is in the foreground and
  PowerToys is not running elevated, remapping may not apply.

## 7. Settings and persistence

### 7.1 Files

* `settings.json`, standard module settings, written by Settings UI:
  ```jsonc
  {
    "name": "SeamlessCursor",
    "version": "1.0",
    "properties": {
      "stop_at_empty_edges": { "value": true },  // Rules 2 and 3 of §6.2
      "map_cursor_wrap": { "value": true }        // Physical wraps, §6.4
    }
  }
  ```
  When `stop_at_empty_edges` is off, rule 2 sends the cursor to the nearest corner of the monitor Windows connects
  there, and rule 3 does not apply, so Windows decides at outer edges (including its "Ease cursor movement between
  displays" setting). The module reads it at startup and in `set_config`. The overlay's **Stop at empty edges**
  switch changes the same setting: it applies at once (Cancel does not undo it), the edge map shows its effect, and
  the module saves it to this file, from where an open Mouse Utilities page picks it up through its settings file
  watcher.
* `calibration.json`, in the same folder, owned and written **only by the native module**. It is never round-tripped
  through Settings UI, so Settings UI can never overwrite it with a stale copy. It is written to a temporary file,
  flushed and renamed over the old one. Reading waits briefly, and only while another program holds the file; the hook
  thread does not wait at all. A UTF-8 byte order mark is accepted. When reading it, malformed entries and positions
  outside 0–1 are skipped, so one bad entry does not cost the others. Overlay preferences are kept in `overlay.json`
  (`{ "tryWhileAdjusting": true, "showEdgeMap": false, "showTrail": false }`), so resetting the calibration keeps
  them.
  ```jsonc
  {
    "version": 1,
    "borders": [
      {
        "monitorA": "<stable id>",
        "monitorB": "<stable id>",
        "edgeOfA": "top",                 // left | right | top | bottom
        "enabled": true,
        "landscapeA": true,               // Orientation at calibration time
        "landscapeB": true,
        "glue": false,                    // Saved for a pair that faces across a gap; there, "enabled": false means unglued
        "pairs": [ { "a": 0.31, "b": 0.12 }, { "a": 0.78, "b": 0.95 } ]
      }
    ]
  }
  ```

### 7.2 Monitor identity

* Use `monitorDevicePath` from `DisplayConfigGetDeviceInfo(DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME)`. It contains
  the hardware ID plus an instance part, so two identical monitors are still told apart.
* Moving a monitor to a different port changes its identity, so it needs recalibration. This is acceptable for v1.
  Matching on EDID serial is possible future work (FancyZones does something similar with its own `MonitorId`).

## 8. Settings UI

A new `SettingsGroup` on `MouseUtilsPage.xaml` with:

* a GPO info control;
* an enable toggle headed **Seamless Multi-Screen Cursor** (off by default);
* a **Calibrate monitors** button card, with description "Align lines across each shared border so the cursor moves
  smoothly between monitors.";
* a **Stop the pointer where no monitor is physically across** toggle (`stop_at_empty_edges`, on by default);
* a **Keep the physical position when CursorWrap wraps the pointer** toggle (`map_cursor_wrap`, on by default);
* a **Reset calibration** button, which sends a custom action that clears `calibration.json`;
* an InfoBar when only one monitor is connected, or when no border is calibrated yet. Both show only while the
  module is enabled.
  * The monitor count comes from `GetSystemMetrics(SM_CMONITORS)` and is refreshed when the page loads.
  * "Calibrated" means `calibration.json` exists and lists at least one border. While the page is loaded, a file
    watcher refreshes the notice when the module saves the file (by renaming a temporary file) or deletes it on
    reset.

All strings go in `Resources.resw` and are indexed by settings search.

## 9. Architecture and files

```
src/modules/MouseUtils/SeamlessCursor/
  SeamlessCursor.vcxproj            → PowerToys.SeamlessCursor.dll (in-proc, loaded by runner)
  Resources.resx                    Localizable strings; converted with SeamlessCursor.base.rc and resource.base.h at build
  dllmain.cpp                       PowertoyModuleIface, settings, custom actions, calibrate event listener
  ModuleConstants.h                 Module key
  SeamlessCursorEngine.h/.cpp       Pure logic: border math, validation, edge tables, crossing resolution (no Win32 calls)
  EdgeClaims.h/.cpp                 Engine snapshot and the SeamlessCursor_IsEdgeClaimed and SeamlessCursor_MapWrap exports
  CalibrationStore.h/.cpp           calibration.json and overlay.json load/save, calibration.json delete
  Monitors.h/.cpp                   Monitor enumeration, QueryDisplayConfig device path and name, EDID physical size
  MouseHookThread.h/.cpp            LL hook, display-change window, exclusions, watchdog, SetPhysicalCursorPos
  CalibrationOverlay.h/.cpp         Per-monitor windows, Direct2D rendering, dragging, animation
  OverlaySnapshot.h                 The overlay drawn into bitmaps instead of windows, for OverlaySnapshots
  trace.h/.cpp
  UnitTests/SeamlessCursorUnitTests.vcxproj
  OverlaySnapshots/SeamlessCursorOverlaySnapshots.vcxproj   Developer tool: renders the overlay for made-up layouts
```

* The overlay follows the Measure Tool approach (`MeasureToolCore/OverlayUI.cpp`): `WS_POPUP`, `WS_EX_TOPMOST`,
  `WS_EX_TOOLWINDOW`, and Direct2D, one window per monitor. `WS_EX_LAYERED` makes it slightly see-through.
* The FancyZones editor overlay was considered and rejected. It is a separate WPF executable tied to FancyZones layout
  data.
* Wiring follows Auto Hide Cursor (commit `f47af41de`):

| Area | Files |
|------|-------|
| Solution | `PowerToys.slnx` (`/modules/MouseUtils/`) |
| Runner | `src/runner/main.cpp` (`knownModules`) |
| Logger | `src/common/logger/logger_settings.h` |
| Module type | `src/common/ManagedCommon/ModuleType.cs` |
| GPO | `src/common/utils/gpo.h`, `src/common/GPOWrapper/GPOWrapper.{cpp,h,idl}`, `src/gpo/assets/PowerToys.admx`, `src/gpo/assets/en-US/PowerToys.adml` |
| Settings library | `SeamlessCursorSettings.cs`, `SeamlessCursorProperties.cs`, `SndSeamlessCursorSettings.cs`, `EnabledModules.cs`, `SettingsSerializationContext.cs`, `Helpers/ModuleHelper.cs` |
| Settings UI | `MouseUtilsViewModel.cs`, `MouseUtilsPage.xaml(.cs)`, `Strings/en-us/Resources.resw`, `Helpers/ModuleGpoHelper.cs`, `SourceGenerationContextContext.cs`, `Utilities/NativeMethods.cs` (`SM_CMONITORS`) |
| Settings tests | `Settings.UI.UnitTests/ModelsTests/SeamlessCursorSettingsTests.cs` |
| Quick Access | `QuickAccess.UI/Helpers/ModuleGpoHelper.cs` |
| CmdPal | `Microsoft.CmdPal.Ext.PowerToys/Helpers/ModuleEnablementService.cs`, `Commands/MouseUtils/CalibrateSeamlessCursorCommand.cs`, `Modules/MouseUtilsModuleCommandProvider.cs`, `Properties/Resources.resx`, `Properties/Resources.Designer.cs` |
| Shared event | `src/common/interop/shared_constants.h`, `Constants.{idl,h,cpp}` |
| First-run page | `Settings.UI/SettingsXAML/OOBE/Views/OobeMouseUtils.xaml` |
| Developer docs | `doc/devdocs/modules/mouseutils/seamlesscursor.md`, `readme.md` |
| UI test helpers | `src/common/UITestAutomation.Next/SettingsConfigHelper.cs` (`KnownModuleNames`) and its unit test |
| CursorWrap | `MonitorTopology.cpp`: skip outer edges claimed through the export, and wrap to the same physical position (§6.4) |
| Signing | `.pipelines/ESRPSigning_core.json` |
| Privacy | `DATA_AND_PRIVACY.md` |

The installer needs no change: VNext harvests the output folder.

## 10. Testing

* **Native unit tests** (the engine is pure logic, so it is fully testable):
  * `f`/`g` round trip, and validation (order, range, minimum separation);
  * crossing on every edge type, crossing measured through pixel centers, and corners (first-crossed edge, fallback
    to the other edge, uncalibrated Windows neighbors, outer walls letting other hooks act first);
  * rule 1 on an outer edge, rule 2 stopping the cursor at the edge (also through a corner), rule 3 letting other
    hooks act first, and rules 2 and 3 with `stop_at_empty_edges` off;
  * several neighbors on one edge, and a calibrated neighbor winning over an uncalibrated one;
  * perpendicular overshoot scaling;
  * no bounce after a jump, and a cursor moved by another tool;
  * Windows re-arrangement keeping the physical mapping; records ignored when a monitor is missing, rotated or on
    another side;
  * glue candidates on every edge and glued pairs, claimed edge parts, the edge map parts, and physical wraps
    (`MapJump`, also through a glued pair);
  * the first usable record per monitor pair, overlapping calibrated neighbors, and a fast move across a thin monitor;
  * the touching pairs of the layout in §1, a side-by-side layout, and a real three-monitor layout.
* **Settings tests:** serialization and defaults (`Settings.UI.UnitTests`).
* **Manual checklist:**
  * calibrate on 2 and 3 monitors;
  * cross slowly and fast in every direction;
  * re-arrange in display settings without recalibrating;
  * unplug and replug a monitor;
  * games with `ClipCursor` and with a hidden cursor;
  * pen/touch;
  * a precision touchpad;
  * CursorWrap enabled together with Seamless, in both enable orders;
  * an elevated window in the foreground.

## 11. Telemetry

* Module enabled/disabled.
* Calibration saved: number of borders, enabled count, invalid count.
* No monitor identifiers, sizes or positions.

## 12. Prior art: LittleBigMouse

* LittleBigMouse is GPL-3.0 licensed. It was used **as a reference for ideas only**; no code is copied.
* Ideas taken:
  * swallow the move and call `SetCursorPos`;
  * pass through when another app has `ClipCursor` active or the cursor is hidden;
  * the hook watchdog;
  * the "nearest physical neighbor along the edge" rule, which is LittleBigMouse's default "Strait" mode.
* Not taken:
  * a full millimeter layout that users edit by hand (sizes, bezels, positions); this is what makes its configuration
    complex;
  * chains of `ClipCursor` rectangles;
  * rewriting the Windows display arrangement;
  * diagonal "Cross" mode.
* The overlay replaces all manual measurement with two drags per border.

## 13. Risks and open questions

* **Absolute-mode tablets** that are not marked as pen input may cause a one-frame flicker at borders. Accepted for v1.
* **Tools that inject absolute positions in small steps** (remote desktop and VNC clients, Mouse Without Borders on
  the controlled machine) stay under the injected-jump threshold (§6.5). At a calibrated crossing, their next position
  undoes the remap, so the cursor flickers for one step. Telling such streams apart from touchpads that inject would
  need a heuristic; accepted for v1.

Decided:

* **Outer-edge behavior of `WH_MOUSE_LL` points:** verified with a probe. Pushing against an outer edge reports hook
  points beyond the edge (for example x = 1193–1202 for an edge at x = 1204). Windows clamps the cursor only after
  the hook runs, so §6.3 detects outer-edge crossings from the hook point alone.
* **CursorWrap coordination:** a cross-DLL export (§6.4), not named shared memory.
* **Mouse Without Borders** switches machines at outer desktop edges in its own process. v1 only documents the
  conflict on claimed outer segments; affected users can switch that border off in the overlay.
* **Name and key:** display name "Seamless Multi-Screen Cursor", key `SeamlessCursor`, binary
  `PowerToys.SeamlessCursor.dll`.
* **Hosting:** the hook and the overlay run in PowerToys.exe on separate threads, like CursorWrap and Find My Mouse.
  A worker process like Auto Hide Cursor's was rejected: it adds process lifetime and IPC code, and it would rule out
  the in-process CursorWrap export.

## 14. Phased delivery

1. Prototype: hook and engine with a hand-written `calibration.json`, to confirm §13's hook behavior and how the
   remapping feels.
2. Engine, unit tests, calibration overlay, runner registration, minimal Settings page.
3. Full wiring (GPO, ADMX/ADML, signing, CmdPal enablement, telemetry, privacy doc) and the CursorWrap coordination.
4. Possible follow-ups: per-monitor speed normalization, cursor size normalization, a hold-key bypass, a hotkey to
   open the calibration, and matching monitors by EDID serial.
