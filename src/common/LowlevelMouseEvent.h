#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

struct LowlevelMouseEvent
{
    MSLLHOOKSTRUCT* lParam;
    WPARAM wParam;
};