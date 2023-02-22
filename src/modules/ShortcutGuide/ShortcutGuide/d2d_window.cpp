#include "pch.h"
#include "d2d_window.h"

#include <common/utils/resources.h>

D2DWindow::D2DWindow()
{
    static const WCHAR* class_name = L"PToyD2DPopup";
    WNDCLASS wc = {};
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hInstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
    wc.lpszClassName = class_name;
    wc.style = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc = d2d_window_proc;
    RegisterClass(&wc);
    hwnd = CreateWindowExW(WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOREDIRECTIONBITMAP | WS_EX_LAYERED,
                           wc.lpszClassName,
                           L"PToyD2DPopup",
                           WS_POPUP | WS_VISIBLE,
                           CW_USEDEFAULT,
                           CW_USEDEFAULT,
                           CW_USEDEFAULT,
                           CW_USEDEFAULT,
                           nullptr,
                           nullptr,
                           wc.hInstance,
                           this);
    WINRT_VERIFY(hwnd);
}

void D2DWindow::show(UINT x, UINT y, UINT width, UINT height)
{
    if (!initialized)
    {
        base_init();
    }
    base_resize(width, height);
    render_empty();
    hidden = false;
    on_show();
    SetWindowPos(hwnd, HWND_TOPMOST, x, y, width, height, 0);
    ShowWindow(hwnd, SW_SHOWNORMAL);
    SetForegroundWindow(hwnd);
    UpdateWindow(hwnd);
}

void D2DWindow::hide()
{
    hidden = true;
    ShowWindow(hwnd, SW_HIDE);
    on_hide();
}

void D2DWindow::initialize()
{
    base_init();
}

void D2DWindow::base_init()
{
    std::unique_lock lock(mutex);
    // D2D1Factory is independent from the device, no need to recreate it if we need to recreate the device.
    if (!d2d_factory)
    {
#ifdef _DEBUG
        D2D1_FACTORY_OPTIONS options = { D2D1_DEBUG_LEVEL_INFORMATION };
#else
        D2D1_FACTORY_OPTIONS options = {};
#endif
        winrt::check_hresult(D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED,
                                               __uuidof(d2d_factory),
                                               &options,
                                               d2d_factory.put_void()));
    }
    // For all other stuff - assign nullptr first to release the object, to reset the com_ptr.
    d2d_dc = nullptr;
    d2d_device = nullptr;
    dxgi_factory = nullptr;
    dxgi_device = nullptr;
    d3d_device = nullptr;
    winrt::check_hresult(D3D11CreateDevice(nullptr,
                                           D3D_DRIVER_TYPE_HARDWARE,
                                           nullptr,
                                           D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                                           nullptr,
                                           0,
                                           D3D11_SDK_VERSION,
                                           d3d_device.put(),
                                           nullptr,
                                           nullptr));
    winrt::check_hresult(d3d_device->QueryInterface(__uuidof(dxgi_device), dxgi_device.put_void()));
    winrt::check_hresult(CreateDXGIFactory2(0, __uuidof(dxgi_factory), dxgi_factory.put_void()));
    winrt::check_hresult(d2d_factory->CreateDevice(dxgi_device.get(), d2d_device.put()));
    winrt::check_hresult(d2d_device->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, d2d_dc.put()));
    init();
    initialized = true;
}

void D2DWindow::base_resize(UINT width, UINT height)
{
    std::unique_lock lock(mutex);
    if (!initialized)
    {
        return;
    }
    window_width = width;
    window_height = height;
    if (window_width == 0 || window_height == 0)
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
    sc_description.Width = window_width;
    sc_description.Height = window_height;
    dxgi_swap_chain = nullptr;
    winrt::check_hresult(dxgi_factory->CreateSwapChainForComposition(dxgi_device.get(),
                                                                     &sc_description,
                                                                     nullptr,
                                                                     dxgi_swap_chain.put()));
    composition_device = nullptr;
    winrt::check_hresult(DCompositionCreateDevice(dxgi_device.get(),
                                                  __uuidof(composition_device),
                                                  composition_device.put_void()));

    composition_target = nullptr;
    winrt::check_hresult(composition_device->CreateTargetForHwnd(hwnd, true, composition_target.put()));

    composition_visual = nullptr;
    winrt::check_hresult(composition_device->CreateVisual(composition_visual.put()));
    winrt::check_hresult(composition_visual->SetContent(dxgi_swap_chain.get()));
    winrt::check_hresult(composition_target->SetRoot(composition_visual.get()));

    dxgi_surface = nullptr;
    winrt::check_hresult(dxgi_swap_chain->GetBuffer(0, __uuidof(dxgi_surface), dxgi_surface.put_void()));
    D2D1_BITMAP_PROPERTIES1 properties = {};
    properties.pixelFormat.alphaMode = D2D1_ALPHA_MODE_PREMULTIPLIED;
    properties.pixelFormat.format = DXGI_FORMAT_B8G8R8A8_UNORM;
    properties.bitmapOptions = D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW;

    d2d_bitmap = nullptr;
    winrt::check_hresult(d2d_dc->CreateBitmapFromDxgiSurface(dxgi_surface.get(),
                                                             properties,
                                                             d2d_bitmap.put()));
    d2d_dc->SetTarget(d2d_bitmap.get());
    resize();
}

void D2DWindow::base_render()
{
    std::unique_lock lock(mutex);
    if (!initialized || !d2d_dc || !d2d_bitmap)
        return;
    d2d_dc->BeginDraw();
    render(d2d_dc.get());
    winrt::check_hresult(d2d_dc->EndDraw());
    winrt::check_hresult(dxgi_swap_chain->Present(1, 0));
    winrt::check_hresult(composition_device->Commit());
}

void D2DWindow::render_empty()
{
    std::unique_lock lock(mutex);
    if (!initialized || !d2d_dc || !d2d_bitmap)
        return;
    d2d_dc->BeginDraw();
    d2d_dc->Clear();
    winrt::check_hresult(d2d_dc->EndDraw());
    winrt::check_hresult(dxgi_swap_chain->Present(1, 0));
    winrt::check_hresult(composition_device->Commit());
}

D2DWindow::~D2DWindow()
{
    ShowWindow(hwnd, SW_HIDE);
    DestroyWindow(hwnd);
}

D2DWindow* D2DWindow::this_from_hwnd(HWND window)
{
    return reinterpret_cast<D2DWindow*>(GetWindowLongPtr(window, GWLP_USERDATA));
}

LRESULT __stdcall D2DWindow::d2d_window_proc(HWND window, UINT message, WPARAM wparam, LPARAM lparam)
{
    auto self = this_from_hwnd(window);
    switch (message)
    {
    case WM_NCCREATE:
    {
        auto create_struct = reinterpret_cast<CREATESTRUCT*>(lparam);
        SetWindowLongPtr(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(create_struct->lpCreateParams));
        return TRUE;
    }
    case WM_MOVE:
    case WM_SIZE:
        self->base_resize(static_cast<unsigned>(lparam) & 0xFFFF, static_cast<unsigned>(lparam) >> 16);
        [[fallthrough]];
    case WM_PAINT:
        self->base_render();
        return 0;

    default:
        return DefWindowProc(window, message, wparam, lparam);
    }
}
