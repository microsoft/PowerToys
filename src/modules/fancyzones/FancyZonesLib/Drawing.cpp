#include "pch.h"
#include "Drawing.h"

#include <common/logger/logger.h>

D2D1_COLOR_F Drawing::ConvertColor(COLORREF color)
{
    return D2D1::ColorF(GetRValue(color) / 255.f,
                        GetGValue(color) / 255.f,
                        GetBValue(color) / 255.f,
                        1.f);
}

D2D1_COLOR_F Drawing::ConvertColor(winrt::Windows::UI::Color color)
{
    return D2D1::ColorF(color.R / 255.f,
                        color.G / 255.f,
                        color.B / 255.f,
                        color.A / 255.f);
}

ID2D1Factory* Drawing::GetD2DFactory()
{
    static auto pD2DFactory = [] {
        ID2D1Factory* res = nullptr;
        D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, &res);
        return res;
    }();
    return pD2DFactory;
}

IDWriteFactory* Drawing::GetWriteFactory()
{
    static auto pDWriteFactory = [] {
        IUnknown* res = nullptr;
        DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), &res);
        return reinterpret_cast<IDWriteFactory*>(res);
    }();
    return pDWriteFactory;
}

IWICImagingFactory2* Drawing::GetImageFactory()
{
    static auto pImageFactory = [] {
        PVOID res = nullptr;
        CoCreateInstance(CLSID_WICImagingFactory2, nullptr, CLSCTX_INPROC_SERVER | CLSCTX_LOCAL_SERVER, __uuidof(IWICImagingFactory2), &res);
        return reinterpret_cast<IWICImagingFactory2*>(res);
    }();
    return pImageFactory;
}

Drawing::Drawing()
{
    m_window = nullptr;
    m_renderTarget = nullptr;
}

void Drawing::Init(HWND window)
{
    m_window = window;

    // Obtain the size of the drawing area.
    if (!GetClientRect(window, m_renderRect.get()))
    {
        Logger::error("couldn't initialize Drawing: GetClientRect failed");
        return;
    }

    // Create a Direct2D render target
    // We should always use the DPI value of 96 since we're running in DPI aware mode
    auto renderTargetProperties = D2D1::RenderTargetProperties(
        D2D1_RENDER_TARGET_TYPE_DEFAULT,
        D2D1::PixelFormat(DXGI_FORMAT_UNKNOWN, D2D1_ALPHA_MODE_PREMULTIPLIED),
        96.f,
        96.f);

    auto renderTargetSize = D2D1::SizeU(m_renderRect.width(), m_renderRect.height());
    auto hwndRenderTargetProperties = D2D1::HwndRenderTargetProperties(window, renderTargetSize);

    auto d2dFactory = GetD2DFactory();
    if (!d2dFactory)
    {
        return;
    }

    winrt::com_ptr<ID2D1HwndRenderTarget> renderTarget = nullptr;
    auto hr = d2dFactory->CreateHwndRenderTarget(renderTargetProperties, hwndRenderTargetProperties, renderTarget.put());
    if (!SUCCEEDED(hr))
    {
        Logger::error("couldn't initialize Drawing: CreateHwndRenderTarget failed with {}", hr);
        return;
    }

    m_renderTarget = renderTarget;
}

Drawing::operator bool() const
{
    return bool(m_renderTarget);
}

void Drawing::BeginDraw(const D2D1_COLOR_F& backColor)
{
    if (!*this)
    {
        return;
    }

    m_renderTarget->BeginDraw();

    // Draw backdrop
    m_renderTarget->Clear(backColor);
}

winrt::com_ptr<IDWriteTextFormat> Drawing::CreateTextFormat(LPCWSTR fontFamilyName, FLOAT fontSize, DWRITE_FONT_WEIGHT fontWeight) const
{
    winrt::com_ptr<IDWriteTextFormat> textFormat = nullptr;

    auto writeFactory = GetWriteFactory();

    if (writeFactory)
    {
        writeFactory->CreateTextFormat(fontFamilyName, nullptr, fontWeight, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH_NORMAL, fontSize, L"en-US", textFormat.put());
    }

    return textFormat;
}

winrt::com_ptr<ID2D1SolidColorBrush> Drawing::CreateBrush(D2D1_COLOR_F color) const
{
    if (!*this)
    {
        return nullptr;
    }

    winrt::com_ptr<ID2D1SolidColorBrush> brush = nullptr;

    m_renderTarget->CreateSolidColorBrush(color, brush.put());

    return brush;
}

