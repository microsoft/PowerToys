#include "ZoomAnimation.h"

#include <cmath>

std::uint64_t ZoomAnimation::OriginalDuration(float currentZoom, float targetZoom, bool firstStepWasImmediate, std::uint64_t stepTimeMilliseconds)
{
    if (currentZoom <= 0.0f || targetZoom <= 0.0f || currentZoom == targetZoom)
    {
        return 0;
    }

    constexpr float zoomInStep = 1.1f;
    constexpr float zoomOutStep = 0.8f;
    std::uint64_t stepCount = 0;
    auto steppedZoom = currentZoom;
    if (targetZoom > currentZoom)
    {
        while (steppedZoom < targetZoom)
        {
            steppedZoom *= zoomInStep;
            ++stepCount;
        }
    }
    else
    {
        while (steppedZoom > targetZoom)
        {
            steppedZoom *= zoomOutStep;
            ++stepCount;
        }
    }

    if (firstStepWasImmediate && stepCount != 0)
    {
        --stepCount;
    }

    return stepCount * stepTimeMilliseconds;
}

std::uint64_t ZoomAnimation::Duration(float currentZoom, float targetZoom, bool firstStepWasImmediate)
{
    constexpr std::uint64_t stepTimeMilliseconds = 40;
    constexpr std::uint64_t durationNumerator = 3;
    constexpr std::uint64_t durationDenominator = 2;
    return OriginalDuration(currentZoom, targetZoom, firstStepWasImmediate, stepTimeMilliseconds) * durationNumerator / durationDenominator;
}

void ZoomAnimation::Start(float currentZoom, float targetZoom, std::uint64_t startTimeMilliseconds, std::uint64_t durationMilliseconds)
{
    m_startZoom = currentZoom;
    m_currentZoom = currentZoom;
    m_targetZoom = targetZoom;
    m_startTimeMilliseconds = startTimeMilliseconds;
    m_durationMilliseconds = durationMilliseconds;
    m_active = currentZoom > 0.0f && targetZoom > 0.0f && currentZoom != targetZoom && durationMilliseconds != 0;

    if (!m_active)
    {
        m_currentZoom = targetZoom;
    }
}

float ZoomAnimation::Sample(std::uint64_t timeMilliseconds)
{
    if (!m_active)
    {
        return m_currentZoom;
    }

    const auto elapsedMilliseconds = timeMilliseconds > m_startTimeMilliseconds ? timeMilliseconds - m_startTimeMilliseconds : 0;
    if (elapsedMilliseconds >= m_durationMilliseconds)
    {
        m_currentZoom = m_targetZoom;
        m_active = false;
        return m_currentZoom;
    }

    const auto progress = static_cast<float>(elapsedMilliseconds) / static_cast<float>(m_durationMilliseconds);
    const auto inverseProgress = 1.0f - progress;
    const auto easedProgress = 1.0f - inverseProgress * inverseProgress * inverseProgress;
    const auto startScale = std::log(m_startZoom);
    const auto targetScale = std::log(m_targetZoom);
    m_currentZoom = std::exp(startScale + (targetScale - startScale) * easedProgress);
    return m_currentZoom;
}

void ZoomAnimation::Stop(float zoomLevel)
{
    m_startZoom = zoomLevel;
    m_currentZoom = zoomLevel;
    m_targetZoom = zoomLevel;
    m_durationMilliseconds = 0;
    m_active = false;
}

bool ZoomAnimation::IsActive() const
{
    return m_active;
}

float ZoomAnimation::Target() const
{
    return m_targetZoom;
}