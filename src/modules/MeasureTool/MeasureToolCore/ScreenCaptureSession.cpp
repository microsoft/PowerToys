#include "pch.h"

#include "ScreenCaptureSession.h"

namespace
{
    winrt::Windows::Graphics::Capture::GraphicsCaptureItem CreateCaptureItemForMonitor(HMONITOR monitor)
    {
        auto captureInterop = winrt::get_activation_factory<
            winrt::Windows::Graphics::Capture::GraphicsCaptureItem,
            IGraphicsCaptureItemInterop>();

        winrt::Windows::Graphics::Capture::GraphicsCaptureItem item = nullptr;
        winrt::check_hresult(captureInterop->CreateForMonitor(
            monitor,
            winrt::guid_of<winrt::Windows::Graphics::Capture::GraphicsCaptureItem>(),
            winrt::put_abi(item)));
        return item;
    }

    template<typename T>
    auto GetDXGIInterfaceFromObject(winrt::Windows::Foundation::IInspectable const& object)
    {
        auto access = object.as<Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();
        winrt::com_ptr<T> result;
        winrt::check_hresult(access->GetInterface(winrt::guid_of<T>(), result.put_void()));
        return result;
    }
}

ScreenCaptureSession::ScreenCaptureSession(
    DxgiAPI* dxgiAPI,
    winrt::com_ptr<IDXGISwapChain1> swapChain,
    winrt::Windows::Graphics::DirectX::DirectXPixelFormat pixelFormat,
    MonitorInfo monitorInfo,
    bool continuousCapture) :
    _dxgiAPI{ dxgiAPI },
    _device{ dxgiAPI->d3dForCapture.d3dDeviceInspectable.as<winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice>() },
    _swapChain{ std::move(swapChain) },
    _monitor{ monitorInfo.GetHandle() },
    _pixelFormat{ pixelFormat },
    _monitorArea{ monitorInfo.GetScreenSize(true) },
    _continuousCapture{ continuousCapture }
{
}

std::unique_ptr<ScreenCaptureSession> ScreenCaptureSession::Create(
    DxgiAPI* dxgiAPI,
    MonitorInfo monitorInfo,
    winrt::Windows::Graphics::DirectX::DirectXPixelFormat pixelFormat,
    bool continuousCapture)
{
    const auto dimensions = monitorInfo.GetScreenSize(true);
    const DXGI_SWAP_CHAIN_DESC1 description = {
        .Width = static_cast<uint32_t>(dimensions.width()),
        .Height = static_cast<uint32_t>(dimensions.height()),
        .Format = static_cast<DXGI_FORMAT>(pixelFormat),
        .SampleDesc = { .Count = 1, .Quality = 0 },
        .BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
        .BufferCount = 2,
        .Scaling = DXGI_SCALING_STRETCH,
        .SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD,
        .AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED,
    };

    winrt::com_ptr<IDXGISwapChain1> swapChain;
    winrt::check_hresult(dxgiAPI->d3dForCapture.dxgiFactory2->CreateSwapChainForComposition(
        dxgiAPI->d3dForCapture.d3dDevice.get(),
        &description,
        nullptr,
        swapChain.put()));

    return std::unique_ptr<ScreenCaptureSession>{
        new ScreenCaptureSession{ dxgiAPI, std::move(swapChain), pixelFormat, std::move(monitorInfo), continuousCapture }
    };
}

ScreenCaptureSession::~ScreenCaptureSession()
{
    std::unique_lock callbackLock{ _frameArrivedMutex };
    StopCaptureLocked();
}

winrt::com_ptr<ID3D11Texture2D> ScreenCaptureSession::CopyFrameToCPU(
    const winrt::com_ptr<ID3D11Texture2D>& frameTexture)
{
    D3D11_TEXTURE2D_DESC description{};
    frameTexture->GetDesc(&description);
    description.Usage = D3D11_USAGE_STAGING;
    description.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    description.MiscFlags = 0;
    description.BindFlags = 0;

    winrt::com_ptr<ID3D11Texture2D> cpuTexture;
    winrt::check_hresult(_dxgiAPI->d3dForCapture.d3dDevice->CreateTexture2D(&description, nullptr, cpuTexture.put()));
    _dxgiAPI->d3dForCapture.d3dContext->CopyResource(cpuTexture.get(), frameTexture.get());
    return cpuTexture;
}

