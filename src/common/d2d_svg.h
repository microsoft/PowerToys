#pragma once
#include <d2d1_3.h>
#include <d2d1_3helper.h>
#include <winrt/base.h>
#include <string>

class D2DSVG
{
public:
    D2DSVG& load(const std::wstring& filename, ID2D1DeviceContext5* d2d_dc);
    D2DSVG& resize(int x, int y, int width, int height, float fill, float max_scale = -1.0f);
    D2DSVG& render(ID2D1DeviceContext5* d2d_dc);
    D2DSVG& recolor(uint32_t oldcolor, uint32_t newcolor);
    float get_scale() const { return used_scale; }
    int width() const { return svg_width; }
    int height() const { return svg_height; }
    D2DSVG& toggle_element(const wchar_t* id, bool visible);
    winrt::com_ptr<ID2D1SvgElement> find_element(const std::wstring& id);
    D2D1_RECT_F rescale(D2D1_RECT_F rect);

protected:
    float used_scale = 1.0f;
    winrt::com_ptr<ID2D1SvgDocument> svg;
    int svg_width = -1, svg_height = -1;
    D2D1::Matrix3x2F transform;
};
