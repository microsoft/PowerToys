#pragma once

#include <functional>

class MouseButtonsHook
{
public:
    MouseButtonsHook(std::function<void()>, std::function<void()>);
    void enable();
    void disable();

private:
    static HHOOK hHook;
    static std::function<void()> middleClickCallback;
    static std::function<void()> secondaryClickCallback;
    static LRESULT CALLBACK MouseButtonsProc(int, WPARAM, LPARAM);
};
