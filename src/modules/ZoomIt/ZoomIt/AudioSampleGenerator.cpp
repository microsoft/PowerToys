#include "pch.h"
#include "AudioSampleGenerator.h"
#include "CaptureFrameWait.h"
#include "LoopbackCapture.h"
#include <wrl/client.h>

extern TCHAR g_MicrophoneDeviceId[];

namespace
{
    // Declare the IMemoryBufferByteAccess interface for accessing raw buffer data
    MIDL_INTERFACE("5b0d3235-4dba-4d44-8657-1f1d0f83e9a3")
    IMemoryBufferByteAccess : public IUnknown
    {
    public:
        virtual HRESULT STDMETHODCALLTYPE GetBuffer(
            BYTE** value,
            UINT32* capacity) = 0;
    };
}

namespace winrt
{
    using namespace Windows::Foundation;
    using namespace Windows::Storage;
    using namespace Windows::Storage::Streams;
    using namespace Windows::Media;
    using namespace Windows::Media::Audio;
    using namespace Windows::Media::Capture;
    using namespace Windows::Media::Core;
    using namespace Windows::Media::Render;
    using namespace Windows::Media::MediaProperties;
    using namespace Windows::Media::Devices;
    using namespace Windows::Devices::Enumeration;
}

AudioSampleGenerator::AudioSampleGenerator(bool captureMicrophone, bool captureSystemAudio)
    : m_captureMicrophone(captureMicrophone)
    , m_captureSystemAudio(captureSystemAudio)
{
    OutputDebugStringA(("AudioSampleGenerator created, captureMicrophone=" +
        std::string(captureMicrophone ? "true" : "false") +
        ", captureSystemAudio=" + std::string(captureSystemAudio ? "true" : "false") + "\n").c_str());
    m_audioEvent.create(wil::EventOptions::ManualReset);
    m_endEvent.create(wil::EventOptions::ManualReset);
    m_startEvent.create(wil::EventOptions::ManualReset);
    m_asyncInitialized.create(wil::EventOptions::ManualReset);
}

AudioSampleGenerator::~AudioSampleGenerator()
{
    Stop();
    if (m_audioGraph)
    {
        m_audioGraph.Close();
    }
}

