#include "pch.h"
#include <CppUnitTest.h>
#include <dwmapi.h>

#include "CaptureFrameWait.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CaptureFrameWaitTests
{
    class CaptureFixture
    {
    public:
        CaptureFixture()
        try
        {
            winrt::init_apartment(winrt::apartment_type::single_threaded);
            Assert::IsTrue(winrt::GraphicsCaptureSession::IsSupported(), L"An interactive Windows capture desktop is required.");

            m_module.reset(LoadLibraryW(L"d3d11.dll"));
            Assert::IsNotNull(m_module.get());
            const auto createDevice = reinterpret_cast<decltype(&D3D11CreateDevice)>(
                GetProcAddress(m_module.get(), "D3D11CreateDevice"));
            const auto createWinrtDevice = reinterpret_cast<decltype(&CreateDirect3D11DeviceFromDXGIDevice)>(
                GetProcAddress(m_module.get(), "CreateDirect3D11DeviceFromDXGIDevice"));
            Assert::IsNotNull(createDevice);
            Assert::IsNotNull(createWinrtDevice);
            winrt::check_hresult(createDevice(
                nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT, nullptr, 0, D3D11_SDK_VERSION, m_device.put(), nullptr, m_context.put()));
            const auto multithread = m_context.as<ID3D11Multithread>();
            multithread->SetMultithreadProtected(TRUE);
            winrt::com_ptr<IInspectable> inspectable;
            winrt::check_hresult(createWinrtDevice(m_device.as<IDXGIDevice>().get(), inspectable.put()));
            m_winrtDevice = inspectable.as<winrt::IDirect3DDevice>();

            WNDCLASSW windowClass{};
            windowClass.lpfnWndProc = WindowProc;
            windowClass.hInstance = GetModuleHandleW(nullptr);
            windowClass.lpszClassName = L"ZoomItCaptureFrameTests";
            Assert::IsTrue(RegisterClassW(&windowClass) != 0);
            m_window.reset(CreateWindowExW(
                WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, windowClass.lpszClassName, L"ZoomIt recording frame test", WS_POPUP | WS_VISIBLE, 40, 40, 320, 180, nullptr, nullptr, windowClass.hInstance, nullptr));
            Assert::IsNotNull(m_window.get());
            Paint(32);
            const auto item = robmikh::common::desktop::CreateCaptureItemForWindow(m_window.get());
            m_capture = std::make_unique<CaptureFrameWait>(m_winrtDevice, item, item.Size());
            m_capture->EnableCursorCapture(false);
        }
        catch (const winrt::hresult_error& error)
        {
            wchar_t message[512]{};
            swprintf_s(message, L"Capture fixture failed: 0x%08X %s", static_cast<unsigned>(error.code()), error.message().c_str());
            Assert::Fail(message);
        }

        ~CaptureFixture()
        {
            m_capture.reset();
            m_window.reset();
            UnregisterClassW(L"ZoomItCaptureFrameTests", GetModuleHandleW(nullptr));
            m_winrtDevice = nullptr;
            m_context = nullptr;
            m_device = nullptr;
            winrt::uninit_apartment();
        }

        CaptureFrameWait& Capture() { return *m_capture; }

        void Paint(BYTE value)
        {
            SetWindowLongPtrW(m_window.get(), GWLP_USERDATA, value);
            Assert::IsTrue(RedrawWindow(m_window.get(), nullptr, nullptr, RDW_INVALIDATE | RDW_UPDATENOW) != FALSE);
            GdiFlush();
            winrt::check_hresult(DwmFlush());
        }

        BYTE Read(const CaptureFrame& frame)
        {
            const auto texture = GetDXGIInterfaceFromObject<ID3D11Texture2D>(frame.FrameTexture);
            D3D11_TEXTURE2D_DESC desc{};
            texture->GetDesc(&desc);
            desc.Usage = D3D11_USAGE_STAGING;
            desc.BindFlags = 0;
            desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
            desc.MiscFlags = 0;
            winrt::com_ptr<ID3D11Texture2D> staging;
            winrt::check_hresult(m_device->CreateTexture2D(&desc, nullptr, staging.put()));
            m_context->CopyResource(staging.get(), texture.get());
            D3D11_MAPPED_SUBRESOURCE mapped{};
            winrt::check_hresult(m_context->Map(staging.get(), 0, D3D11_MAP_READ, 0, &mapped));
            const auto value = static_cast<const BYTE*>(mapped.pData)[90 * mapped.RowPitch + 160 * 4];
            m_context->Unmap(staging.get(), 0);
            return value;
        }

        CaptureFrame Next(BYTE value)
        {
            const auto deadline = GetTickCount64() + 3000;
            int observed = -1;
            while (GetTickCount64() < deadline)
            {
                auto frame = m_capture->TryGetNextFrame(100);
                if (frame)
                {
                    observed = Read(*frame);
                    if (observed == value)
                    {
                        return *frame;
                    }
                }
            }
            wchar_t message[128]{};
            swprintf_s(message, L"Animation state %u was not captured; last pixel value was %d.", value, observed);
            Assert::Fail(message);
            return {};
        }

        void WaitForPendingFrame()
        {
            const auto deadline = GetTickCount64() + 3000;
            while (GetTickCount64() < deadline)
            {
                if (m_capture->PeekCurrentFrame())
                {
                    return;
                }
                Sleep(1);
            }
            Assert::Fail(L"Capture did not deliver a frame while the consumer held the preceding frame.");
        }

    private:
        static LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wordParam, LPARAM longParam)
        {
            if (message == WM_PAINT)
            {
                PAINTSTRUCT paint{};
                const auto dc = BeginPaint(window, &paint);
                const auto value = static_cast<BYTE>(GetWindowLongPtrW(window, GWLP_USERDATA));
                wil::unique_hbrush brush{ CreateSolidBrush(RGB(value, value, value)) };
                FillRect(dc, &paint.rcPaint, brush.get());
                EndPaint(window, &paint);
                return 0;
            }
            return DefWindowProcW(window, message, wordParam, longParam);
        }

        wil::unique_hmodule m_module;
        winrt::com_ptr<ID3D11Device> m_device;
        winrt::com_ptr<ID3D11DeviceContext> m_context;
        winrt::IDirect3DDevice m_winrtDevice{ nullptr };
        wil::unique_hwnd m_window;
        std::unique_ptr<CaptureFrameWait> m_capture;
    };

    TEST_CLASS (FrameDeliveryTests)
    {
    public:
        TEST_METHOD (AlreadyArrivedFrameIsReturnedWithoutAnotherDesktopUpdate)
        {
            CaptureFixture fixture;
            fixture.WaitForPendingFrame();
            const auto pending = fixture.Capture().PeekCurrentFrame();
            const auto frame = fixture.Capture().TryGetNextFrame(0);
            Assert::IsTrue(frame.has_value(), L"A queued frame must not be discarded before waiting.");
            Assert::AreEqual(pending->SystemRelativeTime.count(), frame->SystemRelativeTime.count());
        }

        TEST_METHOD (FramesRemainValidAndOrderedWhileConsumerIsBusy)
        {
            CaptureFixture fixture;
            const auto first = fixture.Next(32);
            fixture.Capture().DiscardPendingFrames();
            fixture.Paint(96);
            fixture.WaitForPendingFrame();
            const auto second = fixture.Capture().PeekCurrentFrame();
            Assert::AreEqual(static_cast<BYTE>(96), fixture.Read(*second));
            fixture.Paint(160);
            Sleep(100);
            const auto deliveredSecond = fixture.Capture().TryGetNextFrame(0);
            Assert::IsTrue(deliveredSecond.has_value());
            Assert::AreEqual(second->SystemRelativeTime.count(), deliveredSecond->SystemRelativeTime.count());
            const auto third = fixture.Next(160);
            Assert::IsTrue(first.SystemRelativeTime < second->SystemRelativeTime);
            Assert::IsTrue(second->SystemRelativeTime < third.SystemRelativeTime);
            Assert::AreEqual(static_cast<BYTE>(32), fixture.Read(first), L"Advancing capture must not recycle a frame still in use.");
            Assert::AreEqual(static_cast<BYTE>(96), fixture.Read(*deliveredSecond));
        }

        TEST_METHOD (EveryPresentedAnimationStateIsDelivered)
        {
            CaptureFixture fixture;
            auto previous = fixture.Next(32).SystemRelativeTime;
            for (unsigned int index = 0; index < 120; ++index)
            {
                const auto value = static_cast<BYTE>(48 + index);
                fixture.Paint(value);
                const auto frame = fixture.Next(value);
                Assert::IsTrue(frame.SystemRelativeTime > previous);
                previous = frame.SystemRelativeTime;
            }
        }

        TEST_METHOD (StoppingCaptureUnblocksConsumerAndIsIdempotent)
        {
            CaptureFixture fixture;
            auto consumer = std::async(std::launch::async, [&] {
                winrt::init_apartment(winrt::apartment_type::multi_threaded);
                auto uninitialize = wil::scope_exit([] { winrt::uninit_apartment(); });
                while (fixture.Capture().TryGetNextFrame())
                {
                }
            });
            fixture.Capture().StopCapture();
            Assert::IsTrue(consumer.wait_for(std::chrono::seconds(2)) == std::future_status::ready);
            consumer.get();
            fixture.Capture().StopCapture();
            Assert::IsFalse(fixture.Capture().TryGetNextFrame().has_value());
            Assert::IsFalse(fixture.Capture().PeekCurrentFrame().has_value());
        }

        TEST_METHOD (TimedAnimationDeliversEveryStateWithoutProducerHandshake)
        {
            CaptureFixture fixture;
            fixture.Next(32);
            constexpr unsigned int frameCount = 60;
            auto consumer = std::async(std::launch::async, [&] {
                winrt::init_apartment(winrt::apartment_type::multi_threaded);
                auto uninitialize = wil::scope_exit([] { winrt::uninit_apartment(); });
                std::vector<BYTE> states;
                const auto deadline = GetTickCount64() + 10000;
                while (states.size() < frameCount && GetTickCount64() < deadline)
                {
                    if (const auto frame = fixture.Capture().TryGetNextFrame(100))
                    {
                        const auto value = fixture.Read(*frame);
                        if (value >= 48 && value < 48 + frameCount &&
                            (states.empty() || value != states.back()))
                        {
                            states.push_back(value);
                        }
                    }
                }
                return states;
            });
            const auto start = std::chrono::steady_clock::now();
            for (unsigned int index = 0; index < frameCount; ++index)
            {
                fixture.Paint(static_cast<BYTE>(48 + index));
                std::this_thread::sleep_until(start + std::chrono::milliseconds((index + 1) * 50));
            }
            const auto states = consumer.get();
            Assert::AreEqual(static_cast<size_t>(frameCount), states.size(), L"Capture must deliver every state of the timed 20 FPS animation.");
            for (unsigned int index = 0; index < frameCount; ++index)
            {
                Assert::AreEqual(static_cast<BYTE>(48 + index), states[index], L"An animation state was skipped or delivered out of order.");
            }
        }
    };
}
