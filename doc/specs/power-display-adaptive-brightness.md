# Power Display – Ambient-Light (ALS) Adaptive Brightness for External Monitors

Tracking issue: microsoft/PowerToys#49038
Related: #47480 (schedule-based auto-brightness), #42566 (Light Switch ALS → theme), #35564 (manual multi-monitor brightness), #1052 (Quick Display adjustment umbrella, completed)

## 1. Overview

Windows can auto-adjust the **built-in laptop panel** from the device's ambient light sensor (ALS), but
**external monitors have no such capability** — they almost never carry an ALS, and Windows will not pipe the
laptop's ALS reading to an external display. Today's PowerToys covers the two halves separately:

* **Light Switch** already reads the device ALS (`Windows.Devices.Sensors.LightSensor` → `IlluminanceInLux`) to drive theme.
* **Power Display** already writes external-monitor brightness over **DDC/CI** (`SetMonitorBrightness`, VCP `0x10`).

This feature wires them together: a per-monitor **Adaptive (ambient light)** mode in Power Display that continuously
maps the device's ambient lux to each external monitor's brightness. It is the **continuous, sensor-driven** counterpart
to the **time/schedule-based** request tracked in #47480 — and it is what most users actually mean by "auto-brightness".

## 2. Goals / Non-goals

### Goals
* Per-external-monitor opt-in adaptive brightness driven by the device ALS.
* User-tunable mapping (lux → brightness %), because the ALS is on the chassis, not the panel, and panels differ.
* Smooth, flicker-free, DDC/CI-friendly updates (smoothing + hysteresis + rate limiting).
* Coexist cleanly with manual brightness changes and the existing schedule/profile features.

### Non-goals
* Driving the built-in laptop panel (Windows already does this).
* Adding hardware/external USB light sensors (possible future extension).
* Content-adaptive / gamma-overlay dimming (that is a different, software-only approach).
* Color-temperature / Night-Light behavior.

## 3. Scenarios

1. A laptop is docked to one or two external monitors. As the room darkens through the evening, the externals dim
   automatically to track the room — today only the built-in panel does this.
2. A light-sensitive user wants externals to follow ambient light continuously, without manual sliders or rigid schedules.
3. A user with a non-DDC/CI monitor: the mode is greyed out for that monitor with a clear "not supported" reason.

## 4. Fit with existing modules

```
[ Device ALS ]
 LightSensor.ReadingChanged (lux)        <-- already consumed by Light Switch (#42566)
        │
        ▼
[ Adaptive Brightness engine ]  (new glue, lives in Power Display)
   smoothing (EMA) → hysteresis/deadband → lux→% curve (per monitor) → rate limit
        │
        ▼
[ Power Display DDC/CI writer ]          <-- already exists (SetMonitorBrightness, VCP 0x10)
   per external monitor
```

No new module. The ALS reader can be refactored into a small shared helper so both Light Switch and Power Display
consume one sensor source (avoids two sensor subscriptions). On #47480 a maintainer noted Light Switch can already
link to a Power Display *profile* on a scheduler; this design adds an *ambient-light* trigger alongside the existing
*time/sun* trigger and addresses the "apply a Power Display profile without forcing a theme change" limitation raised there.

## 5. UX / Settings design

In Power Display's per-monitor settings (WinUI3 `SettingsCard`/`SettingsExpander`):

* **Brightness source** (per monitor): `Manual` (default) | `Adaptive (ambient light)`.
  * `Adaptive` is disabled with a reason string when (a) no ALS is present, or (b) the monitor has no DDC/CI brightness control.
* **Calibration curve** (shown when Adaptive): an editable set of anchor points `(lux, brightness%)`, e.g.
  `0 lx→20%, 50 lx→40%, 300 lx→70%, 1000 lx→100%`, with sensible defaults and a "Reset" button.
* **Responsiveness**: slider mapping to the smoothing time-constant / min update interval (Calm ↔ Snappy).
* **Min/Max brightness clamp** (per monitor).
* A small live readout: "Ambient: 240 lx → target 65%".

