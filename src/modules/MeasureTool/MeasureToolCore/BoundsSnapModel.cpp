#include "BoundsSnapModel.h"

#include <algorithm>
#include <array>
#include <cstdint>
#include <cstdlib>
#include <utility>
#include <vector>

namespace BoundsSnapModel
{
    namespace
    {
        constexpr size_t MinimumContentPixels = 4;
        constexpr int MaximumBoundaryInset = 16;
        constexpr uint8_t MaximumAdaptiveToleranceIncrease = 32;
        constexpr uint8_t AdaptiveTolerancePadding = 4;
        constexpr uint8_t BackgroundDriftMultiplier = 3;
        constexpr size_t AdaptiveTolerancePercentileNumerator = 19;
        constexpr size_t AdaptiveTolerancePercentileDenominator = 20;
        constexpr uint64_t StrongBoundaryRatioNumerator = 3;
        constexpr uint64_t StrongBoundaryRatioDenominator = 5;
        constexpr std::array<POINT, 4> Neighbors{
            POINT{ -1, 0 },
            POINT{ 1, 0 },
            POINT{ 0, -1 },
            POINT{ 0, 1 },
        };

        uint32_t ColorDistance(uint32_t first, uint32_t second)
        {
            uint32_t distance = 0;
            for (int shift = 0; shift < 24; shift += 8)
            {
                const int firstChannel = static_cast<int>((first >> shift) & 0xff);
                const int secondChannel = static_cast<int>((second >> shift) & 0xff);
                distance += static_cast<uint32_t>(std::abs(firstChannel - secondChannel));
            }
            return distance;
        }

        template<bool PerChannel>
        uint32_t ToleranceDistance(uint32_t first, uint32_t second)
        {
            if constexpr (!PerChannel)
            {
                return ColorDistance(first, second);
            }
            else
            {
                uint32_t distance = 0;
                for (int shift = 0; shift < 24; shift += 8)
                {
                    const int firstChannel = static_cast<int>((first >> shift) & 0xff);
                    const int secondChannel = static_cast<int>((second >> shift) & 0xff);
                    distance = std::max(
                        distance,
                        static_cast<uint32_t>(std::abs(firstChannel - secondChannel)));
                }
                return distance;
            }
        }

        template<bool PerChannel>
        uint8_t GetAdaptiveBackgroundTolerance(
            const BGRATextureView& texture,
            const RECT& selection,
            uint8_t configuredTolerance)
        {
            std::vector<uint32_t> perimeterDifferences;
            const size_t horizontalSamples =
                (static_cast<size_t>(selection.right) - static_cast<size_t>(selection.left)) * 2;
            const size_t verticalSamples =
                (static_cast<size_t>(selection.bottom) - static_cast<size_t>(selection.top)) * 2;
            perimeterDifferences.reserve(horizontalSamples + verticalSamples);

            const auto addDifference = [&](LONG firstX, LONG firstY, LONG secondX, LONG secondY) {
                perimeterDifferences.push_back(ToleranceDistance<PerChannel>(
                    texture.GetPixel(static_cast<size_t>(firstX), static_cast<size_t>(firstY)),
                    texture.GetPixel(static_cast<size_t>(secondX), static_cast<size_t>(secondY))));
            };

            for (LONG x = selection.left + 1; x <= selection.right; ++x)
            {
                addDifference(x - 1, selection.top, x, selection.top);
                addDifference(x - 1, selection.bottom, x, selection.bottom);
            }
            for (LONG y = selection.top + 1; y <= selection.bottom; ++y)
            {
                addDifference(selection.left, y - 1, selection.left, y);
                addDifference(selection.right, y - 1, selection.right, y);
            }

            if (perimeterDifferences.empty())
            {
                return configuredTolerance;
            }

            const size_t percentileIndex =
                (perimeterDifferences.size() - 1) *
                AdaptiveTolerancePercentileNumerator /
                AdaptiveTolerancePercentileDenominator;
            std::nth_element(
                perimeterDifferences.begin(),
                perimeterDifferences.begin() + percentileIndex,
                perimeterDifferences.end());
            const uint32_t perimeterNoise = perimeterDifferences[percentileIndex];
            const uint32_t maximumTolerance = std::min(
                uint32_t{ 255 },
                static_cast<uint32_t>(configuredTolerance) + MaximumAdaptiveToleranceIncrease);
            return static_cast<uint8_t>(std::clamp(
                perimeterNoise + AdaptiveTolerancePadding,
                static_cast<uint32_t>(configuredTolerance),
                maximumTolerance));
        }