winrt::IAsyncAction AudioSampleGenerator::InitializeAsync()
{
    auto expected = false;
    if (m_initialized.compare_exchange_strong(expected, true))
    {
        // Reset state in case this instance is reused.
        m_endEvent.ResetEvent();
        m_startEvent.ResetEvent();

        // Initialize the audio graph
        auto audioGraphSettings = winrt::AudioGraphSettings(winrt::AudioRenderCategory::Media);
        auto audioGraphResult = co_await winrt::AudioGraph::CreateAsync(audioGraphSettings);
        if (audioGraphResult.Status() != winrt::AudioGraphCreationStatus::Success)
        {
            throw winrt::hresult_error(E_FAIL, L"Failed to initialize AudioGraph!");
        }
        m_audioGraph = audioGraphResult.Graph();

        // Get AudioGraph encoding properties for resampling
        auto graphProps = m_audioGraph.EncodingProperties();
        m_graphSampleRate = graphProps.SampleRate();
        m_graphChannels = graphProps.ChannelCount();

        OutputDebugStringA(("AudioGraph initialized: " + std::to_string(m_graphSampleRate) +
            " Hz, " + std::to_string(m_graphChannels) + " ch\n").c_str());

        // Create submix node to mix microphone and loopback audio
        m_submixNode = m_audioGraph.CreateSubmixNode();
        m_audioOutputNode = m_audioGraph.CreateFrameOutputNode();
        m_submixNode.AddOutgoingConnection(m_audioOutputNode);

        // Initialize WASAPI loopback capture for system audio (if enabled)
        if (m_captureSystemAudio)
        {
            m_loopbackCapture = std::make_unique<LoopbackCapture>();
        }
        if (m_loopbackCapture && SUCCEEDED(m_loopbackCapture->Initialize()))
        {
            auto loopbackFormat = m_loopbackCapture->GetFormat();
            if (loopbackFormat)
            {
                m_loopbackChannels = loopbackFormat->nChannels;
                m_loopbackSampleRate = loopbackFormat->nSamplesPerSec;
                m_resampleRatio = static_cast<double>(m_loopbackSampleRate) / static_cast<double>(m_graphSampleRate);

                OutputDebugStringA(("Loopback initialized: " + std::to_string(m_loopbackSampleRate) +
                    " Hz, " + std::to_string(m_loopbackChannels) + " ch, resample ratio=" +
                    std::to_string(m_resampleRatio) + "\n").c_str());
            }
        }
        else if (m_captureSystemAudio)
        {
            OutputDebugStringA("WARNING: Failed to initialize loopback capture\n");
            m_loopbackCapture.reset();
        }

        // Always initialize a microphone input node to keep the AudioGraph running at real-time pace.
        // When mic capture is disabled, we mute it so only loopback audio is captured.
        {
            auto defaultMicrophoneId = winrt::MediaDevice::GetDefaultAudioCaptureId(winrt::AudioDeviceRole::Default);
            auto microphoneId = (m_captureMicrophone && g_MicrophoneDeviceId[0] != 0)
                ? winrt::to_hstring(g_MicrophoneDeviceId)
                : defaultMicrophoneId;
            if (!microphoneId.empty())
            {
                auto microphone = co_await winrt::DeviceInformation::CreateFromIdAsync(microphoneId);

                // Initialize audio input node
                auto inputNodeResult = co_await m_audioGraph.CreateDeviceInputNodeAsync(winrt::MediaCategory::Media, m_audioGraph.EncodingProperties(), microphone);
                if (inputNodeResult.Status() != winrt::AudioDeviceNodeCreationStatus::Success && microphoneId != defaultMicrophoneId)
                {
                    // If the selected microphone failed, try again with the default
                    microphone = co_await winrt::DeviceInformation::CreateFromIdAsync(defaultMicrophoneId);
                    inputNodeResult = co_await m_audioGraph.CreateDeviceInputNodeAsync(winrt::MediaCategory::Media, m_audioGraph.EncodingProperties(), microphone);
                }
                if (inputNodeResult.Status() == winrt::AudioDeviceNodeCreationStatus::Success)
                {
                    m_audioInputNode = inputNodeResult.DeviceInputNode();
                    m_audioInputNode.AddOutgoingConnection(m_submixNode);

                    // If mic capture is disabled, mute the input so only loopback is captured
                    if (!m_captureMicrophone)
                    {
                        m_audioInputNode.OutgoingGain(0.0);
                        OutputDebugStringA("Mic input created but muted (loopback-only mode)\n");
                    }
                    else
                    {
                        OutputDebugStringA("Mic input created and active\n");
                    }
                }
            }
        }

        // Loopback capture is only required when system audio capture is enabled
        if (m_captureSystemAudio && !m_loopbackCapture)
        {
            throw winrt::hresult_error(E_FAIL, L"Failed to initialize loopback audio capture!");
        }

        m_audioGraph.QuantumStarted({ this, &AudioSampleGenerator::OnAudioQuantumStarted });

        m_asyncInitialized.SetEvent();
    }
}

winrt::AudioEncodingProperties AudioSampleGenerator::GetEncodingProperties()
{
    CheckInitialized();
    return m_audioOutputNode.EncodingProperties();
}

