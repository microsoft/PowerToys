// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "CaptureMirror.h"

#include <d3d11.h>
#include <dxgi1_2.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>

#include <atomic>
#include <cwchar>
#include <exception>
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

        void ValidateRectangle(const RECT& region, const RECT& monitor)
        {
            if (region.left >= region.right || region.top >= region.bottom ||
                region.left < monitor.left || region.top < monitor.top ||
                region.right > monitor.right || region.bottom > monitor.bottom)
            {
                throw winrt::hresult_invalid_argument(L"The capture rectangle must be fully inside one source monitor.");
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
        HWND targetWindow{};
        HMONITOR sourceMonitor{};
        RECT sourceRect{};
        RECT monitorRect{};
        HWND notifyWindow{};
        UINT failureMessage{};
        std::shared_ptr<Signals> signals = std::make_shared<Signals>();
        std::thread worker;
        std::atomic<uint64_t> framesPresented{};
        mutable std::mutex errorMutex;
        std::wstring error;

        bool Stopped() const noexcept
        {
            return WaitForSingleObject(signals->stop.Get(), 0) == WAIT_OBJECT_0;
        }

        void CheckGeometry() const
        {
            const auto currentRect = MonitorRectangle(sourceMonitor);
            if (!EqualRect(&monitorRect, &currentRect))
            {
                throw winrt::hresult_error(E_ABORT, L"The source monitor moved or changed resolution. Select the region again.");
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

            CaptureSession capture;
            auto interop = winrt::get_activation_factory<GraphicsCaptureItem, IGraphicsCaptureItemInterop>();
            winrt::check_hresult(interop->CreateForMonitor(sourceMonitor, winrt::guid_of<GraphicsCaptureItem>(), winrt::put_abi(capture.item)));
            const auto size = capture.item.Size();
            const auto monitorWidth = static_cast<int64_t>(monitorRect.right) - monitorRect.left;
            const auto monitorHeight = static_cast<int64_t>(monitorRect.bottom) - monitorRect.top;
            if (size.Width != monitorWidth || size.Height != monitorHeight)
            {
                throw winrt::hresult_error(E_ABORT, L"The captured monitor dimensions do not match the selected region.");
            }

            // Capture coordinates start at the monitor origin, even when its
            // virtual-screen coordinates are negative or it uses a different DPI.
            D3D11_BOX crop{};
            crop.left = static_cast<UINT>(static_cast<int64_t>(sourceRect.left) - monitorRect.left);
            crop.top = static_cast<UINT>(static_cast<int64_t>(sourceRect.top) - monitorRect.top);
            crop.right = static_cast<UINT>(static_cast<int64_t>(sourceRect.right) - monitorRect.left);
            crop.bottom = static_cast<UINT>(static_cast<int64_t>(sourceRect.bottom) - monitorRect.top);
            crop.back = 1;

            winrt::com_ptr<IDXGIAdapter> adapter;
            winrt::check_hresult(dxgiDevice->GetAdapter(adapter.put()));
            winrt::com_ptr<IDXGIFactory2> factory;
            winrt::check_hresult(adapter->GetParent(winrt::guid_of<IDXGIFactory2>(), factory.put_void()));
            DXGI_SWAP_CHAIN_DESC1 description{};
            description.Width = crop.right - crop.left;
            description.Height = crop.bottom - crop.top;
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

            capture.pool = Direct3D11CaptureFramePool::CreateFreeThreaded(winrtDevice, DirectXPixelFormat::B8G8R8A8UIntNormalized, 2, size);
            const auto events = signals;
            capture.closedRevoker = capture.item.Closed(winrt::auto_revoke, [events](auto&&, auto&&) noexcept {
                events->closed.Signal();
            });
            capture.frameRevoker = capture.pool.FrameArrived(winrt::auto_revoke, [events](auto&&, auto&&) noexcept {
                events->frame.Signal();
            });
            capture.session = capture.pool.CreateCaptureSession(capture.item);
            capture.session.IsCursorCaptureEnabled(true);
            // Leave the system capture border enabled for this proof of concept.
            capture.session.StartCapture();

            const HANDLE waits[]{ signals->stop.Get(), signals->closed.Get(), signals->frame.Get() };
            while (!Stopped())
            {
                const auto result = WaitForMultipleObjects(ARRAYSIZE(waits), waits, false, 1000);
                if (result == WAIT_OBJECT_0)
                {
                    return;
                }
                if (result == WAIT_OBJECT_0 + 1)
                {
                    throw winrt::hresult_error(E_ABORT, L"The source monitor is no longer available.");
                }
                if (result == WAIT_FAILED)
                {
                    winrt::throw_last_error();
                }
                CheckGeometry();
                if (result == WAIT_TIMEOUT)
                {
                    continue;
                }

                // Drain queued frames. FrameArrived itself does no rendering, so
                // the immediate D3D context is only ever used by this worker.
                while (!Stopped())
                {
                    auto frame = capture.pool.TryGetNextFrame();
                    if (!frame)
                    {
                        break;
                    }
                    CheckGeometry();
                    const auto content = frame.ContentSize();
                    if (content.Width != size.Width || content.Height != size.Height)
                    {
                        throw winrt::hresult_error(E_ABORT, L"The source capture size changed. Select the region again.");
                    }
                    auto access = frame.Surface().as<::Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();
                    winrt::com_ptr<ID3D11Texture2D> texture;
                    winrt::check_hresult(access->GetInterface(winrt::guid_of<ID3D11Texture2D>(), texture.put_void()));
                    D3D11_TEXTURE2D_DESC textureDescription{};
                    texture->GetDesc(&textureDescription);
                    if (crop.right > textureDescription.Width || crop.bottom > textureDescription.Height)
                    {
                        throw winrt::hresult_error(E_ABORT, L"The capture texture no longer contains the selected region.");
                    }
                    winrt::com_ptr<ID3D11Texture2D> backBuffer;
                    winrt::check_hresult(swapChain->GetBuffer(0, winrt::guid_of<ID3D11Texture2D>(), backBuffer.put_void()));
                    context->CopySubresourceRegion(backBuffer.get(), 0, 0, 0, 0, texture.get(), 0, &crop);
                    frame.Close();

                    const auto presentResult = swapChain->Present(1, DXGI_PRESENT_DO_NOT_WAIT);
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
                    }
                    else if (presentResult == DXGI_STATUS_OCCLUDED)
                    {
                        WaitForSingleObject(signals->stop.Get(), 100);
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

    void CaptureMirror::Start(HWND targetWindow, HMONITOR sourceMonitor, RECT sourceRect, HWND notifyWindow, UINT failureMessage)
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
        const auto monitorRect = MonitorRectangle(sourceMonitor);
        ValidateRectangle(sourceRect, monitorRect);

        auto state = std::make_unique<State>();
        state->targetWindow = targetWindow;
        state->sourceMonitor = sourceMonitor;
        state->sourceRect = sourceRect;
        state->monitorRect = monitorRect;
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
