# Screen Region Share — proof of concept

Exploratory PoC for a PowerToys utility that lets you draw a rectangle on your
screen and share **just that region** in Microsoft Teams (or Zoom / Meet /
Discord / any app with a "Share → Window" picker).

> ⚠️ This is throwaway PoC code living outside the real module tree. It exists to
> validate the approach before scaffolding `src/modules/ScreenRegionShare/`.

## The idea

Call apps can only share a whole screen or a whole window — not an arbitrary
rectangle. This PoC works around that by pairing two windows:

| Window | Role | Visible to user? | Visible to Teams? |
|--------|------|------------------|-------------------|
| **Marker** (`SRS_Marker`) | The red hollow frame you draw & resize. Click-through interior. | ✅ Yes | ❌ No — excluded via `WDA_EXCLUDEFROMCAPTURE` |
| **Mirror** (`SRS_Mirror`) | A real-pixel window that Windows.Graphics.Capture (WGC) captures the monitor into, cropped to the marker rect. **This is the window you share in Teams.** | ❌ No — parked fully off-screen | ✅ Yes — listed in the picker (`WS_EX_APPWINDOW`) and live-composed by DWM |

Because DWM composes off-screen (but non-minimized) top-level windows, the mirror
never appears on any monitor yet Teams can still enumerate **and** capture it live
— proven by `capcheck.cpp`.

## Files

| File | What it is |
|------|------------|
| `overlay.cpp` | Earliest PoC — the pure-Win32 red marker frame (resize UX only). Builds `OverlayDemo.exe`. |
| `main.cpp` | Earliest WGC mirror pipeline (D3D11 + WinRT capture). Builds `ScreenRegionShare.exe`. |
| `share.cpp` | **The combined app**: marker + off-screen mirror + live crop. Builds `ShareDemo.exe`. |
| `capcheck.cpp` | Independent proof tool — WGC `CreateForWindow` on a window by title (exactly what Teams does) → saves one frame to BMP. Builds `CapCheck.exe`. |

## Building

Requires the MSVC C++ toolchain and the C++/WinRT projection headers under
`generated/` (produced by `cppwinrt.exe`; not committed).

Run from a **Developer** shell (or after `call vcvars64.bat`), from this folder:

```bat
cl /nologo /std:c++20 /EHsc /permissive- /DUNICODE /D_UNICODE /I generated share.cpp    /Fe:ShareDemo.exe /link /SUBSYSTEM:CONSOLE
cl /nologo /std:c++20 /EHsc /permissive- /DUNICODE /D_UNICODE /I generated capcheck.cpp  /Fe:CapCheck.exe  /link /SUBSYSTEM:CONSOLE
```

Link libraries come from `#pragma comment(lib, ...)` inside each source file
(`d3d11`, `dxgi`, `dwmapi`, `windowsapp`, `user32`, `gdi32`).

### Generating the WinRT headers

If `generated/` is missing:

```bat
cppwinrt -in sdk -out generated
```

## Running `ShareDemo.exe`

- `Ctrl+Shift+R` — draw a region and start sharing (press again to stop).
- `Ctrl+Shift+Q` — quit.
- Drag any border/corner of the red frame to resize; the shared crop follows live.

Then in your call app: **Share → Window → "PowerToys — Shared Region (PoC)"**.

Handy flags for scripted testing:

- `--rect X Y W H` — start sharing a fixed rectangle immediately (physical px).
- `--seconds N` — auto-exit after N seconds.

## Known PoC limitations (to handle in the real module)

- The off-screen mirror leaves a **taskbar button**.
- Cross-DPI sources need the crop math to account for per-monitor scaling.
- Marker is resize-only (no interior drag-to-move), a side effect of the
  cross-process click-through hole.
