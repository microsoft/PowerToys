#pragma once
#include "Drawing.h"

#include <winrt/base.h>
#include <Windows.h>
#include <dxgi1_3.h>
#include <d3d11_2.h>
#include <d2d1_3.h>
#include <d2d1_3helper.h>
#include <d2d1helper.h>
#include <dcomp.h>
#include <dwmapi.h>
#include <string>

class CompositionDrawing : public Drawing
{
public:
    void Init(HWND window);
    void BeginDraw();
    void EndDraw();

private:
    winrt::com_ptr<ID3D11Device> m_d3dDevice;
    winrt::com_ptr<IDXGIDevice> m_dxgiDevice;
    winrt::com_ptr<IDXGIFactory2> m_dxgiFactory;
    winrt::com_ptr<IDXGISwapChain1> m_dxgiSwapChain;
    winrt::com_ptr<IDCompositionDevice> m_compositionDevice;
    winrt::com_ptr<IDCompositionTarget> m_compositionTarget;
    winrt::com_ptr<IDCompositionVisual> m_compositionVisual;
    winrt::com_ptr<IDXGISurface2> m_dxgiSurface;
    winrt::com_ptr<ID2D1Bitmap1> m_d2dBitmap;
    winrt::com_ptr<ID2D1Factory6> m_d2dFactory;
    winrt::com_ptr<ID2D1Device5> m_d2dDevice;
};