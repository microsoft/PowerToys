#include "pch.h"
#include "RawInputKeyboardTracker.h"

#include <vector>

#include <common/logger/logger.h>

namespace
{
    const wchar_t* const RawInputWindowClassName = L"KbmPerKeyboardRawInputSink";

    std::wstring ResolveDevicePath(HANDLE hDevice)
    {
        if (hDevice == nullptr)
        {
            return {};
        }

        UINT size = 0;
        if (GetRawInputDeviceInfoW(hDevice, RIDI_DEVICENAME, nullptr, &size) != 0 || size == 0)
        {
            return {};
        }

        std::wstring path(size, L'\0');
        UINT written = GetRawInputDeviceInfoW(hDevice, RIDI_DEVICENAME, path.data(), &size);
        if (written == static_cast<UINT>(-1))
        {
            return {};
        }

        // RIDI_DEVICENAME returns the count including the null terminator; trim to actual length.
        path.resize(wcsnlen(path.c_str(), path.size()));
        return path;
    }
}

RawInputKeyboardTracker::RawInputKeyboardTracker(Callback callback) :
    m_callback(std::move(callback))
{
}

RawInputKeyboardTracker::~RawInputKeyboardTracker()
{
    Stop();
}

void RawInputKeyboardTracker::Start()
{
    bool expected = false;
    if (!m_started.compare_exchange_strong(expected, true))
    {
        return;
    }

    m_thread = std::thread([this] { ThreadMain(); });
}

void RawInputKeyboardTracker::Stop()
{
    if (!m_started.exchange(false))
    {
        return;
    }

    const DWORD threadId = m_threadId.load();
    if (threadId != 0)
    {
        PostThreadMessageW(threadId, WM_QUIT, 0, 0);
    }

    if (m_thread.joinable())
    {
        m_thread.join();
    }

    m_threadId.store(0);
}

LRESULT CALLBACK RawInputKeyboardTracker::WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    if (msg == WM_NCCREATE)
    {
        auto* create = reinterpret_cast<CREATESTRUCTW*>(lParam);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(create->lpCreateParams));
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    if (msg == WM_INPUT)
    {
        auto* self = reinterpret_cast<RawInputKeyboardTracker*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
        if (self != nullptr)
        {
            self->HandleRawInput(reinterpret_cast<HRAWINPUT>(lParam));
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

void RawInputKeyboardTracker::HandleRawInput(HRAWINPUT hRawInput)
{
    UINT size = 0;
    if (GetRawInputData(hRawInput, RID_INPUT, nullptr, &size, sizeof(RAWINPUTHEADER)) != 0 || size == 0)
    {
        return;
    }

    std::vector<BYTE> buffer(size);
    if (GetRawInputData(hRawInput, RID_INPUT, buffer.data(), &size, sizeof(RAWINPUTHEADER)) != size)
    {
        return;
    }

    const auto* raw = reinterpret_cast<const RAWINPUT*>(buffer.data());
    if (raw->header.dwType != RIM_TYPEKEYBOARD)
    {
        return;
    }

    const RAWKEYBOARD& kb = raw->data.keyboard;
    if (kb.VKey == 0xFF)
    {
        // Part of an escaped sequence / overrun; not a real key.
        return;
    }

    KeyEvent ev;
    ev.vkey = kb.VKey;
    ev.keyDown = (kb.Message == WM_KEYDOWN || kb.Message == WM_SYSKEYDOWN);
    ev.injected = (raw->header.hDevice == nullptr);
    ev.devicePath = ResolveDevicePath(raw->header.hDevice);

    if (m_callback)
    {
        m_callback(ev);
    }
}

void RawInputKeyboardTracker::ThreadMain()
{
    m_threadId.store(GetCurrentThreadId());

    WNDCLASSEXW wc = {};
    wc.cbSize = sizeof(wc);
    wc.lpfnWndProc = &RawInputKeyboardTracker::WndProc;
    wc.hInstance = GetModuleHandleW(nullptr);
    wc.lpszClassName = RawInputWindowClassName;
    RegisterClassExW(&wc);

    // Hidden top-level window (never shown). RIDEV_INPUTSINK is most reliable with a real window.
    HWND hwnd = CreateWindowExW(0, RawInputWindowClassName, L"", 0, 0, 0, 0, 0, nullptr, nullptr, wc.hInstance, this);
    if (hwnd == nullptr)
    {
        Logger::error(L"RawInputKeyboardTracker: CreateWindow failed ({})", GetLastError());
        UnregisterClassW(RawInputWindowClassName, wc.hInstance);
        return;
    }

    RAWINPUTDEVICE rid = {};
    rid.usUsagePage = 0x01; // Generic Desktop
    rid.usUsage = 0x06; // Keyboard
    rid.dwFlags = RIDEV_INPUTSINK; // receive input even when not in the foreground; never suppresses
    rid.hwndTarget = hwnd;
    if (!RegisterRawInputDevices(&rid, 1, sizeof(rid)))
    {
        Logger::error(L"RawInputKeyboardTracker: RegisterRawInputDevices failed ({})", GetLastError());
        DestroyWindow(hwnd);
        UnregisterClassW(RawInputWindowClassName, wc.hInstance);
        return;
    }

    Logger::trace(L"RawInputKeyboardTracker: listening for raw keyboard input");

    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    DestroyWindow(hwnd);
    UnregisterClassW(RawInputWindowClassName, wc.hInstance);
    Logger::trace(L"RawInputKeyboardTracker: stopped");
}
