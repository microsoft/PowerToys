#include "pch.h"
#include "NoiseSuppressor.h"

extern "C" {
#include "rnnoise/rnnoise.h"
}

// RNNoise processes 480 mono samples per frame (10ms at 48kHz)
static constexpr uint32_t RNNOISE_FRAME_SIZE = 480;

// RNNoise expects samples in PCM16 range (-32768 to 32767), not normalized float [-1, 1]
static constexpr float PCM16_SCALE = 32768.0f;
static constexpr float PCM16_SCALE_INV = 1.0f / 32768.0f;

NoiseSuppressor::NoiseSuppressor()
{
}

NoiseSuppressor::~NoiseSuppressor()
{
    for (auto& channel : m_channels)
    {
        if (channel.state)
        {
            rnnoise_destroy(channel.state);
        }
    }
}

void NoiseSuppressor::EnsureChannelCount(uint32_t channels)
{
    if (m_channels.size() == channels)
    {
        return;
    }

    // Channel count changed (or first call): rebuild per-channel RNNoise state.
    for (auto& channel : m_channels)
    {
        if (channel.state)
        {
            rnnoise_destroy(channel.state);
        }
    }

    m_channels.clear();
    m_channels.resize(channels);
    for (auto& channel : m_channels)
    {
        channel.state = rnnoise_create(nullptr);
    }
}

void NoiseSuppressor::Process(float* samples, uint32_t sampleCount, uint32_t channels)
{
    if (sampleCount == 0 || channels == 0)
    {
        return;
    }

    EnsureChannelCount(channels);

    uint32_t frameCount = sampleCount / channels;

    // Denoise each channel independently so the original channel layout is
    // preserved (e.g. a mic wired only to the left channel stays on the left
    // and silent channels stay silent instead of being filled with the voice).
    for (uint32_t ch = 0; ch < channels; ch++)
    {
        ChannelState& channel = m_channels[ch];
        if (!channel.state)
        {
            continue;
        }

        uint32_t residualCount = static_cast<uint32_t>(channel.residual.size());
        uint32_t totalSamples = residualCount + frameCount;

        channel.work.resize(totalSamples);

        // Copy residual from previous call
        if (residualCount > 0)
        {
            memcpy(channel.work.data(), channel.residual.data(), residualCount * sizeof(float));
        }

        // Deinterleave this channel and scale to PCM16 range for RNNoise
        for (uint32_t i = 0; i < frameCount; i++)
        {
            channel.work[residualCount + i] = samples[i * channels + ch] * PCM16_SCALE;
        }

        // Process complete 480-sample frames through RNNoise
        uint32_t processedSamples = 0;
        while (processedSamples + RNNOISE_FRAME_SIZE <= totalSamples)
        {
            rnnoise_process_frame(channel.state, &channel.work[processedSamples], &channel.work[processedSamples]);
            processedSamples += RNNOISE_FRAME_SIZE;
        }

        // Save unprocessed residual for next call
        channel.residual.assign(
            channel.work.begin() + processedSamples,
            channel.work.end());

        // Write denoised samples back to the interleaved buffer, scaling back to
        // normalized float. Only this call's input region is written back.
        for (uint32_t i = 0; i < frameCount; i++)
        {
            samples[i * channels + ch] = channel.work[residualCount + i] * PCM16_SCALE_INV;
        }
    }
}
