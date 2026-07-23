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

// Bright green, distinguishing the mirror border from the yellow
// record/panorama borders and the orange recording-active border.
#define MIRROR_BORDER_COLOR		RGB( 0, 255, 0 )

class MirrorWindow
{
public:
    // ZoomIt annotation modes (zoom/draw/live zoom) render in overlay
    // windows a window capture can't see; while one is active a window
    // mirror temporarily switches to capturing the monitor.
    enum class AnnotationState { None, Annotating, AnnotatingLiveZoom };

    ~MirrorWindow() { Stop(); }

    bool IsActive() const { return m_window != nullptr; }

    // Visible window bounds, excluding the invisible resize/shadow frame
    // that GetWindowRect includes.
    static RECT GetWindowFrameRect( HWND window );

    // For window mirroring, sourceWindow is the mirrored window and
    // sourceRect is its window rect; for region mirroring, sourceWindow is
    // null and sourceRect is the mirrored region in screen coordinates.
    // With trackWindow, item must be a monitor capture: the mirror shows
    // the region under the tracked window instead of the window's own
    // surface, so ZoomIt annotations show in place, at the cost of
    // occluding windows becoming visible.
    // sourceBorderWindow is the caller-owned green border (the region/
    // monitor SelectRectangle); the topmost timer keeps it above the
    // full-screen static zoom/draw window.
    bool Start( winrt::GraphicsCaptureItem const& item,
                RECT sourceRect,
                HWND sourceWindow,
                HMONITOR sourceMonitor,
                HMONITOR targetMonitor,
                HWND notifyWindow,
                std::function<AnnotationState()> annotationQuery = nullptr,
                bool trackWindow = false,
                HWND sourceBorderWindow = nullptr );
    void Stop();

private:
    static const UINT_PTR TOPMOST_TIMER_ID = 1;
    // Private to the mirror window: re-fit it to the source's aspect ratio.
    static const UINT WM_MIRROR_RELAYOUT = WM_USER + 1;
    // Private to the mirror window: move the border to the source window.
    static const UINT WM_MIRROR_BORDER = WM_USER + 2;

    void RenderLoop();
    void RenderFrame();
    bool UpdateAnnotationState();
    bool SwitchCapture( winrt::GraphicsCaptureItem const& item, bool enableCursor );
    RECT ComputeWindowRect() const;
    void UpdateBorderWindow();
    LRESULT WindowProc( HWND window, UINT message, WPARAM wordParam, LPARAM longParam );

    const wchar_t* m_className = L"ZoomitMirrorWindow";
    HWND	m_window = nullptr;
    HWND	m_backdropWindow = nullptr;
    HWND	m_borderWindow = nullptr;
    HWND	m_notifyWindow = nullptr;
    HWND	m_sourceWindow = nullptr;
    HWND	m_sourceBorderWindow = nullptr;	// caller-owned region/monitor border
    RECT	m_borderTarget{};	// source window rect the border follows
    RECT	m_sourceRect{};		// mirrored region in screen coordinates
    RECT	m_textureCrop{};	// mirrored region in capture-texture coordinates
    RECT	m_targetRect{};		// target monitor rectangle
    RECT	m_sourceMonitorRect{};	// source monitor rectangle (window tracking)
    HMONITOR	m_sourceMonitor = nullptr;
    HMONITOR	m_targetMonitor = nullptr;
    bool	m_trackWindow = false;	// monitor capture cropped to the window rect
    int		m_bufferWidth = 0;	// swapchain dimensions
    int		m_bufferHeight = 0;
    int		m_imageWidth = 0;	// displayed image dimensions, for layout
    int		m_imageHeight = 0;
    winrt::SizeInt32	m_contentSize{};
    winrt::SizeInt32	m_poolSize{};

    winrt::com_ptr<ID3D11Device>		m_device;
    winrt::com_ptr<ID3D11DeviceContext>	m_context;
    winrt::com_ptr<IDXGISwapChain2>		m_swapChain;
    winrt::com_ptr<ID3D11Texture2D>		m_sourceTexture;
    winrt::Direct3D11::IDirect3DDevice	m_winrtDevice{ nullptr };
    std::unique_ptr<CaptureFrameWait>	m_frameWait;

    std::function<AnnotationState()>	m_annotationQuery;
    AnnotationState	m_annotationState = AnnotationState::None;
    bool	m_monitorOverride = false;	// capturing the monitor while annotating

    std::thread			m_renderThread;
    wil::unique_event	m_stopEvent{ wil::EventOptions::ManualReset };
};
