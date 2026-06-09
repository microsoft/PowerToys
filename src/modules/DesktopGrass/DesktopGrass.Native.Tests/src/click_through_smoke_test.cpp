#include "../third_party/catch2/catch.hpp"

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <atomic>
#include <chrono>
#include <string>
#include <thread>

namespace {

std::atomic<bool> g_probeReceivedLeftDown{false};

enum class ClickThroughResult {
    Passed,
    Skipped,
    Failed,
};

std::wstring unique_class_name(const wchar_t* suffix) {
    return std::wstring(L"DesktopGrass.Native.ClickThrough.")
        + std::to_wstring(GetCurrentProcessId()) + L"."
        + std::to_wstring(GetTickCount64()) + L"."
        + suffix;
}

LRESULT CALLBACK ProbeWndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp) {
    if (msg == WM_LBUTTONDOWN) {
        g_probeReceivedLeftDown.store(true, std::memory_order_release);
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

LRESULT CALLBACK OverlayWndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp) {
    return DefWindowProcW(hwnd, msg, wp, lp);
}

void pump_messages_for(std::chrono::milliseconds duration) {
    const auto deadline = std::chrono::steady_clock::now() + duration;
    MSG msg{};
    do {
        while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE)) {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        if (g_probeReceivedLeftDown.load(std::memory_order_acquire)) {
            return;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(5));
    } while (std::chrono::steady_clock::now() < deadline);
}

bool has_interactive_desktop() {
    if (GetConsoleWindow() == nullptr) {
        return false;
    }

    HDESK inputDesktop = OpenInputDesktop(0, FALSE, DESKTOP_SWITCHDESKTOP);
    if (inputDesktop == nullptr) {
        return false;
    }
    CloseDesktop(inputDesktop);
    return true;
}

ClickThroughResult spawn_probe_window_and_click_through_overlay() {
    if (!has_interactive_desktop()) {
        return ClickThroughResult::Skipped;
    }

    g_probeReceivedLeftDown.store(false, std::memory_order_release);

    const HINSTANCE instance = GetModuleHandleW(nullptr);
    const std::wstring probeClass = unique_class_name(L"Probe");
    const std::wstring overlayClass = unique_class_name(L"Overlay");

    WNDCLASSEXW probeWc{};
    probeWc.cbSize = sizeof(probeWc);
    probeWc.lpfnWndProc = ProbeWndProc;
    probeWc.hInstance = instance;
    probeWc.lpszClassName = probeClass.c_str();

    WNDCLASSEXW overlayWc{};
    overlayWc.cbSize = sizeof(overlayWc);
    overlayWc.lpfnWndProc = OverlayWndProc;
    overlayWc.hInstance = instance;
    overlayWc.lpszClassName = overlayClass.c_str();

    if (!RegisterClassExW(&probeWc)) {
        return ClickThroughResult::Failed;
    }
    if (!RegisterClassExW(&overlayWc)) {
        UnregisterClassW(probeClass.c_str(), instance);
        return ClickThroughResult::Failed;
    }

    const int x = GetSystemMetrics(SM_XVIRTUALSCREEN) + 96;
    const int y = GetSystemMetrics(SM_YVIRTUALSCREEN) + 96;
    constexpr int kWidth = 96;
    constexpr int kHeight = 64;
    const int clickX = x + 24;
    const int clickY = y + 24;

    HWND probe = CreateWindowExW(
        WS_EX_TOOLWINDOW | WS_EX_TOPMOST,
        probeClass.c_str(), L"DesktopGrass click-through probe",
        WS_POPUP | WS_VISIBLE,
        x, y, kWidth, kHeight,
        nullptr, nullptr, instance, nullptr);

    HWND overlay = nullptr;
    bool ok = probe != nullptr;
    if (ok) {
        SetWindowPos(probe, HWND_TOPMOST, x, y, kWidth, kHeight, SWP_SHOWWINDOW);

        overlay = CreateWindowExW(
            WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST |
                WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
            overlayClass.c_str(), L"DesktopGrass click-through overlay",
            WS_POPUP,
            x, y, kWidth, kHeight,
            nullptr, nullptr, instance, nullptr);
        ok = overlay != nullptr;
    }

    if (ok) {
        SetLayeredWindowAttributes(overlay, 0, 1, LWA_ALPHA);
        ShowWindow(overlay, SW_SHOWNOACTIVATE);
        SetWindowPos(overlay, HWND_TOPMOST, x, y, kWidth, kHeight,
                     SWP_SHOWWINDOW | SWP_NOACTIVATE);
        pump_messages_for(std::chrono::milliseconds(50));

        if (!SetCursorPos(clickX, clickY)) {
            ok = false;
        }
    }

    ClickThroughResult result = ClickThroughResult::Failed;
    if (ok) {
        INPUT inputs[2]{};
        inputs[0].type = INPUT_MOUSE;
        inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
        inputs[1].type = INPUT_MOUSE;
        inputs[1].mi.dwFlags = MOUSEEVENTF_LEFTUP;

        const UINT sent = SendInput(2, inputs, sizeof(INPUT));
        if (sent != 2) {
            result = ClickThroughResult::Skipped;
        } else {
            pump_messages_for(std::chrono::milliseconds(200));
            result = g_probeReceivedLeftDown.load(std::memory_order_acquire)
                ? ClickThroughResult::Passed
                : ClickThroughResult::Failed;
        }
    }

    if (overlay) DestroyWindow(overlay);
    if (probe) DestroyWindow(probe);
    UnregisterClassW(overlayClass.c_str(), instance);
    UnregisterClassW(probeClass.c_str(), instance);
    return result;
}

} // namespace

TEST_CASE("Overlay click-through allows input to reach windows beneath", "[smoke][input]") {
    const ClickThroughResult result = spawn_probe_window_and_click_through_overlay();
    if (result == ClickThroughResult::Skipped) {
        WARN("Skipping click-through smoke test: requires an interactive desktop and SendInput.");
        SUCCEED("Requires interactive session");
        return;
    }

    REQUIRE(result == ClickThroughResult::Passed);
}
