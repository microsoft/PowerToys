// Benchmark.cpp
//
// See Benchmark.h. Minimal, side-effect-free measurement runner.

#include "Benchmark.h"

#include "GrassWindow.h"
#include "Pacing.h"
#include "Sim.h"

#include <shellscalingapi.h>

#include <algorithm>
#include <cstdio>
#include <cwchar>
#include <cstring>
#include <string>

#pragma comment(lib, "Shcore.lib")
#pragma comment(lib, "User32.lib")

namespace desktopgrass::benchmark {

namespace {

// Same seed used by the production App so blade layouts are bit-identical
// across production runs and benchmark runs at the same monitor origin.
constexpr uint64_t kBenchmarkDefaultSeed = 0xD3C7C0F30070D511ull;

bool ParseInt(const wchar_t* s, int& out) {
    if (!s || !*s) return false;
    wchar_t* end = nullptr;
    long v = std::wcstol(s, &end, 10);
    if (end == s || *end != L'\0') return false;
    out = static_cast<int>(v);
    return true;
}

bool ParseU64(const wchar_t* s, uint64_t& out) {
    if (!s || !*s) return false;
    wchar_t* end = nullptr;
    int base = 10;
    if (s[0] == L'0' && (s[1] == L'x' || s[1] == L'X')) {
        base = 16;
        s += 2;
    }
    unsigned long long v = std::wcstoull(s, &end, base);
    if (end == s || *end != L'\0') return false;
    out = static_cast<uint64_t>(v);
    return true;
}

bool ParseScene(const wchar_t* s, Scene& out) {
    int n = -1;
    if (ParseInt(s, n) && n >= 0 && n < SCENE_COUNT) {
        out = static_cast<Scene>(n);
        return true;
    }
    if (_wcsicmp(s, L"grass")  == 0) { out = Scene::Grass;  return true; }
    if (_wcsicmp(s, L"desert") == 0) { out = Scene::Desert; return true; }
    if (_wcsicmp(s, L"winter") == 0) { out = Scene::Winter; return true; }
    if (_wcsicmp(s, L"autumn") == 0) { out = Scene::Autumn; return true; }
    if (_wcsicmp(s, L"ocean")  == 0) { out = Scene::Ocean;  return true; }
    return false;
}

bool ParseCritter(const wchar_t* s, CritterKind& out) {
    int n = -1;
    if (ParseInt(s, n) && n >= 0 && n < CRITTER_COUNT) {
        out = static_cast<CritterKind>(n);
        return true;
    }
    if (_wcsicmp(s, L"none")  == 0) { out = CritterKind::None;  return true; }
    if (_wcsicmp(s, L"sheep") == 0) { out = CritterKind::Sheep; return true; }
    if (_wcsicmp(s, L"cat")   == 0) { out = CritterKind::Cat;   return true; }
    if (_wcsicmp(s, L"bunny") == 0) { out = CritterKind::Bunny; return true; }
    if (_wcsicmp(s, L"all")   == 0) { out = CritterKind::Bunny; return true; }
    return false;
}

// Split an arg of the form `--key=value` into (key, value). If the arg is
// `--key` with the value in the next arg, value is set to the next arg and
// `consumedExtra` is true.
struct KV {
    std::wstring key;
    const wchar_t* value = nullptr;
    bool consumedExtra = false;
};

KV SplitArg(const wchar_t* arg, const wchar_t* nextArg) {
    KV kv;
    if (!arg || arg[0] != L'-' || arg[1] != L'-') return kv;
    const wchar_t* eq = std::wcschr(arg, L'=');
    if (eq) {
        kv.key.assign(arg + 2, eq);
        kv.value = eq + 1;
    } else {
        kv.key.assign(arg + 2);
        if (nextArg && nextArg[0] != L'-') {
            kv.value = nextArg;
            kv.consumedExtra = true;
        }
    }
    return kv;
}

// Get the primary monitor's work area + effective DPI.
bool GetPrimaryMonitorInfo(RECT& workOut, UINT& dpiOut) {
    POINT origin{ 0, 0 };
    HMONITOR mon = MonitorFromPoint(origin, MONITOR_DEFAULTTOPRIMARY);
    if (!mon) return false;
    MONITORINFO mi{};
    mi.cbSize = sizeof(mi);
    if (!GetMonitorInfoW(mon, &mi)) return false;
    workOut = mi.rcWork;
    UINT xDpi = 96, yDpi = 96;
    if (FAILED(GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, &xDpi, &yDpi))) {
        xDpi = 96;
    }
    dpiOut = xDpi;
    return true;
}

} // anonymous

