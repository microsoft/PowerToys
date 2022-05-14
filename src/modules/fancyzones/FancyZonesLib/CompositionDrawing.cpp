#include "pch.h"
#include "CompositionDrawing.h"
#include <common/logger/logger.h>


void CompositionDrawing::Init(HWND window)
{
    m_window = window;

    // Obtain the size of the drawing area
    if (!GetClientRect(window, m_renderRect.get()))
    {
        Logger::error("couldn't initialize CompositionDrawing: GetClientRect failed");
        return;
    }

    // Create devices
    m_d2dFactory.copy_from(GetD2DFactory());
    if (!m_d2dFactory)
    {
        return;
    }

    D3D11CreateDevice(
        nullptr,
        D3D_DRIVER_TYPE_HARDWARE,
        nullptr,
        D3D11_CREATE_DEVICE_BGRA_SUPPORT,
        nullptr,
        0,
        D3D11_SDK_VERSION,
        m_d3dDevice.put(),
        nullptr,
        nullptr);
    if (!m_d3dDevice)
    {
        return;
    }

    m_d3dDevice->QueryInterface(__uuidof(m_dxgiDevice), m_dxgiDevice.put_void());
    if (!m_dxgiDevice)
    {
        return;
    }

    CreateDXGIFactory2(0, __uuidof(m_dxgiFactory), m_dxgiFactory.put_void());
    if (!m_dxgiFactory)
    {
        return;
    }

    m_d2dFactory->CreateDevice(m_dxgiDevice.get(), m_d2dDevice.put());
    if (!m_d2dDevice)
    {
        return;
    }

    winrt::com_ptr<ID2D1DeviceContext5> device_context;
    m_d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, device_context.put());
    if (!device_context)
    {
        return;
    }

    m_renderTarget = device_context;

    // Size specific
    if (m_renderRect.width() == 0 || m_renderRect.height() == 0)
    {
        return;
    }

    DXGI_SWAP_CHAIN_DESC1 sc_description = {};
    sc_description.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    sc_description.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    sc_description.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
    sc_description.BufferCount = 2;
    sc_description.SampleDesc.Count = 1;
    sc_description.AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED;
    sc_description.Width = m_renderRect.width();
    sc_description.Height = m_renderRect.height();

    m_dxgiFactory->CreateSwapChainForComposition(
        m_dxgiDevice.get(),
        &sc_description,
        nullptr,
        m_dxgiSwapChain.put());
    if (!m_dxgiSwapChain)
    {
        return;
    }

    DCompositionCreateDevice(
        m_dxgiDevice.get(),
        __uuidof(m_compositionDevice),
        m_compositionDevice.put_void());
    if (!m_compositionDevice)
    {
        return;
    }

    m_compositionDevice->CreateTargetForHwnd(m_window, true, m_compositionTarget.put());
    if (!m_compositionTarget)
    {
        return;
    }

    m_compositionDevice->CreateVisual(m_compositionVisual.put());
    if (!m_compositionVisual)
    {
        return;
    }

    m_compositionVisual->SetContent(m_dxgiSwapChain.get());
    m_compositionTarget->SetRoot(m_compositionVisual.get());

    m_dxgiSwapChain->GetBuffer(0, __uuidof(m_dxgiSurface), m_dxgiSurface.put_void());
    if (!m_dxgiSurface)
    {
        return;
    }

    D2D1_BITMAP_PROPERTIES1 properties = {};
    properties.pixelFormat.alphaMode = D2D1_ALPHA_MODE_PREMULTIPLIED;
    properties.pixelFormat.format = DXGI_FORMAT_B8G8R8A8_UNORM;
    properties.bitmapOptions = D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW;

    device_context->CreateBitmapFromDxgiSurface(
        m_dxgiSurface.get(),
        properties,
        m_d2dBitmap.put());
    if (!m_d2dBitmap)
    {
        return;
    }

    device_context->SetTarget(m_d2dBitmap.get());
}

CompositionDrawing::operator bool() const
{
    return Drawing::operator bool() && bool(m_dxgiSwapChain) && bool(m_compositionDevice);
}

void CompositionDrawing::BeginDraw()
{
    if (*this)
    {
        m_renderTarget->BeginDraw();
        m_renderTarget->Clear();
    }
}

void CompositionDrawing::EndDraw()
{
    if (*this)
    {
        m_renderTarget->EndDraw();

        m_dxgiSwapChain->Present(1, 0);
        m_compositionDevice->Commit();
    }
}