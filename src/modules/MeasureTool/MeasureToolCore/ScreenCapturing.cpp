#include "pch.h"

#include "BGRATextureView.h"
#include "constants.h"
#include "CoordinateSystemConversion.h"
#include "EdgeDetection.h"
#include "ScreenCapturing.h"

#include <common/Display/monitors.h>

//#define DEBUG_EDGES

namespace
{
    winrt::GraphicsCaptureItem CreateCaptureItemForMonitor(HMONITOR monitor)
    {
        auto captureInterop = winrt::get_activation_factory<
            winrt::GraphicsCaptureItem,
            IGraphicsCaptureItemInterop>();

        winrt::GraphicsCaptureItem item = nullptr;

        winrt::check_hresult(captureInterop->CreateForMonitor(
            monitor,
            winrt::guid_of<winrt::GraphicsCaptureItem>(),
            winrt::put_abi(item)));

        return item;
    }
}

class D3DCaptureState final
{
    DxgiAPI* dxgiAPI = nullptr;
    winrt::IDirect3DDevice device;
    winrt::com_ptr<IDXGISwapChain1> swapChain;

    winrt::SizeInt32 frameSize{};
    HMONITOR monitor = {};
    winrt::DirectXPixelFormat pixelFormat;

    winrt::Direct3D11CaptureFramePool framePool = nullptr;
    winrt::GraphicsCaptureSession session = nullptr;

    std::function<void(MappedTextureView)> frameCallback;
    Box monitorArea;
    bool continuousCapture = false;

    D3DCaptureState(DxgiAPI* dxgiAPI,
                    winrt::com_ptr<IDXGISwapChain1> swapChain,
                    winrt::DirectXPixelFormat pixelFormat,
                    MonitorInfo monitorInfo,
                    const bool continuousCapture);

    winrt::com_ptr<ID3D11Texture2D> CopyFrameToCPU(const winrt::com_ptr<ID3D11Texture2D>& texture);

    void OnFrameArrived(const winrt::Direct3D11CaptureFramePool& sender, const winrt::IInspectable&);

    void StartSessionInPreferredMode();

    std::mutex frameArrivedMutex;

public:
    static std::unique_ptr<D3DCaptureState> Create(DxgiAPI* dxgiAPI,
                                                   MonitorInfo monitorInfo,
                                                   const winrt::DirectXPixelFormat pixelFormat,
                                                   const bool continuousCapture);

    ~D3DCaptureState();

    void StartCapture(std::function<void(MappedTextureView)> _frameCallback);
    MappedTextureView CaptureSingleFrame();

    void StopCapture();
};

D3DCaptureState::D3DCaptureState(DxgiAPI* dxgiAPI,
                                 winrt::com_ptr<IDXGISwapChain1> _swapChain,
                                 winrt::DirectXPixelFormat pixelFormat_,
                                 MonitorInfo monitorInfo,
                                 const bool continuousCapture_) :
    dxgiAPI{ dxgiAPI },
    device{ dxgiAPI->d3dForCapture.d3dDeviceInspectable.as<winrt::IDirect3DDevice>() },
    swapChain{ std::move(_swapChain) },
    pixelFormat{ std::move(pixelFormat_) },
    monitor{ monitorInfo.GetHandle() },
    monitorArea{ monitorInfo.GetScreenSize(true) },
    continuousCapture{ continuousCapture_ }
{
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
    winrt::check_hresult(dxgiAPI->d3dForCapture.d3dDevice->CreateTexture2D(&desc, nullptr, cpuTexture.put()));
    dxgiAPI->d3dForCapture.d3dContext->CopyResource(cpuTexture.get(), frameTexture.get());

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
    std::lock_guard callbackLock{ frameArrivedMutex };

    bool resized = false;
    POINT cursorPos = {};
    GetCursorPos(&cursorPos);

    winrt::Direct3D11CaptureFrame frame = nullptr;
    try
    {
        frame = sender.TryGetNextFrame();
    }
    catch (...)
    {
    }

    if (!frame)
        return;

    if (monitorArea.inside(cursorPos) || !continuousCapture)
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
            MappedTextureView textureView{ texture,
                                           dxgiAPI->d3dForCapture.d3dContext,
                                           static_cast<size_t>(frameSize.Width),
                                           static_cast<size_t>(frameSize.Height) };

            frameCallback(std::move(textureView));
        }
    }

    frame.Close();

    if (resized)
    {
        framePool.Recreate(device, pixelFormat, 2, frameSize);
    }
}

