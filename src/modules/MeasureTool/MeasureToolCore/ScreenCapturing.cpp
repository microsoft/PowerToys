#include "pch.h"

#include "BGRATextureView.h"
#include "constants.h"
#include "CoordinateSystemConversion.h"
#include "EdgeDetection.h"
#include "ScreenCapturing.h"

#include <common/Display/monitors.h>

//#define DEBUG_EDGES

D3DState::D3DState()
{
    UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
#ifndef NDEBUG
    flags |= D3D11_CREATE_DEVICE_DEBUG;
#endif
    HRESULT hr =
        D3D11CreateDevice(nullptr,
                          D3D_DRIVER_TYPE_HARDWARE,
                          nullptr,
                          flags,
                          nullptr,
                          0,
                          D3D11_SDK_VERSION,
                          d3dDevice.put(),
                          nullptr,
                          nullptr);
    if (hr == DXGI_ERROR_UNSUPPORTED)
    {
        hr = D3D11CreateDevice(nullptr,
                               D3D_DRIVER_TYPE_WARP,
                               nullptr,
                               flags,
                               nullptr,
                               0,
                               D3D11_SDK_VERSION,
                               d3dDevice.put(),
                               nullptr,
                               nullptr);
    }
    winrt::check_hresult(hr);

    dxgiDevice = d3dDevice.as<IDXGIDevice>();
    winrt::check_hresult(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.get(), d3dDeviceInspectable.put()));
}

class D3DCaptureState final
{
    D3DState* d3dState = nullptr;
    winrt::IDirect3DDevice device;
    winrt::com_ptr<IDXGISwapChain1> swapChain;
    winrt::com_ptr<ID3D11DeviceContext> context;
    winrt::SizeInt32 frameSize;

    winrt::DirectXPixelFormat pixelFormat;
    winrt::Direct3D11CaptureFramePool framePool;
    winrt::GraphicsCaptureSession session;

    std::function<void(MappedTextureView)> frameCallback;
    Box monitorArea;
    bool captureOutsideOfMonitor = false;

    D3DCaptureState(D3DState* d3dState,
                    winrt::com_ptr<IDXGISwapChain1> _swapChain,
                    winrt::com_ptr<ID3D11DeviceContext> _context,
                    const winrt::GraphicsCaptureItem& item,
                    winrt::DirectXPixelFormat _pixelFormat,
                    Box monitorArea,
                    const bool captureOutsideOfMonitor);

    winrt::com_ptr<ID3D11Texture2D> CopyFrameToCPU(const winrt::com_ptr<ID3D11Texture2D>& texture);

    void OnFrameArrived(const winrt::Direct3D11CaptureFramePool& sender, const winrt::IInspectable&);

    void StartSessionInPreferredMode();

    std::mutex destructorMutex;

public:
    static std::unique_ptr<D3DCaptureState> Create(D3DState* d3dState,
                                                   winrt::GraphicsCaptureItem item,
                                                   const winrt::DirectXPixelFormat pixelFormat,
                                                   Box monitorSize,
                                                   const bool captureOutsideOfMonitor);

    ~D3DCaptureState();

    void StartCapture(std::function<void(MappedTextureView)> _frameCallback);
    MappedTextureView CaptureSingleFrame();

    void StopCapture();
};

D3DCaptureState::D3DCaptureState(D3DState* _d3dState,
                                 winrt::com_ptr<IDXGISwapChain1> _swapChain,
                                 winrt::com_ptr<ID3D11DeviceContext> _context,
                                 const winrt::GraphicsCaptureItem& item,
                                 winrt::DirectXPixelFormat _pixelFormat,
                                 Box _monitorArea,
                                 const bool _captureOutsideOfMonitor) :
    d3dState{ _d3dState },
    device{ _d3dState->d3dDeviceInspectable.as<winrt::IDirect3DDevice>() },
    swapChain{ std::move(_swapChain) },
    context{ std::move(_context) },
    frameSize{ item.Size() },
    pixelFormat{ std::move(_pixelFormat) },
    framePool{ winrt::Direct3D11CaptureFramePool::CreateFreeThreaded(device, pixelFormat, 1, item.Size()) },
    session{ framePool.CreateCaptureSession(item) },
    monitorArea{ _monitorArea },
    captureOutsideOfMonitor{ _captureOutsideOfMonitor }
{
    framePool.FrameArrived({ this, &D3DCaptureState::OnFrameArrived });
}

