#include "pch.h"

#include "../../ModuleConstants.h"
#include "MirrorWindow.xaml.h"

namespace winrt::DEPiP::implementation
{
    namespace
    {
        constexpr UINT_PTR OpacityTimer = 1;
    }

    MirrorWindow::MirrorWindow()
    {
        InitializeComponent();
    }

    MirrorWindow::~MirrorWindow()
    {
        if (m_compositionScaleChangedToken.value)
        {
            CapturePanel().CompositionScaleChanged(m_compositionScaleChangedToken);
            m_compositionScaleChangedToken = {};
        }
        if (m_sizeChangedToken.value)
        {
            CapturePanel().SizeChanged(m_sizeChangedToken);
            m_sizeChangedToken = {};
        }
        if (m_eventWatcher.joinable())
        {
            m_eventWatcher.request_stop();
            m_eventWatcher.join();
        }
        StopCapture();
    }

    void MirrorWindow::Initialize(DisplayInfo source)
    {
        m_source = source;
        m_settings = LoadDEPiPSettings();
        m_captureItem = CreateCaptureItem(source.monitor);
        Title(L"DEPiP - " + GetDisplayLabel(source));

        auto native = this->try_as<::IWindowNative>();
        winrt::check_hresult(native->get_WindowHandle(&m_window));
        AppWindow().SetIcon(L"Assets\\DEPiP.ico");
        SetWindowSubclass(m_window, WindowSubclass, 0, reinterpret_cast<DWORD_PTR>(this));

        auto size = m_captureItem.Size();
        auto displays = EnumerateDisplays();
        auto primary = std::find_if(displays.begin(), displays.end(), [](auto const& display) {
            return (display.info.dwFlags & MONITORINFOF_PRIMARY) != 0;
        });
        RECT workArea =
            primary != displays.end() ? primary->info.rcWork : m_source.info.rcWork;
        UINT dpi = GetDpiForWindow(m_window);
        int maximumClientWidth = (workArea.right - workArea.left) * 3 / 4;
        int maximumClientHeight = (workArea.bottom - workArea.top) * 3 / 4;
        int clientWidth = std::min(
            MulDiv(960, dpi, USER_DEFAULT_SCREEN_DPI),
            maximumClientWidth);
        int clientHeight = MulDiv(clientWidth, size.Height, size.Width);
        if (clientHeight > maximumClientHeight)
        {
            clientHeight = maximumClientHeight;
            clientWidth = MulDiv(clientHeight, size.Width, size.Height);
        }
        RECT windowRect{ 0, 0, clientWidth, clientHeight };
        winrt::check_bool(AdjustWindowRectExForDpi(
            &windowRect,
            WS_OVERLAPPEDWINDOW,
            FALSE,
            0,
            dpi));
        int width = windowRect.right - windowRect.left;
        int height = windowRect.bottom - windowRect.top;
        AppWindow().MoveAndResize({
            workArea.left + (workArea.right - workArea.left - width) / 2,
            workArea.top + (workArea.bottom - workArea.top - height) / 2,
            width,
            height,
        });

        CreateGraphics();
        StartCapture();
        ApplySettings();
        SetTimer(m_window, OpacityTimer, 150, nullptr);
        StartEventWatcher();
    }

    LRESULT CALLBACK MirrorWindow::WindowSubclass(
        HWND window,
        UINT message,
        WPARAM wordParam,
        LPARAM longParam,
        UINT_PTR subclassId,
        DWORD_PTR referenceData)
    {
        auto self = reinterpret_cast<MirrorWindow*>(referenceData);
        if (message == WM_SIZING && self->m_settings.lockAspectRatio)
        {
            self->ApplyAspectRatio(wordParam, *reinterpret_cast<RECT*>(longParam));
            return TRUE;
        }
        if (message == WM_TIMER && wordParam == OpacityTimer)
        {
            self->ApplySettings();
            return 0;
        }
        if (message == WM_NCDESTROY)
        {
            KillTimer(window, OpacityTimer);
            RemoveWindowSubclass(window, WindowSubclass, subclassId);
        }
        return DefSubclassProc(window, message, wordParam, longParam);
    }

