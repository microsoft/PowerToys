#include "pch.h"
#include "LoopbackCapture.h"
#include <functiondiscoverykeys_devpkey.h>

#pragma comment(lib, "ole32.lib")

LoopbackCapture::LoopbackCapture()
{
    m_stopEvent.create(wil::EventOptions::ManualReset);
    m_samplesReadyEvent.create(wil::EventOptions::ManualReset);
}

LoopbackCapture::~LoopbackCapture()
{
    Stop();
    if (m_pwfx)
    {
        CoTaskMemFree(m_pwfx);
        m_pwfx = nullptr;
    }
}

HRESULT LoopbackCapture::Initialize()
{
    if (m_initialized.load())
    {
        return S_OK;
    }

    HRESULT hr = CoCreateInstance(
        __uuidof(MMDeviceEnumerator),
        nullptr,
        CLSCTX_ALL,
        __uuidof(IMMDeviceEnumerator),
        m_deviceEnumerator.put_void());
    if (FAILED(hr))
    {
        return hr;
    }

    // Get the default audio render device (speakers/headphones)
    hr = m_deviceEnumerator->GetDefaultAudioEndpoint(eRender, eConsole, m_device.put());
    if (FAILED(hr))
    {
        return hr;
    }

    hr = m_device->Activate(__uuidof(IAudioClient), CLSCTX_ALL, nullptr, m_audioClient.put_void());
    if (FAILED(hr))
    {
        return hr;
    }

    // Get the mix format
    hr = m_audioClient->GetMixFormat(&m_pwfx);
    if (FAILED(hr))
    {
        return hr;
    }

    // Initialize audio client in loopback mode
    // AUDCLNT_STREAMFLAGS_LOOPBACK enables capturing what's being played on the device
    hr = m_audioClient->Initialize(
        AUDCLNT_SHAREMODE_SHARED,
        AUDCLNT_STREAMFLAGS_LOOPBACK,
        1000000, // 100ms buffer to reduce capture latency
        0,
        m_pwfx,
        nullptr);
    if (FAILED(hr))
    {
        return hr;
    }

    hr = m_audioClient->GetService(__uuidof(IAudioCaptureClient), m_captureClient.put_void());
    if (FAILED(hr))
    {
        return hr;
    }

    m_initialized.store(true);
    return S_OK;
}

HRESULT LoopbackCapture::Start()
{
    if (!m_initialized.load())
    {
        return E_NOT_VALID_STATE;
    }

    if (m_started.load())
    {
        return S_OK;
    }

    m_stopEvent.ResetEvent();
    
    HRESULT hr = m_audioClient->Start();
    if (FAILED(hr))
    {
        return hr;
    }

    m_started.store(true);

    // Start capture thread
    m_captureThread = std::thread(&LoopbackCapture::CaptureThread, this);

    return S_OK;
}

void LoopbackCapture::Stop()
{
    if (!m_started.load())
    {
        return;
    }

    m_stopEvent.SetEvent();

    if (m_captureThread.joinable())
    {
        m_captureThread.join();
    }

    DrainCaptureClient();

    if (m_audioClient)
    {
        m_audioClient->Stop();
    }

    m_started.store(false);
}

void LoopbackCapture::DrainCaptureClient()
{
    if (!m_captureClient)
    {
        return;
    }

    while (true)
    {
        UINT32 packetLength = 0;
        HRESULT hr = m_captureClient->GetNextPacketSize(&packetLength);
        if (FAILED(hr) || packetLength == 0)
        {
            break;
        }

        BYTE* pData = nullptr;
        UINT32 numFramesAvailable = 0;
        DWORD flags = 0;
        hr = m_captureClient->GetBuffer(&pData, &numFramesAvailable, &flags, nullptr, nullptr);
        if (FAILED(hr))
        {
            break;
        }

        if (numFramesAvailable > 0)
        {
            std::vector<float> samples;

            if (m_pwfx->wFormatTag == WAVE_FORMAT_IEEE_FLOAT ||
                (m_pwfx->wFormatTag == WAVE_FORMAT_EXTENSIBLE &&
                 reinterpret_cast<WAVEFORMATEXTENSIBLE*>(m_pwfx)->SubFormat == KSDATAFORMAT_SUBTYPE_IEEE_FLOAT))
            {
                if (flags & AUDCLNT_BUFFERFLAGS_SILENT)
                {
                    samples.resize(numFramesAvailable * m_pwfx->nChannels, 0.0f);
                }
                else
                {
                    float* floatData = reinterpret_cast<float*>(pData);
                    samples.assign(floatData, floatData + (numFramesAvailable * m_pwfx->nChannels));
                }
            }
            else if (m_pwfx->wFormatTag == WAVE_FORMAT_PCM ||
                     (m_pwfx->wFormatTag == WAVE_FORMAT_EXTENSIBLE &&
                      reinterpret_cast<WAVEFORMATEXTENSIBLE*>(m_pwfx)->SubFormat == KSDATAFORMAT_SUBTYPE_PCM))
            {
                if (flags & AUDCLNT_BUFFERFLAGS_SILENT)
                {
                    samples.resize(numFramesAvailable * m_pwfx->nChannels, 0.0f);
                }
                else if (m_pwfx->wBitsPerSample == 16)
                {
                    int16_t* pcmData = reinterpret_cast<int16_t*>(pData);
                    samples.resize(numFramesAvailable * m_pwfx->nChannels);
                    for (size_t i = 0; i < samples.size(); i++)
                    {
                        samples[i] = static_cast<float>(pcmData[i]) / 32768.0f;
                    }
                }
                else if (m_pwfx->wBitsPerSample == 32)
                {
                    int32_t* pcmData = reinterpret_cast<int32_t*>(pData);
                    samples.resize(numFramesAvailable * m_pwfx->nChannels);
                    for (size_t i = 0; i < samples.size(); i++)
                    {
                        samples[i] = static_cast<float>(pcmData[i]) / 2147483648.0f;
                    }
                }
            }

            if (!samples.empty())
            {
                auto lock = m_lock.lock_exclusive();
                m_sampleQueue.push_back(std::move(samples));
                m_samplesReadyEvent.SetEvent();
            }
        }

        hr = m_captureClient->ReleaseBuffer(numFramesAvailable);
        if (FAILED(hr))
        {
            break;
        }
    }
}