void ScreenCaptureSession::OnFrameArrived(
    const winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool& sender,
    const winrt::Windows::Foundation::IInspectable&)
{
    std::lock_guard callbackLock{ _frameArrivedMutex };

    bool resized = false;
    POINT cursorPosition{};
    GetCursorPos(&cursorPosition);

    winrt::Windows::Graphics::Capture::Direct3D11CaptureFrame frame{ nullptr };
    try
    {
        frame = sender.TryGetNextFrame();
    }
    catch (...)
    {
    }

    if (!frame)
    {
        return;
    }

    if (_monitorArea.inside(cursorPosition) || !_continuousCapture)
    {
        if (const auto newFrameSize = frame.ContentSize(); newFrameSize != _frameSize)
        {
            winrt::check_hresult(_swapChain->ResizeBuffers(
                2,
                static_cast<uint32_t>(newFrameSize.Width),
                static_cast<uint32_t>(newFrameSize.Height),
                static_cast<DXGI_FORMAT>(_pixelFormat),
                0));
            _frameSize = newFrameSize;
            resized = true;
        }

        auto surface = frame.Surface();
        auto gpuTexture = GetDXGIInterfaceFromObject<ID3D11Texture2D>(surface);
        auto texture = CopyFrameToCPU(gpuTexture);
        surface.Close();

        if (_frameCallback)
        {
            _frameCallback(MappedTextureView{
                std::move(texture),
                _dxgiAPI->d3dForCapture.d3dContext,
                static_cast<size_t>(_frameSize.Width),
                static_cast<size_t>(_frameSize.Height),
            });
        }
    }

    frame.Close();
    if (resized)
    {
        _framePool.Recreate(_device, _pixelFormat, 2, _frameSize);
    }
}

void ScreenCaptureSession::StartSessionInPreferredMode()
{
    auto item = CreateCaptureItemForMonitor(_monitor);
    _frameSize = item.Size();
    _framePool = winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool::CreateFreeThreaded(
        _device,
        _pixelFormat,
        2,
        item.Size());
    _session = _framePool.CreateCaptureSession(item);
    _framePool.FrameArrived({ this, &ScreenCaptureSession::OnFrameArrived });

    if (auto session3 = _session.try_as<winrt::Windows::Graphics::Capture::IGraphicsCaptureSession3>())
    {
        session3.IsBorderRequired(false);
    }

    _session.IsCursorCaptureEnabled(false);
    _session.StartCapture();
}

void ScreenCaptureSession::StartCapture(std::function<void(MappedTextureView)> frameCallback)
{
    _frameCallback = std::move(frameCallback);
    StartSessionInPreferredMode();
}

MappedTextureView ScreenCaptureSession::CaptureSingleFrame(bool skipInitialFrame)
{
    std::optional<MappedTextureView> result;
    wil::shared_event frameArrivedEvent{ wil::EventOptions::ManualReset };
    uint32_t framesToSkip = skipInitialFrame ? 1 : 0;

    _frameCallback = [frameArrivedEvent, &result, &framesToSkip, this](MappedTextureView texture) {
        if (frameArrivedEvent.is_signaled())
        {
            return;
        }
        if (framesToSkip > 0)
        {
            --framesToSkip;
            return;
        }

        StopCaptureLocked();
        result.emplace(std::move(texture));
        frameArrivedEvent.SetEvent();
    };
    StartSessionInPreferredMode();
    const DWORD waitResult = WaitForSingleObject(frameArrivedEvent.get(), 2000);
    if (waitResult != WAIT_OBJECT_0)
    {
        std::unique_lock callbackLock{ _frameArrivedMutex };
        if (!frameArrivedEvent.is_signaled())
        {
            StopCaptureLocked();
            _frameCallback = nullptr;
            winrt::throw_hresult(
                waitResult == WAIT_TIMEOUT ?
                    HRESULT_FROM_WIN32(ERROR_TIMEOUT) :
                    HRESULT_FROM_WIN32(GetLastError()));
        }
    }
    return std::move(*result);
}

void ScreenCaptureSession::StopCapture()
{
    std::unique_lock callbackLock{ _frameArrivedMutex };
    StopCaptureLocked();
}

void ScreenCaptureSession::StopCaptureLocked()
{
    try
    {
        if (_session)
        {
            _session.Close();
            _session = nullptr;
        }

        if (_framePool)
        {
            _framePool.Close();
            _framePool = nullptr;
        }
    }
    catch (...)
    {
        // Closing a capture session can race with the capture broker shutting down.
    }
}
