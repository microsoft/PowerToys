#pragma once

#include <array>

// Pure layout math for the Composition window border. All values are physical pixels in the border
// host window's coordinate space unless noted. Kept free of Windows types so it can be unit tested.
namespace PowerToysWindowBorder
{
    enum class Placement
    {
        // The band sits just outside the tracked window's visible frame (Always On Top).
        Outside,
        // The band sits just inside the tracked window's visible frame (Grab and Move).
        Inside,
    };

    // An axis-aligned rounded rectangle used as the centerline of a stroke.
    struct RoundedRect
    {
        float left = 0.f;
        float top = 0.f;
        float width = 0.f;
        float height = 0.f;
        float radius = 0.f;

        constexpr bool IsEmpty() const noexcept
        {
            return width <= 0.f || height <= 0.f;
        }
    };

    struct Stroke
    {
        RoundedRect rect;
        float thickness = 0.f;
        // 0..1, multiplied with the border color's alpha.
        float alpha = 1.f;

        constexpr bool IsVisible() const noexcept
        {
            return thickness > 0.f && alpha > 0.f && !rect.IsEmpty();
        }
    };

    inline constexpr int HaloSteps = 8;
    inline constexpr float HaloReachDip = 10.f;
    inline constexpr float HaloStrength = 0.42f;
    // Neighboring halo strokes overlap a little so anti-aliasing does not leave seams between them.
    inline constexpr float HaloStrokeOverlap = 1.5f;
    inline constexpr float HighlightDip = 1.f;
    inline constexpr float HighlightLighten = 0.5f;

    struct LayoutInput
    {
        // Size of the tracked window's visible frame (DWMWA_EXTENDED_FRAME_BOUNDS).
        float frameWidth = 0.f;
        float frameHeight = 0.f;
        // The tracked window's own corner radius; 0 for square corners.
        float cornerRadius = 0.f;
        float thickness = 0.f;
        float scale = 1.f;
        Placement placement = Placement::Outside;
        bool halo = true;
        bool highlight = true;
    };

    struct Layout
    {
        // How far the host window extends beyond the visible frame on every side.
        int inflate = 0;
        int hostWidth = 0;
        int hostHeight = 0;
        Stroke band;
        Stroke highlight;
        std::array<Stroke, HaloSteps> halo{};
    };

    namespace Detail
    {
        constexpr float Max(float a, float b) noexcept
        {
            return a > b ? a : b;
        }

        constexpr float Min(float a, float b) noexcept
        {
            return a < b ? a : b;
        }

        constexpr int CeilToInt(float value) noexcept
        {
            const int truncated = static_cast<int>(value);
            return (static_cast<float>(truncated) < value) ? truncated + 1 : truncated;
        }

        // Centerline of a stroke whose outer edge sits `outerOffset` px outside the frame edge
        // (negative = inside) with outer corner radius `outerRadius`. Square corners stay square:
        // rounding only applies when `rounded` is set.
        constexpr RoundedRect StrokeRect(float origin, float frameWidth, float frameHeight, float outerOffset, float outerRadius, float thickness, bool rounded) noexcept
        {
            const float centerOffset = outerOffset - (thickness / 2.f);
            RoundedRect rect{
                .left = origin - centerOffset,
                .top = origin - centerOffset,
                .width = frameWidth + (2.f * centerOffset),
                .height = frameHeight + (2.f * centerOffset),
                .radius = rounded ? Max(outerRadius - (thickness / 2.f), 0.f) : 0.f,
            };
            rect.radius = Min(rect.radius, Max(Min(rect.width, rect.height) / 2.f, 0.f));
            return rect;
        }
    }

    // Lays out a band that is concentric with the tracked window's corners: the edge touching the
    // window uses the window's radius and the far edge grows by the band thickness.
    constexpr Layout ComputeLayout(const LayoutInput& input) noexcept
    {
        Layout layout{};
        if (input.frameWidth <= 0.f || input.frameHeight <= 0.f)
        {
            return layout;
        }

        const float scale = input.scale > 0.f ? input.scale : 1.f;
        const float radius = Detail::Max(input.cornerRadius, 0.f);
        const bool rounded = radius > 0.f;
        float thickness = Detail::Max(input.thickness, 0.f);
        if (input.placement == Placement::Inside)
        {
            thickness = Detail::Min(thickness, Detail::Min(input.frameWidth, input.frameHeight) / 2.f);
        }

        // Distance of the band's outer edge outside the visible frame, and that edge's corner radius.
        const float bandOuterOffset = input.placement == Placement::Outside ? thickness : 0.f;
        const float bandOuterRadius = rounded ? radius + bandOuterOffset : 0.f;
        const float haloReach = input.halo ? HaloReachDip * scale : 0.f;
        // The outermost halo stroke spills past the reach by its overlap; keep it inside the host.
        const float haloExtent = haloReach > 0.f ? haloReach + ((haloReach / HaloSteps) * (HaloStrokeOverlap - 1.f) / 2.f) : 0.f;

        layout.inflate = Detail::CeilToInt(bandOuterOffset + haloExtent);
        const float origin = static_cast<float>(layout.inflate);
        layout.hostWidth = Detail::CeilToInt(input.frameWidth) + (2 * layout.inflate);
        layout.hostHeight = Detail::CeilToInt(input.frameHeight) + (2 * layout.inflate);

        if (thickness <= 0.f)
        {
            return layout;
        }

        layout.band = Stroke{
            .rect = Detail::StrokeRect(origin, input.frameWidth, input.frameHeight, bandOuterOffset, bandOuterRadius, thickness, rounded),
            .thickness = thickness,
            .alpha = 1.f,
        };

        // A lighter hairline along the band's outer edge, kept fully inside the band. Skipped when the
        // band is too thin for the hairline to read as an edge instead of replacing the band.
        const float hairline = Detail::Max(HighlightDip * scale, 1.f);
        if (input.highlight && thickness >= hairline * 3.f)
        {
            layout.highlight = Stroke{
                .rect = Detail::StrokeRect(origin, input.frameWidth, input.frameHeight, bandOuterOffset, bandOuterRadius, hairline, rounded),
                .thickness = hairline,
                .alpha = 1.f,
            };
        }

        if (haloReach > 0.f)
        {
            const float step = haloReach / HaloSteps;
            const float strokeThickness = step * HaloStrokeOverlap;
            for (int i = 0; i < HaloSteps; ++i)
            {
                const float t = (static_cast<float>(i) + 0.5f) / HaloSteps;
                const float falloff = 1.f - t;
                const float distance = haloReach * t;
                // The halo rounds a little even around square windows so it reads as a glow.
                const float centerRadius = bandOuterRadius + distance;
                const float halfStroke = strokeThickness / 2.f;
                layout.halo.at(i) = Stroke{
                    .rect = Detail::StrokeRect(origin, input.frameWidth, input.frameHeight, bandOuterOffset + distance + halfStroke, centerRadius + halfStroke, strokeThickness, true),
                    .thickness = strokeThickness,
                    .alpha = HaloStrength * falloff * falloff,
                };
            }
        }

        return layout;
    }

    // Blends a color channel toward white for the edge highlight.
    constexpr unsigned char Lighten(unsigned char channel, float amount) noexcept
    {
        const float value = static_cast<float>(channel) + ((255.f - static_cast<float>(channel)) * amount);
        return static_cast<unsigned char>(value + 0.5f);
    }
}
