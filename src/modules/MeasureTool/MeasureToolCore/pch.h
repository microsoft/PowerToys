#pragma once
#define NOMINMAX
#define WIN32_LEAN_AND_MEAN

#include <initguid.h>
#include <windows.h>
#include <unknwn.h>
#include <restrictederrorinfo.h>
#include <hstring.h>
#include <dxgi.h>
#include <d3d11.h>
#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <dxgidebug.h>
#include <d2d1_3.h>
#include <dwrite.h>
#include <dwmapi.h>
#include <windows.graphics.directX.direct3d11.interop.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.capture.h>

#include <thread>
#include <functional>
#include <cassert>
#include <cinttypes>
#include <iomanip>
#include <limits>
#include <sstream>
#include <filesystem>
#include <string_view>
#include <chrono>
#include <stdio.h>
#include <ProjectTelemetry.h>

// Undefine GetCurrentTime macro to prevent
// conflict with Storyboard::GetCurrentTime
#undef GetCurrentTime

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.ApplicationModel.Activation.h>
#include <winrt/Microsoft.UI.Composition.h>
#include <winrt/Microsoft.UI.Xaml.h>
#include <winrt/Microsoft.UI.Xaml.Controls.h>
#include <winrt/Microsoft.UI.Xaml.Controls.Primitives.h>
#include <winrt/Microsoft.UI.Xaml.Data.h>
#include <winrt/Microsoft.UI.Xaml.Interop.h>
#include <winrt/Microsoft.UI.Xaml.Markup.h>
#include <winrt/Microsoft.UI.Xaml.Media.h>
#include <winrt/Microsoft.UI.Xaml.Navigation.h>
#include <winrt/Microsoft.UI.Xaml.Shapes.h>
#include <winrt/Microsoft.UI.Dispatching.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3d11.h>
#include <winrt/Windows.Graphics.Capture.h>

#include <inspectable.h>

#include <wil/cppwinrt_helpers.h>
#include <wil/resource.h>
#include <wil/com.h>

#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>

namespace winrt
{
    using namespace Windows::Foundation;
    using namespace Windows::Foundation::Numerics;
    using namespace Windows::Graphics;
    using namespace Windows::Graphics::Capture;
    using namespace Windows::Graphics::DirectX;
    using namespace Windows::Graphics::DirectX::Direct3D11;
    using namespace Microsoft::UI::Xaml;
    using namespace Microsoft::UI::Xaml::Controls;
    using namespace Microsoft::UI::Xaml::Navigation;
}

template<typename Func>
[[nodiscard]] std::thread SpawnLoggedThread(const wchar_t* description, Func&& f)
{
    return std::thread{ [f = std::move(f), description = std::wstring{ description }] {
        try
        {
            SetThreadDescription(GetCurrentThread(), description.c_str());
            f();
        }
        catch (const std::exception& ex)
        {
            Logger::error(L"{}:", description);
            Logger::error("{}", ex.what());
        }
        catch (winrt::hresult_error const& ex)
        {
            Logger::error(L"{}: {}", description, ex.message().c_str());
        }
        catch (...)
        {
            Logger::error(L"{} unknown error: {}", description, get_last_error_or_default(GetLastError()));
        }
    } };
}

#define WM_CURSOR_LEFT_MONITOR (WM_USER + 1)
