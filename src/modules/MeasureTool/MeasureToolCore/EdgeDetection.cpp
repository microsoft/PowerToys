#include "pch.h"

#include "EdgeDetection.h"

const uint8_t DEFAULT_TOLERANCE = 1;

/* Offset to not try not to use the cursor immediate pixels in measuring, but it seems only neccessary for continuous mode. */
const long CURSOR_OFFSET_AMOUNT_X = 4;
const long CURSOR_OFFSET_AMOUNT_Y = 4;

template<bool continuousCapture, bool IsX, bool Increment>
inline long FindEdge(const BGRATextureView& texture, const POINT centerPoint, const uint8_t tolerance)
{
    long xOffset;
    long yOffset;
    if constexpr (continuousCapture)
    {
        if constexpr (IsX)
        {
            xOffset = Increment ? CURSOR_OFFSET_AMOUNT_X : -CURSOR_OFFSET_AMOUNT_X;
            yOffset = 1; // For continuous capture, we'll be a bit off center from the cursor but at least the cross we draw won't interfere with the measurement.

        }
        else
        {
            yOffset = Increment ? CURSOR_OFFSET_AMOUNT_Y : -CURSOR_OFFSET_AMOUNT_Y;
            xOffset = 1; // For continuous capture,we'll be a bit off center from the cursor but at least the cross we draw won't interfere with the measurement.
        }
    }
    else
    {
        yOffset = 0;
        xOffset = 0;
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
        if (!texture.PixelsClose(startPixel, nextPixel, tolerance))
        {
            long result = (IsX ? oldX : oldY); // Return previous value, since we've hit a different pixel color.
            if constexpr (Increment)
            {
                // Take into account the pixel that we start measuring at.
                result++;
            }
            return result;
        }
    }

    return Increment ? static_cast<long>(IsX ? texture.width : texture.height) : 0;
}

RECT DetectEdges(const BGRATextureView& texture, const POINT centerPoint, const uint8_t tolerance, const bool continuousCapture)
{
    if (continuousCapture)
    {
        return RECT{ .left = FindEdge<true, true, false>(texture, centerPoint, tolerance),
                     .top = FindEdge<true, false, false>(texture, centerPoint, tolerance),
                     .right = FindEdge<true, true, true>(texture, centerPoint, tolerance),
                     .bottom = FindEdge<true, false, true>(texture, centerPoint, tolerance) };
    }
    else
    {
        return RECT{ .left = FindEdge<false, true, false>(texture, centerPoint, tolerance),
                     .top = FindEdge<false, false, false>(texture, centerPoint, tolerance),
                     .right = FindEdge<false, true, true>(texture, centerPoint, tolerance),
                     .bottom = FindEdge<false, false, true>(texture, centerPoint, tolerance) };
    }
}
