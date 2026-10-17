#include "pch.h"
#include "PresenterWindow.h"
#include "Generated Files/resource.h"
#include <common/utils/resources.h>

#include <dwmapi.h>

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Metadata.h>
#include <winrt/Windows.Graphics.DirectX.h>

#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>

namespace
{
    namespace capture = winrt::Windows::Graphics::Capture;
    namespace directx = winrt::Windows::Graphics::DirectX;

    // Parked past the right-hand edge of the whole virtual desktop, far enough that no
    // part of it lands on any monitor.
    constexpr int OFFSCREEN_MARGIN = 200;

    // How often an unpainted target is asked to redraw while waiting for a first frame.
    constexpr uint64_t NUDGE_INTERVAL_MS = 200;

    // How long to wait for a real captured frame before falling back to PrintWindow.
    constexpr uint64_t SEED_AFTER_MS = 1000;

    // Two buffers is enough: frames are consumed on the render tick, and a deeper pool
    // only adds latency between the target updating and the mirror showing it.
    constexpr int CAPTURE_BUFFERS = 2;

    bool IsCloaked(HWND window) noexcept
    {
        int cloaked = 0;
        if (FAILED(DwmGetWindowAttribute(window, DWMWA_CLOAKED, &cloaked, sizeof(cloaked))))
        {
            return false;
        }
        return cloaked != 0;
    }

    // GetWindowRect includes the invisible resize border Windows keeps outside the
    // visible frame, so it is a few pixels larger than what is actually drawn - and it
    // is not what window capture uses either. DWM knows the real bounds.
    RECT VisibleBounds(HWND window)
    {
        RECT bounds{};
        if (SUCCEEDED(DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, &bounds, sizeof(bounds))) &&
            bounds.right > bounds.left && bounds.bottom > bounds.top)
        {
            return bounds;
        }

        GetWindowRect(window, &bounds);
        return bounds;
    }

    // Root of the PowerToys install. The module sits in that root, but plenty of
    // PowerToys runs from subfolders - the Quick Access flyout and Settings live in
    // WinUI3Apps - so the root has to be walked back to, and candidates matched against
    // it by prefix rather than exact equality.
    const std::wstring& OwnDirectory()
    {
        static const std::wstring directory = [] {
            wchar_t path[MAX_PATH]{};
            HMODULE self = nullptr;
            GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                               reinterpret_cast<LPCWSTR>(&IsCloaked),
                               &self);
            const DWORD length = GetModuleFileNameW(self, path, ARRAYSIZE(path));
            std::wstring full(path, length);
            const size_t slash = full.find_last_of(L'\\');
            if (slash == std::wstring::npos)
            {
                return std::wstring{};
            }

            std::wstring dir = full.substr(0, slash);

            // Should this ever be built into a subfolder, step back up to the install
            // root so siblings in other subfolders are still recognised.
            const std::wstring winui = L"\\WinUI3Apps";
            if (dir.size() > winui.size() && _wcsicmp(dir.c_str() + dir.size() - winui.size(), winui.c_str()) == 0)
            {
                dir.resize(dir.size() - winui.size());
            }

            return dir;
        }();

        return directory;
    }

    bool IsPowerToysProcess(DWORD processId)
    {
        const std::wstring& own = OwnDirectory();
        if (own.empty())
        {
            return false;
        }

        const HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId);
        if (process == nullptr)
        {
            // A process we cannot open is not one of ours: everything PowerToys runs is
            // running as this user.
            return false;
        }

        wchar_t path[MAX_PATH]{};
        DWORD size = ARRAYSIZE(path);
        const bool queried = QueryFullProcessImageNameW(process, 0, path, &size) != FALSE;
        CloseHandle(process);
        if (!queried)
        {
            return false;
        }

        // Prefix, not equality: PowerToys binaries live in the install root and in
        // subfolders beneath it. Comparing only the immediate directory let the Quick
        // Access flyout through, and sharing then landed on the flyout itself.
        std::wstring image(path, size);
        if (image.size() <= own.size())
        {
            return false;
        }

        return _wcsnicmp(image.c_str(), own.c_str(), own.size()) == 0 &&
               (image[own.size()] == L'\\' || own.back() == L'\\');
    }

    std::wstring WindowTitle(HWND window)
    {
        const int length = GetWindowTextLengthW(window);
        if (length <= 0)
        {
            return {};
        }

        std::wstring title(static_cast<size_t>(length) + 1, L'\0');
        const int copied = GetWindowTextW(window, title.data(), length + 1);
        title.resize(copied > 0 ? static_cast<size_t>(copied) : 0);
        return title;
    }

    std::wstring WindowClass(HWND window)
    {
        wchar_t buffer[256]{};
        const int copied = GetClassNameW(window, buffer, ARRAYSIZE(buffer));
        return std::wstring(buffer, copied > 0 ? static_cast<size_t>(copied) : 0);
    }
}

