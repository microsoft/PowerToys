#include "pch.h"
#include "d2d_text.h"

D2DText::D2DText(float text_size, float scale)
{
    winrt::check_hresult(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(factory), reinterpret_cast<IUnknown**>(factory.put_void())));
    resize(text_size, scale);
    winrt::check_hresult(format->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER));
    winrt::check_hresult(format->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER));
}

D2DText& D2DText::resize(float text_size, float scale)
{
    format = nullptr;
    winrt::check_hresult(factory->CreateTextFormat(L"Segoe UI",
                                                   nullptr,
                                                   DWRITE_FONT_WEIGHT_NORMAL,
                                                   DWRITE_FONT_STYLE_NORMAL,
                                                   DWRITE_FONT_STRETCH_NORMAL,
                                                   text_size * scale,
                                                   L"en-us",
                                                   format.put()));
    winrt::check_hresult(format->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER));
    return *this;
}

D2DText& D2DText::set_alignment_left()
{
    winrt::check_hresult(format->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING));
    return *this;
}

D2DText& D2DText::set_alignment_center()
{
    winrt::check_hresult(format->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER));
    return *this;
}

D2DText& D2DText::set_alignment_right()
{
    winrt::check_hresult(format->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_TRAILING));
    return *this;
}

void D2DText::write(ID2D1DeviceContext5* d2d_dc, D2D1_COLOR_F color, D2D1_RECT_F rect, std::wstring text)
{
    winrt::com_ptr<ID2D1SolidColorBrush> brush;
    d2d_dc->CreateSolidColorBrush(color, brush.put());
    d2d_dc->DrawText(text.c_str(),
                     static_cast<UINT32>(text.length()),
                     format.get(),
                     rect,
                     brush.get());
}
