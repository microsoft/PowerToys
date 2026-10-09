#include "pch.h"

#include "DxgiAPI.h"

#include <common/Display/dpi_aware.h>

//#define DEBUG_DEVICES
#define SEPARATE_D3D_FOR_CAPTURE

namespace
{
    DxgiAPI::D3D CreateD3D()
    {
        DxgiAPI::D3D d3d;
        UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
#if defined(DEBUG_DEVICES)
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
                              d3d.d3dDevice.put(),
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
                                   d3d.d3dDevice.put(),
                                   nullptr,
                                   nullptr);
        }
        winrt::check_hresult(hr);

        d3d.dxgiDevice = d3d.d3dDevice.as<IDXGIDevice>();
        winrt::check_hresult(CreateDirect3D11DeviceFromDXGIDevice(d3d.dxgiDevice.get(), d3d.d3dDeviceInspectable.put()));

        winrt::com_ptr<IDXGIAdapter> adapter;
        winrt::check_hresult(d3d.dxgiDevice->GetParent(winrt::guid_of<IDXGIAdapter>(), adapter.put_void()));
        winrt::check_hresult(adapter->GetParent(winrt::guid_of<IDXGIFactory2>(), d3d.dxgiFactory2.put_void()));

        d3d.d3dDevice->GetImmediateContext(d3d.d3dContext.put());
        winrt::check_bool(d3d.d3dContext);
        auto contextMultithread = d3d.d3dContext.as<ID3D11Multithread>();
        contextMultithread->SetMultithreadProtected(true);

        return d3d;
    }
}

DxgiAPI::DxgiAPI()
{
    const D2D1_FACTORY_OPTIONS d2dFactoryOptions = {
#if defined(DEBUG_DEVICES)
        D2D1_DEBUG_LEVEL_INFORMATION
#else
        D2D1_DEBUG_LEVEL_NONE
#endif
    };

    winrt::check_hresult(D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, d2dFactoryOptions, d2dFactory2.put()));

    winrt::check_hresult(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED,
                                             winrt::guid_of<IDWriteFactory>(),
                                             reinterpret_cast<IUnknown**>(writeFactory.put())));

    auto d3d = CreateD3D();
    d3dDevice = d3d.d3dDevice;
    dxgiDevice = d3d.dxgiDevice;
    d3dDeviceInspectable = d3d.d3dDeviceInspectable;
    dxgiFactory2 = d3d.dxgiFactory2;
    d3dContext = d3d.d3dContext;
#if defined(SEPARATE_D3D_FOR_CAPTURE)
    auto d3dFC = CreateD3D();
    d3dForCapture = d3dFC;
#else
    d3dForCapture = d3d;
#endif
    winrt::check_hresult(d2dFactory2->CreateDevice(dxgiDevice.get(), d2dDevice1.put()));
    winrt::check_hresult(DCompositionCreateDevice(
        dxgiDevice.get(),
        winrt::guid_of<IDCompositionDevice>(),
        compositionDevice.put_void()));
}

DxgiWindowState DxgiAPI::CreateD2D1RenderTarget(HWND window) const
{
    RECT rect = {};
    winrt::check_bool(GetClientRect(window, &rect));

    const DXGI_SWAP_CHAIN_DESC1 desc = {
        .Width = static_cast<UINT>(rect.right - rect.left),
        .Height = static_cast<UINT>(rect.bottom - rect.top),
        .Format = static_cast<DXGI_FORMAT>(winrt::DirectXPixelFormat::B8G8R8A8UIntNormalized),
        .SampleDesc = { .Count = 1, .Quality = 0 },
        .BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
        .BufferCount = 2,
        .Scaling = DXGI_SCALING_STRETCH,
        .SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD,
        .AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED,
    };

    DxgiWindowState state;
    winrt::com_ptr<ID2D1DeviceContext> rt;
    d2dDevice1->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, rt.put());
    state.rt = rt;

    winrt::check_hresult(dxgiFactory2->CreateSwapChainForComposition(d3dDevice.get(),
                                                                     &desc,
                                                                     nullptr,
                                                                     state.swapChain.put()));
    winrt::com_ptr<IDXGISurface> surface;
    winrt::check_hresult(state.swapChain->GetBuffer(0, winrt::guid_of<IDXGISurface>(), surface.put_void()));

    const D2D1_BITMAP_PROPERTIES1 properties = {
        .pixelFormat = { .format = DXGI_FORMAT_B8G8R8A8_UNORM, .alphaMode = D2D1_ALPHA_MODE_PREMULTIPLIED },
        .bitmapOptions = D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW
    };
    winrt::com_ptr<ID2D1Bitmap1> bitmap;
    winrt::check_hresult(rt->CreateBitmapFromDxgiSurface(surface.get(),
                                                         properties,
                                                         bitmap.put()));
    rt->SetTarget(bitmap.get());
    winrt::check_hresult(compositionDevice->CreateTargetForHwnd(window,
                                                                true,
                                                                state.compositionTarget.put()));

    winrt::com_ptr<IDCompositionVisual> visual;
    winrt::check_hresult(compositionDevice->CreateVisual(visual.put()));
    winrt::check_hresult(visual->SetContent(state.swapChain.get()));
    winrt::check_hresult(state.compositionTarget->SetRoot(visual.get()));
    winrt::check_hresult(compositionDevice->Commit());

    return state;
}
