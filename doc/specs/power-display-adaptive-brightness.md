# Power Display – Ambient-Light (ALS) Adaptive Brightness for External Monitors

Tracking issue: microsoft/PowerToys#49038
Behaviour discussion: #49038 (with @moooyo)
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
* A per-monitor **calibration curve** (lux → %), so differently-behaving panels can be matched.
* A per-monitor **offset** for manual/CLI nudges that rides on top of the curve (iOS-style personalization).
* Correct, safe behaviour when the sensor can't be trusted (e.g. laptop lid closed).
* Smooth, flicker-free, DDC/CI-friendly updates (smoothing + hysteresis + rate limiting).
* Coexist cleanly with manual brightness changes and the existing schedule/profile features.

### Non-goals
* Driving the built-in laptop panel (Windows already does this).
* Bundling external/USB ambient sensors (possible future extension).
* Content-adaptive / gamma-overlay dimming (a different, software-only approach).
* Colour-temperature / Night-Light behaviour.

## 3. Scenarios

1. A laptop is docked to one or two external monitors. As the room darkens through the evening, the externals dim
   automatically to track the room — today only the built-in panel does this.
2. A light-sensitive user wants externals to follow ambient light continuously, without manual sliders or rigid schedules.
3. Two different external panels are calibrated so they *look* equally bright across the whole ambient range.
4. Clamshell (lid closed): the sensor is blocked, so brightness holds steady instead of dimming to black.
5. A non-DDC/CI monitor: adaptive is greyed out for that monitor with a clear "not supported" reason.

## 4. Fit with existing modules

```
[ Device ALS ]
 LightSensor.ReadingChanged (lux)        <-- already consumed by Light Switch (#42566)
        │
        ▼
[ Adaptive Brightness engine ]  (new glue, lives in Power Display)
   smoothing (EMA) → hysteresis/deadband → curve(lux) + offset (per monitor) → rate limit
        │
        ▼
[ Power Display DDC/CI writer ]          <-- already exists (SetMonitorBrightness, VCP 0x10)
   per external monitor
```

No new module. The ALS reader can be refactored into a small shared helper so both Light Switch and Power Display
consume one sensor source (avoids two sensor subscriptions). On #47480 a maintainer noted Light Switch can already
link to a Power Display *profile* on a scheduler; this design adds an *ambient-light* trigger alongside the existing
*time/sun* trigger and addresses the "apply a Power Display profile without forcing a theme change" limitation raised there.

## 5. Core model — adaptive curve + per-monitor offset

The whole feature reduces to a single formula, applied **independently per external monitor**:

```
target%(lux) = clamp( curve(lux) + offset, minPercent, maxPercent )
```

### 5.1 The loop
Continuously: read device ALS (lux) → smooth → map through the monitor's **curve** → add the monitor's **offset** →
clamp → write via DDC/CI. One shared sensor subscription drives an independent engine per external monitor.

### 5.2 Per-monitor calibration curve — the multi-monitor sync mechanism
Each monitor has its own editable `lux → %` curve. This is what keeps multiple monitors *looking* equally bright:
panels differ in nits and have **non-linear** backlight response, so the same ambient lux should map to a *different*
percentage on different panels. A per-monitor curve corrects this across the **entire** ambient range — something a
single scalar value cannot do (a uniform shift would keep them matched at one light level but drift apart at others).

### 5.3 Per-monitor offset — live personalization
A signed per-monitor `offset%` added on top of the curve. Manual/CLI nudges adjust it, so the system learns the
user's preference (iOS-style) instead of overriding them. The **curve does calibration**; the **offset does live,
personal fine-tuning** (and is the quick way to nudge one screen to match the others without editing its curve).

### 5.4 Manual / CLI interaction while adaptive is ON
When the user changes brightness (slider or CLI increase/decrease) with adaptive on, per-monitor `manualBehavior`:
* **`offset` (default):** the delta becomes a persistent offset; adaptation keeps tracking ambient, just shifted.
  A CLI "brightness +10" makes the monitor 10% brighter *than the curve would pick*, and tracking continues.
