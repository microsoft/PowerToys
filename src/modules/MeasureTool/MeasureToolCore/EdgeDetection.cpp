#include "pch.h"

#include "constants.h"
#include "EdgeDetection.h"
template<bool PerChannel,
         bool ContinuousCapture,
         bool IsX,
         bool Increment>
inline long FindEdge(const BGRATextureView& texture, const POINT centerPoint, const uint8_t tolerance)
{
    using namespace consts;

    long xOffset = 0;
    long yOffset = 0;

    // For continuous capture, we'll be a bit off center from the cursor so the cross we draw won't interfere with the measurement.
    if constexpr (ContinuousCapture)
    {
        if constexpr (IsX)
        {
            xOffset = Increment ? CURSOR_OFFSET_AMOUNT_X : -CURSOR_OFFSET_AMOUNT_X;
            yOffset = 1;
        }
        else
        {
            xOffset = 1;
            yOffset = Increment ? CURSOR_OFFSET_AMOUNT_Y : -CURSOR_OFFSET_AMOUNT_Y;
        }
    }

    const size_t maxDim = IsX ? texture.width : texture.height;

    long x = std::clamp<long>(centerPoint.x + xOffset, 1, static_cast<long>(texture.width - 2));
    long y = std::clamp<long>(centerPoint.y + yOffset, 1, static_cast<long>(texture.height - 2));

    const uint32_t startPixel = texture.GetPixel(x, y);
    while (true)
    {
        long oldX = x;
        long oldY = y;
        if constexpr (IsX)
        {
            if constexpr (Increment)
            {
                if (++x == maxDim)
                    break;
            }
            else
            {
                if (--x == 0)
                    break;
            }
        }
        else
        {
            if constexpr (Increment)
            {
                if (++y == maxDim)
                    break;
            }
            else
            {
                if (--y == 0)
                    break;
            }
        }

        const uint32_t nextPixel = texture.GetPixel(x, y);
        if (!texture.PixelsClose<PerChannel>(startPixel, nextPixel, tolerance))
        {
            return IsX ? oldX : oldY;
        }
    }

    return Increment ? static_cast<long>(IsX ? texture.width : texture.height) - 1 : 0;
}

template<bool PerChannel, bool ContinuousCapture>
inline RECT DetectEdgesInternal(const BGRATextureView& texture,
                                const POINT centerPoint,
                                const uint8_t tolerance)
{
    return RECT{ .left = FindEdge<PerChannel,
                                  ContinuousCapture,
                                  true,
                                  false>(texture, centerPoint, tolerance),
                 .top = FindEdge<PerChannel,
                                 ContinuousCapture,
                                 false,
                                 false>(texture, centerPoint, tolerance),
                 .right = FindEdge<PerChannel,
                                   ContinuousCapture,
                                   true,
                                   true>(texture, centerPoint, tolerance),
                 .bottom = FindEdge<PerChannel,
                                    ContinuousCapture,
                                    false,
                                    true>(texture, centerPoint, tolerance) };
}

RECT DetectEdges(const BGRATextureView& texture,
                 const POINT centerPoint,
                 const bool perChannel,
                 const uint8_t tolerance,
                 const bool continuousCapture)
{
    auto function = perChannel ? &DetectEdgesInternal<true, false> : DetectEdgesInternal<false, false>;
    if (continuousCapture)
        function = perChannel ? &DetectEdgesInternal<true, true> : &DetectEdgesInternal<false, true>;

    return function(texture, centerPoint, tolerance);
}
