#pragma once
#include <keyboardmanager/common/KeyboardManagerState.h>

// Function to create the Main Window
__declspec(dllexport) void createMainWindow(HINSTANCE hInstance, KeyboardManagerState& keyboardManagerState);
LRESULT CALLBACK MainWindowProc(HWND, UINT, WPARAM, LPARAM);