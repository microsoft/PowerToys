#include "pch.h"

#include "audio_engine.h"

#include <roapi.h>
#include <winstring.h>
#include <propsys.h>
#include <propkey.h>

// PKEY_Device_FriendlyName lives in here and nowhere else.
#include <functiondiscoverykeys_devpkey.h>

#include <tlhelp32.h>

#include <common/logger/logger.h>

#include <algorithm>
#include <chrono>
#include <cmath>
#include <unordered_map>

using namespace SoundManager;

namespace
{
    constexpr wchar_t AUDIO_POLICY_CONFIG_CLASS[] = L"Windows.Media.Internal.AudioPolicyConfig";
    constexpr wchar_t AUDIO_POLICY_CONFIG_IID[] = L"{ab3d4648-e242-459f-b02f-541c70306324}";

    // How long the list of devices and the list of sessions are kept before they are read again.
    constexpr std::chrono::milliseconds CACHE_LIFETIME = std::chrono::milliseconds(250);

    // How long the endpoint of a program is kept, and how long it stays fresh enough to be used without asking again.
    constexpr std::chrono::seconds ENDPOINT_CACHE_LIFETIME = std::chrono::seconds(60);
    constexpr std::chrono::seconds ENDPOINT_CACHE_FRESHNESS = std::chrono::seconds(5);

    // The classic policy interface of Windows. It is the only way to change the default device, because Windows has no documented one. Only one slot is used.
    struct __declspec(uuid("f8679f50-850a-41cf-9c72-430f290290c8")) PolicyConfig : IUnknown
    {
        virtual HRESULT STDMETHODCALLTYPE GetMixFormat() = 0;
        virtual HRESULT STDMETHODCALLTYPE GetDeviceFormat() = 0;
        virtual HRESULT STDMETHODCALLTYPE ResetDeviceFormat() = 0;
        virtual HRESULT STDMETHODCALLTYPE SetDeviceFormat() = 0;
        virtual HRESULT STDMETHODCALLTYPE GetProcessingPeriod() = 0;
        virtual HRESULT STDMETHODCALLTYPE SetProcessingPeriod() = 0;
        virtual HRESULT STDMETHODCALLTYPE GetShareMode() = 0;
        virtual HRESULT STDMETHODCALLTYPE SetShareMode() = 0;
        virtual HRESULT STDMETHODCALLTYPE GetPropertyValue() = 0;
        virtual HRESULT STDMETHODCALLTYPE SetPropertyValue() = 0;
        virtual HRESULT STDMETHODCALLTYPE SetDefaultEndpoint(const wchar_t* deviceId, ERole role) = 0;
    };

    struct __declspec(uuid("870af99c-171d-4f9e-af0d-e63df40c2bc9")) PolicyConfigClient : IUnknown
    {
    };

    // Windows keeps two default devices per flow: one for everything and one for calls and chats.
    bool SetDefaultEndpoint(const std::wstring& deviceId, ERole role)
    {
        if (deviceId.empty())
        {
            return false;
        }

        wil::com_ptr<PolicyConfig> policy;
        const HRESULT hr = CoCreateInstance(
            __uuidof(PolicyConfigClient), nullptr, CLSCTX_ALL, __uuidof(PolicyConfig), policy.put_void());

        if (FAILED(hr) || policy == nullptr)
        {
            Logger::error(L"Sound Manager cannot change the default device, the policy API is not available");
            return false;
        }

        return SUCCEEDED(policy->SetDefaultEndpoint(deviceId.c_str(), role));
    }

    std::wstring GetFriendlyName(IMMDevice* device)
    {
        std::wstring name;
        wil::com_ptr<IPropertyStore> store;
        if (FAILED(device->OpenPropertyStore(STGM_READ, store.put())))
        {
            return name;
        }

        PROPVARIANT value;
        PropVariantInit(&value);
        if (SUCCEEDED(store->GetValue(PKEY_Device_FriendlyName, &value)) && value.vt == VT_LPWSTR)
        {
            name = value.pwszVal;
        }
        PropVariantClear(&value);
        return name;
    }

    // The property store key of the "listen to this device" setting of a microphone. The key is not in the public SDK.
    constexpr GUID LISTEN_SETTING = { 0x24dbb0fc, 0x9311, 0x4b3d, { 0x9c, 0xf0, 0x18, 0xff, 0x15, 0x56, 0x39, 0xd4 } };

    // The device the sound of the microphone is played through. Empty means the default output device.
    constexpr PROPERTYKEY PLAYBACK_THROUGH_KEY = { LISTEN_SETTING, 0 };

    constexpr PROPERTYKEY LISTEN_TO_DEVICE_KEY = { LISTEN_SETTING, 1 };

    bool IsListening(IMMDevice* device)
    {
        wil::com_ptr<IPropertyStore> store;
        if (FAILED(device->OpenPropertyStore(STGM_READ, store.put())) || store == nullptr)
        {
            return false;
        }

        PROPVARIANT value;
        PropVariantInit(&value);
        const bool listening = SUCCEEDED(store->GetValue(LISTEN_TO_DEVICE_KEY, &value)) && value.vt == VT_BOOL &&
            value.boolVal != FALSE;
        PropVariantClear(&value);
        return listening;
    }

    bool SetListening(IMMDevice* device, bool listening)
    {
        wil::com_ptr<IPropertyStore> store;
        if (FAILED(device->OpenPropertyStore(STGM_READWRITE, store.put())) || store == nullptr)
        {
            return false;
        }

        PROPVARIANT enabled;
        PropVariantInit(&enabled);
        enabled.vt = VT_BOOL;
        enabled.boolVal = listening ? VARIANT_TRUE : VARIANT_FALSE;

        HRESULT result = store->SetValue(LISTEN_TO_DEVICE_KEY, enabled);

        if (listening)
        {
            // The device the sound is played through. The variant is not cleared, because the string is a constant that does not belong to it.
            wchar_t defaultDevice[] = L"";
            PROPVARIANT target;
            PropVariantInit(&target);
            target.vt = VT_LPWSTR;
            target.pwszVal = defaultDevice;

            const HRESULT targetResult = store->SetValue(PLAYBACK_THROUGH_KEY, target);
            if (FAILED(targetResult))
            {
                result = targetResult;
            }
        }

        if (FAILED(result))
        {
            return false;
        }

        return SUCCEEDED(store->Commit());
    }

