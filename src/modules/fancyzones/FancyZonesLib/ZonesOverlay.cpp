#include "pch.h"
#include "ZonesOverlay.h"

#include <algorithm>
#include <map>
#include <string>
#include <vector>

#include <common/logger/logger.h>
#include <common/utils/MsWindowsSettings.h>

namespace
{
    const int FadeInDurationMillis = 200;
    const int FlashZonesDurationMillis = 700;

    const int LayoutNameDurationMillis = 1500;
    const int LayoutNameFadeInMillis = 150;
    const int LayoutNameFadeOutMillis = 300;
    const float LayoutNameFontSize = 36.f;
    const float LayoutNamePaddingX = 24.f;
    const float LayoutNamePaddingY = 12.f;
    const float LayoutNameCornerRadius = 8.f;
    const float LayoutNameTopOffsetRatio = 0.08f; // distance from the top of the work area, relative to its height
}

namespace NonLocalizable
{
    const wchar_t SegoeUiFont[] = L"Segoe ui";
}

float ZonesOverlay::GetAnimationAlpha()
{
    // Lock is held by the caller

    if (!m_animation)
    {
        return 0.f;
    }

    auto tNow = std::chrono::steady_clock().now();
    auto millis = (tNow - m_animation->tStart).count() / 1e6f;

    if (m_animation->autoHide && millis > FlashZonesDurationMillis)
    {
        return 0.f;
    }

    // Return a positive value to avoid hiding
    return std::clamp(millis / FadeInDurationMillis, 0.001f, 1.f);
}

float ZonesOverlay::GetLayoutNameLabelAlpha()
{
    // Lock is held by the caller

    if (!m_layoutNameLabel)
    {
        return 0.f;
    }

    auto tNow = std::chrono::steady_clock().now();
    auto millis = (tNow - m_layoutNameLabel->tStart).count() / 1e6f;

    if (millis >= LayoutNameDurationMillis)
    {
        return 0.f;
    }

    if (millis < LayoutNameFadeInMillis)
    {
        return millis / LayoutNameFadeInMillis;
    }

    const auto remainingMillis = LayoutNameDurationMillis - millis;
    if (remainingMillis < LayoutNameFadeOutMillis)
    {
        return remainingMillis / LayoutNameFadeOutMillis;
    }

    return 1.f;
}

IDWriteFactory* ZonesOverlay::GetWriteFactory()
{
    static auto pDWriteFactory = [] {
        IUnknown* res = nullptr;
        DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), &res);
        return reinterpret_cast<IDWriteFactory*>(res);
    }();
    return pDWriteFactory;
}

D2D1_COLOR_F ZonesOverlay::ConvertColor(COLORREF color)
{
    return D2D1::ColorF(GetRValue(color) / 255.f,
                        GetGValue(color) / 255.f,
                        GetBValue(color) / 255.f,
                        1.f);
}

D2D1_RECT_F ZonesOverlay::ConvertRect(RECT rect)
{
    return D2D1::RectF(rect.left + 0.5f, rect.top + 0.5f, rect.right - 0.5f, rect.bottom - 0.5f);
}

ZonesOverlay::ZonesOverlay(HWND window)
{
    HRESULT hr;
    m_window = window;
    m_renderTarget = nullptr;
    m_shouldRender = false;

    // Obtain the size of the drawing area.
    if (!GetClientRect(window, &m_clientRect))
    {
        Logger::error(L"couldn't initialize ZonesOverlay: GetClientRect failed");
        return;
    }

    // Create a Direct2D render target
    // We should always use the DPI value of 96 since we're running in DPI aware mode
    auto renderTargetProperties = D2D1::RenderTargetProperties(
        D2D1_RENDER_TARGET_TYPE_DEFAULT,
        D2D1::PixelFormat(DXGI_FORMAT_UNKNOWN, D2D1_ALPHA_MODE_PREMULTIPLIED),
        96.f,
        96.f);

    auto renderTargetSize = D2D1::SizeU(m_clientRect.right - m_clientRect.left, m_clientRect.bottom - m_clientRect.top);
    auto hwndRenderTargetProperties = D2D1::HwndRenderTargetProperties(window, renderTargetSize);

    ID2D1Factory* factory = nullptr;
    D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, &factory);
    hr = factory->CreateHwndRenderTarget(renderTargetProperties, hwndRenderTargetProperties, &m_renderTarget);
    factory->Release();
    factory = nullptr;

    if (!SUCCEEDED(hr))
    {
        Logger::error(L"couldn't initialize ZonesOverlay: CreateHwndRenderTarget failed with {}", hr);
        return;
    }

    m_renderThread = std::thread([this]() { RenderLoop(); });
}

