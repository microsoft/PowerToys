#pragma once

#include <vector>
#include <functional>

struct LLMouseEvent
{
    bool
        state;
    WPARAM wParam;
    std::function<void()> callback;
};

class LLMouseHookWrapper
{
public:
    LLMouseHookWrapper(WPARAM, std::function<void()>);
    void enable();
    void disable();

private:
    unsigned int hookNumber;
    static HHOOK hookProc;
    static std::vector<LLMouseEvent> hookList;
    static LRESULT CALLBACK llMouseHookCallback(int, WPARAM, LPARAM);
};