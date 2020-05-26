#include "pch.h"
#include "SecondaryMouseButtonsHook.h"

#pragma region public

HHOOK SecondaryMouseButtonsHook::m_hHook = {};
std::function<void()> SecondaryMouseButtonsHook::callback = {};

SecondaryMouseButtonsHook::SecondaryMouseButtonsHook(std::function<void()> extCallback)
{
    callback = std::move(extCallback);
    m_hHook = SetWindowsHookEx(WH_MOUSE_LL, SecondaryMouseButtonsProc, GetModuleHandle(NULL), 0);
}

void SecondaryMouseButtonsHook::enable()
{
    if (!m_hHook)
    {
        m_hHook = SetWindowsHookEx(WH_MOUSE_LL, SecondaryMouseButtonsProc, GetModuleHandle(NULL), 0);
    }
}

void SecondaryMouseButtonsHook::disable()
{
    if (m_hHook)
    {
        UnhookWindowsHookEx(m_hHook);
        m_hHook = NULL;
    }
}

#pragma endregion

#pragma region private

LRESULT CALLBACK SecondaryMouseButtonsHook::SecondaryMouseButtonsProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode == HC_ACTION)
    {
        if (wParam == (GetSystemMetrics(SM_SWAPBUTTON) ? WM_LBUTTONDOWN : WM_RBUTTONDOWN) || wParam == WM_MBUTTONDOWN || wParam == WM_XBUTTONDOWN)
        {
            callback();
        }
    }
    return CallNextHookEx(m_hHook, nCode, wParam, lParam);
}

#pragma endregion
