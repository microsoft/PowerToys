#pragma once

#include <windows.h>
#include <windowsx.h>
#include <commctrl.h>
#include <WinUser.h>
#include <shlobj.h>
#include <tchar.h>
#include <wincodec.h>
#include <magnification.h>
#include <Uxtheme.h>
#include <math.h>
#include <shellapi.h>
#include <shlwapi.h>
#include <stdio.h>
#include "Eula/eula.h"
#include "registry.h"
#include "resource.h"
#include "dll.h"
#define GDIPVER 0x0110
#include <gdiplus.h>

// Must come before C++/WinRT
#include <wil/cppwinrt.h>

#include <wincodec.h>

// WinRT
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Foundation.Metadata.h>
#include <winrt/Windows.Foundation.Numerics.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Composition.Desktop.h>
#include <winrt/Windows.UI.Popups.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.Imaging.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3d11.h>
#include <winrt/Windows.Media.h>
#include <winrt/Windows.Media.Core.h>
#include <winrt/Windows.Media.Transcoding.h>
#include <winrt/Windows.Media.MediaProperties.h>
#include <winrt/Windows.Media.Devices.h>
#include <winrt/Windows.Storage.h>
#include <winrt/Windows.Storage.Streams.h>
#include <winrt/Windows.Storage.Pickers.h>
#include <winrt/Windows.Devices.Enumeration.h>

#include <filesystem>

// Direct3D wrappers to avoid implicitly linking to d3d11.dll; must come before declaration
#define CreateDirect3D11DeviceFromDXGIDevice WrapCreateDirect3D11DeviceFromDXGIDevice
#define CreateDirect3D11SurfaceFromDXGISurface WrapCreateDirect3D11SurfaceFromDXGISurface
#define D3D11CreateDevice WrapD3D11CreateDevice

#include "VideoRecordingSession.h"
#include "SelectRectangle.h"
#include "DemoType.h"
#include "versionhelper.h"

// WIL
#include <wil/com.h>
#include <wil/resource.h>

// DirectX
#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <d2d1_3.h>


// STL
#include <vector>
#include <string>
#include <list>
#include <atomic>
#include <memory>
#include <algorithm>
#include <filesystem>
#include <future>
#include <regex>
#include <fstream>
#include <sstream>

// robmikh.common
#include <robmikh.common/composition.interop.h>
#include <robmikh.common/direct3d11.interop.h>
#include <robmikh.common/d3d11Helpers.h>
#include <robmikh.common/graphics.interop.h>
#include <robmikh.common/dispatcherQueue.desktop.interop.h>
#include <robmikh.common/d3d11Helpers.desktop.h>
#include <robmikh.common/composition.desktop.interop.h>
#include <robmikh.common/hwnd.interop.h>
#include <robmikh.common/capture.desktop.interop.h>
#include <robmikh.common/DesktopWindow.h>
