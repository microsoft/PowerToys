#pragma once

#include <windows.h>
#include <commctrl.h>
#include <tchar.h>
#include <magnification.h>
#include <shellapi.h>

#define GDIPVER 0x0110
#include <gdiplus.h>
// DirectX
#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <d2d1_3.h>

// Must come before C++/WinRT
#include <wil/cppwinrt.h>

#include <unknwn.h>

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
