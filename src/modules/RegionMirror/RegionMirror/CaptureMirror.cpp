// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "CaptureMirror.h"
#include "Geometry.h"

#include <d3d11.h>
#include <dxgi1_2.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cwchar>
#include <exception>
#include <limits>
#include <mutex>
#include <thread>

namespace RegionMirror
{
    namespace
    {
        using namespace winrt::Windows::Graphics::Capture;
        using winrt::Windows::Graphics::DirectX::DirectXPixelFormat;
        using winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice;

        class Event final
        {
        public:
            explicit Event(bool manualReset) :
                m_handle(CreateEventW(nullptr, manualReset, false, nullptr))
            {
                winrt::check_bool(m_handle != nullptr);
            }

            ~Event() { CloseHandle(m_handle); }
            Event(const Event&) = delete;
            Event& operator=(const Event&) = delete;
            HANDLE Get() const noexcept { return m_handle; }
            void Signal() const noexcept { SetEvent(m_handle); }

        private:
            HANDLE m_handle;
        };

        struct Signals
        {
            Event stop{ true };
            Event closed{ true };
            Event frame{ false };
            std::atomic<size_t> closedSource{ (std::numeric_limits<size_t>::max)() };
        };

        struct CaptureSession
        {
            GraphicsCaptureItem item{ nullptr };
            Direct3D11CaptureFramePool pool{ nullptr };
            GraphicsCaptureSession session{ nullptr };
            GraphicsCaptureItem::Closed_revoker closedRevoker;
            Direct3D11CaptureFramePool::FrameArrived_revoker frameRevoker;

            ~CaptureSession()
            {
                // Callbacks only signal shared events; they never touch the D3D
                // context or this object's lifetime. All WinRT teardown is on MTA.
                frameRevoker.revoke();
                closedRevoker.revoke();
                try
                {
                    if (session)
                    {
                        session.Close();
                    }
                }
                catch (...)
                {
                }
                try
                {
                    if (pool)
                    {
                        pool.Close();
                    }
                }
                catch (...)
                {
                }
            }
        };

        RECT MonitorRectangle(HMONITOR monitor)
        {
            MONITORINFO info{ sizeof(info) };
            winrt::check_bool(GetMonitorInfoW(monitor, &info));
            return info.rcMonitor;
        }

        void ValidateSources(const RECT& region, const std::vector<CaptureSource>& sources)
        {
            if (!IsValidRegion(region) || sources.empty())
            {
                throw winrt::hresult_invalid_argument(L"Capture requires a valid region and at least one intersecting source monitor.");
            }
            std::vector<RECT> coveredRegions;
            for (size_t index = 0; index < sources.size(); ++index)
            {
                const auto& source = sources[index];
                const auto currentBounds = MonitorRectangle(source.monitor);
                if (!EqualRect(&currentBounds, &source.bounds))
                {
                    throw winrt::hresult_invalid_argument(L"A source monitor changed since the capture region was selected.");
                }
                const auto tile = MakeCaptureTile(region, source.bounds);
                if (!tile)
                {
                    throw winrt::hresult_invalid_argument(L"Every capture source must have valid bounds and intersect the selected region.");
                }
                RECT covered{};
                IntersectRect(&covered, &region, &source.bounds);
                for (size_t previous = 0; previous < index; ++previous)
                {
                    RECT overlap{};
                    if (source.monitor == sources[previous].monitor || IntersectRect(&overlap, &covered, &coveredRegions[previous]))
                    {
                        throw winrt::hresult_invalid_argument(L"The capture plan contains duplicate or overlapping source monitors.");
                    }
                }
                coveredRegions.push_back(covered);
            }
        }

        std::wstring DescribeCurrentException()
        {
            try
            {
                throw;
            }
            catch (const winrt::hresult_error& error)
            {
                wchar_t code[16]{};
                swprintf_s(code, L"0x%08X", static_cast<unsigned int>(error.code().value));
                return std::wstring(error.message().c_str()) + L" (" + code + L")";
            }
            catch (const std::exception& error)
            {
                return winrt::to_hstring(error.what()).c_str();
            }
            catch (...)
            {
                return L"An unknown error stopped screen capture.";
            }
        }
    }