winrt::com_ptr<ID3D11Texture2D> D3DCaptureState::CopyFrameToCPU(const winrt::com_ptr<ID3D11Texture2D>& frameTexture)
{
    D3D11_TEXTURE2D_DESC desc = {};
    frameTexture->GetDesc(&desc);
    desc.Usage = D3D11_USAGE_STAGING;
    desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    desc.MiscFlags = 0;
    desc.BindFlags = 0;

    winrt::com_ptr<ID3D11Texture2D> cpuTexture;
    winrt::check_hresult(d3dState->d3dDevice->CreateTexture2D(&desc, nullptr, cpuTexture.put()));
    context->CopyResource(cpuTexture.get(), frameTexture.get());

    return cpuTexture;
}

template<typename T>
auto GetDXGIInterfaceFromObject(winrt::IInspectable const& object)
{
    auto access = object.as<Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();
    winrt::com_ptr<T> result;
    winrt::check_hresult(access->GetInterface(winrt::guid_of<T>(), result.put_void()));
    return result;
}

void D3DCaptureState::OnFrameArrived(const winrt::Direct3D11CaptureFramePool& sender, const winrt::IInspectable&)
{
    // Prevent calling a callback on a partially destroyed state
    std::unique_lock callbackLock{ destructorMutex };

    bool resized = false;
    POINT cursorPos = {};
    GetCursorPos(&cursorPos);

    auto frame = sender.TryGetNextFrame();
    winrt::check_bool(frame);
    if (monitorArea.inside(cursorPos) || captureOutsideOfMonitor)
    {
        winrt::com_ptr<ID3D11Texture2D> texture;
        {
            if (auto newFrameSize = frame.ContentSize(); newFrameSize != frameSize)
            {
                winrt::check_hresult(swapChain->ResizeBuffers(2,
                                                              static_cast<uint32_t>(newFrameSize.Height),
                                                              static_cast<uint32_t>(newFrameSize.Width),
                                                              static_cast<DXGI_FORMAT>(pixelFormat),
                                                              0));
                frameSize = newFrameSize;
                resized = true;
            }

            winrt::check_hresult(swapChain->GetBuffer(0, winrt::guid_of<ID3D11Texture2D>(), texture.put_void()));
            auto surface = frame.Surface();
            auto gpuTexture = GetDXGIInterfaceFromObject<ID3D11Texture2D>(surface);
            texture = CopyFrameToCPU(gpuTexture);
            surface.Close();
            MappedTextureView textureView{ texture, context, static_cast<size_t>(frameSize.Width), static_cast<size_t>(frameSize.Height) };

            frameCallback(std::move(textureView));
        }
    }

    frame.Close();

    DXGI_PRESENT_PARAMETERS presentParameters = {};
    swapChain->Present1(1, 0, &presentParameters);

    if (resized)
    {
        framePool.Recreate(device, pixelFormat, 2, frameSize);
    }
}