    void MirrorWindow::CreateGraphics()
    {
        constexpr D3D_FEATURE_LEVEL levels[]{ D3D_FEATURE_LEVEL_11_1, D3D_FEATURE_LEVEL_11_0 };
        D3D_FEATURE_LEVEL selected{};
        HRESULT result = D3D11CreateDevice(
            nullptr,
            D3D_DRIVER_TYPE_HARDWARE,
            nullptr,
            D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            levels,
            static_cast<UINT>(std::size(levels)),
            D3D11_SDK_VERSION,
            m_device.put(),
            &selected,
            m_context.put());
        if (FAILED(result))
        {
            winrt::check_hresult(D3D11CreateDevice(
                nullptr,
                D3D_DRIVER_TYPE_WARP,
                nullptr,
                D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                levels,
                static_cast<UINT>(std::size(levels)),
                D3D11_SDK_VERSION,
                m_device.put(),
                &selected,
                m_context.put()));
        }
        if (auto multithread = m_context.try_as<ID3D11Multithread>())
        {
            multithread->SetMultithreadProtected(TRUE);
        }

        auto dxgiDevice = m_device.as<IDXGIDevice>();
        winrt::com_ptr<::IInspectable> inspectable;
        winrt::check_hresult(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.get(), inspectable.put()));
        m_winrtDevice = inspectable.as<Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice>();

        winrt::com_ptr<IDXGIAdapter> adapter;
        winrt::check_hresult(dxgiDevice->GetAdapter(adapter.put()));
        winrt::com_ptr<IDXGIFactory2> factory;
        winrt::check_hresult(adapter->GetParent(IID_PPV_ARGS(factory.put())));

