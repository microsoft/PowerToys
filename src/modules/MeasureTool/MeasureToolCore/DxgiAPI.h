#pragma once
#include <d2d1_3.h>
#include <d3d11_4.h>
#include <dcomp.h>
#include <dxgi1_3.h>
#include <inspectable.h>

// Suppressing 26466 - Don't use static_cast downcasts - in base.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include <winrt/base.h>
#pragma warning(pop)


struct DxgiWindowState
{
    winrt::com_ptr<ID2D1RenderTarget> rt;
    winrt::com_ptr<IDXGISwapChain1> swapChain;
    winrt::com_ptr<IDCompositionTarget> compositionTarget;
};

struct DxgiAPI final
{
    struct D3D
    {
        winrt::com_ptr<ID3D11Device> d3dDevice;
        winrt::com_ptr<IDXGIDevice> dxgiDevice;
        winrt::com_ptr<IInspectable> d3dDeviceInspectable;
        winrt::com_ptr<IDXGIFactory2> dxgiFactory2;
        winrt::com_ptr<ID3D11DeviceContext> d3dContext;
    };

    winrt::com_ptr<ID2D1Factory2> d2dFactory2;
    winrt::com_ptr<IDWriteFactory> writeFactory;

    winrt::com_ptr<ID3D11Device> d3dDevice;
    winrt::com_ptr<IDXGIDevice> dxgiDevice;
    winrt::com_ptr<IInspectable> d3dDeviceInspectable;
    winrt::com_ptr<IDXGIFactory2> dxgiFactory2;
    winrt::com_ptr<ID3D11DeviceContext> d3dContext;

    D3D d3dForCapture;

    winrt::com_ptr<ID2D1Device1> d2dDevice1;
    winrt::com_ptr<IDCompositionDevice> compositionDevice;

    DxgiAPI();

    enum class Uninitialized
    {
    };
    explicit inline DxgiAPI(Uninitialized) {}

    DxgiWindowState CreateD2D1RenderTarget(HWND window) const;
};