    struct CaptureMirror::State
    {
        explicit State(size_t sourceCount) :
            sourceFrames(sourceCount)
        {
        }

        HWND targetWindow{};
        std::vector<CaptureSource> sources;
        RECT sourceRect{};
        HWND notifyWindow{};
        UINT failureMessage{};
        std::shared_ptr<Signals> signals = std::make_shared<Signals>();
        std::thread worker;
        std::atomic<uint64_t> framesPresented{};
        std::vector<std::atomic<uint64_t>> sourceFrames;
        mutable std::mutex errorMutex;
        std::wstring error;

        bool Stopped() const noexcept
        {
            return WaitForSingleObject(signals->stop.Get(), 0) == WAIT_OBJECT_0;
        }

        void CheckGeometry() const
        {
            for (const auto& source : sources)
            {
                const auto currentRect = MonitorRectangle(source.monitor);
                if (!EqualRect(&source.bounds, &currentRect))
                {
                    throw winrt::hresult_error(E_ABORT, L"A source monitor moved or changed resolution. Select the region again.");
                }
            }
            if (!IsWindow(targetWindow))
            {
                throw winrt::hresult_error(E_ABORT, L"The mirror output window was closed.");
            }
        }

        void Capture()
        {
            if (!GraphicsCaptureSession::IsSupported())
            {
                throw winrt::hresult_error(E_NOTIMPL, L"Windows Graphics Capture is unavailable on this system.");
            }
            CheckGeometry();

            winrt::com_ptr<ID3D11Device> device;
            winrt::com_ptr<ID3D11DeviceContext> context;
            winrt::check_hresult(D3D11CreateDevice(nullptr,
                                                   D3D_DRIVER_TYPE_HARDWARE,
                                                   nullptr,
                                                   D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                                                   nullptr,
                                                   0,
                                                   D3D11_SDK_VERSION,
                                                   device.put(),
                                                   nullptr,
                                                   context.put()));

            auto dxgiDevice = device.as<IDXGIDevice>();
            winrt::com_ptr<IInspectable> inspectableDevice;
            winrt::check_hresult(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.get(), inspectableDevice.put()));
            auto winrtDevice = inspectableDevice.as<IDirect3DDevice>();

            winrt::com_ptr<IDXGIAdapter> adapter;
            winrt::check_hresult(dxgiDevice->GetAdapter(adapter.put()));
            winrt::com_ptr<IDXGIFactory2> factory;
            winrt::check_hresult(adapter->GetParent(winrt::guid_of<IDXGIFactory2>(), factory.put_void()));
            DXGI_SWAP_CHAIN_DESC1 description{};
            description.Width = static_cast<UINT>(static_cast<int64_t>(sourceRect.right) - sourceRect.left);
            description.Height = static_cast<UINT>(static_cast<int64_t>(sourceRect.bottom) - sourceRect.top);
            description.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            description.SampleDesc.Count = 1;
            description.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
            description.BufferCount = 2;
            description.Scaling = DXGI_SCALING_STRETCH;
            description.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
            description.AlphaMode = DXGI_ALPHA_MODE_IGNORE;
            winrt::com_ptr<IDXGISwapChain1> swapChain;
            winrt::check_hresult(factory->CreateSwapChainForHwnd(device.get(), targetWindow, &description, nullptr, nullptr, swapChain.put()));
            winrt::check_hresult(factory->MakeWindowAssociation(targetWindow, DXGI_MWA_NO_ALT_ENTER | DXGI_MWA_NO_WINDOW_CHANGES));