ZonesOverlay::RenderResult ZonesOverlay::Render()
{
    std::unique_lock lock(m_mutex);

    if (!m_renderTarget)
    {
        return RenderResult::Failed;
    }

    float animationAlpha = GetAnimationAlpha();

    if (animationAlpha <= 0.f)
    {
        return RenderResult::AnimationEnded;
    }

    BOOL isEnabledAnimations = GetAnimationsEnabled();
    if (!isEnabledAnimations)
    {
        animationAlpha = 1.f;
    }

    m_renderTarget->BeginDraw();

    // Draw backdrop
    m_renderTarget->Clear(D2D1::ColorF(0.f, 0.f, 0.f, 0.f));

    IDWriteTextFormat* textFormat = nullptr;

    auto writeFactory = GetWriteFactory();

    if (writeFactory)
    {
        writeFactory->CreateTextFormat(NonLocalizable::SegoeUiFont, nullptr, DWRITE_FONT_WEIGHT_NORMAL, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH_NORMAL, 80.f, L"en-US", &textFormat);
    }

    for (auto drawableRect : m_sceneRects)
    {
        ID2D1SolidColorBrush* textBrush = nullptr;
        ID2D1SolidColorBrush* borderBrush = nullptr;
        ID2D1SolidColorBrush* fillBrush = nullptr;

        // Need to copy the rect from m_sceneRects
        drawableRect.borderColor.a *= animationAlpha;
        drawableRect.fillColor.a *= animationAlpha;

        m_renderTarget->CreateSolidColorBrush(drawableRect.borderColor, &borderBrush);
        m_renderTarget->CreateSolidColorBrush(drawableRect.fillColor, &fillBrush);

        if (fillBrush)
        {
            m_renderTarget->FillRectangle(drawableRect.rect, fillBrush);
            fillBrush->Release();
        }

        if (borderBrush)
        {
            m_renderTarget->DrawRectangle(drawableRect.rect, borderBrush);
            borderBrush->Release();
        }

        std::wstring idStr = std::to_wstring(drawableRect.id + 1);

        if (drawableRect.showText)
        {
            m_renderTarget->CreateSolidColorBrush(drawableRect.textColor, &textBrush);

            if (textFormat && textBrush)
            {
                textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);
                textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER);
                m_renderTarget->DrawTextW(idStr.c_str(), static_cast<UINT32>(idStr.size()), textFormat, drawableRect.rect, textBrush);
            }

            if (textBrush)
            {
                textBrush->Release();
            }
        }
    }

    if (textFormat)
    {
        textFormat->Release();
    }

    if (m_layoutNameLabel.has_value())
    {
        float labelAlpha = GetLayoutNameLabelAlpha();
        if (labelAlpha <= 0.f)
        {
            m_layoutNameLabel.reset();
        }
        else if (writeFactory)
        {
            if (!isEnabledAnimations)
            {
                // No fading, but the label still disappears once its lifetime is over
                labelAlpha = 1.f;
            }

            // The label is part of the overlay, don't let it outshine a fading scene
            labelAlpha *= animationAlpha;

            IDWriteTextFormat* nameFormat = nullptr;
            writeFactory->CreateTextFormat(NonLocalizable::SegoeUiFont, nullptr, DWRITE_FONT_WEIGHT_SEMI_BOLD, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH_NORMAL, LayoutNameFontSize, L"en-US", &nameFormat);

            IDWriteTextLayout* nameLayout = nullptr;
            const float clientWidth = static_cast<float>(m_clientRect.right - m_clientRect.left);
            const float clientHeight = static_cast<float>(m_clientRect.bottom - m_clientRect.top);
            if (nameFormat)
            {
                nameFormat->SetWordWrapping(DWRITE_WORD_WRAPPING_NO_WRAP);
                writeFactory->CreateTextLayout(m_layoutNameLabel->text.c_str(), static_cast<UINT32>(m_layoutNameLabel->text.size()), nameFormat, clientWidth, clientHeight, &nameLayout);
            }

            if (nameLayout)
            {
                DWRITE_TEXT_METRICS metrics{};
                nameLayout->GetMetrics(&metrics);

                const float centerX = clientWidth / 2.f;
                const float top = clientHeight * LayoutNameTopOffsetRatio;

                D2D1_ROUNDED_RECT chip{
                    .rect = D2D1::RectF(centerX - metrics.width / 2.f - LayoutNamePaddingX,
                                        top,
                                        centerX + metrics.width / 2.f + LayoutNamePaddingX,
                                        top + metrics.height + 2 * LayoutNamePaddingY),
                    .radiusX = LayoutNameCornerRadius,
                    .radiusY = LayoutNameCornerRadius,
                };

                auto backgroundColor = m_layoutNameLabel->backgroundColor;
                auto labelTextColor = m_layoutNameLabel->textColor;
                backgroundColor.a *= labelAlpha;
                labelTextColor.a *= labelAlpha;

                ID2D1SolidColorBrush* backgroundBrush = nullptr;
                ID2D1SolidColorBrush* nameBrush = nullptr;
                m_renderTarget->CreateSolidColorBrush(backgroundColor, &backgroundBrush);
                m_renderTarget->CreateSolidColorBrush(labelTextColor, &nameBrush);

                if (backgroundBrush)
                {
                    m_renderTarget->FillRoundedRectangle(chip, backgroundBrush);
                    backgroundBrush->Release();
                }

                if (nameBrush)
                {
                    m_renderTarget->DrawTextLayout(D2D1::Point2F(centerX - metrics.width / 2.f, top + LayoutNamePaddingY), nameLayout, nameBrush);
                    nameBrush->Release();
                }

                nameLayout->Release();
            }

            if (nameFormat)
            {
                nameFormat->Release();
            }
        }
    }

    // The lock must be released here, as EndDraw() will wait for vertical sync
    lock.unlock();

    m_renderTarget->EndDraw();
    return RenderResult::Ok;
}