std::optional<winrt::MediaStreamSample> AudioSampleGenerator::TryGetNextSample()
{
    CheckInitialized();

    // The MediaStreamSource can request audio samples before we've started the audio graph.
    // Instead of throwing (which crashes the app), wait until either Start() is called
    // or Stop() signals end-of-stream.
    if (!m_started.load())
    {
        std::vector<HANDLE> events = { m_endEvent.get(), m_startEvent.get() };
        auto waitResult = WaitForMultipleObjectsEx(static_cast<DWORD>(events.size()), events.data(), false, INFINITE, false);
        auto eventIndex = -1;
        switch (waitResult)
        {
        case WAIT_OBJECT_0:
        case WAIT_OBJECT_0 + 1:
            eventIndex = waitResult - WAIT_OBJECT_0;
            break;
        }
        WINRT_VERIFY(eventIndex >= 0);

        if (events[eventIndex] == m_endEvent.get())
        {
            // End event signaled, but check if there are any remaining samples in the queue
            auto lock = m_lock.lock_exclusive();
            if (!m_samples.empty())
            {
                std::optional result(m_samples.front());
                m_samples.pop_front();
                return result;
            }
            return std::nullopt;
        }
    }

    {
        auto lock = m_lock.lock_exclusive();
        if (m_samples.empty() && m_endEvent.is_signaled())
        {
            return std::nullopt;
        }
        else if (!m_samples.empty())
        {
            std::optional result(m_samples.front());
            m_samples.pop_front();
            return result;
        }
    }

    m_audioEvent.ResetEvent();
    std::vector<HANDLE> events = { m_endEvent.get(), m_audioEvent.get() };
    auto waitResult = WaitForMultipleObjectsEx(static_cast<DWORD>(events.size()), events.data(), false, INFINITE, false);
    auto eventIndex = -1;
    switch (waitResult)
    {
    case WAIT_OBJECT_0:
    case WAIT_OBJECT_0 + 1:
        eventIndex = waitResult - WAIT_OBJECT_0;
        break;
    }
    WINRT_VERIFY(eventIndex >= 0);

    auto signaledEvent = events[eventIndex];
    if (signaledEvent == m_endEvent.get())
    {
        // End was signaled, but check for any remaining samples before returning nullopt
        auto lock = m_lock.lock_exclusive();
        if (!m_samples.empty())
        {
            std::optional result(m_samples.front());
            m_samples.pop_front();
            return result;
        }
        return std::nullopt;
    }
    else
    {
        auto lock = m_lock.lock_exclusive();
        if (m_samples.empty())
        {
            // Spurious wake or race - no samples available
            // If end is signaled, return nullopt; otherwise this shouldn't happen
            return m_endEvent.is_signaled() ? std::nullopt : std::optional<winrt::MediaStreamSample>{};
        }
        std::optional result(m_samples.front());
        m_samples.pop_front();
        return result;
    }
}

void AudioSampleGenerator::Start()
{
    CheckInitialized();
    auto expected = false;
    if (m_started.compare_exchange_strong(expected, true))
    {
        m_endEvent.ResetEvent();
        m_startEvent.SetEvent();

        // Start loopback capture if available
        if (m_loopbackCapture)
        {
            // Clear any stale samples
            {
                auto lock = m_loopbackBufferLock.lock_exclusive();
                m_loopbackBuffer.clear();
            }

            m_resampleInputBuffer.clear();
            m_resampleInputPos = 0.0;

            m_loopbackCapture->Start();
        }

        m_audioGraph.Start();
    }
}

void AudioSampleGenerator::Stop()
{
    // Stop may be called during teardown even if initialization hasn't completed.
    // It must never throw.

    if (!m_initialized.load())
    {
        m_endEvent.SetEvent();
        return;
    }

    m_asyncInitialized.wait();

    // Stop loopback capture first
    if (m_loopbackCapture)
    {
        m_loopbackCapture->Stop();
    }

    // Flush any remaining samples from the loopback capture before stopping the audio graph
    FlushRemainingAudio();

    // Stop the audio graph - no more quantum callbacks will run
    m_audioGraph.Stop();

    // Mark as stopped
    m_started.store(false);

    // Combine all remaining queued samples into one final sample so it can be
    // returned immediately without waiting for additional TryGetNextSample calls
    CombineQueuedSamples();

    // NOW signal end event - this allows TryGetNextSample to return remaining
    // queued samples and then return nullopt
    m_endEvent.SetEvent();
    m_audioEvent.SetEvent(); // Also wake any waiting TryGetNextSample

    // DO NOT clear m_loopbackBuffer or m_samples here - allow MediaTranscoder to
    // consume remaining queued audio samples to avoid audio cutoff at end of recording.
    // TryGetNextSample() will return nullopt once m_samples is empty and
    // m_endEvent is signaled. Buffers will be cleaned up on destruction.
}

