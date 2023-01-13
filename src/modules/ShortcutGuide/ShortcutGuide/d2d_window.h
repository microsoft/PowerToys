#pragma once
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
#include "d2d_svg.h"

#include <functional>
#include <optional>

class D2DWindow
{
public:
    D2DWindow();
    void show(UINT x, UINT y, UINT width, UINT height);
    void hide();
    void initialize();
    virtual ~D2DWindow();

protected:
    // Implement this:

    // Initialization - called when D2D device needs to be created.
    //   When called all D2DWindow members will be initialized, including d2d_dc
    virtual void init() = 0;
    // resize - when called, window_width and window_height will have current window size
    virtual void resize() = 0;
    // render - called on WM_PAINT, BeginPaint/EndPaint is handled by D2DWindow
    virtual void render(ID2D1DeviceContext5* d2d_dc) = 0;
    // on_show, on_hide - called when the window is about to be shown or about to be hidden
    virtual void on_show() = 0;
    virtual void on_hide() = 0;

    static LRESULT __stdcall d2d_window_proc(HWND window, UINT message, WPARAM wparam, LPARAM lparam);
    static D2DWindow* this_from_hwnd(HWND window);

    void base_init();
    void base_resize(UINT width, UINT height);
    void base_render();
    void render_empty();

    std::recursive_mutex mutex;
    bool hidden = true;
    bool initialized = false;
    HWND hwnd;
    UINT window_width{};
    UINT window_height{};
    winrt::com_ptr<ID3D11Device> d3d_device;
    winrt::com_ptr<IDXGIDevice> dxgi_device;
    winrt::com_ptr<IDXGIFactory2> dxgi_factory;
    winrt::com_ptr<IDXGISwapChain1> dxgi_swap_chain;
    winrt::com_ptr<IDCompositionDevice> composition_device;
    winrt::com_ptr<IDCompositionTarget> composition_target;
    winrt::com_ptr<IDCompositionVisual> composition_visual;
    winrt::com_ptr<IDXGISurface2> dxgi_surface;
    winrt::com_ptr<ID2D1Bitmap1> d2d_bitmap;
    winrt::com_ptr<ID2D1Factory6> d2d_factory;
    winrt::com_ptr<ID2D1Device5> d2d_device;
    winrt::com_ptr<ID2D1DeviceContext5> d2d_dc;
};