    // The test sound: one tone in the left ear, one in the right, a short melody and a sweep at the end.
    struct TestSegment
    {
        float from = 0.0f;
        float to = 0.0f;
        float seconds = 0.0f;

        // 1 is the left ear, 2 is the right one, and 3 is both.
        int channels = 3;
    };

    constexpr int TEST_SAMPLE_RATE = 48000;
    constexpr float TEST_AMPLITUDE = 0.5f;
    constexpr float PI = 3.14159265f;
    constexpr float SAMPLE_LIMIT = 32767.0f;

    // The device does not always raise an event when it has played a part of the buffer, so the wait gives up after this many milliseconds.
    constexpr DWORD TEST_SOUND_TIMEOUT = 5000;

    const std::vector<TestSegment> TEST_SEGMENTS = {
        { 440.0f, 440.0f, 0.4f, 1 },
        { 440.0f, 440.0f, 0.4f, 2 },
        { 440.0f, 440.0f, 0.25f, 3 },
        { 554.0f, 554.0f, 0.25f, 3 },
        { 659.0f, 659.0f, 0.25f, 3 },
        { 784.0f, 784.0f, 0.25f, 3 },
        { 100.0f, 8000.0f, 1.5f, 3 },
    };

    // The tone fades in and out, so that it does not click.
    float TestEnvelope(int frame, int frames)
    {
        const int fade = TEST_SAMPLE_RATE / 200;
        if (frame < fade)
        {
            return static_cast<float>(frame) / fade;
        }

        if (frame > frames - fade)
        {
            return static_cast<float>(frames - frame) / fade;
        }

        return 1.0f;
    }

    std::vector<int16_t> BuildTestSamples()
    {
        std::vector<int16_t> samples;

        for (const auto& segment : TEST_SEGMENTS)
        {
            const int frames = static_cast<int>(segment.seconds * TEST_SAMPLE_RATE);
            float phase = 0.0f;

            for (int frame = 0; frame < frames; frame++)
            {
                const float progress = static_cast<float>(frame) / frames;
                const float frequency = segment.from + (segment.to - segment.from) * progress;
                phase += 2.0f * PI * frequency / TEST_SAMPLE_RATE;

                const float value = std::sin(phase) * TEST_AMPLITUDE * TestEnvelope(frame, frames);
                const int16_t sample = static_cast<int16_t>(value * SAMPLE_LIMIT);

                samples.push_back((segment.channels & 1) != 0 ? sample : 0);
                samples.push_back((segment.channels & 2) != 0 ? sample : 0);
            }
        }

        return samples;
    }

    void PlayTestSoundOnDevice(const std::wstring& deviceId)
    {
        CoInitializeEx(nullptr, COINIT_MULTITHREADED);

        wil::com_ptr<IMMDeviceEnumerator> enumerator;
        const HRESULT created = CoCreateInstance(
            __uuidof(MMDeviceEnumerator), nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(enumerator.put()));
        if (FAILED(created) || enumerator == nullptr)
        {
            Logger::error(L"Sound Manager could not open the devices for the test sound, error 0x{:08X}", static_cast<uint32_t>(created));
            CoUninitialize();
            return;
        }

        wil::com_ptr<IMMDevice> device;
        const HRESULT found = enumerator->GetDevice(deviceId.c_str(), device.put());
        if (FAILED(found) || device == nullptr)
        {
            Logger::error(L"Sound Manager could not find the device of the test sound, error 0x{:08X}", static_cast<uint32_t>(found));
            CoUninitialize();
            return;
        }

        wil::com_ptr<IAudioClient> client;
        const HRESULT activated = device->Activate(__uuidof(IAudioClient), CLSCTX_ALL, nullptr, client.put_void());
        if (FAILED(activated) || client == nullptr)
        {
            Logger::error(L"Sound Manager could not open the device of the test sound, error 0x{:08X}", static_cast<uint32_t>(activated));
            CoUninitialize();
            return;
        }

        WAVEFORMATEX format = {};
        format.wFormatTag = WAVE_FORMAT_PCM;
        format.nChannels = 2;
        format.nSamplesPerSec = TEST_SAMPLE_RATE;
        format.wBitsPerSample = 16;
        format.nBlockAlign = format.wBitsPerSample / 8 * format.nChannels;
        format.nAvgBytesPerSec = format.nSamplesPerSec * format.nBlockAlign;

        // The audio engine of Windows converts the format of the sound into the format of the device.
        const DWORD streamFlags = AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM | AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY;

        const HRESULT initialized = client->Initialize(
            AUDCLNT_SHAREMODE_SHARED,
            streamFlags,
            0,
            0,
            &format,
            nullptr);
        if (FAILED(initialized))
        {
            Logger::error(L"Sound Manager could not open the stream of the test sound, error 0x{:08X}", static_cast<uint32_t>(initialized));
            CoUninitialize();
            return;
        }

        // The render client hands out the buffers. Windows only gives it out when the stream has been opened.
        wil::com_ptr<IAudioRenderClient> renderClient;
        const HRESULT service = client->GetService(__uuidof(IAudioRenderClient), renderClient.put_void());
        if (FAILED(service) || renderClient == nullptr)
        {
            Logger::error(L"Sound Manager could not get the buffers of the test sound, error 0x{:08X}", static_cast<uint32_t>(service));
            CoUninitialize();
            return;
        }

        const std::vector<int16_t> samples = BuildTestSamples();

        const HRESULT started = client->Start();
        if (FAILED(started))
        {
            Logger::error(L"Sound Manager could not start the test sound, error 0x{:08X}", static_cast<uint32_t>(started));
            CoUninitialize();
            return;
        }

        size_t offset = 0;
        DWORD lastProgress = GetTickCount();
        while (offset < samples.size())
        {
            UINT32 capacity = 0;
            const HRESULT sizeResult = client->GetBufferSize(&capacity);
            if (FAILED(sizeResult))
            {
                Logger::error(L"Sound Manager could not ask the size of the buffer of the test sound, error 0x{:08X}", static_cast<uint32_t>(sizeResult));
                break;
            }

            UINT32 padding = 0;
            const HRESULT paddingResult = client->GetCurrentPadding(&padding);
            if (FAILED(paddingResult))
            {
                Logger::error(L"Sound Manager could not ask how much of the test sound is left, error 0x{:08X}", static_cast<uint32_t>(paddingResult));
                break;
            }

            const UINT32 freeFrames = capacity > padding ? capacity - padding : 0;
            if (freeFrames == 0)
            {
                // The device does not always raise an event when it has played a part of the buffer, so the wait looks at the buffer again.
                if (GetTickCount() - lastProgress > TEST_SOUND_TIMEOUT)
                {
                    Logger::error(L"Sound Manager stopped waiting for the device of the test sound");
                    break;
                }

                Sleep(10);
                continue;
            }

            BYTE* data = nullptr;
            const HRESULT bufferResult = renderClient->GetBuffer(freeFrames, &data);
            if (FAILED(bufferResult))
            {
                Logger::error(L"Sound Manager could not get a buffer for the test sound, error 0x{:08X}", static_cast<uint32_t>(bufferResult));
                break;
            }

            const size_t wanted = (samples.size() - offset) / format.nBlockAlign;
            const size_t frames = (std::min)(static_cast<size_t>(freeFrames), wanted);
            memcpy(data, samples.data() + offset, frames * format.nBlockAlign);
            offset += frames * format.nBlockAlign;
            lastProgress = GetTickCount();

            const HRESULT releaseResult = renderClient->ReleaseBuffer(static_cast<UINT32>(frames), 0);
            if (FAILED(releaseResult))
            {
                Logger::error(L"Sound Manager could not give the buffer of the test sound back, error 0x{:08X}", static_cast<uint32_t>(releaseResult));
                break;
            }
        }

        // The last piece of the sound is still in the buffer, so it is given time to be played.
        Sleep(300);

        client->Stop();

        Logger::trace(L"Sound Manager played the test sound through {}", deviceId);

        CoUninitialize();
    }

