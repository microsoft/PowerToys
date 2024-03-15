#pragma once

// Collision from minWinDef min/max and std
#define NOMINMAX
#define WIN32_LEAN_AND_MEAN

// Windows
#include <windows.h>
#include <windowsx.h>

// Must come before C++/WinRT
#include <wil/cppwinrt.h>

// WinRT
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Foundation.Numerics.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Composition.Desktop.h>
#include <winrt/Windows.UI.Popups.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3d11.h>

// WIL
#include <wil/resource.h>
#include <wil/cppwinrt_helpers.h>

// DirectX
#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <d2d1_3.h>
#include <wincodec.h>

// DWM
#include <dwmapi.h>

// Shell
#include <shellscalingapi.h>

// STL
#include <vector>
#include <string>
#include <atomic>
#include <memory>
#include <algorithm>
#include <mutex>
#include <sstream>

// robmikh.common
#include <robmikh.common/composition.interop.h>
#include <robmikh.common/direct3d11.interop.h>
#include <robmikh.common/d3dHelpers.h>
#include <robmikh.common/graphics.interop.h>
// robmikh.common needs to be updated to support newer versions of C++/WinRT https://github.com/robmikh/robmikh.common/issues/2
// Applying workaround from https://github.com/robmikh/Win32CaptureSample/commit/fc758e343ca886795b05af5003d9a3bb85ff4da2
// #include <robmikh.common/dispatcherQueue.desktop.interop.h>
#include "DispatcherQueue.desktop.interop.h"
#include <robmikh.common/d3dHelpers.desktop.h>
#include <robmikh.common/composition.desktop.interop.h>
#include <robmikh.common/hwnd.interop.h>
#include <robmikh.common/capture.desktop.interop.h>
#include <robmikh.common/DesktopWindow.h>
#include <robmikh.common/DisplayInfo.h>
#include <robmikh.common/shellHelpers.desktop.h>
#include <robmikh.common/ControlsHelper.h>
#include <robmikh.common/WindowInfo.h>

// Helpers
#include "DisplaysUtil.h"
#include "ThumbnailUtil.h"
#include "WindowRectUtil.h"

// PowerToys
#include <ProjectTelemetry.h>
#include <common/logger/logger.h>

// Application resources
#include "resource.h"
#define MAIN_ICON MAKEINTRESOURCEW(IDI_ICON1)