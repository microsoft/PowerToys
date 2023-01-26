#include "pch.h"
#include "d2d_svg.h"

D2DSVG& D2DSVG::load(const std::wstring& filename, ID2D1DeviceContext5* d2d_dc)
{
    svg = nullptr;
    winrt::com_ptr<IStream> svg_stream;
    auto h = SHCreateStreamOnFileEx(filename.c_str(),
                                    STGM_READ,
                                    FILE_ATTRIBUTE_NORMAL,
                                    FALSE,
                                    nullptr,
                                    svg_stream.put());
    winrt::check_hresult(h);

    auto h1 = d2d_dc->CreateSvgDocument(
        svg_stream.get(),
        D2D1::SizeF(1, 1),
        svg.put());

    winrt::check_hresult(h1);

    winrt::com_ptr<ID2D1SvgElement> root;
    svg->GetRoot(root.put());
    float tmp;
    winrt::check_hresult(root->GetAttributeValue(L"width", &tmp));
    svg_width = static_cast<int>(tmp);
    winrt::check_hresult(root->GetAttributeValue(L"height", &tmp));
    svg_height =  static_cast<int>(tmp);
    return *this;
}

D2DSVG& D2DSVG::resize(int x, int y, int width, int height, float fill, float max_scale)
{
    // Center
    transform = D2D1::Matrix3x2F::Identity();
    transform = transform * D2D1::Matrix3x2F::Translation((width - svg_width) / 2.0f, (height - svg_height) / 2.0f);
    float h_scale = fill * height / svg_height;
    float v_scale = fill * width / svg_width;
    used_scale = std::min(h_scale, v_scale);
    if (max_scale > 0)
    {
        used_scale = std::min(used_scale, max_scale);
    }
    transform = transform * D2D1::Matrix3x2F::Scale(used_scale, used_scale, D2D1::Point2F(width / 2.0f, height / 2.0f));
    transform = transform * D2D1::Matrix3x2F::Translation(static_cast<float>(x), static_cast<float>(y));
    return *this;
}

D2DSVG& D2DSVG::recolor(uint32_t oldcolor, uint32_t newcolor)
{
    auto new_color = D2D1::ColorF(newcolor & 0xFFFFFF, 1);
    auto old_color = D2D1::ColorF(oldcolor & 0xFFFFFF, 1);
    std::function<void(ID2D1SvgElement * element)> recurse = [&](ID2D1SvgElement* element) {
        if (!element)
            return;
        if (element->IsAttributeSpecified(L"fill"))
        {
            D2D1_COLOR_F elem_fill;
            winrt::com_ptr<ID2D1SvgPaint> paint;
            element->GetAttributeValue(L"fill", paint.put());
            paint->GetColor(&elem_fill);
            if (elem_fill.r == old_color.r && elem_fill.g == old_color.g && elem_fill.b == old_color.b)
            {
                winrt::check_hresult(element->SetAttributeValue(L"fill", new_color));
            }
        }
        winrt::com_ptr<ID2D1SvgElement> sub;
        element->GetFirstChild(sub.put());
        while (sub)
        {
            recurse(sub.get());
            winrt::com_ptr<ID2D1SvgElement> next;
            element->GetNextChild(sub.get(), next.put());
            sub = next;
        }
    };
    winrt::com_ptr<ID2D1SvgElement> root;
    svg->GetRoot(root.put());
    recurse(root.get());
    return *this;
}

D2DSVG& D2DSVG::render(ID2D1DeviceContext5* d2d_dc)
{
    D2D1_MATRIX_3X2_F current;
    d2d_dc->GetTransform(&current);
    d2d_dc->SetTransform(transform * current);
    d2d_dc->DrawSvgDocument(svg.get());
    d2d_dc->SetTransform(current);
    return *this;
}

D2DSVG& D2DSVG::toggle_element(const wchar_t* id, bool visible)
{
    winrt::com_ptr<ID2D1SvgElement> element;
    if (svg->FindElementById(id, element.put()) != S_OK)
        return *this;
    if (!element)
        return *this;
    element->SetAttributeValue(L"display", visible ? D2D1_SVG_DISPLAY::D2D1_SVG_DISPLAY_INLINE : D2D1_SVG_DISPLAY::D2D1_SVG_DISPLAY_NONE);
    return *this;
}

winrt::com_ptr<ID2D1SvgElement> D2DSVG::find_element(const std::wstring& id)
{
    winrt::com_ptr<ID2D1SvgElement> element;
    winrt::check_hresult(svg->FindElementById(id.c_str(), element.put()));
    return element;
}

D2D1_RECT_F D2DSVG::rescale(D2D1_RECT_F rect)
{
    D2D1_RECT_F result;
    auto src = reinterpret_cast<D2D1_POINT_2F*>(&rect);
    auto dst = reinterpret_cast<D2D1_POINT_2F*>(&result);
    dst[0] = src[0] * transform;
    dst[1] = src[1] * transform;
    return result;
}
