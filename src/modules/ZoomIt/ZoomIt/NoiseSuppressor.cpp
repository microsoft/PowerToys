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
    m_state = rnnoise_create(nullptr);
}

NoiseSuppressor::~NoiseSuppressor()
{
    if (m_state)
    {
        rnnoise_destroy(m_state);
    }
}

void NoiseSuppressor::Process(float* samples, uint32_t sampleCount, uint32_t channels)
{
    if (!m_state || sampleCount == 0 || channels == 0)
    {
        return;
    }

    // Convert interleaved multi-channel to mono by averaging channels
    uint32_t frameCount = sampleCount / channels;
    uint32_t totalMonoSamples = static_cast<uint32_t>(m_residualBuffer.size()) + frameCount;

    m_monoBuffer.resize(totalMonoSamples);

    // Copy residual from previous call
    uint32_t residualCount = static_cast<uint32_t>(m_residualBuffer.size());
    if (residualCount > 0)
    {
        memcpy(m_monoBuffer.data(), m_residualBuffer.data(), residualCount * sizeof(float));
    }

    // Downmix to mono and scale to PCM16 range for RNNoise
    for (uint32_t i = 0; i < frameCount; i++)
    {
        float sum = 0.0f;
        for (uint32_t ch = 0; ch < channels; ch++)
        {
            sum += samples[i * channels + ch];
        }
        m_monoBuffer[residualCount + i] = (sum / channels) * PCM16_SCALE;
    }

    // Process complete 480-sample frames through RNNoise
    uint32_t processedMonoSamples = 0;
    while (processedMonoSamples + RNNOISE_FRAME_SIZE <= totalMonoSamples)
    {
        rnnoise_process_frame(m_state, &m_monoBuffer[processedMonoSamples], &m_monoBuffer[processedMonoSamples]);
        processedMonoSamples += RNNOISE_FRAME_SIZE;
    }

    // Save unprocessed residual for next call
    uint32_t residualRemaining = totalMonoSamples - processedMonoSamples;
    m_residualBuffer.assign(
        m_monoBuffer.begin() + processedMonoSamples,
        m_monoBuffer.end());

    // Write denoised mono back to interleaved output, scaling back to normalized float
    // Only write back samples that correspond to this call's input (skip residual from previous call)
    uint32_t outputMonoSamples = processedMonoSamples > residualCount
        ? processedMonoSamples - residualCount
        : 0;

    for (uint32_t i = 0; i < frameCount; i++)
    {
        float denoised = m_monoBuffer[residualCount + i] * PCM16_SCALE_INV;

        // Duplicate mono to all channels
        for (uint32_t ch = 0; ch < channels; ch++)
        {
            samples[i * channels + ch] = denoised;
        }
    }
}