    // What Windows does with the other sounds when a program is talking. Windows keeps it in the registry of the user.
    constexpr wchar_t DUCKING_KEY[] = L"Software\\Microsoft\\Multimedia\\Audio";
    constexpr wchar_t DUCKING_VALUE[] = L"UserDuckingPreference";

    DuckingMode ReadDuckingMode()
    {
        DWORD value = 0;
        DWORD size = sizeof(value);

        if (RegGetValueW(HKEY_CURRENT_USER, DUCKING_KEY, DUCKING_VALUE, RRF_RT_REG_DWORD, nullptr, &value, &size) != ERROR_SUCCESS)
        {
            // Windows holds the other sounds down by eighty percent when nothing was chosen.
            return DuckingMode::ReduceBy80;
        }

        return static_cast<DuckingMode>(value);
    }

    bool WriteDuckingMode(DuckingMode mode)
    {
        HKEY key = nullptr;
        if (RegCreateKeyExW(
                HKEY_CURRENT_USER, DUCKING_KEY, 0, nullptr, REG_OPTION_NON_VOLATILE, KEY_SET_VALUE, nullptr, &key, nullptr) != ERROR_SUCCESS)
        {
            return false;
        }

        const DWORD value = static_cast<DWORD>(mode);
        const bool written = RegSetValueExW(
                                 key, DUCKING_VALUE, 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value)) == ERROR_SUCCESS;
        RegCloseKey(key);
        return written;
    }

    std::wstring GetDeviceId(IMMDevice* device)
    {
        LPWSTR id = nullptr;
        std::wstring result;
        if (SUCCEEDED(device->GetId(&id)) && id != nullptr)
        {
            result = id;
            CoTaskMemFree(id);
        }
        return result;
    }

    DWORD GetSessionProcessId(IAudioSessionControl* control)
    {
        DWORD processId = 0;
        wil::com_ptr<IAudioSessionControl2> control2;
        if (SUCCEEDED(control->QueryInterface(__uuidof(IAudioSessionControl2), control2.put_void())))
        {
            control2->GetProcessId(&processId);
        }
        return processId;
    }

    AudioSessionState GetSessionState(IAudioSessionControl* control)
    {
        AudioSessionState state = AudioSessionStateInactive;
        if (control != nullptr)
        {
            control->GetState(&state);
        }
        return state;
    }

    bool GetSimpleVolume(IAudioSessionControl* control, wil::com_ptr<ISimpleAudioVolume>& simpleVolume)
    {
        if (control == nullptr)
        {
            return false;
        }

        return SUCCEEDED(control->QueryInterface(__uuidof(ISimpleAudioVolume), simpleVolume.put_void()));
    }

    bool GetEndpointVolume(IMMDevice* device, wil::com_ptr<IAudioEndpointVolume>& endpointVolume)
    {
        if (device == nullptr)
        {
            return false;
        }

        return SUCCEEDED(device->Activate(__uuidof(IAudioEndpointVolume), CLSCTX_ALL, nullptr, endpointVolume.put_void())) &&
               endpointVolume != nullptr;
    }

    // The level of the two channels of a device.
    void ReadChannelLevels(IAudioEndpointVolume* endpointVolume, float& left, float& right)
    {
        UINT channelCount = 0;
        endpointVolume->GetChannelCount(&channelCount);
        if (channelCount > 0)
        {
            endpointVolume->GetChannelVolumeLevelScalar(0, &left);
        }
        if (channelCount > 1)
        {
            endpointVolume->GetChannelVolumeLevelScalar(1, &right);
        }
    }

    // The level of the two channels of a stream of a program.
    void ReadChannelLevels(IChannelAudioVolume* channelVolume, float& left, float& right)
    {
        UINT channelCount = 0;
        channelVolume->GetChannelCount(&channelCount);
        if (channelCount > 0)
        {
            channelVolume->GetChannelVolume(0, &left);
        }
        if (channelCount > 1)
        {
            channelVolume->GetChannelVolume(1, &right);
        }
    }

    bool IsSessionMuted(IAudioSessionControl* control)
    {
        wil::com_ptr<ISimpleAudioVolume> simpleVolume;
        if (!GetSimpleVolume(control, simpleVolume))
        {
            return false;
        }

        BOOL muted = FALSE;
        if (FAILED(simpleVolume->GetMute(&muted)))
        {
            return false;
        }

        return muted != FALSE;
    }

    // Silences one stream of a program.
    bool SetSessionMute(IAudioSessionControl* control, bool muted)
    {
        wil::com_ptr<ISimpleAudioVolume> simpleVolume;
        if (!GetSimpleVolume(control, simpleVolume))
        {
            return false;
        }

        return SUCCEEDED(simpleVolume->SetMute(muted ? TRUE : FALSE, nullptr));
    }

    // The id of the group of streams a program owns in one direction. A change is applied to every stream of the group.
    std::wstring MakeGroupId(DWORD processId, EDataFlow flow)
    {
        return std::to_wstring(processId) + L"|" + (flow == eCapture ? L"in" : L"out");
    }
}

