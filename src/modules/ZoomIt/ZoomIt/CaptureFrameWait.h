//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Video capture code derived from https://github.com/robmikh/capturevideosample
//
//==============================================================================
#pragma once

// Must come before C++/WinRT
#include <wil/cppwinrt.h>

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
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3d11.h>
#include <winrt/Windows.Media.h>
#include <winrt/Windows.Media.Audio.h>
#include <winrt/Windows.Media.Core.h>
#include <winrt/Windows.Media.Render.h>
#include <winrt/Windows.Media.Transcoding.h>
#include <winrt/Windows.Media.MediaProperties.h>
#include <winrt/Windows.Storage.h>
#include <winrt/Windows.Storage.Streams.h>
#include <winrt/Windows.Storage.Pickers.h>

// WIL
#include <wil/resource.h>

// DirectX
#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <d2d1_3.h>
#include <wincodec.h>

// STL
#include <vector>
#include <string>
#include <atomic>
#include <memory>
#include <algorithm>
#include <filesystem>
#include <array>
#include <thread>
#include <functional>
#include <optional>
#include <chrono>
#include <mutex>
#include <deque>

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

namespace winrt
{
    using namespace Windows::Foundation;
    using namespace Windows::Foundation::Metadata;
    using namespace Windows::Graphics;
    using namespace Windows::Graphics::Capture;
    using namespace Windows::Graphics::DirectX;
    using namespace Windows::Graphics::DirectX::Direct3D11;
    using namespace Windows::Storage;
    using namespace Windows::UI::Composition;
    using namespace Windows::Media::Core;
    using namespace Windows::Media::Transcoding;
    using namespace Windows::Media::MediaProperties;
}

namespace util
{
    using namespace robmikh::common::uwp;
}

struct CaptureFrame
{
    winrt::Direct3D11::IDirect3DSurface FrameTexture;
    winrt::SizeInt32 ContentSize;
    winrt::TimeSpan SystemRelativeTime;
};

class CaptureFrameWait
{
public:
    CaptureFrameWait(
        winrt::Direct3D11::IDirect3DDevice const& device,
        winrt::GraphicsCaptureItem const& item,
        winrt::SizeInt32 const& size );
    ~CaptureFrameWait();

    std::optional<CaptureFrame> TryGetNextFrame();
    void StopCapture();
    void EnableCursorCapture( bool enable = true )
    {
        if( winrt::ApiInformation::IsPropertyPresent( winrt::name_of<decltype(m_session)>(), L"IsCursorCaptureEnabled" ) )
        {
            m_session.IsCursorCaptureEnabled( enable );
        }
    }
    void ShowCaptureBorder( bool show = true )
    {
        if( winrt::ApiInformation::IsPropertyPresent( winrt::name_of<decltype(m_session)>(), L"IsBorderRequired" ) )
        {
            m_session.IsBorderRequired( show );
        }
    }

private:
    void OnFrameArrived(
        winrt::Direct3D11CaptureFramePool const& sender,
        winrt::IInspectable const& args );

private:
    winrt::Direct3D11::IDirect3DDevice m_device{ nullptr };
    winrt::GraphicsCaptureItem m_item{ nullptr };
    winrt::Direct3D11CaptureFramePool m_framePool{ nullptr };
    winrt::GraphicsCaptureSession m_session{ nullptr };
    wil::shared_event m_nextFrameEvent;
    wil::shared_event m_endEvent;
    wil::shared_event m_closedEvent;
    wil::srwlock m_lock;

    winrt::Direct3D11CaptureFrame m_currentFrame{ nullptr };
};