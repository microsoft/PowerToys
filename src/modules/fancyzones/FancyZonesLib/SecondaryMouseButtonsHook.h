#pragma once

#include <functional>

class SecondaryMouseButtonsHook
{
public:
    SecondaryMouseButtonsHook(std::function<void()>);
    void enable();
    void disable();

private:
    static HHOOK hHook;
    static std::function<void()> callback;
    static LRESULT CALLBACK SecondaryMouseButtonsProc(int, WPARAM, LPARAM);
};
