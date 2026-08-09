#pragma once

#include <algorithm>
#include <cmath>
#include <cstdint>

namespace GuideVisualMetrics
{
    inline constexpr uint32_t DefaultDpi = 96;

    struct LabelMetrics
    {
        float scale = 1.0f;
        float width = 16.0f;
        float height = 8.0f;
        float fontSize = 14.0f;
        float backgroundInset = 0.0f;
        float cornerRadius = 8.0f;
        float pointerOffset = 12.0f;
        float guideOffset = 12.0f;
        float screenMargin = 4.0f;
        float horizontalTextInset = 8.0f;
        float verticalTextInset = 4.0f;
        float borderThickness = 1.0f;
    };

    constexpr float DpiScale(uint32_t dpi) noexcept
    {
        return static_cast<float>(dpi == 0 ? DefaultDpi : dpi) / static_cast<float>(DefaultDpi);
    }

    constexpr LabelMetrics LabelForDpi(uint32_t dpi) noexcept
    {
        const float scale = DpiScale(dpi);
        return {
            .scale = scale,
            .width = 16.0f * scale,
            .height = 8.0f * scale,
            .fontSize = 14.0f * scale,
            .backgroundInset = 0.0f,
            .cornerRadius = 8.0f * scale,
            .pointerOffset = 12.0f * scale,
            .guideOffset = 12.0f * scale,
            .screenMargin = 4.0f * scale,
            .horizontalTextInset = 8.0f * scale,
            .verticalTextInset = 4.0f * scale,
            .borderThickness = 1.0f * scale,
        };
    }

    inline LabelMetrics SizeToContent(LabelMetrics metrics, float textWidth, float textHeight) noexcept
    {
        metrics.width = std::max(
            1.0f,
            std::ceil(
                std::max(0.0f, textWidth) +
                (2.0f * (metrics.backgroundInset + metrics.horizontalTextInset))));
        metrics.height = std::max(
            1.0f,
            std::ceil(
                std::max(0.0f, textHeight) +
                (2.0f * (metrics.backgroundInset + metrics.verticalTextInset))));
        return metrics;
    }
}