PresenterWindow::~PresenterWindow()
{
    Stop();
}

bool PresenterWindow::IsPresentableWindow(HWND window) noexcept
{
    if (window == nullptr || !IsWindow(window) || !IsWindowVisible(window) || IsIconic(window))
    {
        return false;
    }

    // A cloaked window is composed but not on screen - a background virtual desktop, or
    // a suspended UWP app. Mirroring one would show stale pixels.
    if (IsCloaked(window))
    {
        return false;
    }

    RECT rect{};
    if (!GetWindowRect(window, &rect) || (rect.right - rect.left) <= 0 || (rect.bottom - rect.top) <= 0)
    {
        return false;
    }

    if (WindowTitle(window).empty())
    {
        return false;
    }

    // The shell surfaces are not applications: pressing the shortcut over the taskbar or
    // the desktop should just draw on screen as usual.
    const std::wstring className = WindowClass(window);
    // The tray and its flyouts are reachable by touch in a way the taskbar proper is
    // not, and a share started from there would otherwise mirror the tray overflow.
    static const wchar_t* const shellClasses[] = {
        L"Shell_TrayWnd",
        L"Shell_SecondaryTrayWnd",
        L"Progman",
        L"WorkerW",
        L"NotifyIconOverflowWindow",             // tray overflow, Windows 10
        L"TopLevelWindowForOverflowXamlIsland",  // tray overflow, Windows 11
        L"Xaml_WindowedPopupClass",              // shell and app flyouts
        L"XamlExplorerHostIslandWindow",         // task view and other shell islands
        L"ControlCenterWindow",                  // quick settings
        L"Windows.UI.Core.CoreWindow",
        L"PowerToysLaserPointer",
    };

    for (const wchar_t* shell : shellClasses)
    {
        if (className == shell)
        {
            return false;
        }
    }

    if (className == m_className)
    {
        return false;
    }

    // Never mirror any part of PowerToys. Comparing process ids only covers windows in
    // this process, which misses the Quick Access flyout and Settings - both separate
    // executables, and the flyout in particular is in front at the exact moment its
    // button toggles the presenter. Everything PowerToys ships lives in one directory,
    // so that is what is compared.
    DWORD windowProcess = 0;
    GetWindowThreadProcessId(window, &windowProcess);
    if (windowProcess == GetCurrentProcessId())
    {
        return false;
    }

    return !IsPowerToysProcess(windowProcess);
}

LRESULT CALLBACK PresenterWindow::WndProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam) noexcept
{
    if (message == WM_CLOSE)
    {
        // The window is listed in the taskbar, so the shell offers to close it - from the
        // jump list, or with Alt+F4. Swallowing that made the option look broken. It is
        // not destroyed here though: the module owns the capture session and the overlay
        // state that go with it, so the request is handed back and it stops sharing in
        // order, exactly as the shortcut does.
        auto* self = reinterpret_cast<PresenterWindow*>(GetWindowLongPtrW(window, GWLP_USERDATA));
        if (self != nullptr && self->m_closeNotifyWindow != nullptr)
        {
            PostMessageW(self->m_closeNotifyWindow, self->m_closeNotifyMessage, 0, 0);
        }
        return 0;
    }

    return DefWindowProc(window, message, wParam, lParam);
}