        auto size = m_captureItem.Size();
        DXGI_SWAP_CHAIN_DESC1 description{};
        description.Width = size.Width;
        description.Height = size.Height;
        description.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
        description.SampleDesc.Count = 1;
        description.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
        description.BufferCount = 2;
        description.Scaling = DXGI_SCALING_STRETCH;
        description.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
        description.AlphaMode = DXGI_ALPHA_MODE_IGNORE;
        winrt::com_ptr<IDXGISwapChain1> chain;
        winrt::check_hresult(factory->CreateSwapChainForComposition(
            m_device.get(),
            &description,
            nullptr,
            chain.put()));
        m_swapChain = chain.as<IDXGISwapChain2>();
        auto panelNative = CapturePanel().as<ISwapChainPanelNative>();
        winrt::check_hresult(panelNative->SetSwapChain(m_swapChain.get()));
        m_compositionScaleChangedToken =
            CapturePanel().CompositionScaleChanged({ this, &MirrorWindow::OnCompositionScaleChanged });
        m_sizeChangedToken =
            CapturePanel().SizeChanged({ this, &MirrorWindow::OnCapturePanelSizeChanged });
        UpdateSwapChainScale();
    }

    void MirrorWindow::StartCapture()
    {
        using namespace Windows::Graphics;
        using namespace Windows::Graphics::Capture;
        m_framePool = Direct3D11CaptureFramePool::CreateFreeThreaded(
            m_winrtDevice,
            DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized,
            2,
            m_captureItem.Size());
        m_session = m_framePool.CreateCaptureSession(m_captureItem);
        m_session.IsCursorCaptureEnabled(true);
        m_frameArrivedToken = m_framePool.FrameArrived({ this, &MirrorWindow::OnFrameArrived });
        m_closedToken = m_captureItem.Closed([this](auto&&, auto&&) {
            DispatcherQueue().TryEnqueue([this] { Close(); });
        });
        m_session.StartCapture();
    }

    void MirrorWindow::StopCapture()
    {
        std::scoped_lock lock(m_captureMutex);
        if (m_framePool && m_frameArrivedToken.value)
        {
            m_framePool.FrameArrived(m_frameArrivedToken);
            m_frameArrivedToken = {};
        }
        if (m_captureItem && m_closedToken.value)
        {
            m_captureItem.Closed(m_closedToken);
            m_closedToken = {};
        }
        if (m_session)
        {
            m_session.Close();
            m_session = nullptr;
        }
        if (m_framePool)
        {
            m_framePool.Close();
            m_framePool = nullptr;
        }
    }

    void MirrorWindow::OnFrameArrived(
        Windows::Graphics::Capture::Direct3D11CaptureFramePool const& sender,
        Windows::Foundation::IInspectable const&)
    {
        std::scoped_lock lock(m_captureMutex);
        try
        {
            auto frame = sender.TryGetNextFrame();
            if (!frame)
            {
                return;
            }
            auto source = GetDXGIInterface<ID3D11Texture2D>(frame.Surface());
            winrt::com_ptr<ID3D11Texture2D> target;
            winrt::check_hresult(m_swapChain->GetBuffer(0, IID_PPV_ARGS(target.put())));
            D3D11_TEXTURE2D_DESC sourceDescription{};
            D3D11_TEXTURE2D_DESC targetDescription{};
            source->GetDesc(&sourceDescription);
            target->GetDesc(&targetDescription);
            D3D11_BOX sourceBox{
                0,
                0,
                0,
                std::min(sourceDescription.Width, targetDescription.Width),
                std::min(sourceDescription.Height, targetDescription.Height),
                1,
            };
            m_context->CopySubresourceRegion(
                target.get(),
                0,
                0,
                0,
                0,
                source.get(),
                0,
                &sourceBox);
            winrt::check_hresult(m_swapChain->Present(1, 0));
        }
        catch (...)
        {
            DispatcherQueue().TryEnqueue([this] { Close(); });
        }
    }

    void MirrorWindow::OnCompositionScaleChanged(
        Microsoft::UI::Xaml::Controls::SwapChainPanel const&,
        Windows::Foundation::IInspectable const&)
    {
        UpdateSwapChainScale();
    }

    void MirrorWindow::OnCapturePanelSizeChanged(
        Windows::Foundation::IInspectable const&,
        Microsoft::UI::Xaml::SizeChangedEventArgs const&)
    {
        UpdateSwapChainScale();
    }

    void MirrorWindow::UpdateSwapChainScale()
    {
        std::scoped_lock lock(m_captureMutex);
        auto sourceSize = m_captureItem.Size();
        double panelWidth = CapturePanel().ActualWidth();
        double panelHeight = CapturePanel().ActualHeight();
        if (sourceSize.Width <= 0 ||
            sourceSize.Height <= 0 ||
            panelWidth <= 0 ||
            panelHeight <= 0)
        {
            return;
        }

        DXGI_MATRIX_3X2_F fitToPanel{
            static_cast<float>(panelWidth / sourceSize.Width),
            0,
            0,
            static_cast<float>(panelHeight / sourceSize.Height),
            0,
            0,
        };
        winrt::check_hresult(m_swapChain->SetMatrixTransform(&fitToPanel));
    }

    void MirrorWindow::ReloadSettings()
    {
        m_settings = LoadDEPiPSettings();
        ApplySettings();
    }

    void MirrorWindow::ApplySettings()
    {
        SetWindowPos(
            m_window,
            m_settings.alwaysOnTop ? HWND_TOPMOST : HWND_NOTOPMOST,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        POINT cursor{};
        if (GetCursorPos(&cursor))
        {
            RECT windowBounds{};
            bool cursorOnSource =
                MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST) == m_source.monitor;
            bool cursorOnMirror =
                GetWindowRect(m_window, &windowBounds) && PtInRect(&windowBounds, cursor);
            BYTE alpha =
                cursorOnSource || cursorOnMirror ?
                    255 :
                    static_cast<BYTE>(MulDiv(255, 100 - m_settings.inactiveTransparency, 100));
            LONG_PTR style = GetWindowLongPtrW(m_window, GWL_EXSTYLE);
            SetWindowLongPtrW(m_window, GWL_EXSTYLE, style | WS_EX_LAYERED);
            SetLayeredWindowAttributes(m_window, 0, alpha, LWA_ALPHA);
        }
    }

    void MirrorWindow::ApplyAspectRatio(WPARAM edge, RECT& proposed)
    {
        RECT currentClient{};
        RECT currentWindow{};
        if (!GetClientRect(m_window, &currentClient) ||
            !GetWindowRect(m_window, &currentWindow))
        {
            return;
        }

        auto sourceSize = m_captureItem.Size();
        int nonClientWidth =
            (currentWindow.right - currentWindow.left) - (currentClient.right - currentClient.left);
        int nonClientHeight =
            (currentWindow.bottom - currentWindow.top) - (currentClient.bottom - currentClient.top);
        int clientWidth = std::max(
            1,
            static_cast<int>(proposed.right - proposed.left) - nonClientWidth);
        int clientHeight = std::max(
            1,
            static_cast<int>(proposed.bottom - proposed.top) - nonClientHeight);

        if (edge == WMSZ_LEFT || edge == WMSZ_RIGHT)
        {
            clientHeight = MulDiv(clientWidth, sourceSize.Height, sourceSize.Width);
        }
        else if (edge == WMSZ_TOP || edge == WMSZ_BOTTOM)
        {
            clientWidth = MulDiv(clientHeight, sourceSize.Width, sourceSize.Height);
        }
        else
        {
            int widthDrivenHeight = MulDiv(clientWidth, sourceSize.Height, sourceSize.Width);
            int heightDrivenWidth = MulDiv(clientHeight, sourceSize.Width, sourceSize.Height);
            if (std::abs(widthDrivenHeight - clientHeight) <=
                std::abs(heightDrivenWidth - clientWidth))
            {
                clientHeight = widthDrivenHeight;
            }
            else
            {
                clientWidth = heightDrivenWidth;
            }
        }

        int windowWidth = clientWidth + nonClientWidth;
        int windowHeight = clientHeight + nonClientHeight;
        LONG horizontalCenter = proposed.left + (proposed.right - proposed.left) / 2;
        LONG verticalCenter = proposed.top + (proposed.bottom - proposed.top) / 2;

        switch (edge)
        {
        case WMSZ_LEFT:
            proposed.left = proposed.right - windowWidth;
            proposed.top = verticalCenter - windowHeight / 2;
            proposed.bottom = proposed.top + windowHeight;
            break;
        case WMSZ_RIGHT:
            proposed.right = proposed.left + windowWidth;
            proposed.top = verticalCenter - windowHeight / 2;
            proposed.bottom = proposed.top + windowHeight;
            break;
        case WMSZ_TOP:
            proposed.top = proposed.bottom - windowHeight;
            proposed.left = horizontalCenter - windowWidth / 2;
            proposed.right = proposed.left + windowWidth;
            break;
        case WMSZ_BOTTOM:
            proposed.bottom = proposed.top + windowHeight;
            proposed.left = horizontalCenter - windowWidth / 2;
            proposed.right = proposed.left + windowWidth;
            break;
        case WMSZ_TOPLEFT:
            proposed.left = proposed.right - windowWidth;
            proposed.top = proposed.bottom - windowHeight;
            break;
        case WMSZ_TOPRIGHT:
            proposed.right = proposed.left + windowWidth;
            proposed.top = proposed.bottom - windowHeight;
            break;
        case WMSZ_BOTTOMLEFT:
            proposed.left = proposed.right - windowWidth;
            proposed.bottom = proposed.top + windowHeight;
            break;
        case WMSZ_BOTTOMRIGHT:
            proposed.right = proposed.left + windowWidth;
            proposed.bottom = proposed.top + windowHeight;
            break;
        }
    }

    void MirrorWindow::StartEventWatcher()
    {
        auto queue = DispatcherQueue();
        m_eventWatcher = std::jthread([this, queue](std::stop_token token) {
            winrt::handle exitEvent(
                CreateEventW(nullptr, FALSE, FALSE, DEPiPConstants::ExitEvent));
            winrt::handle reloadEvent(
                CreateEventW(nullptr, FALSE, FALSE, DEPiPConstants::ReloadSettingsEvent));
            if (!exitEvent || !reloadEvent)
            {
                return;
            }
            HANDLE handles[]{ exitEvent.get(), reloadEvent.get() };
            while (!token.stop_requested())
            {
                DWORD result = WaitForMultipleObjects(2, handles, FALSE, 250);
                if (result == WAIT_OBJECT_0)
                {
                    queue.TryEnqueue([this] { Close(); });
                    return;
                }
                if (result == WAIT_OBJECT_0 + 1)
                {
                    queue.TryEnqueue([this] { ReloadSettings(); });
                }
            }
        });
    }
}