bool ParseOptions(int argc, wchar_t** argv, Options& out) {
    // argv[0] is the executable; start at 1.
    for (int i = 1; i < argc; ++i) {
        const wchar_t* arg = argv[i];
        if (!arg || arg[0] != L'-' || arg[1] != L'-') continue;
        // --benchmark is handled by the caller (it's the mode switch) — skip.
        if (_wcsicmp(arg, L"--benchmark") == 0) continue;

        const wchar_t* nextArg = (i + 1 < argc) ? argv[i + 1] : nullptr;
        KV kv = SplitArg(arg, nextArg);
        if (kv.key.empty()) continue;
        if (kv.consumedExtra) ++i;
        if (!kv.value) continue;

        if (_wcsicmp(kv.key.c_str(), L"scene") == 0) {
            if (!ParseScene(kv.value, out.scene)) return false;
        } else if (_wcsicmp(kv.key.c_str(), L"critter") == 0) {
            if (!ParseCritter(kv.value, out.critter)) return false;
        } else if (_wcsicmp(kv.key.c_str(), L"critter-count") == 0 ||
                   _wcsicmp(kv.key.c_str(), L"crittercount") == 0) {
            if (!ParseInt(kv.value, out.critterCount)) return false;
        } else if (_wcsicmp(kv.key.c_str(), L"seed") == 0) {
            if (!ParseU64(kv.value, out.seed)) return false;
        } else if (_wcsicmp(kv.key.c_str(), L"duration") == 0) {
            if (!ParseInt(kv.value, out.durationSec)) return false;
            if (out.durationSec < 1) out.durationSec = 1;
        } else if (_wcsicmp(kv.key.c_str(), L"width") == 0) {
            if (!ParseInt(kv.value, out.widthPx)) return false;
        } else if (_wcsicmp(kv.key.c_str(), L"height") == 0) {
            if (!ParseInt(kv.value, out.heightPx)) return false;
        } else if (_wcsicmp(kv.key.c_str(), L"fps") == 0 ||
                   _wcsicmp(kv.key.c_str(), L"target-fps") == 0) {
            if (!ParseInt(kv.value, out.targetFps)) return false;
            if (out.targetFps < 1) out.targetFps = 1;
        } else if (_wcsicmp(kv.key.c_str(), L"out") == 0 ||
                   _wcsicmp(kv.key.c_str(), L"csv") == 0) {
            out.outCsvPath.assign(kv.value);
        } else if (_wcsicmp(kv.key.c_str(), L"hidden") == 0 ||
                   _wcsicmp(kv.key.c_str(), L"hide") == 0) {
            // Treat presence of the flag as `true`. Accept explicit value too.
            int v = 1;
            ParseInt(kv.value, v);
            out.hideWindow = (v != 0);
        }
        // Unknown flags are silently ignored — older drivers stay compatible
        // with newer binaries that add flags.
    }
    return true;
}