bool PresenterWindow::CreateHostWindow(HINSTANCE instance)
{
    WNDCLASSW wc{};
    if (!GetClassInfoW(instance, m_className, &wc))
    {
        wc.lpfnWndProc = WndProc;
        wc.hInstance = instance;
        wc.hIcon = static_cast<HICON>(LoadImageW(instance, MAKEINTRESOURCEW(IDI_SHARABLE_WINDOW), IMAGE_ICON, 0, 0, LR_DEFAULTSIZE | LR_SHARED));
        wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
        wc.hbrBackground = static_cast<HBRUSH>(GetStockObject(BLACK_BRUSH));
        wc.lpszClassName = m_className;
        if (!RegisterClassW(&wc))
        {
            return false;
        }
    }

    const int virtualLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
    const int virtualTop = GetSystemMetrics(SM_YVIRTUALSCREEN);
    const int virtualWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);

    // GetWindowRect reports physical pixels, so the mirror has to be created in a
    // per-monitor-aware context or Windows scales the requested size by the monitor's
    // DPI and the capture ends up stretched into a smaller window.
    const auto previousDpiContext = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    // Sharing pickers offer windows the shell counts as applications. WS_EX_APPWINDOW is
    // what earns that; WS_EX_NOACTIVATE is what forfeited it, since it explicitly keeps a
    // window off the taskbar. Focus is avoided with SW_SHOWNOACTIVATE instead, which
    // costs nothing here. No caption, so what gets shared is only the mirrored window -
    // a title bar would otherwise sit above the content for every viewer.
    const DWORD style = WS_POPUP;
    const DWORD exStyle = WS_EX_APPWINDOW;

    // Without a caption the client area is the whole window, so no adjustment is needed.
    const int outerWidth = static_cast<int>(m_width);
    const int outerHeight = static_cast<int>(m_height);

    m_hwnd = CreateWindowExW(exStyle,
                             m_className,
                             L"",
                             style,
                             virtualLeft + virtualWidth + OFFSCREEN_MARGIN,
                             virtualTop,
                             outerWidth,
                             outerHeight,
                             nullptr,
                             nullptr,
                             instance,
                             nullptr);

    if (previousDpiContext != nullptr)
    {
        SetThreadDpiAwarenessContext(previousDpiContext);
    }

    if (m_hwnd == nullptr)
    {
        return false;
    }

    // The window procedure is static, so this is how a message arriving for the window
    // finds the object that owns it.
    SetWindowLongPtrW(m_hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(this));

    // Force the outer size, then check the client area is what the swap chain expects.
    SetWindowPos(m_hwnd, nullptr, 0, 0, outerWidth, outerHeight, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);

    RECT client{};
    if (GetClientRect(m_hwnd, &client))
    {
        const UINT clientWidth = static_cast<UINT>(client.right - client.left);
        const UINT clientHeight = static_cast<UINT>(client.bottom - client.top);
        if (clientWidth != m_width || clientHeight != m_height)
        {
            Logger::warn("Laser Pointer presenter client area is {}x{} but {}x{} was asked for; the mirror will be scaled.",
                         clientWidth,
                         clientHeight,
                         m_width,
                         m_height);
        }
    }

    RefreshTitle();
    const int smallCx = GetSystemMetrics(SM_CXSMICON);
    const int bigCx = GetSystemMetrics(SM_CXICON);
    if (const HICON smallIcon = static_cast<HICON>(LoadImageW(instance, MAKEINTRESOURCEW(IDI_SHARABLE_WINDOW), IMAGE_ICON, smallCx, smallCx, LR_SHARED)))
    {
        SendMessageW(m_hwnd, WM_SETICON, ICON_SMALL, reinterpret_cast<LPARAM>(smallIcon));
    }
    if (const HICON bigIcon = static_cast<HICON>(LoadImageW(instance, MAKEINTRESOURCEW(IDI_SHARABLE_WINDOW), IMAGE_ICON, bigCx, bigCx, LR_SHARED)))
    {
        SendMessageW(m_hwnd, WM_SETICON, ICON_BIG, reinterpret_cast<LPARAM>(bigIcon));
    }

    ShowWindow(m_hwnd, SW_SHOWNOACTIVATE);
    return true;
}

