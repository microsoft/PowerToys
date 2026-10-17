# Laser Pointer unit tests

`LaserStrokeTests.cpp` exercises `LaserStroke` — the trail model that decides where the
laser trail is, how wide it is at each sample, and when it disappears. The model is
deliberately free of Windows dependencies, so it is compiled straight into this test
binary and runs without the overlay, the mouse hook or a GPU.

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

This is a `NativeUnitTestProject` (`LaserPointer.UnitTests`) in `PowerToys.slnx`, so Test
Explorer discovers and runs it like any other. From the command line:

```cmd
msbuild src\modules\MouseUtils\LaserPointer\LaserPointerTests\LaserPointerUnitTests.vcxproj /p:Configuration=Debug /p:Platform=x64
vstest.console.exe x64\Debug\tests\LaserPointer\LaserPointer.UnitTests.dll /Platform:x64
```

CI runs it as part of the `Native Tests` step, which picks up `**\*UnitTest*.dll` from the
build output — the project name is what puts it in that set, so renaming it would silently
drop it from the run.

`LaserStroke.cpp` is compiled by this project as well as by the module. It includes no
`pch.h` for exactly that reason, and the module marks it `PrecompiledHeader=NotUsing` to
match; keep both in step if that ever changes.
