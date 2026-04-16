#pragma once

struct DenoiseState;

class NoiseSuppressor
{
public:
    NoiseSuppressor();
    ~NoiseSuppressor();

    NoiseSuppressor(const NoiseSuppressor&) = delete;
    NoiseSuppressor& operator=(const NoiseSuppressor&) = delete;

    // Process interleaved stereo float samples in-place.
    // Converts to mono, runs RNNoise denoising in 480-sample frames,
    // and writes the denoised audio back as stereo.
    void Process(float* samples, uint32_t sampleCount, uint32_t channels);

private:
    DenoiseState* m_state = nullptr;
    std::vector<float> m_monoBuffer;
    std::vector<float> m_residualBuffer;  // Leftover samples from previous quantum
};