wil::com_ptr<AudioPolicyConfigFactory> AudioPolicyConfig::CreateFactory()
{
    HSTRING className = nullptr;
    if (FAILED(WindowsCreateString(AUDIO_POLICY_CONFIG_CLASS, static_cast<UINT32>(wcslen(AUDIO_POLICY_CONFIG_CLASS)), &className)))
    {
        return nullptr;
    }

    GUID iid = {};
    if (FAILED(IIDFromString(AUDIO_POLICY_CONFIG_IID, &iid)))
    {
        WindowsDeleteString(className);
        return nullptr;
    }

    // The activation factory is requested for the policy interface itself, so the pointer already points at the vtable of AudioPolicyConfigFactory.
    AudioPolicyConfigFactory* created = nullptr;
    const HRESULT hr = RoGetActivationFactory(className, iid, reinterpret_cast<void**>(&created));
    WindowsDeleteString(className);
    if (FAILED(hr) || created == nullptr)
    {
        return nullptr;
    }

    return wil::com_ptr<AudioPolicyConfigFactory>(created);
}

bool AudioPolicyConfig::IsAvailable()
{
    if (m_factory == nullptr)
    {
        m_factory = CreateFactory();
    }

    return m_factory != nullptr;
}

std::wstring AudioPolicyConfig::ToPolicyEndpointId(const std::wstring& deviceId)
{
    if (deviceId.empty())
    {
        return {};
    }

    wchar_t buffer[512] = {};
    swprintf_s(buffer, L"\\\\?\\SWD#MMDEVAPI#%s#{e6327cad-dcec-4949-ae8a-991e976a79d2}", deviceId.c_str());
    return buffer;
}

bool AudioPolicyConfig::SetApplicationEndpoint(DWORD processId, const std::wstring& endpointId)
{
    if (!IsAvailable())
    {
        Logger::error(L"Sound Manager cannot route applications, the audio policy API is not available");
        return false;
    }

    HSTRING hEndpoint = nullptr;
    if (!endpointId.empty() && FAILED(WindowsCreateString(endpointId.c_str(), static_cast<UINT32>(endpointId.size()), &hEndpoint)))
    {
        return false;
    }

    // Windows keeps one route per role and does not say which one it reads back, so both are written and the application is pinned either way.
    bool ok = true;
    for (int role : { 1, 0 })
    {
        // The second value is the data flow, eRender = 0.
        const HRESULT hr = m_factory->set_persisted_default_audio_endpoint(processId, 0, role, hEndpoint);
        if (FAILED(hr))
        {
            Logger::error(L"Sound Manager could not route application {}, error 0x{:08X}", processId, static_cast<uint32_t>(hr));
            ok = false;
        }
    }

    if (hEndpoint != nullptr)
    {
        WindowsDeleteString(hEndpoint);
    }

    return ok;
}

bool AudioPolicyConfig::GetApplicationEndpoint(DWORD processId, std::wstring& endpointId)
{
    endpointId.clear();

    if (!IsAvailable())
    {
        return false;
    }

    for (int role : { 1, 0 })
    {
        HSTRING hEndpoint = nullptr;
        const HRESULT hr = m_factory->get_persisted_default_audio_endpoint(processId, 0, role, &hEndpoint);
        if (FAILED(hr))
        {
            continue;
        }

        if (hEndpoint != nullptr)
        {
            UINT32 length = 0;
            const wchar_t* raw = WindowsGetStringRawBuffer(hEndpoint, &length);
            if (raw != nullptr && length > 0)
            {
                endpointId.assign(raw, length);
            }
            WindowsDeleteString(hEndpoint);
        }

        if (!endpointId.empty())
        {
            break;
        }
    }

    return true;
}

bool AudioEngine::Initialize()
{
    std::lock_guard<std::recursive_mutex> lock(m_lock);

    if (m_enumerator != nullptr)
    {
        return true;
    }

    const HRESULT hr = CoCreateInstance(__uuidof(MMDeviceEnumerator),
                                        nullptr,
                                        CLSCTX_INPROC_SERVER,
                                        IID_PPV_ARGS(m_enumerator.put()));
    return SUCCEEDED(hr);
}

std::wstring AudioEngine::GetProcessPath(DWORD processId)
{
    if (processId == 0)
    {
        return {};
    }

    wil::unique_handle process(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId));
    if (!process)
    {
        return {};
    }

    wchar_t path[MAX_PATH] = {};
    DWORD size = MAX_PATH;
    if (QueryFullProcessImageNameW(process.get(), 0, path, &size) == 0)
    {
        return {};
    }
    return path;
}

