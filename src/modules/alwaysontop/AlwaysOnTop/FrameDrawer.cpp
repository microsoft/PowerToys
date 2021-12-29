#include "pch.h"
#include "FrameDrawer.h"

std::unique_ptr<FrameDrawer> FrameDrawer::Create(HWND window)
{
    auto self = std::make_unique<FrameDrawer>(window);
    if (self->Init())
    {
        return self;
    }

    return nullptr;
}

FrameDrawer::FrameDrawer(FrameDrawer&& other) :
    m_window(other.m_window),
    m_renderTarget(std::move(other.m_renderTarget)), 
    m_sceneRect(std::move(other.m_sceneRect)),
    m_renderThread(std::move(m_renderThread))
{
}

FrameDrawer::FrameDrawer(HWND window) :
    m_window(window), m_renderTarget(nullptr)
{
}

FrameDrawer::~FrameDrawer()
{
    m_abortThread = true;
    m_renderThread.join();

    if (m_renderTarget)
    {
        m_renderTarget->Release();
    }
}

bool FrameDrawer::Init()
{
    RECT clientRect;

    // Obtain the size of the drawing area.
    if (!GetClientRect(m_window, &clientRect))
    {
        return false;
    }

    HRESULT hr;

    // Create a Direct2D render target
    // We should always use the DPI value of 96 since we're running in DPI aware mode
    auto renderTargetProperties = D2D1::RenderTargetProperties(
        D2D1_RENDER_TARGET_TYPE_DEFAULT,
        D2D1::PixelFormat(DXGI_FORMAT_UNKNOWN, D2D1_ALPHA_MODE_PREMULTIPLIED),
        96.f,
        96.f);

    auto renderTargetSize = D2D1::SizeU(clientRect.right - clientRect.left, clientRect.bottom - clientRect.top);
    auto hwndRenderTargetProperties = D2D1::HwndRenderTargetProperties(m_window, renderTargetSize);

    hr = GetD2DFactory()->CreateHwndRenderTarget(renderTargetProperties, hwndRenderTargetProperties, &m_renderTarget);

    if (!SUCCEEDED(hr))
    {
        return false;
    }

    m_renderThread = std::thread([this]() { RenderLoop(); });

    return true;
}

void FrameDrawer::Hide()
{
    ShowWindow(m_window, SW_HIDE);
}

void FrameDrawer::Show()
{
    ShowWindow(m_window, SW_SHOWNA);
}

void FrameDrawer::SetBorderRect(RECT windowRect, COLORREF color, float thickness)
{
    std::unique_lock lock(m_mutex);

    auto borderColor = ConvertColor(color);

    m_sceneRect = DrawableRect{
        .rect = ConvertRect(windowRect),
        .borderColor = borderColor,
        .thickness = thickness
    };
}

ID2D1Factory* FrameDrawer::GetD2DFactory()
{
    static auto pD2DFactory = [] {
        ID2D1Factory* res = nullptr;
        D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, &res);
        return res;
    }();
    return pD2DFactory;
}

IDWriteFactory* FrameDrawer::GetWriteFactory()
{
    static auto pDWriteFactory = [] {
        IUnknown* res = nullptr;
        DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), &res);
        return reinterpret_cast<IDWriteFactory*>(res);
    }();
    return pDWriteFactory;
}

D2D1_COLOR_F FrameDrawer::ConvertColor(COLORREF color)
{
    return D2D1::ColorF(GetRValue(color) / 255.f,
                        GetGValue(color) / 255.f,
                        GetBValue(color) / 255.f,
                        1.f);
}

D2D1_RECT_F FrameDrawer::ConvertRect(RECT rect)
{
    return D2D1::RectF((float)rect.left, (float)rect.top, (float)rect.right, (float)rect.bottom);
}

FrameDrawer::RenderResult FrameDrawer::Render()
{
    std::unique_lock lock(m_mutex);

    if (!m_renderTarget)
    {
        return RenderResult::Failed;
    }

    m_renderTarget->BeginDraw();

    // Draw backdrop
    m_renderTarget->Clear(D2D1::ColorF(0.f, 0.f, 0.f, 0.f));

    ID2D1SolidColorBrush* borderBrush = nullptr;
    m_renderTarget->CreateSolidColorBrush(m_sceneRect.borderColor, &borderBrush);

    if (borderBrush)
    {
        m_renderTarget->DrawRectangle(m_sceneRect.rect, borderBrush, m_sceneRect.thickness);
        borderBrush->Release();
    }

    // The lock must be released here, as EndDraw() will wait for vertical sync
    lock.unlock();

    m_renderTarget->EndDraw();
    return RenderResult::Ok;
}

void FrameDrawer::RenderLoop()
{
    while (!m_abortThread)
    {
        auto result = Render();
        if (result == RenderResult::Failed)
        {
            Logger::error("Render failed");
            Hide();
            m_abortThread = true;
        }
    }
}
