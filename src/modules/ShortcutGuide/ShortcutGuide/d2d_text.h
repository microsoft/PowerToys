#pragma once
#include <winrt/base.h>
#include <dwrite.h>

class D2DText
{
public:
    D2DText(float text_size = 15.0f, float scale = 1.0f);
    D2DText& resize(float text_size, float scale);
    D2DText& set_alignment_left();
    D2DText& set_alignment_center();
    D2DText& set_alignment_right();
    void write(ID2D1DeviceContext5* d2d_dc, D2D1_COLOR_F color, D2D1_RECT_F rect, std::wstring text);

private:
    winrt::com_ptr<IDWriteFactory> factory;
    winrt::com_ptr<IDWriteTextFormat> format;
};
