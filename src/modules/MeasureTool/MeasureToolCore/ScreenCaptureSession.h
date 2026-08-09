#pragma once

#include "BGRATextureView.h"
#include "DxgiAPI.h"

#include <common/Display/monitors.h>

#include <functional>
#include <mutex>

class ScreenCaptureSession final
{
public:
    static std::unique_ptr<ScreenCaptureSession> Create(
        DxgiAPI* dxgiAPI,
        MonitorInfo monitorInfo,
        winrt::Windows::Graphics::DirectX::DirectXPixelFormat pixelFormat,
        bool continuousCapture);

    ~ScreenCaptureSession();

    void StartCapture(std::function<void(MappedTextureView)> frameCallback);
    MappedTextureView CaptureSingleFrame(bool skipInitialFrame = false);
    void StopCapture();

private:
    ScreenCaptureSession(
        DxgiAPI* dxgiAPI,
        winrt::com_ptr<IDXGISwapChain1> swapChain,
        winrt::Windows::Graphics::DirectX::DirectXPixelFormat pixelFormat,
        MonitorInfo monitorInfo,
        bool continuousCapture);

    winrt::com_ptr<ID3D11Texture2D> CopyFrameToCPU(const winrt::com_ptr<ID3D11Texture2D>& texture);
    void OnFrameArrived(
        const winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool& sender,
        const winrt::Windows::Foundation::IInspectable&);
    void StartSessionInPreferredMode();
    void StopCaptureLocked();

    DxgiAPI* _dxgiAPI = nullptr;
    winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice _device{ nullptr };
    winrt::com_ptr<IDXGISwapChain1> _swapChain;
    winrt::Windows::Graphics::SizeInt32 _frameSize{};
    HMONITOR _monitor = {};
    winrt::Windows::Graphics::DirectX::DirectXPixelFormat _pixelFormat;
    winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool _framePool{ nullptr };
    winrt::Windows::Graphics::Capture::GraphicsCaptureSession _session{ nullptr };
    std::function<void(MappedTextureView)> _frameCallback;
    Box _monitorArea;
    bool _continuousCapture = false;
    std::mutex _frameArrivedMutex;
};