        constexpr uint8_t GetBackgroundDriftTolerance(uint8_t backgroundTolerance)
        {
            return static_cast<uint8_t>(std::min(
                uint32_t{ 255 },
                static_cast<uint32_t>(backgroundTolerance) * BackgroundDriftMultiplier));
        }

        constexpr uint8_t GetBoundaryTolerance(uint8_t configuredTolerance)
        {
            return configuredTolerance == 0 ?
                       0 :
                       static_cast<uint8_t>(std::max(1, configuredTolerance / 2));
        }

        struct BoundaryScore
        {
            LONG coordinate = 0;
            uint64_t strength = 0;
            size_t contrastingPixels = 0;
        };

        template<bool PerChannel, typename PixelPair>
        BoundaryScore ScoreBoundary(
            LONG coordinate,
            LONG spanStart,
            LONG spanEnd,
            uint8_t tolerance,
            PixelPair&& pixelPair)
        {
            BoundaryScore result{ .coordinate = coordinate };
            for (LONG position = spanStart; position <= spanEnd; ++position)
            {
                const auto [first, second] = pixelPair(coordinate, position);
                if (!BGRATextureView::PixelsClose<PerChannel>(first, second, tolerance))
                {
                    ++result.contrastingPixels;
                    result.strength += ColorDistance(first, second);
                }
            }
            return result;
        }

        template<bool PerChannel, typename PixelPair>
        std::optional<LONG> FindStrongBoundary(
            LONG firstCoordinate,
            LONG lastCoordinate,
            LONG step,
            LONG spanStart,
            LONG spanEnd,
            uint8_t tolerance,
            PixelPair&& pixelPair)
        {
            const size_t span =
                static_cast<size_t>(spanEnd) - static_cast<size_t>(spanStart) + size_t{ 1 };
            const size_t minimumContrastingPixels = std::max(MinimumContentPixels, span / 3);
            std::vector<BoundaryScore> candidates;
            for (LONG coordinate = firstCoordinate;
                 step > 0 ? coordinate <= lastCoordinate : coordinate >= lastCoordinate;
                 coordinate += step)
            {
                auto score = ScoreBoundary<PerChannel>(
                    coordinate,
                    spanStart,
                    spanEnd,
                    tolerance,
                    pixelPair);
                if (score.contrastingPixels >= minimumContrastingPixels)
                {
                    candidates.push_back(score);
                }
            }
            if (candidates.empty())
            {
                return std::nullopt;
            }

            const uint64_t strongest = std::max_element(
                                                   candidates.begin(),
                                                   candidates.end(),
                                                   [](const auto& first, const auto& second) {
                                                       return first.strength < second.strength;
                                                   })
                                                   ->strength;
            const uint64_t threshold =
                strongest * StrongBoundaryRatioNumerator / StrongBoundaryRatioDenominator;
            const auto outermostStrongBoundary = std::find_if(
                candidates.begin(),
                candidates.end(),
                [threshold](const auto& candidate) {
                    return candidate.strength >= threshold;
                });
            return outermostStrongBoundary->coordinate;
        }

