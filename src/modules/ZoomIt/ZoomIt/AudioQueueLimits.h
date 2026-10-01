#pragma once

#include <cstddef>
#include <cstdint>

namespace audio_queue
{
    // Timestamped MediaStreamSamples waiting for the transcoder; depth costs only memory.
    constexpr size_t maxEncodedDurationSeconds = 20;

    // Untimestamped loopback audio consumed at real-time pace; depth becomes A/V drift.
    constexpr size_t maxLiveDurationSeconds = 1;

    constexpr size_t SampleCount(uint32_t sampleRate, uint32_t channels, size_t seconds)
    {
        return static_cast<size_t>(sampleRate) * channels * seconds;
    }

    constexpr size_t MaxEncodedFloatBytes(uint32_t sampleRate, uint32_t channels)
    {
        return SampleCount(sampleRate, channels, maxEncodedDurationSeconds) * sizeof(float);
    }

    constexpr size_t MaxLiveSampleCount(uint32_t sampleRate, uint32_t channels)
    {
        return SampleCount(sampleRate, channels, maxLiveDurationSeconds);
    }
}