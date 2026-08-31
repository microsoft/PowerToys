# Laser Pointer stroke checks

`LaserStrokeChecks.cpp` exercises `LaserStroke` — the trail model that decides where the
laser trail is, how wide it is at each sample, and when it disappears. The model is
deliberately free of Windows dependencies so it can be run on its own, without the
overlay, the mouse hook or a GPU.

It covers:

- an empty stroke draws nothing, and a single sample draws a round dot of the right size
- the taper: widest at the head, narrowing over the trailing `decayLength` samples
- the per-sample time decay, and that `Prune` eventually empties the stroke
- `widthScale`, which the renderer uses to derive the glow and core passes from the same
  centerline
- streamline smoothing placing a sample between the previous one and the raw input
- the minimum-distance guard, so a stationary pointer does not pile up samples
- `Finish()` / `Clear()` semantics
- a stroke that doubles back on itself still producing a finite, non-degenerate outline

## Running

The checks are not part of the module build (the module is a DLL with no entry point of
its own). Build them standalone from a Developer Command Prompt:

```cmd
cl /nologo /EHsc /std:c++20 /W4 /permissive- /I.. LaserStrokeChecks.cpp ..\LaserStroke.cpp /Fe:LaserStrokeChecks.exe
LaserStrokeChecks.exe
```

`LaserStroke.cpp` includes the module's `pch.h`; when building outside the project, put an
empty `pch.h` on the include path (or pass `/FI` a stub), since the stroke model itself
needs nothing from it.

The program prints one line per check and exits non-zero if any of them fail.