* **`pause`:** adaptation suspends for that monitor (hard manual override) until the user re-enables it.

## 6. UX / Settings design

Per-monitor (WinUI3 `SettingsCard`/`SettingsExpander`):

* **Brightness source:** `Manual` (default) | `Adaptive (ambient light)`. Adaptive is disabled with a reason string
  when (a) no ALS is present, or (b) the monitor has no DDC/CI brightness control.
* **Default (zero-config):** on/off + a single **Responsiveness** slider (Calm ↔ Snappy). A sensible default curve
  ships out of the box so most users touch nothing else.
* **Advanced (collapsible):** editable **calibration curve** (anchor points, e.g. `0 lx→20%, 50→40%, 300→70%, 1000→100%`
  with a "Reset"), **min/max** clamps, and the `manualBehavior` choice.
* **Live slider:** when adaptive is on, the brightness slider becomes **live** — it reflects the current computed
  target and moves on its own as ambient changes, with an **"Auto"** badge and a readout (`Ambient 240 lx → 65%`).
  Dragging it applies the **offset** (§5.3); it does not silently disable adaptive.

Global: a module-level toggle and an enable/disable hotkey (consistent with other Power Display shortcuts, e.g. #48784).

## 7. Sensor trust & lifecycle

The ALS reading is used **only when it can be trusted**.

### 7.1 ALS / DDC-CI availability
`LightSensor.GetDefault()` null → adaptive unavailable everywhere (e.g. a desktop). Per monitor, if DDC/CI brightness
is unsupported → adaptive is disabled for that monitor with a reason.

### 7.2 Lid closed / clamshell (sensor blocked)
When the lid is closed the ALS faces the keyboard deck and reads ~darkness, so we must **not** drive brightness from it.
* **Detect:** lid state via `RegisterPowerSettingNotification(GUID_LIDSWITCH_STATE_CHANGE)` (0 = closed, 1 = open),
  corroborated by `QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS)` for an active internal panel
  (`DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL`). Lid *position* is the true determinant — a user may have the lid open
  but the internal display disabled, and there the sensor still sees the room.
* **Behaviour:** on lid-close → pause adaptation and **hold the last known-good brightness** (never dim-to-black),
  surface a status ("Adaptive paused — lid closed / ambient sensor blocked"), and **resume live on reopen**.

### 7.3 Honest boundary + fallbacks
Clamshell-docked is a *primary* scenario for this feature, yet it's exactly where the laptop ALS can't see the room.
Therefore:
* **v1:** full ALS adaptive when the lid is open; hold/pause when closed.
* **Optional fallback:** when the ALS is untrusted, fall back to **time/schedule-based** brightness (the #47480
  mechanism) so clamshell users still get some automation.
* **Future:** an **external ambient sensor** (monitor-integrated or USB) for true clamshell tracking (deferred).

## 8. Technical design

### 8.1 ALS acquisition
* `LightSensor.GetDefault()`; subscribe to `ReadingChanged`; set `ReportInterval = max(minimumReportInterval, ~500ms)`
  to limit churn/battery. Value: `LightSensorReading.IlluminanceInLux` (double).

### 8.2 Smoothing + hysteresis (anti-flicker, anti-wear)
* **EMA** on lux: `s_t = α·lux + (1−α)·s_{t−1}` (α from the Responsiveness slider).
* **Deadband:** ignore changes whose mapped target moves < ~2–3% from the last applied value.
* **Min update interval** per monitor (~1–2 s) and optional **ramping** (step toward target).
* Rationale: DDC/CI writes are slow (tens–hundreds of ms) and excessive writes can flicker or wear some panels.

### 8.3 Mapping
* Piecewise-linear interpolation over the anchor points; add `offset`; clamp to per-monitor min/max.
* Curve + offset stored per monitor (keyed by stable monitor id, as Power Display already does for profiles).

### 8.4 Brightness write
* Reuse Power Display's DDC/CI path (`GetPhysicalMonitorsFromHMONITOR` → `SetMonitorBrightness`, VCP `0x10`).
* Capability probe per monitor (`GetMonitorBrightness` / VCP support); if unsupported, disable adaptive for it.

### 8.5 Concurrency
* One engine per external monitor, one shared ALS subscription. Serialize DDC/CI writes per monitor on a worker;
  **never block the sensor callback**. Handle monitor hotplug / dock connect-disconnect (Power Display already tracks add/remove).

## 9. Settings persistence

Extend Power Display's `settings.json` per-monitor entry, e.g.:

```jsonc
{
  "monitors": [{
    "id": "<stable-monitor-id>",
    "brightnessSource": "adaptive",          // "manual" | "adaptive"
    "adaptive": {
      "curve": [[0,20],[50,40],[300,70],[1000,100]],  // [lux, percent] calibration, per monitor
      "offsetPercent": 0,                    // signed live nudge added on top of the curve
      "minPercent": 15,
      "maxPercent": 100,
      "responsiveness": 0.4,                 // 0..1 → EMA α / update cadence
      "manualBehavior": "offset",            // "offset" (default) | "pause"
      "deadbandPercent": 3,
      "minUpdateMs": 1500
    }
  }]
}
```

`target = clamp( interpolate(curve, lux) + offsetPercent, minPercent, maxPercent )`.

## 10. Telemetry

Per `doc/devdocs/development/logging.md`, emit (privacy-respecting, counts only):
* Adaptive mode enabled/disabled per monitor; ALS-present; DDC/CI-supported; `manualBehavior` chosen.
* Apply-rate (writes/min), deadband/clamp suppression counts, and lid-close pause events (to validate anti-wear tuning).
* No raw lux series or PII.

## 11. Performance & reliability
* Sensor at ~0.5–2 s cadence; writes throttled per §8.2 → negligible CPU and few DDC/CI writes.
* Worker isolates slow DDC/CI calls from the UI/sensor thread.
* Graceful degradation: no ALS → mode hidden/disabled; no DDC/CI → mode disabled per monitor with reason; lid closed → hold last-good.

## 12. Accessibility / Localization
* All new strings via `.resw` + `x:Uid` (and discoverable by the settings search index, settings-search.md).
* Curve editor keyboard-navigable; the live slider value, "Auto" badge, and ambient readout exposed to narrator.

## 13. Risks & open questions
* **Manual-vs-adaptive arbitration** — resolved: default **`offset`** (iOS-style), with **`pause`** opt-in (direction agreed in #49038).
* **ALS placement** — chassis lux ≠ panel-facing lux; mitigated by the per-monitor curve; documented as a limitation.
* **Clamshell** — laptop ALS is blind when the lid is closed; handled via hold/pause + optional time fallback; external sensor is future work.
* **DDC/CI variance** across docks/USB-C/adapters (cf. #47577) — robust capability probing + clear unsupported messaging.
* **Shared ALS source** — refactor Light Switch's reader into a shared helper (preferred) vs. two subscriptions.
* **Open (tune during implementation):** exact default curve values and the Responsiveness → (α, min-update-interval) mapping.

## 14. Phased delivery
1. Engine + per-monitor `adaptive` (curve + offset) + default curve + DDC/CI write reuse + sensor-trust gating (JSON-config; minimal UI).
2. Settings UX: source dropdown, live slider + "Auto" badge/readout, curve editor, responsiveness, clamps, `manualBehavior`.
3. Shared ALS helper refactor (Light Switch + Power Display); precedence with schedule/profile; optional time-based fallback.
4. Telemetry + docs (`doc/devdocs/modules/powerdisplay/…`) + tests.