void ZonesOverlay::RenderLoop()
{
    while (!m_abortThread)
    {
        {
            // Wait here while rendering is disabled
            std::unique_lock lock(m_mutex);
            m_cv.wait(lock, [this]() { return (bool)m_shouldRender; });
        }

        auto result = Render();

        if (result == RenderResult::AnimationEnded || result == RenderResult::Failed)
        {
            Hide();
        }
    }
}

void ZonesOverlay::Hide()
{
    bool shouldHideWindow = true;
    {
        std::unique_lock lock(m_mutex);
        m_animation.reset();
        m_layoutNameLabel.reset();
        shouldHideWindow = m_shouldRender;
        m_shouldRender = false;
    }

    if (shouldHideWindow)
    {
        ShowWindow(m_window, SW_HIDE);
    }
}

void ZonesOverlay::Show()
{
    bool shouldShowWindow = true;
    {
        std::unique_lock lock(m_mutex);
        shouldShowWindow = !m_shouldRender;
        m_shouldRender = true;

        if (!m_animation)
        {
            m_animation.emplace(AnimationInfo{ .tStart = std::chrono::steady_clock().now(), .autoHide = false });
        }
        else if (m_animation->autoHide)
        {
            // Do not change the starting time of the animation, just reset autoHide
            m_animation->autoHide = false;
        }
    }

    if (shouldShowWindow)
    {
        ShowWindow(m_window, SW_SHOWNA);
    }

    m_cv.notify_all();
}

