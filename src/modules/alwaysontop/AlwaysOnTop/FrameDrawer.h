#pragma once

#include <mutex>

#include <d2d1.h>
#include <winrt/base.h>
#include <dwrite.h>

class FrameDrawer
{
public:
    static std::unique_ptr<FrameDrawer> Create(HWND window);

    FrameDrawer(HWND window);
    FrameDrawer(FrameDrawer&& other) = default;

    bool Init();

    void Show();
    void Hide();
    void SetBorderRect(RECT windowRect, COLORREF color, int thickness);

private:
    bool CreateRenderTargets(const RECT& clientRect);

    struct DrawableRect
    {
        D2D1_RECT_F rect;
        D2D1_COLOR_F borderColor;
        int thickness;
    };

    static ID2D1Factory* GetD2DFactory();
    static IDWriteFactory* GetWriteFactory();
    static D2D1_COLOR_F ConvertColor(COLORREF color);
    static D2D1_RECT_F ConvertRect(RECT rect);
    void Render();

    HWND m_window = nullptr;
    size_t m_renderTargetSizeHash = {};
    winrt::com_ptr<ID2D1HwndRenderTarget> m_renderTarget;
    winrt::com_ptr<ID2D1SolidColorBrush> m_borderBrush;
    DrawableRect m_sceneRect = {};
};