void AudioSampleGenerator::AppendResampledLoopbackSamples(std::vector<float> const& rawLoopbackSamples, bool flushRemaining)
{
    if (rawLoopbackSamples.empty())
    {
        return;
    }

    m_resampleInputBuffer.insert(m_resampleInputBuffer.end(), rawLoopbackSamples.begin(), rawLoopbackSamples.end());

    if (m_loopbackChannels == 0 || m_graphChannels == 0 || m_resampleRatio <= 0.0)
    {
        return;
    }

    std::vector<float> resampledSamples;
    while (true)
    {
        const uint32_t inputFrames = static_cast<uint32_t>(m_resampleInputBuffer.size() / m_loopbackChannels);
        if (inputFrames == 0)
        {
            break;
        }

        if (!flushRemaining)
        {
            if (inputFrames < 2 || (m_resampleInputPos + 1.0) >= inputFrames)
            {
                break;
            }
        }
        else
        {
            if (m_resampleInputPos >= inputFrames)
            {
                break;
            }
        }

        uint32_t inputFrame = static_cast<uint32_t>(m_resampleInputPos);
        double frac = m_resampleInputPos - inputFrame;
        uint32_t nextFrame = (inputFrame + 1 < inputFrames) ? (inputFrame + 1) : inputFrame;

        for (uint32_t outCh = 0; outCh < m_graphChannels; outCh++)
        {
            float sample = 0.0f;

            if (m_loopbackChannels == m_graphChannels)
            {
                uint32_t idx1 = inputFrame * m_loopbackChannels + outCh;
                uint32_t idx2 = nextFrame * m_loopbackChannels + outCh;
                float s1 = m_resampleInputBuffer[idx1];
                float s2 = m_resampleInputBuffer[idx2];
                sample = static_cast<float>(s1 * (1.0 - frac) + s2 * frac);
            }
            else if (m_loopbackChannels > m_graphChannels)
            {
                float sum = 0.0f;
                for (uint32_t inCh = 0; inCh < m_loopbackChannels; inCh++)
                {
                    uint32_t idx1 = inputFrame * m_loopbackChannels + inCh;
                    uint32_t idx2 = nextFrame * m_loopbackChannels + inCh;
                    float s1 = m_resampleInputBuffer[idx1];
                    float s2 = m_resampleInputBuffer[idx2];
                    sum += static_cast<float>(s1 * (1.0 - frac) + s2 * frac);
                }
                sample = sum / m_loopbackChannels;
            }
            else
            {
                uint32_t idx1 = inputFrame * m_loopbackChannels;
                uint32_t idx2 = nextFrame * m_loopbackChannels;
                float s1 = m_resampleInputBuffer[idx1];
                float s2 = m_resampleInputBuffer[idx2];
                sample = static_cast<float>(s1 * (1.0 - frac) + s2 * frac);
            }

            resampledSamples.push_back(sample);
        }

        m_resampleInputPos += m_resampleRatio;
    }

    uint32_t consumedFrames = static_cast<uint32_t>(m_resampleInputPos);
    if (consumedFrames > 0)
    {
        size_t samplesToErase = static_cast<size_t>(consumedFrames) * m_loopbackChannels;
        if (samplesToErase >= m_resampleInputBuffer.size())
        {
            m_resampleInputBuffer.clear();
            m_resampleInputPos = 0.0;
        }
        else
        {
            m_resampleInputBuffer.erase(m_resampleInputBuffer.begin(), m_resampleInputBuffer.begin() + samplesToErase);
            m_resampleInputPos -= consumedFrames;
        }
    }

    if (flushRemaining)
    {
        m_resampleInputBuffer.clear();
        m_resampleInputPos = 0.0;
    }

    if (!resampledSamples.empty())
    {
        auto loopbackLock = m_loopbackBufferLock.lock_exclusive();
        const size_t maxBufferSize = static_cast<size_t>(m_graphSampleRate) * m_graphChannels;

        if (m_loopbackBuffer.size() + resampledSamples.size() > maxBufferSize)
        {
            size_t overflow = (m_loopbackBuffer.size() + resampledSamples.size()) - maxBufferSize;
            if (overflow >= m_loopbackBuffer.size())
            {
                m_loopbackBuffer.clear();
            }
            else
            {
                m_loopbackBuffer.erase(m_loopbackBuffer.begin(), m_loopbackBuffer.begin() + overflow);
            }
        }

        m_loopbackBuffer.insert(m_loopbackBuffer.end(), resampledSamples.begin(), resampledSamples.end());
    }
}