void PresenterWindow::RefreshTitle()
{
    if (m_hwnd == nullptr)
    {
        return;
    }

    // The title is what identifies this in the sharing picker, so it names the window it
    // is mirroring rather than the module.
    std::wstring title = GET_RESOURCE_STRING(IDS_PRESENTER_WINDOW_TITLE_PREFIX) +
                         (m_targetTitle.empty() ? GET_RESOURCE_STRING(IDS_PRESENTER_WINDOW_UNTITLED) : m_targetTitle);
    SetWindowTextW(m_hwnd, title.c_str());
}

bool PresenterWindow::CreateGraphics(ID3D11Device* d3dDevice, ID2D1Device1* d2dDevice)
{
    m_d3dDevice.copy_from(d3dDevice);
    m_d2dDevice.copy_from(d2dDevice);

    if (FAILED(m_d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, m_d2dContext.put())))
    {
        return false;
    }

    winrt::com_ptr<IDXGIDevice> dxgiDevice;
    if (FAILED(m_d3dDevice->QueryInterface(IID_PPV_ARGS(dxgiDevice.put()))))
    {
        return false;
    }

    winrt::com_ptr<IDXGIAdapter> adapter;
    if (FAILED(dxgiDevice->GetAdapter(adapter.put())))
    {
        return false;
    }

    winrt::com_ptr<IDXGIFactory2> factory;
    if (FAILED(adapter->GetParent(IID_PPV_ARGS(factory.put()))))
    {
        return false;
    }

    DXGI_SWAP_CHAIN_DESC1 description{};
    description.Width = m_width;
    description.Height = m_height;
    description.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    description.SampleDesc.Count = 1;
    description.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    description.BufferCount = 2;
    description.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
    description.AlphaMode = DXGI_ALPHA_MODE_IGNORE;

    if (FAILED(factory->CreateSwapChainForHwnd(m_d3dDevice.get(), m_hwnd, &description, nullptr, nullptr, m_swapChain.put())))
    {
        return false;
    }

    // A window parked off screen should not be waiting on vblank for a monitor it is not
    // on, so presentation never blocks.
    factory->MakeWindowAssociation(m_hwnd, DXGI_MWA_NO_ALT_ENTER);

    winrt::com_ptr<IDXGISurface> backBuffer;
    if (FAILED(m_swapChain->GetBuffer(0, IID_PPV_ARGS(backBuffer.put()))))
    {
        return false;
    }

    const auto properties = D2D1::BitmapProperties1(
        D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
        D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_IGNORE));

    winrt::com_ptr<ID2D1Bitmap1> target;
    if (FAILED(m_d2dContext->CreateBitmapFromDxgiSurface(backBuffer.get(), properties, target.put())))
    {
        return false;
    }

    m_d2dContext->SetTarget(target.get());
    return true;
}