        template<bool PerChannel>
        RECT RefineContentBounds(
            const BGRATextureView& texture,
            const RECT& selection,
            RECT bounds,
            uint8_t tolerance)
        {
            const RECT original = bounds;
            const LONG leftSearchStart = std::max(selection.left + 1, original.left - MaximumBoundaryInset);
            const LONG leftSearchEnd = std::min(selection.right - 1, original.left + MaximumBoundaryInset);
            const LONG rightSearchStart = std::min(selection.right - 1, original.right + MaximumBoundaryInset);
            const LONG rightSearchEnd = std::max(selection.left + 1, original.right - MaximumBoundaryInset);
            const LONG topSearchStart = std::max(selection.top + 1, original.top - MaximumBoundaryInset);
            const LONG topSearchEnd = std::min(selection.bottom - 1, original.top + MaximumBoundaryInset);
            const LONG bottomSearchStart = std::min(selection.bottom - 1, original.bottom + MaximumBoundaryInset);
            const LONG bottomSearchEnd = std::max(selection.top + 1, original.bottom - MaximumBoundaryInset);

            const auto verticalPair = [&](LONG x, LONG y) {
                return std::pair{
                    texture.GetPixel(static_cast<size_t>(x) - 1, static_cast<size_t>(y)),
                    texture.GetPixel(static_cast<size_t>(x), static_cast<size_t>(y)),
                };
            };
            const auto verticalEndPair = [&](LONG x, LONG y) {
                return std::pair{
                    texture.GetPixel(static_cast<size_t>(x), static_cast<size_t>(y)),
                    texture.GetPixel(static_cast<size_t>(x) + 1, static_cast<size_t>(y)),
                };
            };
            const auto horizontalPair = [&](LONG y, LONG x) {
                return std::pair{
                    texture.GetPixel(static_cast<size_t>(x), static_cast<size_t>(y) - 1),
                    texture.GetPixel(static_cast<size_t>(x), static_cast<size_t>(y)),
                };
            };
            const auto horizontalEndPair = [&](LONG y, LONG x) {
                return std::pair{
                    texture.GetPixel(static_cast<size_t>(x), static_cast<size_t>(y)),
                    texture.GetPixel(static_cast<size_t>(x), static_cast<size_t>(y) + 1),
                };
            };

            if (const auto left = FindStrongBoundary<PerChannel>(
                    leftSearchStart,
                    leftSearchEnd,
                    1,
                    original.top,
                    original.bottom,
                    tolerance,
                    verticalPair))
            {
                bounds.left = *left;
            }
            if (const auto right = FindStrongBoundary<PerChannel>(
                    rightSearchStart,
                    rightSearchEnd,
                    -1,
                    original.top,
                    original.bottom,
                    tolerance,
                    verticalEndPair))
            {
                bounds.right = *right;
            }
            if (const auto top = FindStrongBoundary<PerChannel>(
                    topSearchStart,
                    topSearchEnd,
                    1,
                    original.left,
                    original.right,
                    tolerance,
                    horizontalPair))
            {
                bounds.top = *top;
            }
            if (const auto bottom = FindStrongBoundary<PerChannel>(
                    bottomSearchStart,
                    bottomSearchEnd,
                    -1,
                    original.left,
                    original.right,
                    tolerance,
                    horizontalEndPair))
            {
                bounds.bottom = *bottom;
            }
            return bounds.left <= bounds.right && bounds.top <= bounds.bottom ?
                       bounds :
                       original;
        }

