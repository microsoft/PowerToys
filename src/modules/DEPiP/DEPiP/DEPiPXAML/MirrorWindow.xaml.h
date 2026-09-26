#pragma once

#include "../DisplayCapture.h"
#include "MirrorWindow.g.h"

namespace winrt::DEPiP::implementation
{
    struct MirrorWindow : MirrorWindowT<MirrorWindow>
    {
        MirrorWindow();
        ~MirrorWindow();
        void Initialize(DisplayInfo source);

    private:
        static LRESULT CALLBACK WindowSubclass(
            HWND window,
            UINT message,
            WPARAM wordParam,
            LPARAM longParam,
            UINT_PTR subclassId,
            DWORD_PTR referenceData);
        void CreateGraphics();
        void StartCapture();
        void StopCapture();
        void OnFrameArrived(
            Windows::Graphics::Capture::Direct3D11CaptureFramePool const& sender,
            Windows::Foundation::IInspectable const&);
        void OnCompositionScaleChanged(
            Microsoft::UI::Xaml::Controls::SwapChainPanel const&,
            Windows::Foundation::IInspectable const&);
        void OnCapturePanelSizeChanged(
            Windows::Foundation::IInspectable const&,
            Microsoft::UI::Xaml::SizeChangedEventArgs const&);
        void UpdateSwapChainScale();
        void ReloadSettings();
        void ApplySettings();
        void ApplyAspectRatio(WPARAM edge, RECT& proposed);
        void StartEventWatcher();

        DisplayInfo m_source;
        DEPiPSettings m_settings;
        HWND m_window = nullptr;
        std::mutex m_captureMutex;
        std::jthread m_eventWatcher;
        winrt::com_ptr<ID3D11Device> m_device;
        winrt::com_ptr<ID3D11DeviceContext> m_context;
        winrt::com_ptr<IDXGISwapChain2> m_swapChain;
        Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice m_winrtDevice{ nullptr };
        Windows::Graphics::Capture::GraphicsCaptureItem m_captureItem{ nullptr };
        Windows::Graphics::Capture::Direct3D11CaptureFramePool m_framePool{ nullptr };
        Windows::Graphics::Capture::GraphicsCaptureSession m_session{ nullptr };
        winrt::event_token m_frameArrivedToken{};
        winrt::event_token m_closedToken{};
        winrt::event_token m_compositionScaleChangedToken{};
        winrt::event_token m_sizeChangedToken{};
    };
}

namespace winrt::DEPiP::factory_implementation
{
    struct MirrorWindow : MirrorWindowT<MirrorWindow, implementation::MirrorWindow>
    {
    };
}