bool PresenterWindow::StartCapture()
{
    try
    {
        winrt::com_ptr<IDXGIDevice> dxgiDevice;
        if (FAILED(m_d3dDevice->QueryInterface(IID_PPV_ARGS(dxgiDevice.put()))))
        {
            return false;
        }

        winrt::com_ptr<::IInspectable> inspectable;
        if (FAILED(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.get(), inspectable.put())))
        {
            return false;
        }
        m_captureDevice = inspectable.as<directx::Direct3D11::IDirect3DDevice>();

        auto interop = winrt::get_activation_factory<capture::GraphicsCaptureItem, ::IGraphicsCaptureItemInterop>();
        if (FAILED(interop->CreateForWindow(m_target,
                                            winrt::guid_of<capture::GraphicsCaptureItem>(),
                                            winrt::put_abi(m_captureItem))))
        {
            return false;
        }

        m_framePool = capture::Direct3D11CaptureFramePool::CreateFreeThreaded(
            m_captureDevice,
            directx::DirectXPixelFormat::B8G8R8A8UIntNormalized,
            CAPTURE_BUFFERS,
            m_captureItem.Size());

        m_session = m_framePool.CreateCaptureSession(m_captureItem);

        // Windows 11 draws a yellow border around a captured window by default, which
        // would follow the presenter around for the whole session. Turning it off is
        // best effort: the property does not exist on older builds.
        try
        {
            if (winrt::Windows::Foundation::Metadata::ApiInformation::IsPropertyPresent(
                    L"Windows.Graphics.Capture.GraphicsCaptureSession", L"IsBorderRequired"))
            {
                m_session.IsBorderRequired(false);
            }
        }
        catch (...)
        {
        }

        m_session.StartCapture();
        return true;
    }
    catch (...)
    {
        Logger::error("Laser Pointer presenter could not start window capture.");
        return false;
    }
}

bool PresenterWindow::Start(HINSTANCE instance, HWND target, ID3D11Device* d3dDevice, ID2D1Device1* d2dDevice)
{
    Stop();

    if (!IsPresentableWindow(target) || d3dDevice == nullptr || d2dDevice == nullptr)
    {
        return false;
    }

    m_target = target;
    m_targetTitle = WindowTitle(target);
    m_targetRect = VisibleBounds(target);
    if (m_targetRect.right <= m_targetRect.left || m_targetRect.bottom <= m_targetRect.top)
    {
        m_target = nullptr;
        return false;
    }

    m_width = static_cast<UINT>((std::max)(1L, m_targetRect.right - m_targetRect.left));
    m_height = static_cast<UINT>((std::max)(1L, m_targetRect.bottom - m_targetRect.top));

    if (!CreateHostWindow(instance) || !CreateGraphics(d3dDevice, d2dDevice) || !StartCapture())
    {
        Stop();
        return false;
    }

    m_captureStartedMs = GetTickCount64();

    Logger::info("Laser Pointer presenter started for '{}' ({}x{}).",
                 winrt::to_string(m_targetTitle),
                 m_width,
                 m_height);
    return true;
}

// Just the capture half, so retargeting can replace it without disturbing the window.
void PresenterWindow::StopCapture()
{
    if (m_session != nullptr)
    {
        try
        {
            m_session.Close();
        }
        catch (...)
        {
        }
        m_session = nullptr;
    }

    if (m_framePool != nullptr)
    {
        try
        {
            m_framePool.Close();
        }
        catch (...)
        {
        }
        m_framePool = nullptr;
    }

    m_captureItem = nullptr;
    m_captureDevice = nullptr;

    m_frameBitmap = nullptr;
    m_frameCopy = nullptr;
    m_haveFirstFrame = false;
    m_lastNudgeMs = 0;
    m_captureStartedMs = 0;
    m_seedAttempted = false;
}