void AudioSampleGenerator::FlushRemainingAudio()
{
    // Called during stop to drain any remaining samples from loopback capture
    // and convert them to MediaStreamSamples before the audio graph stops.

    if (!m_loopbackCapture)
    {
        return;
    }

    auto lock = m_lock.lock_exclusive();

    // Drain all remaining samples from the loopback capture client
    std::vector<float> rawLoopbackSamples;
    {
        std::vector<float> tempSamples;
        while (m_loopbackCapture->TryGetSamples(tempSamples))
        {
            rawLoopbackSamples.insert(rawLoopbackSamples.end(), tempSamples.begin(), tempSamples.end());
        }
    }

    // Resample and channel-convert the loopback audio to match AudioGraph format
    if (!rawLoopbackSamples.empty())
    {
        AppendResampledLoopbackSamples(rawLoopbackSamples, true);
    }

    // Now convert everything in m_loopbackBuffer to MediaStreamSamples
    auto loopbackLock = m_loopbackBufferLock.lock_exclusive();

    if (!m_loopbackBuffer.empty())
    {
        uint32_t outputSampleCount = static_cast<uint32_t>(m_loopbackBuffer.size());
        std::vector<uint8_t> outputData(outputSampleCount * sizeof(float), 0);
        float* outputFloats = reinterpret_cast<float*>(outputData.data());

        for (uint32_t i = 0; i < outputSampleCount; i++)
        {
            float sample = m_loopbackBuffer[i];
            if (sample > 1.0f) sample = 1.0f;
            else if (sample < -1.0f) sample = -1.0f;
            outputFloats[i] = sample;
        }

        m_loopbackBuffer.clear();

        // Create buffer and sample
        winrt::Buffer sampleBuffer(outputSampleCount * sizeof(float));
        memcpy(sampleBuffer.data(), outputData.data(), outputData.size());
        sampleBuffer.Length(static_cast<uint32_t>(outputData.size()));

        if (sampleBuffer.Length() > 0)
        {
            const uint32_t sampleCount = sampleBuffer.Length() / sizeof(float);
            const uint32_t frames = (m_graphChannels > 0) ? (sampleCount / m_graphChannels) : 0;
            const int64_t durationTicks = (m_graphSampleRate > 0) ? (static_cast<int64_t>(frames) * 10000000LL / m_graphSampleRate) : 0;
            const winrt::TimeSpan duration{ durationTicks };

            winrt::TimeSpan timestamp{ 0 };
            if (m_hasLastSampleTimestamp)
            {
                timestamp = winrt::TimeSpan{ m_lastSampleTimestamp.count() + m_lastSampleDuration.count() };
            }

            auto sample = winrt::MediaStreamSample::CreateFromBuffer(sampleBuffer, timestamp);
            m_samples.push_back(sample);
            m_audioEvent.SetEvent();

            m_lastSampleTimestamp = timestamp;
            m_lastSampleDuration = duration;
            m_hasLastSampleTimestamp = true;
        }
    }
}