int Run(HINSTANCE hInst, const Options& opts) {
    RECT primaryWork{};
    UINT primaryDpi = 96;
    if (!GetPrimaryMonitorInfo(primaryWork, primaryDpi)) {
        std::fwprintf(stderr, L"[benchmark] failed to query primary monitor\n");
        return 1;
    }

    const int primaryW = primaryWork.right - primaryWork.left;
    const int primaryH = primaryWork.bottom - primaryWork.top;
    const int targetW  = opts.widthPx  > 0 ? opts.widthPx  : primaryW;
    const int defaultStripPx = static_cast<int>(
        ((STRIP_HEIGHT + HEADROOM) * primaryDpi / 96.0) + 0.5);
    const int targetH  = opts.heightPx > 0 ? opts.heightPx : defaultStripPx;

    // GrassWindow.Create takes a "monitor work area" rect; it derives window
    // origin/size from it. To honour width/height overrides while keeping the
    // window pinned to the bottom-left of the primary work area, fabricate a
    // monitorBounds rect of the requested width whose bottom matches the
    // primary work-area bottom. Height is forced via the strip-DIP formula
    // inside GrassWindow.Create; if the caller asked for a non-default
    // heightPx we ignore that for the actual HWND (the renderer always uses
    // STRIP_HEIGHT + HEADROOM in DIPs), but we still log targetH in the CSV
    // header for traceability.
    RECT monitorBounds = primaryWork;
    monitorBounds.right = monitorBounds.left + targetW;
    (void)targetH; // logged in CSV header below; HWND height is fixed by spec

    if (!GrassWindow::RegisterWindowClass(hInst)) {
        std::fwprintf(stderr, L"[benchmark] RegisterWindowClass failed\n");
        return 1;
    }

    const uint64_t seed = opts.seed != 0 ? opts.seed : kBenchmarkDefaultSeed;

    GrassWindow window;
    if (!window.Create(hInst, monitorBounds, primaryDpi, seed,
                       /*density=*/1.0, /*swaySpeed=*/1.0, /*swayAmplitude=*/1.0)) {
        std::fwprintf(stderr, L"[benchmark] GrassWindow::Create failed\n");
        return 1;
    }

    if (!opts.hideWindow) {
        window.Show();
    }

    Sim& sim = window.GetRenderer().GetSim();
    sim_set_scene(sim, opts.scene);
    sim_set_critter_count(sim, opts.critterCount);
    sim_set_critter(sim, opts.critter);

    // Optional per-frame CSV.
    FILE* csv = nullptr;
    if (!opts.outCsvPath.empty()) {
        if (_wfopen_s(&csv, opts.outCsvPath.c_str(), L"w, ccs=UTF-8") != 0 || !csv) {
            std::fwprintf(stderr, L"[benchmark] failed to open %ls for write\n",
                          opts.outCsvPath.c_str());
            return 1;
        }
        std::fwprintf(csv,
            L"# scene=%d critter=%d critter_count=%d seed=0x%016llX "
            L"duration_s=%d target_fps=%d width_px=%d height_px=%d dpi=%u "
            L"primary_work=%dx%d\n",
            static_cast<int>(opts.scene),
            static_cast<int>(opts.critter),
            opts.critterCount,
            static_cast<unsigned long long>(seed),
            opts.durationSec, opts.targetFps, targetW, targetH,
            primaryDpi, primaryW, primaryH);
        std::fwprintf(csv, L"frame_index,t_seconds,dt_ms,render_ms\n");
    }

    LARGE_INTEGER qpcFreq{};
    QueryPerformanceFrequency(&qpcFreq);
    const double freq = static_cast<double>(qpcFreq.QuadPart);

    LARGE_INTEGER tStart{};
    QueryPerformanceCounter(&tStart);
    LARGE_INTEGER tPrev = tStart;

    const double durationS = static_cast<double>(opts.durationSec);
    const double targetFrameSec = 1.0 / static_cast<double>(opts.targetFps);

    long long frameIndex = 0;
    bool userClosed = false;
    FramePacer pacer;
    int  exitCode = 0;

    MSG msg{};
    while (true) {
        while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE)) {
            if (msg.message == WM_QUIT) {
                userClosed = true;
                break;
            }
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        if (userClosed) break;

        LARGE_INTEGER tNow{};
        QueryPerformanceCounter(&tNow);
        const double elapsedSinceStart =
            static_cast<double>(tNow.QuadPart - tStart.QuadPart) / freq;
        if (elapsedSinceStart >= durationS) break;

        const double dt = static_cast<double>(tNow.QuadPart - tPrev.QuadPart) / freq;
        tPrev = tNow;

        LARGE_INTEGER tRenderStart{};
        QueryPerformanceCounter(&tRenderStart);
        window.RenderFrame(dt, nullptr, 0);
        LARGE_INTEGER tRenderEnd{};
        QueryPerformanceCounter(&tRenderEnd);

        const double renderMs =
            static_cast<double>(tRenderEnd.QuadPart - tRenderStart.QuadPart) * 1000.0 / freq;

        if (csv) {
            std::fwprintf(csv, L"%lld,%.6f,%.4f,%.4f\n",
                          frameIndex, elapsedSinceStart, dt * 1000.0, renderMs);
        }
        ++frameIndex;

        // Pace to target fps via the shared high-resolution waitable timer.
        LARGE_INTEGER tAfter{};
        QueryPerformanceCounter(&tAfter);
        const double spentSec =
            static_cast<double>(tAfter.QuadPart - tNow.QuadPart) / freq;
        const double remaining = targetFrameSec - spentSec;
        pacer.WaitUntilNextFrame(remaining);
    }

    if (csv) std::fclose(csv);

    if (userClosed) exitCode = 2;

    // Print one-line summary so the harness can capture even without parsing
    // the CSV — written to stdout so it shows up in run logs.
    LARGE_INTEGER tEnd{};
    QueryPerformanceCounter(&tEnd);
    const double totalSec =
        static_cast<double>(tEnd.QuadPart - tStart.QuadPart) / freq;
    const double effectiveFps = frameIndex > 0 && totalSec > 0
        ? static_cast<double>(frameIndex) / totalSec
        : 0.0;
    std::wprintf(L"[benchmark] scene=%d frames=%lld duration_s=%.3f fps=%.2f exit=%d\n",
                 static_cast<int>(opts.scene), frameIndex, totalSec, effectiveFps,
                 exitCode);
    return exitCode;
}

} // namespace desktopgrass::benchmark
