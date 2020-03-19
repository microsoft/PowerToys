#pragma once
#include <windows.h>
#include <stdlib.h>
#include <string.h>
#include <thread>

#include <winrt/Windows.system.h>
#include <winrt/windows.ui.xaml.hosting.h>
#include <windows.ui.xaml.hosting.desktopwindowxamlsource.h>
#include <winrt/windows.ui.xaml.controls.h>
#include <winrt/Windows.ui.xaml.media.h>
#include <winrt/Windows.Foundation.Collections.h>
#include "winrt/Windows.Foundation.h"
#include "winrt/Windows.Foundation.Numerics.h"
#include "winrt/Windows.UI.Xaml.Controls.Primitives.h"

#include <keyboardmanager/common/KeyboardManagerState.h>

__declspec(dllexport) void createMainWindow(HINSTANCE hInstance, KeyboardManagerState& keyboardManagerState);
LRESULT CALLBACK MainWindowProc(HWND, UINT, WPARAM, LPARAM);