            // Keep one persistent composite. Flip-model back buffers rotate, so
            // updating only the changed monitor in a back buffer would leave old
            // or undefined tiles in the other regions. Gaps remain opaque black.
            D3D11_TEXTURE2D_DESC compositeDescription{};
            compositeDescription.Width = description.Width;
            compositeDescription.Height = description.Height;
            compositeDescription.MipLevels = 1;
            compositeDescription.ArraySize = 1;
            compositeDescription.Format = description.Format;
            compositeDescription.SampleDesc.Count = 1;
            compositeDescription.Usage = D3D11_USAGE_DEFAULT;
            compositeDescription.BindFlags = D3D11_BIND_RENDER_TARGET;
            winrt::com_ptr<ID3D11Texture2D> composite;
            winrt::check_hresult(device->CreateTexture2D(&compositeDescription, nullptr, composite.put()));
            winrt::com_ptr<ID3D11RenderTargetView> compositeView;
            winrt::check_hresult(device->CreateRenderTargetView(composite.get(), nullptr, compositeView.put()));
            constexpr float black[]{ 0.0f, 0.0f, 0.0f, 1.0f };
            context->ClearRenderTargetView(compositeView.get(), black);

            struct SourceCapture
            {
                CaptureSession capture;
                CaptureTile tile{};
                winrt::Windows::Graphics::SizeInt32 size{};
                bool receivedFrame{};
            };
            std::vector<std::unique_ptr<SourceCapture>> captures;
            captures.reserve(sources.size());
            auto interop = winrt::get_activation_factory<GraphicsCaptureItem, IGraphicsCaptureItemInterop>();
            for (size_t index = 0; index < sources.size(); ++index)
            {
                if (Stopped())
                {
                    return;
                }
                auto source = std::make_unique<SourceCapture>();
                source->tile = MakeCaptureTile(sourceRect, sources[index].bounds).value();
                auto& capture = source->capture;
                winrt::check_hresult(interop->CreateForMonitor(sources[index].monitor, winrt::guid_of<GraphicsCaptureItem>(), winrt::put_abi(capture.item)));
                source->size = capture.item.Size();
                const auto& bounds = sources[index].bounds;
                if (source->size.Width != static_cast<int64_t>(bounds.right) - bounds.left ||
                    source->size.Height != static_cast<int64_t>(bounds.bottom) - bounds.top)
                {
                    throw winrt::hresult_error(E_ABORT, L"A captured monitor's dimensions do not match the selected region.");
                }

                // A shared D3D11 device makes every delivered source texture
                // compatible with the compositor's immediate context. Windows
                // handles transfer from monitors connected to other adapters.
                capture.pool = Direct3D11CaptureFramePool::CreateFreeThreaded(winrtDevice, DirectXPixelFormat::B8G8R8A8UIntNormalized, 2, source->size);
                const auto events = signals;
                capture.closedRevoker = capture.item.Closed(winrt::auto_revoke, [events, index](auto&&, auto&&) noexcept {
                    events->closedSource.store(index, std::memory_order_relaxed);
                    events->closed.Signal();
                });
                capture.frameRevoker = capture.pool.FrameArrived(winrt::auto_revoke, [events](auto&&, auto&&) noexcept {
                    events->frame.Signal();
                });
                capture.session = capture.pool.CreateCaptureSession(capture.item);
                capture.session.IsCursorCaptureEnabled(true);
                // Leave the system capture border enabled on every source screen.
                capture.session.StartCapture();
                captures.push_back(std::move(source));
            }

