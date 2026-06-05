#pragma once

#include <Windows.h>

#include <cmath>
#include <vector>

namespace FindMyMouseLogic
{
    struct PointerRecentMovement
    {
        POINT diff;
        ULONGLONG tick;
    };

    inline bool ShouldActivateFromShake(const std::vector<PointerRecentMovement>& movementHistory, int shakeMinimumDistance, int shakeFactor) noexcept
    {
        double distanceTravelled = 0;
        LONGLONG currentX = 0;
        LONGLONG minX = 0;
        LONGLONG maxX = 0;
        LONGLONG currentY = 0;
        LONGLONG minY = 0;
        LONGLONG maxY = 0;

        for (const PointerRecentMovement& movement : movementHistory)
        {
            currentX += movement.diff.x;
            currentY += movement.diff.y;
            distanceTravelled += std::sqrt(static_cast<double>(movement.diff.x) * movement.diff.x + static_cast<double>(movement.diff.y) * movement.diff.y);

            if (currentX < minX)
            {
                minX = currentX;
            }
            if (currentX > maxX)
            {
                maxX = currentX;
            }
            if (currentY < minY)
            {
                minY = currentY;
            }
            if (currentY > maxY)
            {
                maxY = currentY;
            }
        }

        if (distanceTravelled < static_cast<double>(shakeMinimumDistance))
        {
            return false;
        }

        const double rectangleWidth = static_cast<double>(maxX) - minX;
        const double rectangleHeight = static_cast<double>(maxY) - minY;
        const double diagonal = std::sqrt(rectangleWidth * rectangleWidth + rectangleHeight * rectangleHeight);
        return diagonal > 0 && distanceTravelled / diagonal > (static_cast<double>(shakeFactor) / 100.0);
    }
}
