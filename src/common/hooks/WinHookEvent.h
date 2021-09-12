#pragma once

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

struct WinHookEvent
{
    DWORD event;
    HWND hwnd;
    LONG idObject;
    LONG idChild;
    DWORD idEventThread;
    DWORD dwmsEventTime;
};