            using Clock = std::chrono::steady_clock;
            constexpr auto PresentInterval = std::chrono::nanoseconds(16666667);
            const auto initialFrameDeadline = Clock::now() + std::chrono::seconds(10);
            auto nextPresentation = Clock::time_point::min();
            size_t sourcesWaiting = captures.size();
            bool dirty = false;
            const HANDLE waits[]{ signals->stop.Get(), signals->closed.Get(), signals->frame.Get() };
            while (!Stopped())
            {
                const auto beforeWait = Clock::now();
                auto wakeAt = beforeWait + std::chrono::seconds(1);
                if (sourcesWaiting)
                {
                    wakeAt = (std::min)(wakeAt, initialFrameDeadline);
                }
                else if (dirty)
                {
                    wakeAt = (std::min)(wakeAt, nextPresentation);
                }
                const auto timeout = wakeAt > beforeWait ? static_cast<DWORD>(std::chrono::ceil<std::chrono::milliseconds>(wakeAt - beforeWait).count()) : 0;
                const auto result = WaitForMultipleObjects(ARRAYSIZE(waits), waits, false, timeout);
                if (result == WAIT_OBJECT_0)
                {
                    return;
                }
                if (result == WAIT_OBJECT_0 + 1)
                {
                    throw winrt::hresult_error(E_ABORT, L"Source monitor " + std::to_wstring(signals->closedSource.load(std::memory_order_relaxed) + 1) + L" is no longer available.");
                }
                if (result == WAIT_FAILED)
                {
                    winrt::throw_last_error();
                }
                CheckGeometry();

                // Visit each source once, dequeuing at most its two pool buffers.
                // A fast monitor cannot keep us inside its queue indefinitely, and
                // a static monitor's previous tile stays in the composite.
                for (size_t index = 0; index < captures.size() && !Stopped(); ++index)
                {
                    auto& source = *captures[index];
                    Direct3D11CaptureFrame frame{ nullptr };
                    for (unsigned queued = 0; queued < 2; ++queued)
                    {
                        auto newer = source.capture.pool.TryGetNextFrame();
                        if (!newer)
                        {
                            break;
                        }
                        if (frame)
                        {
                            frame.Close();
                        }
                        frame = std::move(newer);
                    }
                    if (!frame)
                    {
                        continue;
                    }
                    const auto content = frame.ContentSize();
                    if (content.Width != source.size.Width || content.Height != source.size.Height)
                    {
                        throw winrt::hresult_error(E_ABORT, L"A source capture size changed. Select the region again.");
                    }
                    auto access = frame.Surface().as<::Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();
                    winrt::com_ptr<ID3D11Texture2D> texture;
                    winrt::check_hresult(access->GetInterface(winrt::guid_of<ID3D11Texture2D>(), texture.put_void()));
                    D3D11_TEXTURE2D_DESC textureDescription{};
                    texture->GetDesc(&textureDescription);
                    const auto& tile = source.tile;
                    D3D11_BOX crop{};
                    crop.left = static_cast<UINT>(tile.source.left);
                    crop.top = static_cast<UINT>(tile.source.top);
                    crop.right = static_cast<UINT>(tile.source.right);
                    crop.bottom = static_cast<UINT>(tile.source.bottom);
                    crop.back = 1;
                    if (crop.right > textureDescription.Width || crop.bottom > textureDescription.Height)
                    {
                        throw winrt::hresult_error(E_ABORT, L"The capture texture no longer contains the selected region.");
                    }
                    context->CopySubresourceRegion(composite.get(), 0, static_cast<UINT>(tile.destination.x), static_cast<UINT>(tile.destination.y), 0, texture.get(), 0, &crop);
                    frame.Close();
                    sourceFrames[index].fetch_add(1, std::memory_order_relaxed);
                    dirty = true;
                    if (!source.receivedFrame)
                    {
                        source.receivedFrame = true;
                        --sourcesWaiting;
                    }
                }

                const auto now = Clock::now();
                if (sourcesWaiting && now >= initialFrameDeadline)
                {
                    std::wstring missing;
                    for (size_t index = 0; index < captures.size(); ++index)
                    {
                        if (!captures[index]->receivedFrame)
                        {
                            if (!missing.empty())
                            {
                                missing += L", ";
                            }
                            missing += std::to_wstring(index + 1);
                        }
                    }
                    throw winrt::hresult_error(E_ABORT, L"No initial capture frame arrived within 10 seconds from source monitor(s): " + missing + L". Check that these displays are active, then select the region again.");
                }
                if (!Stopped() && !sourcesWaiting && dirty && now >= nextPresentation)
                {
                    winrt::com_ptr<ID3D11Texture2D> backBuffer;
                    winrt::check_hresult(swapChain->GetBuffer(0, winrt::guid_of<ID3D11Texture2D>(), backBuffer.put_void()));
                    context->CopyResource(backBuffer.get(), composite.get());
                    const auto presentResult = swapChain->Present(0, DXGI_PRESENT_DO_NOT_WAIT);
                    nextPresentation = now + PresentInterval;
                    if (presentResult == DXGI_ERROR_WAS_STILL_DRAWING)
                    {
                        continue;
                    }
                    if (presentResult == DXGI_ERROR_DEVICE_REMOVED || presentResult == DXGI_ERROR_DEVICE_RESET)
                    {
                        winrt::check_hresult(device->GetDeviceRemovedReason());
                    }
                    winrt::check_hresult(presentResult);
                    if (presentResult == S_OK)
                    {
                        framesPresented.fetch_add(1, std::memory_order_relaxed);
                        dirty = false;
                    }
                }
            }
        }

