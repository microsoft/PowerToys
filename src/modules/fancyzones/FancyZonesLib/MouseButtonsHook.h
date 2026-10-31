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
    static std::function<bool(bool)> wheelCallback; // true = previous (up/left), false = next (down/right); returns true if handled
    static LRESULT CALLBACK MouseButtonsProc(int, WPARAM, LPARAM);
};
