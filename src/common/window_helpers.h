#pragma once
#include "common.h"

HWND CreateMsgWindow(_In_ HINSTANCE hInst, _In_ WNDPROC pfnWndProc, _In_ void* p);

// If HWND is already dead, we assume it wasn't elevated
bool IsProcessOfWindowElevated(HWND window);