void AudioEngine::FillProcessImageNames(const std::vector<DWORD>& processIds)
{
    std::vector<DWORD> missing;
    for (DWORD processId : processIds)
    {
        if (m_processNames.find(processId) == m_processNames.end())
        {
            missing.push_back(processId);
        }
    }

    if (missing.empty())
    {
        return;
    }

    const HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot == INVALID_HANDLE_VALUE)
    {
        return;
    }

    // A snapshot holds every process on the machine, so all the names are taken at once.
    PROCESSENTRY32W entry = { sizeof(entry) };
    if (Process32FirstW(snapshot, &entry))
    {
        do
        {
            if (std::find(missing.begin(), missing.end(), entry.th32ProcessID) != missing.end())
            {
                m_processNames[entry.th32ProcessID] = entry.szExeFile;
            }
        }
        while (Process32NextW(snapshot, &entry));
    }

    CloseHandle(snapshot);

    // The names of programs that are gone are removed, so that the map does not grow.
    for (auto it = m_processNames.begin(); it != m_processNames.end();)
    {
        if (std::find(processIds.begin(), processIds.end(), it->first) == processIds.end())
        {
            it = m_processNames.erase(it);
            continue;
        }

        ++it;
    }
}

std::wstring AudioEngine::GetDefaultDeviceId(EDataFlow flow, ERole role)
{
    wil::com_ptr<IMMDevice> device;
    if (FAILED(m_enumerator->GetDefaultAudioEndpoint(flow, role, device.put())) || device == nullptr)
    {
        return {};
    }

    return GetDeviceId(device.get());
}

bool AudioEngine::SetDefaultDevice(const std::wstring& deviceId, bool communications)
{
    std::lock_guard<std::recursive_mutex> lock(m_lock);

    if (m_enumerator == nullptr || deviceId.empty())
    {
        return false;
    }

    // The device decides whether it is an output or an input, so both lists are looked through.
    for (const EDataFlow flow : { eRender, eCapture })
    {
        std::vector<wil::com_ptr<IMMDevice>> devices;
        CollectDevices(flow, devices);

        for (const auto& device : devices)
        {
            if (GetDeviceId(device.get()) != deviceId)
            {
                continue;
            }

            // Windows keeps two roles for every flow, one for everything and one for calls and chats. The calls role is the one it reads back.
            const ERole role = communications ? eCommunications : eMultimedia;

            const bool done = SetDefaultEndpoint(deviceId, role);
            Logger::trace(L"Set default device flow={} role={} communications={} result={} device={}",
                          flow == eRender ? L"output" : L"input", static_cast<int>(role), communications, done, deviceId);
            return done;
        }
    }

    return false;
}

void AudioEngine::CollectDevices(EDataFlow flow, std::vector<wil::com_ptr<IMMDevice>>& devices)
{
    wil::com_ptr<IMMDeviceCollection> collection;
    if (FAILED(m_enumerator->EnumAudioEndpoints(flow, DEVICE_STATE_ACTIVE, collection.put())) || collection == nullptr)
    {
        return;
    }

    UINT count = 0;
    collection->GetCount(&count);
    for (UINT i = 0; i < count; i++)
    {
        wil::com_ptr<IMMDevice> device;
        if (SUCCEEDED(collection->Item(i, device.put())) && device != nullptr)
        {
            devices.push_back(std::move(device));
        }
    }
}

void AudioEngine::CollectSessions(EDataFlow flow, std::vector<AudioSession>& sessions)
{
    const CachedDevices& devices = Devices();
    const std::vector<wil::com_ptr<IMMDevice>>& list = flow == eCapture ? devices.capture : devices.render;

    for (const auto& device : list)
    {
        wil::com_ptr<IAudioSessionManager2> manager;
        if (FAILED(device->Activate(__uuidof(IAudioSessionManager2), CLSCTX_ALL, nullptr, manager.put_void())) || manager == nullptr)
        {
            continue;
        }

        wil::com_ptr<IAudioSessionEnumerator> enumerator;
        if (FAILED(manager->GetSessionEnumerator(enumerator.put())) || enumerator == nullptr)
        {
            continue;
        }

        int count = 0;
        enumerator->GetCount(&count);
        for (int i = 0; i < count; i++)
        {
            wil::com_ptr<IAudioSessionControl> control;
            if (FAILED(enumerator->GetSession(i, control.put())) || control == nullptr)
            {
                continue;
            }

            AudioSession session;
            session.control = std::move(control);
            session.processId = GetSessionProcessId(session.control.get());
            session.groupId = MakeGroupId(session.processId, flow);
            session.isCapture = flow == eCapture;
            sessions.push_back(std::move(session));
        }
    }
}

const AudioEngine::CachedDevices& AudioEngine::Devices()
{
    const auto now = std::chrono::steady_clock::now();
    if (now - m_deviceCache.readAt > CACHE_LIFETIME)
    {
        m_deviceCache.render.clear();
        m_deviceCache.capture.clear();
        CollectDevices(eRender, m_deviceCache.render);
        CollectDevices(eCapture, m_deviceCache.capture);
        m_deviceCache.readAt = now;
    }

    return m_deviceCache;
}

const std::vector<AudioEngine::AudioSession>& AudioEngine::Sessions()
{
    const auto now = std::chrono::steady_clock::now();
    if (now - m_sessionCache.readAt > CACHE_LIFETIME)
    {
        std::vector<AudioSession> sessions;
        CollectSessions(eRender, sessions);
        CollectSessions(eCapture, sessions);

        m_sessionCache.sessions = std::move(sessions);
        m_sessionCache.readAt = now;
    }

    return m_sessionCache.sessions;
}

