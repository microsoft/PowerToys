#pragma once

#include <functional>

class MiddleMouseButtonsHook
{
public:
    MiddleMouseButtonsHook(std::function<void()>);
    void enable();
    void disable();

private:
    static HHOOK hHook;
    static std::function<void()> callback;
    static LRESULT CALLBACK MiddleMouseButtonsProc(int, WPARAM, LPARAM);
};
