// main.cpp
//
// Entry point: set up DPI awareness, COM, the App, run the message loop.
//
// When the command line contains `--benchmark`, dispatch into the benchmark
// runner instead of the normal App lifecycle. Production tray/persistence
// code remains untouched in that path.

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <combaseapi.h>
#include <shellapi.h>

#include <cwchar>

#include "App.h"
#include "Benchmark.h"

namespace {

bool HasBenchmarkFlag(int argc, wchar_t** argv) {
    for (int i = 1; i < argc; ++i) {
        if (argv[i] && _wcsicmp(argv[i], L"--benchmark") == 0) return true;
    }
    return false;
}

} // anonymous

int APIENTRY wWinMain(HINSTANCE hInst, HINSTANCE, LPWSTR, int) {
    // Per-Monitor V2 DPI awareness. Also declared in the manifest so OSes that
    // honour the manifest pick it up before WinMain runs.
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    if (FAILED(hr)) {
        return -1;
    }

    int argc = 0;
    wchar_t** argv = CommandLineToArgvW(GetCommandLineW(), &argc);

    int exitCode = 0;
    if (argv && HasBenchmarkFlag(argc, argv)) {
        desktopgrass::benchmark::Options opts;
        if (!desktopgrass::benchmark::ParseOptions(argc, argv, opts)) {
            if (argv) LocalFree(argv);
            CoUninitialize();
            return -3;
        }
        exitCode = desktopgrass::benchmark::Run(hInst, opts);
    } else {
        desktopgrass::App app;
        if (!app.Initialize(hInst)) {
            if (argv) LocalFree(argv);
            CoUninitialize();
            return -2;
        }
        exitCode = app.Run();
    }

    if (argv) LocalFree(argv);
    CoUninitialize();
    return exitCode;
}