void ZonesOverlay::Flash()
{
    bool shouldShowWindow = true;
    {
        std::unique_lock lock(m_mutex);
        shouldShowWindow = !m_shouldRender;
        m_shouldRender = true;

        m_animation.emplace(AnimationInfo{ .tStart = std::chrono::steady_clock().now(), .autoHide = true });
    }

    if (shouldShowWindow)
    {
        ShowWindow(m_window, SW_SHOWNA);
    }

    m_cv.notify_all();
}

void ZonesOverlay::DrawActiveZoneSet(const ZonesMap& zones,
                                     const ZoneIndexSet& highlightZones,
                                     const Colors::ZoneColors& colors,
                                     const bool showZoneText)
{
    std::unique_lock lock(m_mutex);

    m_sceneRects = {};

    auto borderColor = ConvertColor(colors.borderColor);
    auto inactiveColor = ConvertColor(colors.primaryColor);
    auto highlightColor = ConvertColor(colors.highlightColor);
    auto numberColor = ConvertColor(colors.numberColor);

    inactiveColor.a = colors.highlightOpacity / 100.f;
    highlightColor.a = colors.highlightOpacity / 100.f;

    std::vector<bool> isHighlighted(zones.size() + 1, false);
    for (ZoneIndex x : highlightZones)
    {
        isHighlighted[x] = true;
    }

    // First draw the inactive zones
    for (const auto& [zoneId, zone] : zones)
    {
        if (!isHighlighted[zoneId])
        {
            DrawableRect drawableRect{
                .rect = ConvertRect(zone.GetZoneRect()),
                .borderColor = borderColor,
                .fillColor = inactiveColor,
                .textColor = numberColor,
                .id = zone.Id(),
                .showText = showZoneText
            };

            m_sceneRects.push_back(drawableRect);
        }
    }

    // Draw the active zones on top of the inactive zones
    for (const auto& [zoneId, zone] : zones)
    {
        if (isHighlighted[zoneId])
        {
            DrawableRect drawableRect{
                .rect = ConvertRect(zone.GetZoneRect()),
                .borderColor = borderColor,
                .fillColor = highlightColor,
                .textColor = numberColor,
                .id = zone.Id(),
                .showText = showZoneText
            };

            m_sceneRects.push_back(drawableRect);
        }
    }
}

void ZonesOverlay::ShowLayoutName(const std::wstring& text, const Colors::ZoneColors& colors)
{
    if (text.empty())
    {
        return;
    }

    std::unique_lock lock(m_mutex);

    auto backgroundColor = ConvertColor(colors.primaryColor);
    backgroundColor.a = 0.9f;

    m_layoutNameLabel.emplace(LayoutNameLabel{
        .text = text,
        .textColor = ConvertColor(colors.numberColor),
        .backgroundColor = backgroundColor,
        .tStart = std::chrono::steady_clock().now(),
    });
}

ZonesOverlay::~ZonesOverlay()
{
    // Constructor early-returns (e.g. CreateHwndRenderTarget failing during a
    // display-driver TDR) leave m_renderThread default-constructed; calling
    // join() on a non-joinable thread terminates the process.
    if (m_renderThread.joinable())
    {
        {
            std::unique_lock lock(m_mutex);
            m_abortThread = true;
            m_shouldRender = true;
        }
        m_cv.notify_all();
        m_renderThread.join();
    }

    if (m_renderTarget)
    {
        m_renderTarget->Release();
        m_renderTarget = nullptr;
    }
}