winrt::com_ptr<ID2D1Bitmap> Drawing::CreateIcon(HICON icon) const
{
    if (!*this)
    {
        return nullptr;
    }

    winrt::com_ptr<ID2D1Bitmap> bitmap = nullptr;

    auto imageFactory = GetImageFactory();

    if (imageFactory)
    {
        winrt::com_ptr<IWICBitmap> wicBitmap = nullptr;
        imageFactory->CreateBitmapFromHICON(icon, wicBitmap.put());

        if (wicBitmap)
        {
            winrt::com_ptr<IWICFormatConverter> converter = nullptr;
            imageFactory->CreateFormatConverter(converter.put());

            if (converter)
            {
                converter->Initialize(wicBitmap.get(), GUID_WICPixelFormat32bppPBGRA, WICBitmapDitherTypeNone, nullptr, 0, WICBitmapPaletteTypeMedianCut);

                D2D1_BITMAP_PROPERTIES bitmapProps;
                bitmapProps.pixelFormat.format = DXGI_FORMAT_B8G8R8A8_UNORM;
                bitmapProps.pixelFormat.alphaMode = D2D1_ALPHA_MODE_PREMULTIPLIED;
                bitmapProps.dpiX = 96;
                bitmapProps.dpiY = 96;

                m_renderTarget->CreateBitmapFromWicBitmap(wicBitmap.get(), &bitmapProps, bitmap.put());
            }
        }
    }

    return bitmap;
}

void Drawing::FillRectangle(const D2D1_RECT_F& rect, D2D1_COLOR_F color)
{
    if (!*this)
    {
        return;
    }

    auto brush = CreateBrush(color);
    if (brush)
    {
        m_renderTarget->FillRectangle(rect, brush.get());
    }
}

void Drawing::FillRoundedRectangle(const D2D1_RECT_F& rect, D2D1_COLOR_F color)
{
    if (!*this)
    {
        return;
    }

    auto brush = CreateBrush(color);
    if (brush)
    {
        D2D1_ROUNDED_RECT roundedRect;
        roundedRect.rect = rect;
        roundedRect.radiusX = (rect.right - rect.left) * .1f;
        roundedRect.radiusY = (rect.bottom - rect.top) * .1f;

        auto radius = min(roundedRect.radiusX, roundedRect.radiusY);
        roundedRect.radiusX = radius;
        roundedRect.radiusY = radius;

        m_renderTarget->FillRoundedRectangle(roundedRect, brush.get());
    }
}

void Drawing::DrawRectangle(const D2D1_RECT_F& rect, D2D1_COLOR_F color, float strokeWidth)
{
    if (!*this)
    {
        return;
    }

    auto brush = CreateBrush(color);
    if (brush)
    {
        m_renderTarget->DrawRectangle(rect, brush.get(), strokeWidth);
    }
}

void Drawing::DrawRoundedRectangle(const D2D1_RECT_F& rect, D2D1_COLOR_F color, float strokeWidth)
{
    if (!*this)
    {
        return;
    }

    auto brush = CreateBrush(color);
    if (brush)
    {
        D2D1_ROUNDED_RECT roundedRect;
        roundedRect.rect = rect;
        roundedRect.radiusX = (rect.right - rect.left) * .1f;
        roundedRect.radiusY = (rect.bottom - rect.top) * .1f;
        m_renderTarget->DrawRoundedRectangle(roundedRect, brush.get(), strokeWidth);
    }
}

void Drawing::DrawTextW(std::wstring text, IDWriteTextFormat* textFormat, const D2D1_RECT_F& rect, D2D1_COLOR_F color)
{
    if (!*this || !textFormat)
    {
        return;
    }

    auto brush = CreateBrush(color);
    if (brush)
    {
        textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);
        textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER);
        m_renderTarget->DrawTextW(text.c_str(), (UINT32)text.size(), textFormat, rect, brush.get());
    }
}

void Drawing::DrawTextTrim(std::wstring text, IDWriteTextFormat* textFormat, const D2D1_RECT_F& rect, D2D1_COLOR_F color, bool hasUnderline)
{
    if (!*this || !textFormat)
    {
        return;
    }

    auto brush = CreateBrush(color);

    auto writeFactory = GetWriteFactory();
    if (!writeFactory)
    {
        return;
    }

    winrt::com_ptr<IDWriteInlineObject> ellipsis;
    writeFactory->CreateEllipsisTrimmingSign(textFormat, ellipsis.put());

    if (brush && ellipsis)
    {
        DWRITE_TRIMMING trimming{};
        trimming.granularity = DWRITE_TRIMMING_GRANULARITY_CHARACTER;
        textFormat->SetTrimming(&trimming, ellipsis.get());
        textFormat->SetWordWrapping(DWRITE_WORD_WRAPPING_NO_WRAP);
        textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING);
        textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER);

        auto width = rect.right - rect.left;
        auto height = rect.bottom - rect.top;
        auto textSize = (UINT32)text.size();

        winrt::com_ptr<IDWriteTextLayout> textLayout;
        writeFactory->CreateTextLayout(text.c_str(), textSize, textFormat, width, height, textLayout.put());

        if (textLayout)
        {
            DWRITE_TEXT_RANGE range = { 0, textSize };
            textLayout->SetUnderline(hasUnderline, range);

            auto origin = D2D1::Point2F(rect.left, rect.top);
            m_renderTarget->DrawTextLayout(origin, textLayout.get(), brush.get());
        }
    }
}

void Drawing::DrawBitmap(const D2D1_RECT_F& rect, ID2D1Bitmap* bitmap, float opacity)
{
    if (!*this)
    {
        return;
    }

    m_renderTarget->DrawBitmap(bitmap, rect, opacity);
}

void Drawing::EndDraw()
{
    if (!*this)
    {
        return;
    }

    m_renderTarget->EndDraw();
}