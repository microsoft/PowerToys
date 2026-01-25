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

        // Loopback capture is required
        if (!m_loopbackCapture)
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
        return std::nullopt;
    }
    else
    {
        auto lock = m_lock.lock_exclusive();
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
            
            OutputDebugStringA("Starting loopback capture...\n");
            m_loopbackCapture->Start();
            OutputDebugStringA("Loopback capture started\n");
        }
        
        m_audioGraph.Start();
        OutputDebugStringA("AudioGraph started\n");
    }
}

void AudioSampleGenerator::Stop()
{
    // Stop may be called during teardown even if initialization hasn't completed.
    // It must never throw.
    m_endEvent.SetEvent();

    if (!m_initialized.load())
    {
        return;
    }

    m_asyncInitialized.wait();
    
    // Mark as stopped first to prevent further sample processing
    auto wasStarted = m_started.exchange(false);
    
    // Stop loopback capture
    if (m_loopbackCapture)
    {
        m_loopbackCapture->Stop();
    }
    
    // Clear any accumulated buffers
    {
        auto lock = m_loopbackBufferLock.lock_exclusive();
        m_loopbackBuffer.clear();
    }
    {
        auto lock = m_lock.lock_exclusive();
        m_samples.clear();
    }
    
    if (wasStarted)
    {
        m_audioGraph.Stop();
    }
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
                uint32_t inputFrames = static_cast<uint32_t>(rawLoopbackSamples.size()) / m_loopbackChannels;
                uint32_t outputFrames = static_cast<uint32_t>(inputFrames / m_resampleRatio);
                
                if (outputFrames > 0)
                {
                    std::vector<float> resampledSamples;
                    resampledSamples.reserve(outputFrames * m_graphChannels);
                    
                    for (uint32_t outFrame = 0; outFrame < outputFrames; outFrame++)
                    {
                        double inputPos = outFrame * m_resampleRatio;
                        uint32_t inputFrame = static_cast<uint32_t>(inputPos);
                        double frac = inputPos - inputFrame;
                        
                        if (inputFrame >= inputFrames - 1)
                        {
                            inputFrame = inputFrames > 1 ? inputFrames - 2 : 0;
                            frac = 1.0;
                        }
                        
                        for (uint32_t outCh = 0; outCh < m_graphChannels; outCh++)
                        {
                            float sample;
                            
                            if (m_loopbackChannels == m_graphChannels)
                            {
                                uint32_t idx1 = inputFrame * m_loopbackChannels + outCh;
                                uint32_t idx2 = (inputFrame + 1) * m_loopbackChannels + outCh;
                                
                                if (idx2 < rawLoopbackSamples.size())
                                {
                                    sample = static_cast<float>(rawLoopbackSamples[idx1] * (1.0 - frac) + 
                                                                rawLoopbackSamples[idx2] * frac);
                                }
                                else
                                {
                                    sample = rawLoopbackSamples[idx1];
                                }
                            }
                            else if (m_loopbackChannels > m_graphChannels)
                            {
                                float sum = 0.0f;
                                for (uint32_t inCh = 0; inCh < m_loopbackChannels; inCh++)
                                {
                                    uint32_t idx = inputFrame * m_loopbackChannels + inCh;
                                    if (idx < rawLoopbackSamples.size())
                                    {
                                        sum += rawLoopbackSamples[idx];
                                    }
                                }
                                sample = sum / m_loopbackChannels;
                            }
                            else
                            {
                                uint32_t idx = inputFrame * m_loopbackChannels;
                                sample = (idx < rawLoopbackSamples.size()) ? rawLoopbackSamples[idx] : 0.0f;
                            }
                            
                            resampledSamples.push_back(sample);
                        }
                    }
                    
                    auto loopbackLock = m_loopbackBufferLock.lock_exclusive();
                    const size_t maxBufferSize = m_graphSampleRate * m_graphChannels;
                    
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
                
                static int noMicDebugCounter = 0;
                if (noMicDebugCounter++ % 50 == 0)
                {
                    OutputDebugStringA(("Loopback-only mode: using " + std::to_string(samplesToUse) + " loopback samples, outputCount=" + 
                        std::to_string(outputSampleCount) + "\n").c_str());
                }
                
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
            
            static int mixDebugCounter = 0;
            if (samplesToMix > 0 && mixDebugCounter++ % 50 == 0)
            {
                OutputDebugStringA(("Mixing " + std::to_string(samplesToMix) + " loopback into " + 
                    std::to_string(numMicSamples) + " mic samples\n").c_str());
            }
            
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
        }
    }
    m_audioEvent.SetEvent();
}
