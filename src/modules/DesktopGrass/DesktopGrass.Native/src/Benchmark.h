// Benchmark.h
//
// Optional benchmark entry point used by tools/benchmark/ to measure renderer
// cost in a deterministic, headless-ish run. NOT compiled into the production
// path — main.cpp only invokes this when `--benchmark` appears on the command
// line. Production code is untouched.
//
// The benchmark:
//   * Creates ONE GrassWindow on the primary monitor's bottom strip (visible,
//     same DComp/Direct2D path users see).
//   * Skips the tray icon, MouseHook, persistence, and multi-monitor enum.
//   * Forces a fixed scene + critter + seed so blade/entity content is
//     reproducible across runs.
//   * Renders for the requested duration, capturing per-frame CPU-side
//     timings to a CSV.
//   * Exits cleanly so an external driver can collect counters and move on.

#pragma once

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <cstdint>
#include <string>

#include "Constants.h"

namespace desktopgrass::benchmark {

struct Options {
    Scene        scene        = Scene::Grass;
    CritterKind  critter      = CRITTER_DEFAULT;
    int          critterCount = 0;      // 0 = scene default (random species count)
    uint64_t     seed         = 0;      // 0 = use the production app seed
    int          durationSec  = 60;
    int          widthPx      = 0;      // 0 = primary-monitor work-area width
    int          heightPx     = 0;      // 0 = STRIP_HEIGHT + HEADROOM at primary DPI
    int          targetFps    = 24;     // matches Config.h kTargetFpsDefault; overridable
    std::wstring outCsvPath;            // empty = no per-frame log written
    bool         hideWindow   = false;  // SW_HIDE instead of SW_SHOWNOACTIVATE
};

// Parse `--benchmark`-mode args. argv[0] should be the executable; subsequent
// entries are recognized in the form `--key=value` or `--key value`. Unknown
// flags are silently ignored so future production flags don't break older
// harness invocations. Returns false if a required value is malformed.
bool ParseOptions(int argc, wchar_t** argv, Options& out);

// Run a single benchmark using `opts`. Returns the process exit code:
//   0  -> ran for the full duration
//   1  -> setup failure (window/renderer init, output CSV open)
//   2  -> early exit (user closed the window before duration elapsed)
int Run(HINSTANCE hInst, const Options& opts);

} // namespace desktopgrass::benchmark