void LoopbackCapture::CaptureThread()
{
    while (WaitForSingleObject(m_stopEvent.get(), 10) == WAIT_TIMEOUT)
    {
        UINT32 packetLength = 0;
        HRESULT hr = m_captureClient->GetNextPacketSize(&packetLength);
        if (FAILED(hr))
        {
            break;
        }

        while (packetLength != 0)
        {
            BYTE* pData = nullptr;
            UINT32 numFramesAvailable = 0;
            DWORD flags = 0;

            hr = m_captureClient->GetBuffer(&pData, &numFramesAvailable, &flags, nullptr, nullptr);
            if (FAILED(hr))
            {
                break;
            }

            if (numFramesAvailable > 0)
            {
                std::vector<float> samples;
                
                // Convert to float samples
                if (m_pwfx->wFormatTag == WAVE_FORMAT_IEEE_FLOAT ||
                    (m_pwfx->wFormatTag == WAVE_FORMAT_EXTENSIBLE &&
                     reinterpret_cast<WAVEFORMATEXTENSIBLE*>(m_pwfx)->SubFormat == KSDATAFORMAT_SUBTYPE_IEEE_FLOAT))
                {
                    // Already float format
                    if (flags & AUDCLNT_BUFFERFLAGS_SILENT)
                    {
                        // Insert silence
                        samples.resize(numFramesAvailable * m_pwfx->nChannels, 0.0f);
                    }
                    else
                    {
                        float* floatData = reinterpret_cast<float*>(pData);
                        samples.assign(floatData, floatData + (numFramesAvailable * m_pwfx->nChannels));
                    }
                }
                else if (m_pwfx->wFormatTag == WAVE_FORMAT_PCM ||
                         (m_pwfx->wFormatTag == WAVE_FORMAT_EXTENSIBLE &&
                          reinterpret_cast<WAVEFORMATEXTENSIBLE*>(m_pwfx)->SubFormat == KSDATAFORMAT_SUBTYPE_PCM))
                {
                    // Convert PCM to float
                    if (flags & AUDCLNT_BUFFERFLAGS_SILENT)
                    {
                        samples.resize(numFramesAvailable * m_pwfx->nChannels, 0.0f);
                    }
                    else if (m_pwfx->wBitsPerSample == 16)
                    {
                        int16_t* pcmData = reinterpret_cast<int16_t*>(pData);
                        samples.resize(numFramesAvailable * m_pwfx->nChannels);
                        for (size_t i = 0; i < samples.size(); i++)
                        {
                            samples[i] = static_cast<float>(pcmData[i]) / 32768.0f;
                        }
                    }
                    else if (m_pwfx->wBitsPerSample == 32)
                    {
                        int32_t* pcmData = reinterpret_cast<int32_t*>(pData);
                        samples.resize(numFramesAvailable * m_pwfx->nChannels);
                        for (size_t i = 0; i < samples.size(); i++)
                        {
                            samples[i] = static_cast<float>(pcmData[i]) / 2147483648.0f;
                        }
                    }
                }

                if (!samples.empty())
                {
                    auto lock = m_lock.lock_exclusive();
                    m_sampleQueue.push_back(std::move(samples));
                    m_samplesReadyEvent.SetEvent();
                }
            }

            hr = m_captureClient->ReleaseBuffer(numFramesAvailable);
            if (FAILED(hr))
            {
                break;
            }

            hr = m_captureClient->GetNextPacketSize(&packetLength);
            if (FAILED(hr))
            {
                break;
            }
        }
    }
}

bool LoopbackCapture::TryGetSamples(std::vector<float>& samples)
{
    auto lock = m_lock.lock_exclusive();
    if (m_sampleQueue.empty())
    {
        return false;
    }

    samples = std::move(m_sampleQueue.front());
    m_sampleQueue.pop_front();
    
    if (m_sampleQueue.empty())
    {
        m_samplesReadyEvent.ResetEvent();
    }
    
    return true;
}