bool PresenterWindow::Retarget(HWND target)
{
    if (m_hwnd == nullptr || !IsPresentableWindow(target))
    {
        return false;
    }

    if (target == m_target)
    {
        return true;
    }

    const RECT bounds = VisibleBounds(target);
    if (bounds.right <= bounds.left || bounds.bottom <= bounds.top)
    {
        return false;
    }

    StopCapture();

    m_target = target;
    m_targetTitle = WindowTitle(target);
    m_targetRect = bounds;

    const UINT width = static_cast<UINT>(bounds.right - bounds.left);
    const UINT height = static_cast<UINT>(bounds.bottom - bounds.top);
    if (!ResizeSwapChain(width, height))
    {
        return false;
    }

    // The host window has no caption, so its outer size is its client size.
    SetWindowPos(m_hwnd, nullptr, 0, 0, static_cast<int>(width), static_cast<int>(height), SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
    SetWindowTextW(m_hwnd, (GET_RESOURCE_STRING(IDS_PRESENTER_WINDOW_TITLE_PREFIX) + m_targetTitle).c_str());

    if (!StartCapture())
    {
        return false;
    }

    m_captureStartedMs = GetTickCount64();

    Logger::info("Laser Pointer presenter retargeted to '{}' ({}x{}).",
                 winrt::to_string(m_targetTitle),
                 width,
                 height);
    return true;
}

void PresenterWindow::Stop()
{
    StopCapture();

    if (m_d2dContext)
    {
        m_d2dContext->SetTarget(nullptr);
    }
    m_swapChain = nullptr;
    m_d2dContext = nullptr;
    m_d2dDevice = nullptr;
    m_d3dDevice = nullptr;

    if (m_hwnd != nullptr)
    {
        DestroyWindow(m_hwnd);
        m_hwnd = nullptr;
        Logger::info("Laser Pointer presenter stopped.");
    }

    m_target = nullptr;
    m_targetTitle.clear();
    m_width = 0;
    m_height = 0;
}

// Holds the most recent captured frame. Recreated only when the size changes, so the
// steady state is one CopyResource per frame and nothing else.
bool PresenterWindow::EnsureFrameCopy(UINT width, UINT height)
{
    if (m_frameCopy && m_frameBitmap)
    {
        D3D11_TEXTURE2D_DESC existing{};
        m_frameCopy->GetDesc(&existing);
        if (existing.Width == width && existing.Height == height)
        {
            return true;
        }
    }

    m_frameBitmap = nullptr;
    m_frameCopy = nullptr;

    D3D11_TEXTURE2D_DESC description{};
    description.Width = width;
    description.Height = height;
    description.MipLevels = 1;
    description.ArraySize = 1;
    description.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    description.SampleDesc.Count = 1;
    description.Usage = D3D11_USAGE_DEFAULT;
    description.BindFlags = D3D11_BIND_SHADER_RESOURCE | D3D11_BIND_RENDER_TARGET;

    if (FAILED(m_d3dDevice->CreateTexture2D(&description, nullptr, m_frameCopy.put())))
    {
        return false;
    }

    winrt::com_ptr<IDXGISurface> surface;
    if (FAILED(m_frameCopy->QueryInterface(IID_PPV_ARGS(surface.put()))))
    {
        m_frameCopy = nullptr;
        return false;
    }

    const auto properties = D2D1::BitmapProperties1(
        D2D1_BITMAP_OPTIONS_NONE,
        D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_IGNORE));

    if (FAILED(m_d2dContext->CreateBitmapFromDxgiSurface(surface.get(), properties, m_frameBitmap.put())))
    {
        m_frameCopy = nullptr;
        return false;
    }

    return true;
}