AppVolume AudioEngine::ReadSession(const AudioSession& session, const std::wstring& imageName)
{
    AppVolume app;
    app.sessionId = session.groupId;
    app.processId = session.processId;
    app.isCapture = session.isCapture;
    app.processPath = GetProcessPath(session.processId);
    if (!app.processPath.empty())
    {
        app.displayName = app.processPath;
    }
    else if (session.processId == 0)
    {
        app.displayName = L"System Sounds";
    }
    else
    {
        app.displayName = imageName;
    }

    app.isActive = GetSessionState(session.control.get()) == AudioSessionStateActive;

    wil::com_ptr<ISimpleAudioVolume> simpleVolume;
    if (GetSimpleVolume(session.control.get(), simpleVolume))
    {
        simpleVolume->GetMasterVolume(&app.volume);
        BOOL muted = FALSE;
        simpleVolume->GetMute(&muted);
        app.muted = muted != FALSE;
    }

    // The peak level is read by GetPeaks only. Asking for it here as well would shorten the window the meter covers.

    wil::com_ptr<IChannelAudioVolume> channelVolume;
    if (SUCCEEDED(session.control->QueryInterface(__uuidof(IChannelAudioVolume), channelVolume.put_void())))
    {
        ReadChannelLevels(channelVolume.get(), app.balanceLeft, app.balanceRight);
    }

    return app;
}

void AudioEngine::GetDevices(std::vector<DeviceVolume>& outputs, std::vector<DeviceVolume>& inputs)
{
    std::lock_guard<std::recursive_mutex> lock(m_lock);

    outputs.clear();
    inputs.clear();

    if (m_enumerator == nullptr)
    {
        return;
    }

    const CachedDevices& devices = Devices();
    AddDevices(devices.render, eRender, outputs);
    AddDevices(devices.capture, eCapture, inputs);
}

void AudioEngine::AddDevices(const std::vector<wil::com_ptr<IMMDevice>>& devices, EDataFlow flow, std::vector<DeviceVolume>& target)
{
    const std::wstring defaultId = GetDefaultDeviceId(flow, eMultimedia);
    const std::wstring communicationsId = GetDefaultDeviceId(flow, eCommunications);

    for (const auto& device : devices)
    {
        DeviceVolume entry;
        entry.device.id = GetDeviceId(device.get());
        entry.device.name = GetFriendlyName(device.get());
        entry.device.isDefault = entry.device.id == defaultId;
        entry.device.isDefaultCommunications = entry.device.id == communicationsId;

        wil::com_ptr<IAudioEndpointVolume> endpointVolume;
        if (GetEndpointVolume(device.get(), endpointVolume))
        {
            endpointVolume->GetMasterVolumeLevelScalar(&entry.volume);
            BOOL muted = FALSE;
            endpointVolume->GetMute(&muted);
            entry.muted = muted != FALSE;

            // The level of the two channels is only read for the devices that play.
            if (flow == eRender)
            {
                ReadChannelLevels(endpointVolume.get(), entry.balanceLeft, entry.balanceRight);
            }
        }

        if (flow == eCapture)
        {
            entry.listening = IsListening(device.get());
        }

        target.push_back(entry);
    }
}

// The streams of one program and one direction belong together, one per device it plays on. The stream that is making sound is the one that is shown.
std::vector<const AudioEngine::AudioSession*> AudioEngine::GroupSessions()
{
    struct Group
    {
        const AudioSession* representative = nullptr;
        bool representativeIsActive = false;
    };

    std::vector<Group> groups;
    std::unordered_map<std::wstring, size_t> groupIndex;

    for (const auto& session : Sessions())
    {
        const AudioSessionState state = GetSessionState(session.control.get());

        auto found = groupIndex.find(session.groupId);
        if (found == groupIndex.end())
        {
            groupIndex[session.groupId] = groups.size();
            groups.push_back(Group{ &session, state == AudioSessionStateActive });
            continue;
        }

        Group& group = groups[found->second];
        if (state == AudioSessionStateActive && !group.representativeIsActive)
        {
            group.representative = &session;
            group.representativeIsActive = true;
        }
    }

    std::vector<const AudioSession*> representatives;
    representatives.reserve(groups.size());
    for (const Group& group : groups)
    {
        representatives.push_back(group.representative);
    }

    return representatives;
}

std::vector<AppVolume> AudioEngine::GetSessions()
{
    std::lock_guard<std::recursive_mutex> lock(m_lock);

    const std::vector<const AudioSession*> representatives = GroupSessions();

    // The names of the processes that cannot be opened are looked up in one go, because a snapshot of all the processes is expensive.
    std::vector<DWORD> unreadable;
    for (const AudioSession* session : representatives)
    {
        if (session->processId != 0 && GetProcessPath(session->processId).empty())
        {
            unreadable.push_back(session->processId);
        }
    }

    if (!unreadable.empty())
    {
        FillProcessImageNames(unreadable);
    }

    std::vector<AppVolume> apps;
    for (const AudioSession* session : representatives)
    {
        std::wstring imageName;
        const auto imageEntry = m_processNames.find(session->processId);
        if (imageEntry != m_processNames.end())
        {
            imageName = imageEntry->second;
        }

        AppVolume app = ReadSession(*session, imageName);
        if (app.displayName.empty())
        {
            // The process behind the session is gone, so there is nothing left to show.
            continue;
        }

        // A program that only records is only interesting while it is really recording.
        if (app.isCapture && !app.isActive)
        {
            continue;
        }
        apps.push_back(std::move(app));
    }

    ApplyOutputDevices(apps);

    return apps;
}

// The device each program is pinned to is filled in, as far as the policy API tells.
void AudioEngine::ApplyOutputDevices(std::vector<AppVolume>& apps)
{
    // The policy API reports the endpoint in its own format, so it has to be matched against the devices to find the one an application is pinned to.
    std::unordered_map<std::wstring, std::wstring> deviceIdByPolicyId;
    for (const auto& device : Devices().render)
    {
        const std::wstring deviceId = GetDeviceId(device.get());
        deviceIdByPolicyId[AudioPolicyConfig::ToPolicyEndpointId(deviceId)] = deviceId;
    }

    for (auto& app : apps)
    {
        // Only the streams that play can be sent to another device.
        if (app.isCapture)
        {
            continue;
        }

        std::wstring endpointId;
        if (!GetApplicationEndpointCached(app.processId, app.processPath, endpointId) || endpointId.empty())
        {
            continue;
        }

        const auto found = deviceIdByPolicyId.find(endpointId);
        if (found != deviceIdByPolicyId.end())
        {
            app.outputDeviceId = found->second;
        }
    }
}

