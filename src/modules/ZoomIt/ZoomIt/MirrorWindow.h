//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// DemoMirror: mirrors a screen region or window, including the mouse cursor,
// onto a second monitor so an audience can follow a demo app on the
// presentation monitor without the presenter leaving the presentation.
//
//==============================================================================
#pragma once

#include "pch.h"
#include "CaptureFrameWait.h"

// Posted to the notify window when mirroring must stop because the mirrored
// window closed. Keep distinct from the WM_USER messages in zoomit.h.
#define WM_USER_MIRROR_STOP		WM_USER+112

class MirrorWindow
{
public:
    ~MirrorWindow() { Stop(); }

    bool IsActive() const { return m_window != nullptr; }

    // For window mirroring, sourceWindow is the mirrored window and
    // sourceRect is its window rect; for region mirroring, sourceWindow is
    // null and sourceRect is the mirrored region in screen coordinates.
    bool Start( winrt::GraphicsCaptureItem const& item,
                RECT sourceRect,
                HWND sourceWindow,
                HMONITOR sourceMonitor,
                HMONITOR targetMonitor,
                HWND notifyWindow );
    void Stop();

private:
    static const int ZOOM_IN_HOTKEY_ID = 1;
    static const int ZOOM_OUT_HOTKEY_ID = 2;
    static const UINT_PTR TOPMOST_TIMER_ID = 1;

    void RenderLoop();
    void RenderFrame();
    LRESULT WindowProc( HWND window, UINT message, WPARAM wordParam, LPARAM longParam );

    const wchar_t* m_className = L"ZoomitMirrorWindow";
    HWND	m_window = nullptr;
    HWND	m_notifyWindow = nullptr;
    HWND	m_sourceWindow = nullptr;
    RECT	m_sourceRect{};		// mirrored region in screen coordinates
    RECT	m_textureCrop{};	// mirrored region in capture-texture coordinates
    int		m_bufferWidth = 0;
    int		m_bufferHeight = 0;
    winrt::SizeInt32	m_contentSize{};

    winrt::com_ptr<ID3D11Device>		m_device;
    winrt::com_ptr<ID3D11DeviceContext>	m_context;
    winrt::com_ptr<IDXGISwapChain2>		m_swapChain;
    winrt::com_ptr<ID3D11Texture2D>		m_sourceTexture;
    winrt::Direct3D11::IDirect3DDevice	m_winrtDevice{ nullptr };
    std::unique_ptr<CaptureFrameWait>	m_frameWait;

    std::thread			m_renderThread;
    wil::unique_event	m_stopEvent{ wil::EventOptions::ManualReset };
    std::atomic<float>	m_zoomTarget{ 1.0f };
    float	m_zoom = 1.0f;
    float	m_centerX = 0;
    float	m_centerY = 0;
};