        template<bool PerChannel>
        std::optional<RECT> FitSelectionToContentInternal(
            const BGRATextureView& texture,
            const RECT& selection,
            uint8_t tolerance)
        {
            if (!texture.pixels || texture.width < 3 || texture.height < 3)
            {
                return std::nullopt;
            }

            const RECT clamped{
                .left = std::clamp<LONG>(selection.left, 0, static_cast<LONG>(texture.width) - 1),
                .top = std::clamp<LONG>(selection.top, 0, static_cast<LONG>(texture.height) - 1),
                .right = std::clamp<LONG>(selection.right, 0, static_cast<LONG>(texture.width) - 1),
                .bottom = std::clamp<LONG>(selection.bottom, 0, static_cast<LONG>(texture.height) - 1),
            };
            if (clamped.left >= clamped.right || clamped.top >= clamped.bottom)
            {
                return std::nullopt;
            }

            const int width = clamped.right - clamped.left + 1;
            const int height = clamped.bottom - clamped.top + 1;
            const size_t area = static_cast<size_t>(width) * static_cast<size_t>(height);
            std::vector<uint8_t> pixelState(area);
            std::vector<uint32_t> backgroundReference(area);
            std::vector<size_t> queue;
            queue.reserve(area);
            const uint8_t backgroundTolerance =
                GetAdaptiveBackgroundTolerance<PerChannel>(texture, clamped, tolerance);
            const uint8_t backgroundDriftTolerance =
                GetBackgroundDriftTolerance(backgroundTolerance);

            const auto indexOf = [width](int x, int y) {
                return static_cast<size_t>(y) * static_cast<size_t>(width) + static_cast<size_t>(x);
            };
            const auto addBackgroundSeed = [&](int x, int y) {
                const size_t index = indexOf(x, y);
                if (pixelState[index] == 0)
                {
                    pixelState[index] = 1;
                    backgroundReference[index] = texture.GetPixel(
                        static_cast<size_t>(clamped.left) + static_cast<size_t>(x),
                        static_cast<size_t>(clamped.top) + static_cast<size_t>(y));
                    queue.push_back(index);
                }
            };

            for (int x = 0; x < width; ++x)
            {
                addBackgroundSeed(x, 0);
                addBackgroundSeed(x, height - 1);
            }
            for (int y = 1; y < height - 1; ++y)
            {
                addBackgroundSeed(0, y);
                addBackgroundSeed(width - 1, y);
            }

            size_t readIndex = 0;
            while (readIndex < queue.size())
            {
                const size_t currentIndex = queue[readIndex++];
                const int x = static_cast<int>(currentIndex % static_cast<size_t>(width));
                const int y = static_cast<int>(currentIndex / static_cast<size_t>(width));
                const uint32_t currentPixel = texture.GetPixel(
                    static_cast<size_t>(clamped.left) + static_cast<size_t>(x),
                    static_cast<size_t>(clamped.top) + static_cast<size_t>(y));

                for (const POINT offset : Neighbors)
                {
                    const int neighborX = x + offset.x;
                    const int neighborY = y + offset.y;
                    if (neighborX < 0 || neighborX >= width || neighborY < 0 || neighborY >= height)
                    {
                        continue;
                    }

                    const size_t neighborIndex = indexOf(neighborX, neighborY);
                    if (pixelState[neighborIndex] != 0)
                    {
                        continue;
                    }

                    const uint32_t neighborPixel = texture.GetPixel(
                        static_cast<size_t>(clamped.left) + static_cast<size_t>(neighborX),
                        static_cast<size_t>(clamped.top) + static_cast<size_t>(neighborY));
                    const uint32_t referencePixel = backgroundReference[currentIndex];
                    if (BGRATextureView::PixelsClose<PerChannel>(
                            currentPixel,
                            neighborPixel,
                            backgroundTolerance) &&
                        BGRATextureView::PixelsClose<PerChannel>(
                            referencePixel,
                            neighborPixel,
                            backgroundDriftTolerance))
                    {
                        pixelState[neighborIndex] = 1;
                        backgroundReference[neighborIndex] = referencePixel;
                        queue.push_back(neighborIndex);
                    }
                }
            }

            size_t largestComponentSize = 0;
            RECT largestComponent{};
            for (int startY = 1; startY < height - 1; ++startY)
            {
                for (int startX = 1; startX < width - 1; ++startX)
                {
                    const size_t startIndex = indexOf(startX, startY);
                    if (pixelState[startIndex] != 0)
                    {
                        continue;
                    }

                    queue.clear();
                    queue.push_back(startIndex);
                    pixelState[startIndex] = 2;
                    size_t componentSize = 0;
                    RECT component{
                        .left = clamped.left + startX,
                        .top = clamped.top + startY,
                        .right = clamped.left + startX,
                        .bottom = clamped.top + startY,
                    };

                    for (size_t componentReadIndex = 0; componentReadIndex < queue.size(); ++componentReadIndex)
                    {
                        const size_t currentIndex = queue[componentReadIndex];
                        const int x = static_cast<int>(currentIndex % static_cast<size_t>(width));
                        const int y = static_cast<int>(currentIndex / static_cast<size_t>(width));
                        ++componentSize;
                        component.left = std::min(component.left, clamped.left + x);
                        component.top = std::min(component.top, clamped.top + y);
                        component.right = std::max(component.right, clamped.left + x);
                        component.bottom = std::max(component.bottom, clamped.top + y);

                        for (const POINT offset : Neighbors)
                        {
                            const int neighborX = x + offset.x;
                            const int neighborY = y + offset.y;
                            if (neighborX <= 0 || neighborX >= width - 1 ||
                                neighborY <= 0 || neighborY >= height - 1)
                            {
                                continue;
                            }

                            const size_t neighborIndex = indexOf(neighborX, neighborY);
                            if (pixelState[neighborIndex] == 0)
                            {
                                pixelState[neighborIndex] = 2;
                                queue.push_back(neighborIndex);
                            }
                        }
                    }

                    if (componentSize > largestComponentSize)
                    {
                        largestComponentSize = componentSize;
                        largestComponent = component;
                    }
                }
            }

            if (largestComponentSize < MinimumContentPixels)
            {
                return std::nullopt;
            }
            return RefineContentBounds<PerChannel>(
                texture,
                clamped,
                largestComponent,
                GetBoundaryTolerance(tolerance));
        }
    }

    std::optional<RECT> FitSelectionToContent(
        const BGRATextureView& texture,
        const RECT& selection,
        bool perColorChannel,
        uint8_t tolerance)
    {
        return perColorChannel ?
                   FitSelectionToContentInternal<true>(texture, selection, tolerance) :
                   FitSelectionToContentInternal<false>(texture, selection, tolerance);
    }
}