void AudioSampleGenerator::CombineQueuedSamples()
{
    // Combine all queued samples into a single sample so it can be returned
    // immediately in the next TryGetNextSample call. This is critical because
    // once video ends, the MediaTranscoder may only request one more audio sample.

    auto lock = m_lock.lock_exclusive();

    if (m_samples.size() <= 1)
    {
        return;
    }

    // Calculate total size and collect all sample data
    size_t totalBytes = 0;
    std::vector<std::pair<winrt::Windows::Storage::Streams::IBuffer, winrt::Windows::Foundation::TimeSpan>> buffers;
    winrt::Windows::Foundation::TimeSpan firstTimestamp{ 0 };
    bool hasFirstTimestamp = false;

    for (auto& sample : m_samples)
    {
        auto buffer = sample.Buffer();
        if (buffer)
        {
            totalBytes += buffer.Length();
            if (!hasFirstTimestamp)
            {
                firstTimestamp = sample.Timestamp();
                hasFirstTimestamp = true;
            }
            buffers.push_back({ buffer, sample.Timestamp() });
        }
    }

    if (totalBytes == 0)
    {
        return;
    }

    // Create combined buffer
    winrt::Buffer combinedBuffer(static_cast<uint32_t>(totalBytes));
    uint8_t* dest = combinedBuffer.data();
    uint32_t offset = 0;

    for (auto& [buffer, ts] : buffers)
    {
        uint32_t len = buffer.Length();
        memcpy(dest + offset, buffer.data(), len);
        offset += len;
    }
    combinedBuffer.Length(static_cast<uint32_t>(totalBytes));

    // Create combined sample with first timestamp
    auto combinedSample = winrt::Windows::Media::Core::MediaStreamSample::CreateFromBuffer(combinedBuffer, firstTimestamp);

    // Clear queue and add combined sample
    m_samples.clear();
    m_samples.push_back(combinedSample);

    // Update timestamp tracking
    const uint32_t sampleCount = static_cast<uint32_t>(totalBytes) / sizeof(float);
    const uint32_t frames = (m_graphChannels > 0) ? (sampleCount / m_graphChannels) : 0;
    const int64_t durationTicks = (m_graphSampleRate > 0) ? (static_cast<int64_t>(frames) * 10000000LL / m_graphSampleRate) : 0;
    m_lastSampleTimestamp = firstTimestamp;
    m_lastSampleDuration = winrt::Windows::Foundation::TimeSpan{ durationTicks };
    m_hasLastSampleTimestamp = true;
}

