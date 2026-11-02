#include "pch.h"
#include "ProfileCycleHotkey.h"

#include <common/logger/logger.h>

namespace
{
    const wchar_t* const HotkeyWindowClassName = L"KbmProfileCycleHotkey";
}

ProfileCycleHotkey::ProfileCycleHotkey(Callback callback) :
    m_callback(std::move(callback))
{
    m_readyEvent = CreateEventW(nullptr, TRUE /* manual reset */, FALSE, nullptr);
}

ProfileCycleHotkey::~ProfileCycleHotkey()
{
    Stop();
    if (m_readyEvent != nullptr)
    {
        CloseHandle(m_readyEvent);
        m_readyEvent = nullptr;
    }
}

void ProfileCycleHotkey::Start()
{
    bool expected = false;
    if (!m_started.compare_exchange_strong(expected, true))
    {
        return;
    }

    if (m_readyEvent != nullptr)
    {
        ResetEvent(m_readyEvent);
    }

    m_thread = std::thread([this] { ThreadMain(); });
}

void ProfileCycleHotkey::Stop()
{
    if (!m_started.exchange(false))
    {
        return;
    }

    // Wait until the worker has created its message queue before posting WM_QUIT; otherwise the
    // post can be lost and join() would block forever. Bounded so a worker that died before
    // signalling still lets Stop() proceed.
    if (m_readyEvent != nullptr)
    {
        WaitForSingleObject(m_readyEvent, 5000);
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
    m_hwnd.store(nullptr);
}

void ProfileCycleHotkey::Update(UINT modifiers, UINT vk)
{
    m_pendingModifiers.store(modifiers);
    m_pendingVk.store(vk);

    // If the window exists, apply on its thread; otherwise ThreadMain applies it after creation.
    const HWND hwnd = m_hwnd.load();
    if (hwnd != nullptr)
    {
        PostMessageW(hwnd, ApplyHotkeyMessage, 0, 0);
    }
}

LRESULT CALLBACK ProfileCycleHotkey::WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    if (msg == WM_NCCREATE)
    {
        auto* create = reinterpret_cast<CREATESTRUCTW*>(lParam);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(create->lpCreateParams));
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    auto* self = reinterpret_cast<ProfileCycleHotkey*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    if (self != nullptr)
    {
        if (msg == ApplyHotkeyMessage)
        {
            self->ApplyPendingRegistration(hwnd);
            return 0;
        }

        if (msg == WM_HOTKEY && wParam == HotkeyId && self->m_callback)
        {
            self->m_callback();
            return 0;
        }
    }

    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

void ProfileCycleHotkey::ApplyPendingRegistration(HWND hwnd)
{
    if (m_registered)
    {
        UnregisterHotKey(hwnd, HotkeyId);
        m_registered = false;
    }

    const UINT vk = m_pendingVk.load();
    if (vk == 0)
    {
        return; // hotkey disabled
    }

    // MOD_NOREPEAT: holding the chord fires once, not repeatedly.
    if (RegisterHotKey(hwnd, HotkeyId, m_pendingModifiers.load() | MOD_NOREPEAT, vk))
    {
        m_registered = true;
        Logger::trace(L"ProfileCycleHotkey: registered (modifiers=0x{:x}, vk=0x{:x})", m_pendingModifiers.load(), vk);
    }
    else
    {
        Logger::warn(L"ProfileCycleHotkey: RegisterHotKey failed ({}) — hotkey in use by another app?", GetLastError());
    }
}

void ProfileCycleHotkey::ThreadMain()
{
    m_threadId.store(GetCurrentThreadId());

    WNDCLASSEXW wc = {};
    wc.cbSize = sizeof(wc);
    wc.lpfnWndProc = &ProfileCycleHotkey::WndProc;
    wc.hInstance = GetModuleHandleW(nullptr);
    wc.lpszClassName = HotkeyWindowClassName;
    RegisterClassExW(&wc);

    HWND hwnd = CreateWindowExW(0, HotkeyWindowClassName, L"", 0, 0, 0, 0, 0, nullptr, nullptr, wc.hInstance, this);
    if (hwnd == nullptr)
    {
        Logger::error(L"ProfileCycleHotkey: CreateWindow failed ({})", GetLastError());
        UnregisterClassW(HotkeyWindowClassName, wc.hInstance);
        if (m_readyEvent != nullptr)
        {
            SetEvent(m_readyEvent);
        }
        return;
    }

    m_hwnd.store(hwnd);
    ApplyPendingRegistration(hwnd);

    // The window exists, so this thread now has a message queue and Stop()'s PostThreadMessageW
    // will be delivered. Let Stop() proceed.
    if (m_readyEvent != nullptr)
    {
        SetEvent(m_readyEvent);
    }

    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    if (m_registered)
    {
        UnregisterHotKey(hwnd, HotkeyId);
        m_registered = false;
    }

    m_hwnd.store(nullptr);
    DestroyWindow(hwnd);
    UnregisterClassW(HotkeyWindowClassName, wc.hInstance);
}
