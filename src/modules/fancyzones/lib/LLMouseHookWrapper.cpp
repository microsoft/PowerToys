#include "pch.h"
#include "LLMouseHookWrapper.h"

#pragma region public

LLMouseHookWrapper::LLMouseHookWrapper(WPARAM wParam, std::function<void()> callback)
{
    hookNumber = hookList.size();
    hookList.push_back(
        { false,
          wParam,
          callback });
}
void LLMouseHookWrapper::enable()
{
    bool isEnabled = false;
    for (LLMouseEvent hook: hookList)
    {
        isEnabled |= hook.state;
    }
    if (!isEnabled)
    {
        hookProc = SetWindowsHookEx(WH_MOUSE_LL, llMouseHookCallback, GetModuleHandle(NULL), 0);
    }

    hookList[hookNumber].state = true;
}

void LLMouseHookWrapper::disable()
{
    hookList[hookNumber].state = false;

    bool isEnabled = false;
    for (LLMouseEvent hook : hookList)
    {
        isEnabled |= hook.state;
    }
    if (!isEnabled)
    {
        UnhookWindowsHookEx(hookProc);
    }
}

#pragma endregion

#pragma region private

HHOOK LLMouseHookWrapper::hookProc= {};
std::vector<LLMouseEvent> LLMouseHookWrapper::hookList = {};

LRESULT CALLBACK LLMouseHookWrapper::llMouseHookCallback(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode == HC_ACTION)
    {
        for (LLMouseEvent hook : hookList)
        {
            if (hook.state && hook.wParam == wParam)
            {
                hook.callback();
            }
        }
    }
    return CallNextHookEx(hookProc, nCode, wParam, lParam);
}

#pragma endregion