std::unique_ptr<D3DCaptureState> D3DCaptureState::Create(D3DState* d3dState,
                                                         winrt::GraphicsCaptureItem item,
                                                         const winrt::DirectXPixelFormat pixelFormat,
                                                         Box monitorArea,
                                                         const bool captureOutsideOfMonitor)
{
    std::lock_guard guard{ gpuAccessLock };

    const DXGI_SWAP_CHAIN_DESC1 desc = {
        .Width = static_cast<uint32_t>(item.Size().Width),
        .Height = static_cast<uint32_t>(item.Size().Height),
        .Format = static_cast<DXGI_FORMAT>(pixelFormat),
        .SampleDesc = { .Count = 1, .Quality = 0 },
        .BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
        .BufferCount = 2,
        .Scaling = DXGI_SCALING_STRETCH,
        .SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL,
        .AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED,
    };
    winrt::com_ptr<IDXGIAdapter> adapter;
    winrt::check_hresult(d3dState->dxgiDevice->GetParent(winrt::guid_of<IDXGIAdapter>(), adapter.put_void()));
    winrt::com_ptr<IDXGIFactory2> factory;
    winrt::check_hresult(adapter->GetParent(winrt::guid_of<IDXGIFactory2>(), factory.put_void()));

    winrt::com_ptr<IDXGISwapChain1> swapChain;
    winrt::check_hresult(factory->CreateSwapChainForComposition(d3dState->d3dDevice.get(), &desc, nullptr, swapChain.put()));

    winrt::com_ptr<ID3D11DeviceContext> context;
    d3dState->d3dDevice->GetImmediateContext(context.put());
    winrt::check_bool(context);
    auto contextMultithread = context.as<ID3D11Multithread>();
    contextMultithread->SetMultithreadProtected(true);

    // We must create the object in a heap, since we need to pin it in memory to receive callbacks
    auto statePtr = new D3DCaptureState{ d3dState,
                                         std::move(swapChain),
                                         std::move(context),
                                         item,
                                         pixelFormat,
                                         monitorArea,
                                         captureOutsideOfMonitor };

    return std::unique_ptr<D3DCaptureState>{ statePtr };
}

D3DCaptureState::~D3DCaptureState()
{
    std::unique_lock callbackLock{ destructorMutex };
    StopCapture();
    framePool.Close();
}

void D3DCaptureState::StartSessionInPreferredMode()
{
    // Try disable border if possible (available on Windows ver >= 20348)
    if (auto session3 = session.try_as<winrt::IGraphicsCaptureSession3>())
    {
        session3.IsBorderRequired(false);
    }

    session.IsCursorCaptureEnabled(false);
    session.StartCapture();
}

void D3DCaptureState::StartCapture(std::function<void(MappedTextureView)> _frameCallback)
{
    frameCallback = std::move(_frameCallback);
    StartSessionInPreferredMode();
}

MappedTextureView D3DCaptureState::CaptureSingleFrame()
{
    std::optional<MappedTextureView> result;
    wil::shared_event frameArrivedEvent(wil::EventOptions::ManualReset);

    frameCallback = [frameArrivedEvent, &result, this](MappedTextureView tex) {
        if (result)
            return;

        StopCapture();
        result.emplace(std::move(tex));
        frameArrivedEvent.SetEvent();
    };
    std::lock_guard guard{ gpuAccessLock };
    StartSessionInPreferredMode();

    frameArrivedEvent.wait();

    assert(result.has_value());
    return std::move(*result);
}

void D3DCaptureState::StopCapture()
{
    session.Close();
}