std::unique_ptr<D3DCaptureState> D3DCaptureState::Create(DxgiAPI* dxgiAPI,
                                                         MonitorInfo monitorInfo,
                                                         const winrt::DirectXPixelFormat pixelFormat,
                                                         const bool continuousCapture)
{
    const auto dims = monitorInfo.GetScreenSize(true);
    const DXGI_SWAP_CHAIN_DESC1 desc = {
        .Width = static_cast<uint32_t>(dims.width()),
        .Height = static_cast<uint32_t>(dims.height()),
        .Format = static_cast<DXGI_FORMAT>(pixelFormat),
        .SampleDesc = { .Count = 1, .Quality = 0 },
        .BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
        .BufferCount = 2,
        .Scaling = DXGI_SCALING_STRETCH,
        .SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD,
        .AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED,
    };

    winrt::com_ptr<IDXGISwapChain1> swapChain;
    winrt::check_hresult(dxgiAPI->d3dForCapture.dxgiFactory2->CreateSwapChainForComposition(dxgiAPI->d3dForCapture.d3dDevice.get(),
                                                                                            &desc,
                                                                                            nullptr,
                                                                                            swapChain.put()));

    // We must create the object in a heap, since we need to pin it in memory to receive callbacks
    auto statePtr = std::unique_ptr<D3DCaptureState>(new D3DCaptureState{ dxgiAPI, std::move(swapChain), pixelFormat, std::move(monitorInfo), continuousCapture });

    return statePtr;
}

D3DCaptureState::~D3DCaptureState()
{
    std::unique_lock callbackLock{ frameArrivedMutex };
    StopCapture();
}

void D3DCaptureState::StartSessionInPreferredMode()
{
    auto item = CreateCaptureItemForMonitor(monitor);
    frameSize = item.Size();
    framePool = winrt::Direct3D11CaptureFramePool::CreateFreeThreaded(device, pixelFormat, 2, item.Size());
    session = framePool.CreateCaptureSession(item);
    framePool.FrameArrived({ this, &D3DCaptureState::OnFrameArrived });

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
        if (frameArrivedEvent.is_signaled())
            return;

        StopCapture();
        result.emplace(std::move(tex));
        frameArrivedEvent.SetEvent();
    };
    StartSessionInPreferredMode();

    frameArrivedEvent.wait();

    return std::move(*result);
}

void D3DCaptureState::StopCapture()
{
    try
    {
        if (session)
            session.Close();

        if (framePool)
            framePool.Close();
    }
    catch (...)
    {
        // RPC call might fail here
    }
}

void UpdateCaptureState(const CommonState& commonState,
                        Serialized<MeasureToolState>& state,
                        HWND window,
                        const MappedTextureView& textureView)
{
    const auto cursorPos = convert::FromSystemToWindow(window, commonState.cursorPosSystemSpace);
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
                                    pixelTolerance);

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
        state.perScreen[window].measuredEdges = Measurement{ bounds };
    });
}

std::thread StartCapturingThread(DxgiAPI* dxgiAPI,
                                 const CommonState& commonState,
                                 Serialized<MeasureToolState>& state,
                                 HWND window,
                                 MonitorInfo monitor)
{
    return SpawnLoggedThread(L"Screen Capture thread", [&state, &commonState, monitor, window, dxgiAPI] {
        bool continuousCapture = {};
        state.Read([&](const MeasureToolState& state) {
            continuousCapture = state.global.continuousCapture;
        });

        auto captureState = D3DCaptureState::Create(dxgiAPI,
                                                    monitor,
                                                    winrt::DirectXPixelFormat::B8G8R8A8UIntNormalized,
                                                    continuousCapture);
        const auto monitorArea = monitor.GetScreenSize(true);
        bool mouseOnMonitor = false;
        if (continuousCapture)
        {
            while (IsWindow(window) && !commonState.closeOnOtherMonitors)
            {
                if (mouseOnMonitor == monitorArea.inside(commonState.cursorPosSystemSpace))
                {
                    std::this_thread::sleep_for(consts::TARGET_FRAME_DURATION);
                    continue;
                }

                mouseOnMonitor = !mouseOnMonitor;
                if (mouseOnMonitor)
                {
                    captureState->StartCapture([&, window](MappedTextureView textureView) {
                        UpdateCaptureState(commonState, state, window, textureView);
                    });
                }
                else
                {
                    state.Access([&](MeasureToolState& state) {
                        state.perScreen[window].measuredEdges = {};
                    });

                    captureState->StopCapture();
                }
            }
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
                    UpdateCaptureState(commonState, state, window, textureView);
                    mouseOnMonitor = true;
                }
                else if (mouseOnMonitor)
                {
                    state.Access([&](MeasureToolState& state) {
                        state.perScreen[window].measuredEdges = {};
                    });
                    mouseOnMonitor = false;
                }

                const auto frameTime = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::high_resolution_clock::now() - now);
                if (frameTime < consts::TARGET_FRAME_DURATION)
                {
                    std::this_thread::sleep_for(consts::TARGET_FRAME_DURATION - frameTime);
                }
            }
        }

        captureState->StopCapture();
    });
}