void AudioEngine::GetPeaks(std::vector<PeakLevel>& peaks)
{
    std::lock_guard<std::recursive_mutex> lock(m_lock);

    peaks.clear();

    const std::vector<AudioSession>& sessions = Sessions();

    // The level of a program is the loudest of its streams, because that is what the user hears. The row gets one level, so the streams are put together here.
    std::unordered_map<std::wstring, float> loudest;

    for (const auto& session : sessions)
    {
        wil::com_ptr<IAudioMeterInformation> meter;
        if (FAILED(session.control->QueryInterface(__uuidof(IAudioMeterInformation), meter.put_void())) || meter == nullptr)
        {
            continue;
        }

        float peak = 0.0f;
        if (FAILED(meter->GetPeakValue(&peak)))
        {
            continue;
        }

        auto& best = loudest[session.groupId];
        if (peak > best)
        {
            best = peak;
        }
    }

    for (const auto& entry : loudest)
    {
        PeakLevel level;
        level.sessionId = entry.first;
        level.peak = entry.second;
        peaks.push_back(std::move(level));
    }
}

bool AudioEngine::GetApplicationEndpointCached(DWORD processId, const std::wstring& processPath, std::wstring& endpointId)
{
    const auto now = std::chrono::steady_clock::now();

    // The entries of programs that are gone are removed, so that the map does not grow.
    for (auto it = m_endpointCache.begin(); it != m_endpointCache.end();)
    {
        if (now - it->second.readAt > ENDPOINT_CACHE_LIFETIME)
        {
            it = m_endpointCache.erase(it);
            continue;
        }

        ++it;
    }

    const auto found = m_endpointCache.find(processId);
    if (found != m_endpointCache.end() && found->second.processPath == processPath &&
        now - found->second.readAt < ENDPOINT_CACHE_FRESHNESS)
    {
        endpointId = found->second.endpointId;
        return !endpointId.empty();
    }

    // The lookup is slow, so the answer is kept even when the application is not pinned to a device.
    std::wstring id;
    const bool foundPolicy = m_policy.GetApplicationEndpoint(processId, id);
    m_endpointCache[processId] = { foundPolicy ? id : std::wstring{}, processPath, now };
    endpointId = id;

    return !endpointId.empty();
}

bool AudioEngine::WithSessionControls(DWORD processId, bool isCapture, const std::function<bool(IAudioSessionControl*)>& action)
{
    const std::vector<AudioSession>& sessions = Sessions();

    bool updated = false;
    for (const auto& session : sessions)
    {
        if (session.processId != processId || session.isCapture != isCapture)
        {
            continue;
        }

        if (action(session.control.get()))
        {
            updated = true;
        }
    }

    return updated;
}

bool AudioEngine::SetApplicationVolume(DWORD processId, bool isCapture, float volume)
{
    std::lock_guard<std::recursive_mutex> lock(m_lock);

    const float clamped = std::clamp(volume, 0.0f, 1.0f);
    return WithSessionControls(processId, isCapture, [clamped](IAudioSessionControl* control) {
        wil::com_ptr<ISimpleAudioVolume> simpleVolume;
        if (!GetSimpleVolume(control, simpleVolume))
        {
            return false;
        }

        return SUCCEEDED(simpleVolume->SetMasterVolume(clamped, nullptr));
    });
}

bool AudioEngine::SetApplicationMute(DWORD processId, bool isCapture, bool muted)
{
    std::lock_guard<std::recursive_mutex> lock(m_lock);

    return WithSessionControls(processId, isCapture, [muted](IAudioSessionControl* control) {
        return SetSessionMute(control, muted);
    });
}

bool AudioEngine::ToggleApplicationMute(const std::wstring& programName)
{
    std::lock_guard<std::recursive_mutex> lock(m_lock);

    if (programName.empty())
    {
        return false;
    }

    const std::vector<AudioSession>& sessions = Sessions();

    // The names are taken at once, because the silenced program may not be openable.
    std::vector<DWORD> processIds;
    for (const auto& session : sessions)
    {
        if (!session.isCapture)
        {
            processIds.push_back(session.processId);
        }
    }

    FillProcessImageNames(processIds);

    // The program is looked for by its name and not by its process id, because the process that owns the sound is not always the one that owns the window.
    std::vector<IAudioSessionControl*> controls;
    bool allMuted = true;

    for (const auto& session : sessions)
    {
        if (session.isCapture || !IsProgramName(session.processId, programName))
        {
            continue;
        }

        if (!IsSessionMuted(session.control.get()))
        {
            allMuted = false;
        }

        controls.push_back(session.control.get());
    }

    if (controls.empty())
    {
        return false;
    }

    // The program is brought back when every one of its streams is silent, and silenced otherwise.
    const bool muted = !allMuted;

    bool updated = false;
    for (IAudioSessionControl* control : controls)
    {
        if (SetSessionMute(control, muted))
        {
            updated = true;
        }
    }

    return updated;
}

bool AudioEngine::IsProgramName(DWORD processId, const std::wstring& programName)
{
    const std::wstring path = GetProcessPath(processId);
    if (!path.empty())
    {
        const size_t separator = path.find_last_of(L'\\');
        const std::wstring name = separator == std::wstring::npos ? path : path.substr(separator + 1);
        return _wcsicmp(name.c_str(), programName.c_str()) == 0;
    }

    // The process cannot be opened, so the name is taken from the list of all the processes.
    const auto found = m_processNames.find(processId);
    return found != m_processNames.end() && _wcsicmp(found->second.c_str(), programName.c_str()) == 0;
}