        void Run() noexcept
        {
            bool apartmentInitialized = false;
            try
            {
                winrt::init_apartment(winrt::apartment_type::multi_threaded);
                apartmentInitialized = true;
                SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
                if (!Stopped())
                {
                    Capture();
                }
            }
            catch (...)
            {
                if (!Stopped())
                {
                    {
                        std::lock_guard lock(errorMutex);
                        error = DescribeCurrentException();
                    }
                    PostMessageW(notifyWindow, failureMessage, 0, 0);
                }
            }
            if (apartmentInitialized)
            {
                winrt::uninit_apartment();
            }
        }
    };

    CaptureMirror::CaptureMirror() = default;

    CaptureMirror::~CaptureMirror()
    {
        Stop();
    }

    void CaptureMirror::Start(HWND targetWindow, const std::vector<CaptureSource>& sources, RECT sourceRect, HWND notifyWindow, UINT failureMessage)
    {
        if (m_stopping)
        {
            throw winrt::hresult_illegal_method_call(L"Wait for the previous capture to stop before starting another.");
        }
        Stop();
        if (!IsWindow(targetWindow) || !IsWindow(notifyWindow) || failureMessage < WM_APP || failureMessage > 0xBFFF)
        {
            throw winrt::hresult_invalid_argument(L"Capture requires live output/notification windows and a WM_APP message.");
        }
        ValidateSources(sourceRect, sources);

        auto state = std::make_unique<State>(sources.size());
        state->targetWindow = targetWindow;
        state->sources = sources;
        state->sourceRect = sourceRect;
        state->notifyWindow = notifyWindow;
        state->failureMessage = failureMessage;
        state->worker = std::thread([workerState = state.get()]() { workerState->Run(); });
        m_state = std::move(state);
    }

    void CaptureMirror::Stop() noexcept
    {
        if (!m_state || !m_state->worker.joinable() || m_stopping)
        {
            return;
        }
        m_stopping = true;
        m_state->signals->stop.Signal();

        // DXGI can send synchronous messages to the window-owning thread. Service
        // only sent messages while joining; normal posted UI commands stay queued.
        const HANDLE workerHandle = m_state->worker.native_handle();
        for (;;)
        {
            const auto result = MsgWaitForMultipleObjectsEx(1, &workerHandle, INFINITE, QS_SENDMESSAGE, MWMO_INPUTAVAILABLE);
            if (result != WAIT_OBJECT_0 + 1)
            {
                break;
            }
            MSG message{};
            PeekMessageW(&message, nullptr, 0, 0, PM_NOREMOVE | PM_QS_SENDMESSAGE);
        }
        m_state->worker.join();

        // A failure can already be queued when the user stops or selects a new
        // region. Remove this session's notification so it cannot stop a new one.
        MSG notification{};
        while (PeekMessageW(&notification, m_state->notifyWindow, m_state->failureMessage, m_state->failureMessage, PM_REMOVE))
        {
        }
        m_stopping = false;
    }

    uint64_t CaptureMirror::FramesPresented() const noexcept
    {
        return m_state ? m_state->framesPresented.load(std::memory_order_relaxed) : 0;
    }

    std::vector<uint64_t> CaptureMirror::SourceFrames() const
    {
        std::vector<uint64_t> result;
        if (m_state)
        {
            result.reserve(m_state->sourceFrames.size());
            for (const auto& frames : m_state->sourceFrames)
            {
                result.push_back(frames.load(std::memory_order_relaxed));
            }
        }
        return result;
    }

    std::wstring CaptureMirror::Error() const
    {
        if (!m_state)
        {
            return {};
        }
        std::lock_guard lock(m_state->errorMutex);
        return m_state->error;
    }
}
