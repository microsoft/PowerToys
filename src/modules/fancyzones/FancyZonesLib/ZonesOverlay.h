#pragma once

#include <map>
#include <optional>
#include <vector>
#include <wil\resource.h>
#include <winrt/base.h>
#include <d2d1.h>
#include <dwrite.h>

#include "util.h"
#include "Zone.h"
#include "FancyZones.h"
#include "Colors.h"
#include "LayoutConfigurator.h"

class ZonesOverlay
{
    struct DrawableRect
    {
        D2D1_RECT_F rect;
        D2D1_COLOR_F borderColor;
        D2D1_COLOR_F fillColor;
        D2D1_COLOR_F textColor;
        ZoneIndex id;
        bool showText;
    };

    struct AnimationInfo
    {
        std::chrono::steady_clock::time_point tStart;
        bool autoHide;
    };

    enum class RotationDirection
    {
        None,
        Left,
        Right,
        Both,
    };

    enum struct RenderResult
    {
        Ok,
        AnimationEnded,
        Failed,
    };

    HWND m_window = nullptr;
    RECT m_clientRect{};
    ID2D1HwndRenderTarget* m_renderTarget = nullptr;
    std::optional<AnimationInfo> m_animation;

    std::mutex m_mutex;
    std::vector<DrawableRect> m_sceneRects;
    bool m_drawBackdrop = false;
    RotationDirection m_rotationDirection = RotationDirection::None;
    bool m_animateRotation = false;
    std::optional<size_t> m_monitorNumber;
    std::optional<std::chrono::steady_clock::time_point> m_rotationPulseStart;

    float GetAnimationAlpha();
    static IDWriteFactory* GetWriteFactory();
    static D2D1_COLOR_F ConvertColor(COLORREF color);
    static D2D1_RECT_F ConvertRect(RECT rect);
    static D2D1_RECT_F OffsetRect(D2D1_RECT_F rect, float x, float y);
    RenderResult Render();
    void RenderLoop();

    std::atomic<bool> m_shouldRender = false;
    std::atomic<bool> m_abortThread = false;
    std::condition_variable m_cv;
    std::thread m_renderThread;

public:

    ~ZonesOverlay();
    ZonesOverlay(HWND window);
    void Hide();
    void Show();
    void Flash();
    void DrawActiveZoneSet(const ZonesMap& zones,
                           const ZoneIndexSet& highlightZones,
                           const Colors::ZoneColors& colors,
                           const bool showZoneText);
    void DrawMonitorRotationPreview(const std::vector<RECT>& windowRects, size_t monitorNumber, std::optional<bool> reverse, bool animateRotation);
};