void AudioSampleGenerator::OnAudioQuantumStarted(winrt::AudioGraph const& sender, winrt::IInspectable const& args)
{
    // Don't process if we're not actively recording
    if (!m_started.load())
    {
        return;
    }

    {
        auto lock = m_lock.lock_exclusive();

        auto frame = m_audioOutputNode.GetFrame();
        std::optional<winrt::TimeSpan> timestamp = frame.RelativeTime();
        auto audioBuffer = frame.LockBuffer(winrt::AudioBufferAccessMode::Read);

        // Get mic audio as a buffer (may be empty if no microphone)
        auto sampleBuffer = winrt::Buffer::CreateCopyFromMemoryBuffer(audioBuffer);
        sampleBuffer.Length(audioBuffer.Length());

        // Calculate expected samples per quantum (~10ms at graph sample rate)
        // AudioGraph uses 10ms quantums by default
        uint32_t expectedSamplesPerQuantum = (m_graphSampleRate / 100) * m_graphChannels;
        uint32_t numMicSamples = audioBuffer.Length() / sizeof(float);

        // Drain loopback samples regardless of whether we have mic audio
        if (m_loopbackCapture)
        {
            std::vector<float> rawLoopbackSamples;
            {
                std::vector<float> tempSamples;
                while (m_loopbackCapture->TryGetSamples(tempSamples))
                {
                    rawLoopbackSamples.insert(rawLoopbackSamples.end(), tempSamples.begin(), tempSamples.end());
                }
            }

            // Resample and channel-convert the loopback audio to match AudioGraph format
            if (!rawLoopbackSamples.empty())
            {
                AppendResampledLoopbackSamples(rawLoopbackSamples);
            }
        }

        // Determine the actual number of samples we'll output
        // Use mic sample count if mic is enabled, otherwise use expected quantum size
        uint32_t outputSampleCount = m_captureMicrophone ? numMicSamples : expectedSamplesPerQuantum;

        // If microphone is disabled, create a buffer with only loopback audio
        if (!m_captureMicrophone && outputSampleCount > 0)
        {
            // Create a buffer filled with loopback audio or silence
            std::vector<uint8_t> outputData(outputSampleCount * sizeof(float), 0);
            float* outputFloats = reinterpret_cast<float*>(outputData.data());

            {
                auto loopbackLock = m_loopbackBufferLock.lock_exclusive();
                uint32_t samplesToUse = min(outputSampleCount, static_cast<uint32_t>(m_loopbackBuffer.size()));

                for (uint32_t i = 0; i < samplesToUse; i++)
                {
                    float sample = m_loopbackBuffer[i];
                    if (sample > 1.0f) sample = 1.0f;
                    else if (sample < -1.0f) sample = -1.0f;
                    outputFloats[i] = sample;
                }

                if (samplesToUse > 0)
                {
                    m_loopbackBuffer.erase(m_loopbackBuffer.begin(), m_loopbackBuffer.begin() + samplesToUse);
                }
            }

            // Create a new buffer with our loopback data
            sampleBuffer = winrt::Buffer(outputSampleCount * sizeof(float));
            memcpy(sampleBuffer.data(), outputData.data(), outputData.size());
            sampleBuffer.Length(static_cast<uint32_t>(outputData.size()));
        }
        else if (m_captureMicrophone && numMicSamples > 0)
        {
            // Mix loopback into mic samples
            auto loopbackLock = m_loopbackBufferLock.lock_exclusive();
            float* bufferData = reinterpret_cast<float*>(sampleBuffer.data());
            uint32_t samplesToMix = min(numMicSamples, static_cast<uint32_t>(m_loopbackBuffer.size()));

            for (uint32_t i = 0; i < samplesToMix; i++)
            {
                float mixed = bufferData[i] + m_loopbackBuffer[i];
                if (mixed > 1.0f) mixed = 1.0f;
                else if (mixed < -1.0f) mixed = -1.0f;
                bufferData[i] = mixed;
            }

            if (samplesToMix > 0)
            {
                m_loopbackBuffer.erase(m_loopbackBuffer.begin(), m_loopbackBuffer.begin() + samplesToMix);
            }
        }

        if (sampleBuffer.Length() > 0)
        {
            auto sample = winrt::MediaStreamSample::CreateFromBuffer(sampleBuffer, timestamp.value());
            m_samples.push_back(sample);

            const uint32_t sampleCount = sampleBuffer.Length() / sizeof(float);
            const uint32_t frames = (m_graphChannels > 0) ? (sampleCount / m_graphChannels) : 0;
            const int64_t durationTicks = (m_graphSampleRate > 0) ? (static_cast<int64_t>(frames) * 10000000LL / m_graphSampleRate) : 0;
            m_lastSampleTimestamp = timestamp.value();
            m_lastSampleDuration = winrt::TimeSpan{ durationTicks };
            m_hasLastSampleTimestamp = true;
        }
    }
    m_audioEvent.SetEvent();
}
