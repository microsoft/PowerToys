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

FrameDrawer::FrameDrawer(FrameDrawer&& other)
    : m_window(other.m_window)
    , m_clientRect(std::move(other.m_clientRect))
    , m_renderTarget(std::move(other.m_renderTarget))
    , m_sceneRect(std::move(other.m_sceneRect))
    , m_shouldRender(other.m_shouldRender.load())
    , m_abortThread(other.m_abortThread.load())
    , m_renderThread(std::move(m_renderThread))
{
}

FrameDrawer::FrameDrawer(HWND window)
    : m_window(window)
    , m_renderTarget(nullptr)
    , m_shouldRender(false)
{
    
}

FrameDrawer::~FrameDrawer()
{
    {
        std::unique_lock lock(m_mutex);
        m_abortThread = true;
        m_shouldRender = true;
    }
    m_cv.notify_all();
    m_renderThread.join();

    if (m_renderTarget)
    {
        m_renderTarget->Release();
    }
}

bool FrameDrawer::Init()
{
    // Obtain the size of the drawing area.
    if (!GetClientRect(m_window, &m_clientRect))
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

    auto renderTargetSize = D2D1::SizeU(m_clientRect.right - m_clientRect.left, m_clientRect.bottom - m_clientRect.top);
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
    bool shouldHideWindow = true;
    {
        std::unique_lock lock(m_mutex);
        shouldHideWindow = m_shouldRender;
        m_shouldRender = false;
    }

    if (shouldHideWindow)
    {
        ShowWindow(m_window, SW_HIDE);
    }
}

void FrameDrawer::Show()
{
    bool shouldShowWindow = true;
    {
        std::unique_lock lock(m_mutex);
        shouldShowWindow = !m_shouldRender;
        m_shouldRender = true;
    }

    if (shouldShowWindow)
    {
        ShowWindow(m_window, SW_SHOWNA);
    }

    m_cv.notify_all();
}

void FrameDrawer::SetBorderRect(RECT windowRect)
{
    std::unique_lock lock(m_mutex);

    auto borderColor = ConvertColor(RGB(0, 173, 239));

    m_sceneRect = DrawableRect{
        .rect = ConvertRect(windowRect),
        .borderColor = borderColor
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
        m_renderTarget->DrawRectangle(m_sceneRect.rect, borderBrush, 15.f);
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
        {
            // Wait here while rendering is disabled
            std::unique_lock lock(m_mutex);
            m_cv.wait(lock, [this]() { return (bool)m_shouldRender; });
        }

        auto result = Render();

        if (result == RenderResult::Failed)
        {
            Hide();
        }
    }
}
