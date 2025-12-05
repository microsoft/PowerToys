#include "pch.h"
#include "AudioSampleGenerator.h"
#include "CaptureFrameWait.h"

extern TCHAR g_MicrophoneDeviceId[];

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

AudioSampleGenerator::AudioSampleGenerator()
{
    m_audioEvent.create(wil::EventOptions::ManualReset);
    m_endEvent.create(wil::EventOptions::ManualReset);
    m_asyncInitialized.create(wil::EventOptions::ManualReset);
}

AudioSampleGenerator::~AudioSampleGenerator()
{
    Stop();
    if (m_started.load())
    {
        m_audioGraph.Close();
    }
}

winrt::IAsyncAction AudioSampleGenerator::InitializeAsync()
{
    auto expected = false;
    if (m_initialized.compare_exchange_strong(expected, true))
    {
        // Initialize the audio graph
        auto audioGraphSettings = winrt::AudioGraphSettings(winrt::AudioRenderCategory::Media);
        auto audioGraphResult = co_await winrt::AudioGraph::CreateAsync(audioGraphSettings);
        if (audioGraphResult.Status() != winrt::AudioGraphCreationStatus::Success)
        {
            throw winrt::hresult_error(E_FAIL, L"Failed to initialize AudioGraph!");
        }
        m_audioGraph = audioGraphResult.Graph();

        // Initialize the selected microphone
        auto defaultMicrophoneId = winrt::MediaDevice::GetDefaultAudioCaptureId(winrt::AudioDeviceRole::Default);
        auto microphoneId = (g_MicrophoneDeviceId[0] == 0) ? defaultMicrophoneId : winrt::to_hstring(g_MicrophoneDeviceId);
        auto microphone = co_await winrt::DeviceInformation::CreateFromIdAsync(microphoneId);

        // Initialize audio input and output nodes
        auto inputNodeResult = co_await m_audioGraph.CreateDeviceInputNodeAsync(winrt::MediaCategory::Media, m_audioGraph.EncodingProperties(), microphone);
        if (inputNodeResult.Status() != winrt::AudioDeviceNodeCreationStatus::Success && microphoneId != defaultMicrophoneId)
        {
            // If the selected microphone failed, try again with the default
            microphone = co_await winrt::DeviceInformation::CreateFromIdAsync(defaultMicrophoneId);
            inputNodeResult = co_await m_audioGraph.CreateDeviceInputNodeAsync(winrt::MediaCategory::Media, m_audioGraph.EncodingProperties(), microphone);
        }
        if (inputNodeResult.Status() != winrt::AudioDeviceNodeCreationStatus::Success)
        {
            throw winrt::hresult_error(E_FAIL, L"Failed to initialize input audio node!");
        }
        m_audioInputNode = inputNodeResult.DeviceInputNode();
        m_audioOutputNode = m_audioGraph.CreateFrameOutputNode();

        // Hookup audio nodes
        m_audioInputNode.AddOutgoingConnection(m_audioOutputNode);
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
    CheckStarted();

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
        m_audioGraph.Start();
    }
}

void AudioSampleGenerator::Stop()
{
    CheckInitialized();
    if (m_started.load())
    {
        m_asyncInitialized.wait();
        m_audioGraph.Stop();
        m_endEvent.SetEvent();
    }
}

void AudioSampleGenerator::OnAudioQuantumStarted(winrt::AudioGraph const& sender, winrt::IInspectable const& args)
{
    {
        auto lock = m_lock.lock_exclusive();

        auto frame = m_audioOutputNode.GetFrame();
        std::optional<winrt::TimeSpan> timestamp = frame.RelativeTime();
        auto audioBuffer = frame.LockBuffer(winrt::AudioBufferAccessMode::Read);

        auto sampleBuffer = winrt::Buffer::CreateCopyFromMemoryBuffer(audioBuffer);
        sampleBuffer.Length(audioBuffer.Length());
        auto sample = winrt::MediaStreamSample::CreateFromBuffer(sampleBuffer, timestamp.value());
        m_samples.push_back(sample);
    }
    m_audioEvent.SetEvent();
}