void UpdateCaptureState(const CommonState& commonState,
                        Serialized<MeasureToolState>& state,
                        HWND window,
                        const MappedTextureView& textureView,
                        const bool continuousCapture)
{
    const auto cursorPos = convert::FromSystemToRelative(window, commonState.cursorPosSystemSpace);
    const bool cursorInLeftScreenHalf = cursorPos.x < textureView.view.width / 2;
    const bool cursorInTopScreenHalf = cursorPos.y < textureView.view.height / 2;
    uint8_t pixelTolerance = {};
    bool perColorChannelEdgeDetection = {};
    state.Access([&](MeasureToolState& state) {
        state.perScreen[window].cursorInLeftScreenHalf = cursorInLeftScreenHalf;
        state.perScreen[window].cursorInTopScreenHalf = cursorInTopScreenHalf;
        pixelTolerance = state.global.pixelTolerance;
        perColorChannelEdgeDetection = state.global.perColorChannelEdgeDetection;
    });

    // Every one of 4 edges is a coordinate of the last similar pixel in a row
    // Example: given a 5x5 green square on a blue background with its top-left pixel
    //          at 20x100, bounds should be [20,100]-[24,104]. We don't include [25,105] or
    //          [19,99], since those pixels are blue. Thus, square dims are equal to
    //          [24-20+1,104-100+1]=[5,5].
    const RECT bounds = DetectEdges(textureView.view,
                                    cursorPos,
                                    perColorChannelEdgeDetection,
                                    pixelTolerance,
                                    continuousCapture);

#if defined(DEBUG_EDGES)
    char buffer[256];
    sprintf_s(buffer,
              "Cursor: [%ld,%ld] Bounds: [%ld,%ld]-[%ld,%ld] Screen size: [%zu, %zu]\n",
              cursorPos.x,
              cursorPos.y,
              bounds.left,
              bounds.top,
              bounds.right,
              bounds.bottom,
              textureView.view.width,
              textureView.view.height);
    OutputDebugStringA(buffer);
#endif
    state.Access([&](MeasureToolState& state) {
        state.perScreen[window].measuredEdges = bounds;
    });
}

std::thread StartCapturingThread(D3DState* d3dState,
                                 const CommonState& commonState,
                                 Serialized<MeasureToolState>& state,
                                 HWND window,
                                 MonitorInfo monitor)
{
    return SpawnLoggedThread(L"Screen Capture thread", [&state, &commonState, monitor, window, d3dState] {
        auto captureInterop = winrt::get_activation_factory<
            winrt::GraphicsCaptureItem,
            IGraphicsCaptureItemInterop>();

        winrt::GraphicsCaptureItem item = nullptr;

        winrt::check_hresult(captureInterop->CreateForMonitor(
            monitor.GetHandle(),
            winrt::guid_of<winrt::GraphicsCaptureItem>(),
            winrt::put_abi(item)));

        bool continuousCapture = {};
        state.Read([&](const MeasureToolState& state) {
            continuousCapture = state.global.continuousCapture;
        });

        const auto monitorArea = monitor.GetScreenSize(true);
        auto captureState = D3DCaptureState::Create(d3dState,
                                                    item,
                                                    winrt::DirectXPixelFormat::B8G8R8A8UIntNormalized,
                                                    monitorArea,
                                                    !continuousCapture);
        if (continuousCapture)
        {
            captureState->StartCapture([&, window](MappedTextureView textureView) {
                UpdateCaptureState(commonState, state, window, textureView, continuousCapture);
            });

            while (IsWindow(window) && !commonState.closeOnOtherMonitors)
            {
                std::this_thread::sleep_for(consts::TARGET_FRAME_DURATION);
            }
            captureState->StopCapture();
        }
        else
        {
            const auto textureView = captureState->CaptureSingleFrame();

            state.Access([&](MeasureToolState& s) {
                s.perScreen[window].capturedScreenTexture = &textureView;
            });

            while (IsWindow(window) && !commonState.closeOnOtherMonitors)
            {
                const auto now = std::chrono::high_resolution_clock::now();
                if (monitorArea.inside(commonState.cursorPosSystemSpace))
                {
#if defined(DEBUG_TEXTURE)
                    SYSTEMTIME lt{};
                    GetLocalTime(&lt);
                    char buf[256];
                    sprintf_s(buf, "frame-%02d-%02d-Monitor-%zu.bmp", lt.wHour, lt.wMinute, (uint64_t)window);
                    auto path = std::filesystem::temp_directory_path() / buf;
                    textureView.view.SaveAsBitmap(path.string().c_str());
#endif
                    UpdateCaptureState(commonState, state, window, textureView, continuousCapture);
                }

                const auto frameTime = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::high_resolution_clock::now() - now);
                if (frameTime < consts::TARGET_FRAME_DURATION)
                {
                    std::this_thread::sleep_for(consts::TARGET_FRAME_DURATION - frameTime);
                }
            }
        }
    });
}