Global:
* A module-level toggle and an enable/disable hotkey (consistent with other Power Display shortcuts, e.g. #48784).

## 6. Technical design

### 6.1 ALS acquisition
* `LightSensor.GetDefault()`; if null → ALS unavailable → adaptive mode unavailable everywhere.
* Subscribe to `ReadingChanged`; set `ReportInterval = max(minimumReportInterval, ~500ms)` to limit churn/battery.
* Reading value: `LightSensorReading.IlluminanceInLux` (double).

### 6.2 Smoothing + hysteresis (anti-flicker, anti-wear)
* **EMA** on lux: `s_t = α·lux + (1−α)·s_{t−1}` (α from the Responsiveness slider).
* **Deadband**: ignore changes whose mapped target moves < `N`% (e.g. 2–3%) from the last applied value.
* **Min update interval** per monitor (e.g. ≥ 1–2 s) and optional **ramping** (step toward target) to avoid visible jumps.
* Rationale: DDC/CI writes are slow (tens–hundreds of ms) and excessive writes can flicker or wear some panels.

### 6.3 Mapping
* Piecewise-linear interpolation over the user's anchor points; clamp to per-monitor min/max.
* Curve stored per monitor (keyed by stable monitor id, as Power Display already does for profiles).

### 6.4 Brightness write
* Reuse Power Display's existing DDC/CI path (`GetPhysicalMonitorsFromHMONITOR` → `SetMonitorBrightness`, VCP `0x10`).
* Capability probe per monitor (`GetMonitorBrightness` / VCP support); if unsupported, disable adaptive for it.
* Serialize writes per monitor; never block the sensor callback (queue + worker).

### 6.5 Interaction & precedence
* A **manual** brightness change while Adaptive is on → either (a) temporarily pause adaptation for that monitor until
  re-enabled, or (b) treat it as a curve offset. (Open question — see §11.)
* Precedence with schedule/profile (#47480): define a single owner of brightness at a time; Adaptive and Schedule are
  mutually exclusive per monitor (last selected wins), surfaced clearly in UI.

### 6.6 Multi-monitor & lifecycle
* Independent engine per external monitor; shared single ALS subscription.
* Handle monitor hotplug / dock connect-disconnect (Power Display already tracks monitor add/remove).
* Clamshell / lid-closed: if the ALS is unavailable when the lid is closed, hold last value and resume on reopen.

## 7. Settings persistence

Extend Power Display's `settings.json` per-monitor entry, e.g.:

```jsonc
{
  "monitors": [{
    "id": "<stable-monitor-id>",
    "brightnessSource": "adaptive",           // "manual" | "adaptive"
    "adaptive": {
      "curve": [[0,20],[50,40],[300,70],[1000,100]],   // [lux, percent]
      "minPercent": 15,
      "maxPercent": 100,
      "responsiveness": 0.4,                   // 0..1 → EMA α / interval
      "deadbandPercent": 3,
      "minUpdateMs": 1500
    }
  }]
}
```

## 8. Telemetry

Per `doc/devdocs/development/logging.md`, emit (privacy-respecting, counts only):
* Adaptive mode enabled/disabled per monitor; ALS-present; DDC/CI-supported.
* Apply-rate (writes/min) and clamp/deadband suppression counts (to validate anti-wear tuning).
* No raw lux series or PII.

## 9. Performance & reliability
* Sensor at ~0.5–2 s cadence; writes throttled per §6.2 → negligible CPU and few DDC/CI writes.
* Worker isolates slow DDC/CI calls from the UI/sensor thread.
* Graceful degradation: no ALS → mode hidden/disabled; no DDC/CI → mode disabled per monitor with reason.

## 10. Accessibility / Localization
* All new strings via `.resw` + `x:Uid` (and discoverable by the settings search index, settings-search.md).
* Curve editor keyboard-navigable; live readout exposed to narrator.

## 11. Risks & open questions
* **Manual-vs-adaptive arbitration** — pause-on-manual vs. offset model? (Recommend pause-until-toggle for v1.)
* **ALS placement** — chassis lux ≠ panel-facing lux; mitigated by user curve, but document the limitation.
* **DDC/CI variance** across docks/USB-C/adapters (cf. #47577) — robust capability probing + clear unsupported messaging.
* **Shared ALS source** — refactor Light Switch's reader into a shared helper, or keep two subscriptions? (Prefer shared.)
* **Scope of v1** — single internal ALS only; external USB sensors deferred.

## 12. Phased delivery
1. Engine + per-monitor `adaptive` setting + default curve + DDC/CI write reuse (no curve editor UI yet; JSON-config).
2. Settings UX: source dropdown, curve editor, responsiveness, clamps, live readout.
3. Shared ALS helper refactor (Light Switch + Power Display); precedence with schedule/profile.
4. Telemetry + docs (`doc/devdocs/modules/powerdisplay/…`) + tests.
