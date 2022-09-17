#include "pch.h"

#include "constants.h"
#include "EdgeDetection.h"

template<bool PerChannel,
         bool IsX,
         bool Increment>
inline long FindEdge(const BGRATextureView& texture, const POINT centerPoint, const uint8_t tolerance)
{
    using namespace consts;

    const size_t maxDim = IsX ? texture.width : texture.height;

    long x = std::clamp<long>(centerPoint.x, 1, static_cast<long>(texture.width - 2));
    long y = std::clamp<long>(centerPoint.y, 1, static_cast<long>(texture.height - 2));

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

template<bool PerChannel>
inline RECT DetectEdgesInternal(const BGRATextureView& texture,
                                const POINT centerPoint,
                                const uint8_t tolerance)
{
    return RECT{ .left = FindEdge<PerChannel,
                                  true,
                                  false>(texture, centerPoint, tolerance),
                 .top = FindEdge<PerChannel,
                                 false,
                                 false>(texture, centerPoint, tolerance),
                 .right = FindEdge<PerChannel,
                                   true,
                                   true>(texture, centerPoint, tolerance),
                 .bottom = FindEdge<PerChannel,
                                    false,
                                    true>(texture, centerPoint, tolerance) };
}

RECT DetectEdges(const BGRATextureView& texture,
                 const POINT centerPoint,
                 const bool perChannel,
                 const uint8_t tolerance)
{
    auto function = perChannel ? &DetectEdgesInternal<true> : DetectEdgesInternal<false>;

    return function(texture, centerPoint, tolerance);
}