bool AudioEngine::SetApplicationBalance(DWORD processId, bool isCapture, float left, float right)
{
    std::lock_guard<std::recursive_mutex> lock(m_lock);

    const float clampedLeft = std::clamp(left, 0.0f, 1.0f);
    const float clampedRight = std::clamp(right, 0.0f, 1.0f);
    return WithSessionControls(processId, isCapture, [clampedLeft, clampedRight](IAudioSessionControl* control) {
        wil::com_ptr<IChannelAudioVolume> channelVolume;
        if (FAILED(control->QueryInterface(__uuidof(IChannelAudioVolume), channelVolume.put_void())))
        {
            return false;
        }

        UINT channelCount = 0;
        channelVolume->GetChannelCount(&channelCount);
        if (FAILED(channelVolume->SetChannelVolume(0, clampedLeft, nullptr)))
        {
            return false;
        }

        if (channelCount > 1)
        {
            return SUCCEEDED(channelVolume->SetChannelVolume(1, clampedRight, nullptr));
        }

        return true;
    });
}

bool AudioEngine::SetApplicationDevice(DWORD processId, const std::wstring& endpointId)
{
    std::lock_guard<std::recursive_mutex> lock(m_lock);

    // The policy API takes the device interface path, not the plain device id.
    const std::wstring policyId = AudioPolicyConfig::ToPolicyEndpointId(endpointId);
    if (!m_policy.SetApplicationEndpoint(processId, policyId))
    {
        return false;
    }

    m_endpointCache[processId] = { policyId, GetProcessPath(processId), std::chrono::steady_clock::now() };
    return true;
}

bool AudioEngine::FindEndpointVolume(const std::wstring& deviceId, wil::com_ptr<IAudioEndpointVolume>& endpointVolume)
{
    if (m_enumerator == nullptr)
    {
        return false;
    }

    wil::com_ptr<IMMDevice> device;
    if (FAILED(m_enumerator->GetDevice(deviceId.c_str(), device.put())) || device == nullptr)
    {
        return false;
    }

    return GetEndpointVolume(device.get(), endpointVolume);
}

bool AudioEngine::SetDeviceVolume(const std::wstring& deviceId, float volume)
{
    std::lock_guard<std::recursive_mutex> lock(m_lock);

    wil::com_ptr<IAudioEndpointVolume> endpointVolume;
    if (!FindEndpointVolume(deviceId, endpointVolume))
    {
        return false;
    }

    return SUCCEEDED(endpointVolume->SetMasterVolumeLevelScalar(std::clamp(volume, 0.0f, 1.0f), nullptr));
}

bool AudioEngine::SetDeviceMute(const std::wstring& deviceId, bool muted)
{
    std::lock_guard<std::recursive_mutex> lock(m_lock);

    wil::com_ptr<IAudioEndpointVolume> endpointVolume;
    if (!FindEndpointVolume(deviceId, endpointVolume))
    {
        return false;
    }

    return SUCCEEDED(endpointVolume->SetMute(muted ? TRUE : FALSE, nullptr));
}

bool AudioEngine::SetDeviceListening(const std::wstring& deviceId, bool listening)
{
    std::lock_guard<std::recursive_mutex> lock(m_lock);

    if (m_enumerator == nullptr)
    {
        return false;
    }

    wil::com_ptr<IMMDevice> device;
    if (FAILED(m_enumerator->GetDevice(deviceId.c_str(), device.put())) || device == nullptr)
    {
        return false;
    }

    return SetListening(device.get(), listening);
}

bool AudioEngine::SetDeviceBalance(const std::wstring& deviceId, float left, float right)
{
    std::lock_guard<std::recursive_mutex> lock(m_lock);

    wil::com_ptr<IAudioEndpointVolume> endpointVolume;
    if (!FindEndpointVolume(deviceId, endpointVolume))
    {
        return false;
    }

    const float clampedLeft = std::clamp(left, 0.0f, 1.0f);
    const float clampedRight = std::clamp(right, 0.0f, 1.0f);

    if (FAILED(endpointVolume->SetChannelVolumeLevelScalar(0, clampedLeft, nullptr)))
    {
        return false;
    }

    // A device with one channel has nothing to move the sound between.
    UINT channelCount = 0;
    endpointVolume->GetChannelCount(&channelCount);
    if (channelCount > 1)
    {
        return SUCCEEDED(endpointVolume->SetChannelVolumeLevelScalar(1, clampedRight, nullptr));
    }

    return true;
}

bool AudioEngine::PlayTestSound(const std::wstring& deviceId)
{
    std::lock_guard<std::recursive_mutex> lock(m_lock);

    if (deviceId.empty())
    {
        return false;
    }

    // Only one test sound runs at a time.
    if (m_testPlaying.load())
    {
        return false;
    }

    // The thread of the last test is waited for, so that it is not left over. It is only waited for when it is really done.
    if (m_testThread.joinable())
    {
        if (WaitForSingleObject(m_testThread.native_handle(), 0) != WAIT_OBJECT_0)
        {
            return false;
        }

        m_testThread.join();
    }

    m_testPlaying = true;
    m_testingDeviceId = deviceId;
    m_testThread = std::thread([this, deviceId] {
        PlayTestSoundOnDevice(deviceId);

        {
            std::lock_guard<std::recursive_mutex> lock(m_lock);
            m_testingDeviceId.clear();
        }

        m_testPlaying = false;
    });

    return true;
}

std::wstring AudioEngine::GetTestingDeviceId()
{
    std::lock_guard<std::recursive_mutex> lock(m_lock);
    return m_testingDeviceId;
}

DuckingMode AudioEngine::GetDuckingMode()
{
    return ReadDuckingMode();
}

bool AudioEngine::SetDuckingMode(DuckingMode mode)
{
    return WriteDuckingMode(mode);
}

AudioEngine::~AudioEngine()
{
    // The test thread holds a pointer to this engine, so it is waited for before the engine is gone.
    if (m_testThread.joinable())
    {
        m_testThread.join();
    }
}
