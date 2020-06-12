#include "pch.h"
#include "ShiftKeyHook.h"

#pragma region public

HHOOK ShiftKeyHook::hHook = {};
std::function<void()> ShiftKeyHook::callbackKeyDown = {};
std::function<void()> ShiftKeyHook::callbackKeyUp = {};

ShiftKeyHook::ShiftKeyHook(std::function<void()> extCallbackKeyDown, std::function<void()> extCallbackKeyUp)
{
    callbackKeyDown = std::move(extCallbackKeyDown);
    callbackKeyUp = std::move(extCallbackKeyUp);
    hHook = SetWindowsHookEx(WH_KEYBOARD_LL, ShiftKeyHookProc, GetModuleHandle(NULL), 0);
}

void ShiftKeyHook::enable()
{
    if (!hHook)
    {
        hHook = SetWindowsHookEx(WH_KEYBOARD_LL, ShiftKeyHookProc, GetModuleHandle(NULL), 0);
    }
}

void ShiftKeyHook::disable()
{
    if (hHook)
    {
        UnhookWindowsHookEx(hHook);
        hHook = NULL;
    }
}

#pragma endregion

#pragma region private

LRESULT CALLBACK ShiftKeyHook::ShiftKeyHookProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode == HC_ACTION)
    {
        if (wParam == WM_KEYDOWN)
        {
            PKBDLLHOOKSTRUCT kbdHookStruct = (PKBDLLHOOKSTRUCT)lParam;
            if (kbdHookStruct->vkCode == VK_LSHIFT)
            {
                callbackKeyDown();
            }
        }
        else if (wParam == WM_KEYUP)
        {
            PKBDLLHOOKSTRUCT kbdHookStruct = (PKBDLLHOOKSTRUCT)lParam;
            if (kbdHookStruct->vkCode == VK_LSHIFT)
            {
                callbackKeyUp();
            }
        }
    }
    return CallNextHookEx(hHook, nCode, wParam, lParam);
}

#pragma endregion
