#pragma once

#include "LoopbackCapture.h"
#include "NoiseSuppressor.h"

#include <deque>
#include <optional>
#include <memory>

class AudioSampleGenerator
{
public:
    AudioSampleGenerator(bool captureMicrophone = true, bool captureSystemAudio = true, bool mixMicrophoneMono = false, bool useNoiseCancellation = false);
    ~AudioSampleGenerator();

    winrt::Windows::Foundation::IAsyncAction InitializeAsync();
    winrt::Windows::Media::MediaProperties::AudioEncodingProperties GetEncodingProperties();

    std::optional<winrt::Windows::Media::Core::MediaStreamSample> TryGetNextSample();
    void Start(int64_t videoStartTimestamp = 0);
    void Stop();

private:
    void OnAudioQuantumStarted(
        winrt::Windows::Media::Audio::AudioGraph const& sender,
        winrt::Windows::Foundation::IInspectable const& args);

    void FlushRemainingAudio();
    void CombineQueuedSamples();
    void AppendResampledLoopbackSamples(std::vector<float> const& rawLoopbackSamples, bool flushRemaining = false);

    void CheckInitialized()
    {
        if (!m_initialized.load())
        {
            throw winrt::hresult_error(E_FAIL, L"Must initialize audio sample generator before use!");
        }
    }

    void CheckStarted()
    {
        if (!m_started.load())
        {
            throw winrt::hresult_error(E_FAIL, L"Must start audio sample generator before calling this method!");
        }
    }

private:
    winrt::Windows::Media::Audio::AudioGraph m_audioGraph{ nullptr };
    winrt::Windows::Media::Audio::AudioDeviceInputNode m_audioInputNode{ nullptr };
    winrt::Windows::Media::Audio::AudioSubmixNode m_submixNode{ nullptr };
    winrt::Windows::Media::Audio::AudioFrameOutputNode m_audioOutputNode{ nullptr };
    
    std::unique_ptr<LoopbackCapture> m_loopbackCapture;
    std::vector<float> m_loopbackBuffer;  // Accumulated loopback samples (resampled to match AudioGraph)
    wil::srwlock m_loopbackBufferLock;
    uint32_t m_loopbackChannels = 2;
    uint32_t m_loopbackSampleRate = 48000;
    uint32_t m_graphSampleRate = 48000;
    uint32_t m_graphChannels = 2;
    double m_resampleRatio = 1.0;  // loopbackSampleRate / graphSampleRate
    winrt::Windows::Foundation::TimeSpan m_lastSampleTimestamp{};
    winrt::Windows::Foundation::TimeSpan m_lastSampleDuration{};
    bool m_hasLastSampleTimestamp = false;
    std::vector<float> m_resampleInputBuffer; // raw loopback samples buffered for resampling
    double m_resampleInputPos = 0.0; // fractional input frame position for resampling
    
    wil::srwlock m_lock;
    wil::unique_event m_audioEvent;
    wil::unique_event m_endEvent;
    wil::unique_event m_startEvent;
    wil::unique_event m_asyncInitialized;
    std::deque<winrt::Windows::Media::Core::MediaStreamSample> m_samples;
    std::atomic<bool> m_initialized = false;
    std::atomic<bool> m_started = false;
    bool m_captureMicrophone = true;
    bool m_captureSystemAudio = true;
    bool m_mixMicrophoneMono = false;
    bool m_useNoiseCancellation = false;
    std::unique_ptr<NoiseSuppressor> m_noiseSuppressor;

    // Timestamp rebasing: audio RelativeTime → video SystemRelativeTime domain.
    // Without this, the transcoder sees audio timestamps (~350ms) far below video
    // timestamps (~111 billion ticks) and starves video while trying to fill the gap.
    int64_t m_videoStartTimestamp = 0;
    int64_t m_timestampOffset = 0;
    bool m_hasTimestampOffset = false;
};
