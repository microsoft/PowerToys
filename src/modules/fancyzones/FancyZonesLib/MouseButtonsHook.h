#pragma once

#include <functional>

class MouseButtonsHook
{
public:
    MouseButtonsHook(std::function<void()>, std::function<void()>, std::function<bool(bool)>);
    void enable();
    void disable();

private:
    static HHOOK hHook;
    static std::function<void()> middleClickCallback;
    static std::function<void()> secondaryClickCallback;
    static std::function<bool(bool)> wheelCallback; // gets wheel direction (true = up), returns true to swallow the event
    static LRESULT CALLBACK MouseButtonsProc(int, WPARAM, LPARAM);
};
