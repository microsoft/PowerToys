#pragma once

#include <vector>
#include <stdint.h>

struct DenoiseState;

class NoiseSuppressor
{
public:
    NoiseSuppressor();
    ~NoiseSuppressor();

    NoiseSuppressor(const NoiseSuppressor&) = delete;
    NoiseSuppressor& operator=(const NoiseSuppressor&) = delete;

    // Process interleaved multi-channel float samples in-place.
    // Each channel is denoised independently through its own RNNoise state in
    // 480-sample frames, preserving the original channel layout (e.g. a mic
    // wired only to the left channel stays on the left and is not duplicated
    // onto the right).
    void Process(float* samples, uint32_t sampleCount, uint32_t channels);

private:
    // Per-channel RNNoise state and buffers so each channel is denoised
    // independently and the channel layout is preserved.
    struct ChannelState
    {
        DenoiseState* state = nullptr;
        std::vector<float> work;      // Working buffer for the current quantum
        std::vector<float> residual;  // Leftover samples from previous quantum
    };

    void EnsureChannelCount(uint32_t channels);

    std::vector<ChannelState> m_channels;
};
