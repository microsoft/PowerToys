#pragma once

#include <functional>

class ShiftKeyHook
{
public:
    ShiftKeyHook(std::function<void()>, std::function<void()>);
    void enable();
    void disable();

private:
    static HHOOK hHook;
    static std::function<void()> callbackKeyDown;
    static std::function<void()> callbackKeyUp;
    static LRESULT CALLBACK ShiftKeyHookProc(int, WPARAM, LPARAM);
};
