#include "pch.h"
#include <atomic>
#include <common/logger/logger.h>
#include <common/interop/shared_constants.h>
#include <common/utils/elevation.h>
#include <exdisp.h>
#include <synchapi.h>

// If we should always try to run Peek non-elevated.
bool m_alwaysRunNotElevated = true;
bool m_enableSpaceToActivate = false; // toggle from settings

HANDLE m_hProcess = 0;
DWORD m_processPid = 0;

HANDLE CreateDefaultEvent(const wchar_t* eventName)
{
    SECURITY_ATTRIBUTES sa;
    sa.nLength = sizeof(sa);
    sa.bInheritHandle = false;
    sa.lpSecurityDescriptor = NULL;
    return CreateEventW(&sa, FALSE, FALSE, eventName);
}

HANDLE m_hInvokeEvent = CreateDefaultEvent(CommonSharedConstants::SHOW_PEEK_SHARED_EVENT);

std::atomic_bool g_foregroundHookActive{ false };
std::atomic_bool g_foregroundEligible{ false };
std::atomic<DWORD> g_foregroundLastScheduleTick{ 0 };
HANDLE g_foregroundDebounceTimer = nullptr;
constexpr DWORD FOREGROUND_DEBOUNCE_MS = 40;

bool is_peek_or_explorer_or_desktop_window_focused();

void recompute_space_mode_eligibility()
{
    if (!m_enableSpaceToActivate)
    {
        g_foregroundEligible.store(false, std::memory_order_relaxed);
        return;
    }
    const bool eligible = is_peek_or_explorer_or_desktop_window_focused();
    g_foregroundEligible.store(eligible, std::memory_order_relaxed);
    Logger::debug(L"Peek space-mode eligibility recomputed: {}", eligible);
}

static void CALLBACK ForegroundDebounceTimerProc(PVOID /*param*/, BOOLEAN /*fired*/)
{
    if (!g_foregroundHookActive.load(std::memory_order_relaxed))
    {
        return;
    }
    recompute_space_mode_eligibility();
}

static void CALLBACK ForegroundWinEventProc(HWINEVENTHOOK /*hook*/, DWORD /*event*/, HWND /*hwnd*/, LONG /*idObject*/, LONG /*idChild*/, DWORD /*thread*/, DWORD /*time*/)
{
    if (!g_foregroundHookActive.load(std::memory_order_relaxed))
    {
        return;
    }
    const DWORD now = GetTickCount();
    const DWORD last = g_foregroundLastScheduleTick.load(std::memory_order_relaxed);
    // If no timer or sufficient time since last schedule, create a new one.
    if (!g_foregroundDebounceTimer || (now - last) >= FOREGROUND_DEBOUNCE_MS || now < last)
    {
        if (g_foregroundDebounceTimer)
        {
            // Best effort: cancel previous pending timer; ignore failure.
            DeleteTimerQueueTimer(nullptr, g_foregroundDebounceTimer, INVALID_HANDLE_VALUE);
            g_foregroundDebounceTimer = nullptr;
        }
        if (CreateTimerQueueTimer(&g_foregroundDebounceTimer, nullptr, ForegroundDebounceTimerProc, nullptr, FOREGROUND_DEBOUNCE_MS, 0, WT_EXECUTEDEFAULT))
        {
            g_foregroundLastScheduleTick.store(now, std::memory_order_relaxed);
        }
        else
        {
            Logger::warn(L"Peek failed to create foreground debounce timer");
            // Fallback: compute immediately if timer creation failed.
            recompute_space_mode_eligibility();
        }
    }
}


HWINEVENTHOOK g_foregroundHook = nullptr;

void install_foreground_hook()
{
    if (g_foregroundHook || !m_enableSpaceToActivate)
    {
        return;
    }

    g_foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, nullptr, ForegroundWinEventProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    if (g_foregroundHook)
    {
        g_foregroundHookActive.store(true, std::memory_order_relaxed);
        recompute_space_mode_eligibility();
    }
    else
    {
        g_foregroundHookActive.store(false, std::memory_order_relaxed);
        Logger::warn(L"Peek failed to install foreground hook. Falling back to polling.");
    }
}

void uninstall_foreground_hook()
{
    if (g_foregroundHook)
    {
        UnhookWinEvent(g_foregroundHook);
        g_foregroundHook = nullptr;
    }
    if (g_foregroundDebounceTimer)
    {
        DeleteTimerQueueTimer(nullptr, g_foregroundDebounceTimer, INVALID_HANDLE_VALUE);
        g_foregroundDebounceTimer = nullptr;
    }
    g_foregroundLastScheduleTick.store(0, std::memory_order_relaxed);
    g_foregroundHookActive.store(false, std::memory_order_relaxed);
    g_foregroundEligible.store(false, std::memory_order_relaxed);
}

