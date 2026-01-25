#pragma once

#include <mmdeviceapi.h>
#include <audioclient.h>
#include <atomic>
#include <vector>
#include <deque>
#include <wil/resource.h>

class LoopbackCapture
{
public:
    LoopbackCapture();
    ~LoopbackCapture();

    HRESULT Initialize();
    HRESULT Start();
    void Stop();

    // Returns audio samples in the format: PCM float, stereo, 48kHz
    bool TryGetSamples(std::vector<float>& samples);

    WAVEFORMATEX* GetFormat() const { return m_pwfx; }
    uint32_t GetSampleRate() const { return m_pwfx ? m_pwfx->nSamplesPerSec : 48000; }
    uint32_t GetChannels() const { return m_pwfx ? m_pwfx->nChannels : 2; }

private:
    void CaptureThread();

    winrt::com_ptr<IMMDeviceEnumerator> m_deviceEnumerator;
    winrt::com_ptr<IMMDevice> m_device;
    winrt::com_ptr<IAudioClient> m_audioClient;
    winrt::com_ptr<IAudioCaptureClient> m_captureClient;
    WAVEFORMATEX* m_pwfx{ nullptr };

    wil::unique_event m_stopEvent;
    wil::unique_event m_samplesReadyEvent;
    std::thread m_captureThread;

    wil::srwlock m_lock;
    std::deque<std::vector<float>> m_sampleQueue;

    std::atomic<bool> m_initialized{ false };
    std::atomic<bool> m_started{ false };
};
