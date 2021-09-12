#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

struct LowlevelKeyboardEvent
{
    KBDLLHOOKSTRUCT* lParam;
    WPARAM wParam;
};