// Last resort for a window that never repaints: PrintWindow renders it on demand,
// including windows drawn through DirectX, which plain invalidation cannot reach. Used
// once, only to put something on screen until real capture frames start flowing.
bool PresenterWindow::SeedFromPrintWindow()
{
    if (!EnsureFrameCopy(m_width, m_height))
    {
        return false;
    }

    BITMAPINFO info{};
    info.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    info.bmiHeader.biWidth = static_cast<LONG>(m_width);
    // Negative height gives a top-down DIB, matching the texture's row order.
    info.bmiHeader.biHeight = -static_cast<LONG>(m_height);
    info.bmiHeader.biPlanes = 1;
    info.bmiHeader.biBitCount = 32;
    info.bmiHeader.biCompression = BI_RGB;

    const HDC screenDc = GetDC(nullptr);
    if (screenDc == nullptr)
    {
        return false;
    }

    void* bits = nullptr;
    const HBITMAP bitmap = CreateDIBSection(screenDc, &info, DIB_RGB_COLORS, &bits, nullptr, 0);
    const HDC memoryDc = CreateCompatibleDC(screenDc);
    ReleaseDC(nullptr, screenDc);

    bool seeded = false;
    if (bitmap != nullptr && memoryDc != nullptr && bits != nullptr)
    {
        const HGDIOBJ previous = SelectObject(memoryDc, bitmap);

        // PW_RENDERFULLCONTENT is what makes this work for composited windows.
        if (PrintWindow(m_target, memoryDc, PW_RENDERFULLCONTENT))
        {
            winrt::com_ptr<ID3D11DeviceContext> context;
            m_d3dDevice->GetImmediateContext(context.put());
            context->UpdateSubresource(m_frameCopy.get(), 0, nullptr, bits, m_width * 4, 0);
            seeded = true;
        }

        SelectObject(memoryDc, previous);
    }

    if (memoryDc != nullptr)
    {
        DeleteDC(memoryDc);
    }
    if (bitmap != nullptr)
    {
        DeleteObject(bitmap);
    }

    if (seeded)
    {
        Logger::info("Laser Pointer presenter seeded its first image with PrintWindow.");
    }

    return seeded;
}

// Resizing while a capture is live also has to move the frame pool over; resizing while
// it is torn down must not go near it. Retarget does the latter, and calling Recreate on
// a closed pool is an access violation rather than something catch(...) would see.
bool PresenterWindow::ResizeSurfaces(UINT width, UINT height)
{
    if (width == m_width && height == m_height)
    {
        return true;
    }

    if (!ResizeSwapChain(width, height))
    {
        return false;
    }

    try
    {
        if (m_framePool != nullptr)
        {
            m_framePool.Recreate(m_captureDevice,
                                 directx::DirectXPixelFormat::B8G8R8A8UIntNormalized,
                                 CAPTURE_BUFFERS,
                                 { static_cast<int32_t>(m_width), static_cast<int32_t>(m_height) });
        }
    }
    catch (...)
    {
        return false;
    }

    return true;
}

bool PresenterWindow::ResizeSwapChain(UINT width, UINT height)
{
    if (width == m_width && height == m_height)
    {
        return true;
    }

    m_width = (std::max)(1u, width);
    m_height = (std::max)(1u, height);

    m_frameBitmap = nullptr;
    m_frameCopy = nullptr;

    m_d2dContext->SetTarget(nullptr);
    if (FAILED(m_swapChain->ResizeBuffers(0, m_width, m_height, DXGI_FORMAT_UNKNOWN, 0)))
    {
        return false;
    }

    winrt::com_ptr<IDXGISurface> backBuffer;
    if (FAILED(m_swapChain->GetBuffer(0, IID_PPV_ARGS(backBuffer.put()))))
    {
        return false;
    }

    const auto properties = D2D1::BitmapProperties1(
        D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
        D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_IGNORE));

    winrt::com_ptr<ID2D1Bitmap1> target;
    if (FAILED(m_d2dContext->CreateBitmapFromDxgiSurface(backBuffer.get(), properties, target.put())))
    {
        return false;
    }

    m_d2dContext->SetTarget(target.get());
    SetWindowPos(m_hwnd, nullptr, 0, 0, static_cast<int>(m_width), static_cast<int>(m_height),
                 SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);

    return true;
}

