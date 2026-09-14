#pragma once

#include <cstdint>

class ZoomAnimation
{
public:
    static std::uint64_t OriginalDuration(float currentZoom, float targetZoom, bool firstStepWasImmediate, std::uint64_t stepTimeMilliseconds = 20);
    static std::uint64_t Duration(float currentZoom, float targetZoom, bool firstStepWasImmediate);

    void Start(float currentZoom, float targetZoom, std::uint64_t startTimeMilliseconds, std::uint64_t durationMilliseconds);
    float Retarget(float targetZoom, std::uint64_t startTimeMilliseconds, std::uint64_t durationMilliseconds);
    float Sample(std::uint64_t timeMilliseconds);
    void Stop(float zoomLevel);

    bool IsActive() const;
    float Target() const;

private:
    float m_startZoom = 1.0f;
    float m_currentZoom = 1.0f;
    float m_targetZoom = 1.0f;
    std::uint64_t m_startTimeMilliseconds = 0;
    std::uint64_t m_durationMilliseconds = 0;
    float m_startVelocityLogPerMillisecond = 0.0f;
    bool m_active = false;
};