EXTERN_C __declspec(dllexport) void PeekManageSpaceModeHook()
{
    if (m_enableSpaceToActivate)
    {
        install_foreground_hook();
    }
    else
    {
        uninstall_foreground_hook();
    }
}

bool is_desktop_window(HWND windowHandle)
{
    // Similar to the logic in IsDesktopWindow in Peek UI. Keep logic synced.
    // TODO: Refactor into same C++ class consumed by both.
    wchar_t className[MAX_PATH];
    if (GetClassName(windowHandle, className, MAX_PATH) == 0)
    {
        return false;
    }
    if (wcsncmp(className, L"Progman", MAX_PATH) != 0 && wcsncmp(className, L"WorkerW", MAX_PATH) != 0)
    {
        return false;
    }
    return FindWindowEx(windowHandle, NULL, L"SHELLDLL_DefView", NULL);
}

bool is_explorer_window(HWND windowHandle)
{
    CComPtr<IShellWindows> spShellWindows;
    auto result = spShellWindows.CoCreateInstance(CLSID_ShellWindows);
    if (result != S_OK || spShellWindows == nullptr)
    {
        Logger::warn(L"Failed to create instance. {}", GetErrorString(result));
        return true; // Might as well assume it's possible it's an explorer window.
    }

    // Enumerate all Shell Windows to compare the window handle against.
    IUnknownPtr spEnum{}; // _com_ptr_t; no Release required.
    result = spShellWindows->_NewEnum(&spEnum);
    if (result != S_OK || spEnum == nullptr)
    {
        Logger::warn(L"Failed to list explorer Windows. {}", GetErrorString(result));
        return true; // Might as well assume it's possible it's an explorer window.
    }

    IEnumVARIANTPtr spEnumVariant{}; // _com_ptr_t; no Release required.
    result = spEnum.QueryInterface(__uuidof(spEnumVariant), &spEnumVariant);
    if (result != S_OK || spEnumVariant == nullptr)
    {
        Logger::warn(L"Failed to enum explorer Windows. {}", GetErrorString(result));
        spEnum->Release();
        return true; // Might as well assume it's possible it's an explorer window.
    }

    variant_t variantElement{};
    while (spEnumVariant->Next(1, &variantElement, NULL) == S_OK)
    {
        IWebBrowserApp* spWebBrowserApp;
        result = variantElement.pdispVal->QueryInterface(IID_IWebBrowserApp, reinterpret_cast<void**>(&spWebBrowserApp));
        if (result == S_OK)
        {
            HWND hwnd;
            result = spWebBrowserApp->get_HWND(reinterpret_cast<SHANDLE_PTR*>(&hwnd));
            if (result == S_OK)
            {
                if (hwnd == windowHandle)
                {
                    VariantClear(&variantElement);
                    spWebBrowserApp->Release();
                    return true;
                }
            }
            spWebBrowserApp->Release();
        }
        VariantClear(&variantElement);
    }

    return false;
}

bool is_peek_or_explorer_or_desktop_window_focused()
{
    HWND foregroundWindowHandle = GetForegroundWindow();
    if (foregroundWindowHandle == NULL)
    {
        return false;
    }

    DWORD pid{};
    if (GetWindowThreadProcessId(foregroundWindowHandle, &pid) != 0)
    {
        // If the foreground window is the Peek window, send activation signal.
        if (m_processPid != 0 && pid == m_processPid)
        {
            return true;
        }
    }

    if (is_desktop_window(foregroundWindowHandle))
    {
        return true;
    }

    return is_explorer_window(foregroundWindowHandle);
}

bool is_viewer_running()
{
    if (m_hProcess == 0)
    {
        return false;
    }
    return WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
}

EXTERN_C __declspec(dllexport) void PeekSetForegroundHookActive(bool status)
{
    m_enableSpaceToActivate = status;
}

EXTERN_C __declspec(dllexport) bool PeekOnHotkey(size_t /*hotkeyId*/)
{
    bool spaceMode = m_enableSpaceToActivate;
    bool eligible = false;
    if (spaceMode && g_foregroundHookActive.load(std::memory_order_relaxed))
    {
        eligible = g_foregroundEligible.load(std::memory_order_relaxed);
    }
    else
    {
        eligible = is_peek_or_explorer_or_desktop_window_focused();
    }

    if (eligible)
    {
        Logger::trace(L"Peek hotkey pressed and eligible for launching");

        SetEvent(m_hInvokeEvent);

        if (spaceMode)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    return false;
}