bool PresenterWindow::Present(const std::function<void(ID2D1DeviceContext*)>& drawTrail)
{
    if (m_hwnd == nullptr || !m_d2dContext || !m_swapChain)
    {
        return false;
    }

    // The target going away is the normal end of a presentation, not an error.
    if (!IsWindow(m_target))
    {
        return false;
    }

    const RECT rect = VisibleBounds(m_target);
    if (rect.right > rect.left && rect.bottom > rect.top)
    {
        m_targetRect = rect;
    }

    // Frames are pulled on the render tick rather than handled in FrameArrived, so
    // everything stays on the thread that owns the graphics device.
    try
    {
        if (m_framePool != nullptr)
        {
            if (auto frame = m_framePool.TryGetNextFrame())
            {
                const auto size = frame.ContentSize();
                if (size.Width > 0 && size.Height > 0 &&
                    (static_cast<UINT>(size.Width) != m_width || static_cast<UINT>(size.Height) != m_height))
                {
                    frame.Close();
                    return ResizeSurfaces(static_cast<UINT>(size.Width), static_cast<UINT>(size.Height));
                }

                winrt::com_ptr<ID3D11Texture2D> frameTexture;
                auto access = frame.Surface().as<::Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();
                access->GetInterface(IID_PPV_ARGS(frameTexture.put()));

                // Copy before closing: the surface goes straight back into the pool and
                // is reused, so anything drawn from it afterwards is whatever landed
                // there next.
                if (frameTexture && EnsureFrameCopy(m_width, m_height))
                {
                    winrt::com_ptr<ID3D11DeviceContext> context;
                    m_d3dDevice->GetImmediateContext(context.put());
                    context->CopyResource(m_frameCopy.get(), frameTexture.get());

                    if (!m_haveFirstFrame)
                    {
                        m_haveFirstFrame = true;
                        Logger::info("Laser Pointer presenter received its first frame.");
                    }
                }

                frame.Close();
            }
        }
    }
    catch (...)
    {
        return false;
    }

    // Nothing has been captured yet, which happens when the mirrored window is behind
    // others and simply is not repainting. Ask it to, or the mirror stays black until
    // something incidental - moving the mouse over it - makes it draw. Invalidation is
    // posted rather than forced, so an unresponsive app cannot stall the render tick.
    if (!m_haveFirstFrame)
    {
        const uint64_t now = GetTickCount64();
        if (now - m_lastNudgeMs >= NUDGE_INTERVAL_MS)
        {
            m_lastNudgeMs = now;
            RedrawWindow(m_target, nullptr, nullptr, RDW_INVALIDATE | RDW_ALLCHILDREN);
        }

        // Invalidation does nothing for a window that renders through DirectX rather
        // than WM_PAINT, so after a moment fall back to rendering it directly.
        if (!m_seedAttempted && now - m_captureStartedMs >= SEED_AFTER_MS)
        {
            m_seedAttempted = true;
            SeedFromPrintWindow();
        }
    }

    m_d2dContext->BeginDraw();
    m_d2dContext->Clear(D2D1::ColorF(0.0f, 0.0f, 0.0f, 1.0f));

    // Drawn every tick from our own copy, not only when a frame arrived, so a window
    // that is not repainting still shows its last known contents.
    if (m_frameBitmap)
    {
        m_d2dContext->DrawBitmap(m_frameBitmap.get());
    }

    if (drawTrail)
    {
        // The trail is held in screen coordinates; shift it into the mirror and clip so
        // pointing outside the presented window does not bleed into the capture.
        m_d2dContext->PushAxisAlignedClip(
            D2D1::RectF(0.0f, 0.0f, static_cast<float>(m_width), static_cast<float>(m_height)),
            D2D1_ANTIALIAS_MODE_ALIASED);
        m_d2dContext->SetTransform(D2D1::Matrix3x2F::Translation(
            -static_cast<float>(m_targetRect.left),
            -static_cast<float>(m_targetRect.top)));

        drawTrail(m_d2dContext.get());

        m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
        m_d2dContext->PopAxisAlignedClip();
    }

    const HRESULT endDraw = m_d2dContext->EndDraw();
    if (endDraw == D2DERR_RECREATE_TARGET || endDraw == DXGI_ERROR_DEVICE_REMOVED || endDraw == DXGI_ERROR_DEVICE_RESET)
    {
        return false;
    }

    const HRESULT present = m_swapChain->Present(0, 0);
    return present != DXGI_ERROR_DEVICE_REMOVED && present != DXGI_ERROR_DEVICE_RESET;
}
