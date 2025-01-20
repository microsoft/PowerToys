#pragma once

class AudioSampleGenerator
{
public:
    AudioSampleGenerator();
    ~AudioSampleGenerator();

    winrt::Windows::Foundation::IAsyncAction InitializeAsync();
    winrt::Windows::Media::MediaProperties::AudioEncodingProperties GetEncodingProperties();

    std::optional<winrt::Windows::Media::Core::MediaStreamSample> TryGetNextSample();
    void Start();
    void Stop();

private:
    void OnAudioQuantumStarted(
        winrt::Windows::Media::Audio::AudioGraph const& sender,
        winrt::Windows::Foundation::IInspectable const& args);

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
    winrt::Windows::Media::Audio::AudioFrameOutputNode m_audioOutputNode{ nullptr };
    wil::srwlock m_lock;
    wil::unique_event m_audioEvent;
    wil::unique_event m_endEvent;
    wil::unique_event m_asyncInitialized;
    std::deque<winrt::Windows::Media::Core::MediaStreamSample> m_samples;
    std::atomic<bool> m_initialized = false;
    std::atomic<bool> m_started = false;
};