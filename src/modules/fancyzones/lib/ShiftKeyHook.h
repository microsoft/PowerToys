#pragma once

#include <functional>

class ShiftKeyHook
{
public:
    ShiftKeyHook(std::function<void(bool)>);
    void enable();
    void disable();

private:
    static HHOOK hHook;
    static std::function<void(bool)> callback;
    static LRESULT CALLBACK ShiftKeyHookProc(int, WPARAM, LPARAM);
};
