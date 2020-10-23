#pragma once

#include <map>
#include <vector>
#include <wil\resource.h>
#include <winrt/base.h>
#include <d2d1.h>
#include <dwrite.h>

#include "util.h"
#include "Zone.h"
#include "ZoneSet.h"

class ZoneWindowDrawing
{
    struct DrawableRect
    {
        D2D1_RECT_F rect;
        D2D1_COLOR_F borderColor;
        D2D1_COLOR_F fillColor;
        size_t id;
    };

    HWND m_window;
    RECT m_clientRect;
    // winrt::com_ptr<IZoneWindowHost> m_host;
    ID2D1HwndRenderTarget* m_renderTarget;
    std::optional<std::chrono::steady_clock::time_point> m_tAnimationStart;
    unsigned m_animationDuration;

    std::mutex m_mutex;
    std::vector<DrawableRect> m_sceneRects;

    void DrawBackdrop();
    float GetAnimationAlpha();
    static ID2D1Factory* GetD2DFactory();
    static IDWriteFactory* GetWriteFactory();
    static D2D1_COLOR_F ConvertColor(COLORREF color);
    static D2D1_RECT_F ConvertRect(RECT rect);

public:

    ZoneWindowDrawing(HWND window);
    void Render();
    void Hide();
    void Show(unsigned animationMillis);
    void DrawActiveZoneSet(const std::vector<winrt::com_ptr<IZone>>& zones,
                           const std::vector<size_t>& highlightZones,
                           winrt::com_ptr<IZoneWindowHost> host);
};
