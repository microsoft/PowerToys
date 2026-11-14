#pragma once

#include <windows.h>
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <audiopolicy.h>
#include <endpointvolume.h>
#include <inspectable.h>
#include <winstring.h>
#include <wil/com.h>
#include <wil/resource.h>

#include <atomic>
#include <chrono>
#include <functional>
#include <mutex>
#include <string>
#include <thread>
#include <unordered_map>
#include <vector>

namespace SoundManager
{
    // What Windows does with the other sounds when a program is talking. The values are kept in the registry of the user.
    enum class DuckingMode
    {
        Mute = 0,
        ReduceBy80 = 1,
        ReduceBy50 = 2,
        DoNothing = 3,
    };

    struct AudioDevice
    {
        std::wstring id;
        std::wstring name;

        bool isDefault = false;

        bool isDefaultCommunications = false;
    };

    struct DeviceVolume
    {
        AudioDevice device;
        float volume = 1.0f;
        bool muted = false;

        // Whether the sound of the device is played through the speakers. Only a microphone can do this.
        bool listening = false;

        // The level of the left and the right channel of the device.
        float balanceLeft = 1.0f;
        float balanceRight = 1.0f;
    };

    struct AppVolume
    {
        // The id of the group of streams this entry stands for. A program that plays and records has two entries, one for each direction.
        std::wstring sessionId;

        std::wstring processPath;
        std::wstring displayName;
        DWORD processId = 0;
        float volume = 1.0f;
        bool muted = false;
        bool isActive = false;
        bool isCapture = false;
        std::wstring outputDeviceId;
        float balanceLeft = 1.0f;
        float balanceRight = 1.0f;
    };

    struct PeakLevel
    {
        std::wstring sessionId;
        float peak = 0.0f;
    };

    struct MixerSnapshot
    {
        std::vector<DeviceVolume> outputs;
        std::vector<DeviceVolume> inputs;
        std::vector<AppVolume> apps;
    };

    // The undocumented interface behind Windows.Media.Internal.AudioPolicyConfig. The three methods that are used come after nineteen others in the vtable, so those are kept here to hold their place.
    struct AudioPolicyConfigFactory : IInspectable
    {
        virtual HRESULT STDMETHODCALLTYPE unused_01() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_02() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_03() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_04() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_05() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_06() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_07() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_08() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_09() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_10() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_11() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_12() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_13() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_14() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_15() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_16() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_17() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_18() = 0;
        virtual HRESULT STDMETHODCALLTYPE unused_19() = 0;
        virtual HRESULT STDMETHODCALLTYPE set_persisted_default_audio_endpoint(UINT32, int, int, HSTRING) = 0;
        virtual HRESULT STDMETHODCALLTYPE get_persisted_default_audio_endpoint(UINT32, int, int, HSTRING*) = 0;
        virtual HRESULT STDMETHODCALLTYPE clear_all_persisted_application_default_endpoints() = 0;
    };

    // The object is owned by the engine, so that it is released on the thread that made it and not by the destructor of the DLL.
    class AudioPolicyConfig
    {
    public:
        AudioPolicyConfig() = default;
        ~AudioPolicyConfig() = default;

        AudioPolicyConfig(const AudioPolicyConfig&) = delete;
        AudioPolicyConfig& operator=(const AudioPolicyConfig&) = delete;

        bool IsAvailable();

        // Pins an application to an endpoint. An empty id restores the system default.
        bool SetApplicationEndpoint(DWORD processId, const std::wstring& endpointId);

        // Reads back the endpoint an application is pinned to. Empty means system default.
        bool GetApplicationEndpoint(DWORD processId, std::wstring& endpointId);

        // Converts an IMMDevice id into the device interface path the policy API expects.
        static std::wstring ToPolicyEndpointId(const std::wstring& deviceId);

    private:
        wil::com_ptr<AudioPolicyConfigFactory> CreateFactory();

        wil::com_ptr<AudioPolicyConfigFactory> m_factory;
    };

    // Reads the mixer state through WASAPI and applies the changes. Every call has to happen on a thread that has a COM apartment.
    class AudioEngine
    {
    public:
        AudioEngine() = default;
        ~AudioEngine();

        AudioEngine(const AudioEngine&) = delete;
        AudioEngine& operator=(const AudioEngine&) = delete;

        bool Initialize();

        void GetDevices(std::vector<DeviceVolume>& outputs, std::vector<DeviceVolume>& inputs);
        std::vector<AppVolume> GetSessions();

        // Reads the peak level of every session. This is cheap enough to be called many times a second, because it looks at the session alone.
        void GetPeaks(std::vector<PeakLevel>& peaks);

        bool SetApplicationVolume(DWORD processId, bool isCapture, float volume);
        bool SetApplicationMute(DWORD processId, bool isCapture, bool muted);
        bool SetApplicationBalance(DWORD processId, bool isCapture, float left, float right);
        bool SetApplicationDevice(DWORD processId, const std::wstring& endpointId);

        // Silences a program, or brings it back when it is already silent. The program is found by the name of its executable. Only the playback side is touched.
        bool ToggleApplicationMute(const std::wstring& programName);

        bool SetDeviceVolume(const std::wstring& deviceId, float volume);
        bool SetDeviceMute(const std::wstring& deviceId, bool muted);

        // Plays a microphone through the speakers, onto the default output device of Windows.
        bool SetDeviceListening(const std::wstring& deviceId, bool listening);

        // Moves the sound of a device between its two channels, like the balance of a program.
        bool SetDeviceBalance(const std::wstring& deviceId, float left, float right);

        // Plays the test sound through a device. It takes a few seconds and runs on a thread of its own.
        bool PlayTestSound(const std::wstring& deviceId);

        // The device the test sound is playing through at this moment, or nothing when none is.
        std::wstring GetTestingDeviceId();

        DuckingMode GetDuckingMode();
        bool SetDuckingMode(DuckingMode mode);

        // Windows keeps two default devices: one for everything and one for calls and chats.
        bool SetDefaultDevice(const std::wstring& deviceId, bool communications);

        static std::wstring GetProcessPath(DWORD processId);

    private:
        struct AudioSession
        {
            wil::com_ptr<IAudioSessionControl> control;

            // The id of the group of streams that belong together: the program and the direction.
            std::wstring groupId;

            DWORD processId = 0;
            bool isCapture = false;
        };

        // The endpoint an application is pinned to, with the time it was read and its program.
        struct CachedEndpoint
        {
            std::wstring endpointId;
            std::wstring processPath;
            std::chrono::steady_clock::time_point readAt;
        };

        // The sessions the peak levels are read from. Collecting them costs more than asking for the levels, so they are kept for a short while.
        struct CachedSessions
        {
            std::vector<AudioSession> sessions;
            std::chrono::steady_clock::time_point readAt;
        };

        // The devices of both directions. Walking them is the most expensive part of a refresh, so they are kept for a short while as well.
        struct CachedDevices
        {
            std::vector<wil::com_ptr<IMMDevice>> render;
            std::vector<wil::com_ptr<IMMDevice>> capture;
            std::chrono::steady_clock::time_point readAt;
        };

        std::wstring GetDefaultDeviceId(EDataFlow flow, ERole role);
        void CollectDevices(EDataFlow flow, std::vector<wil::com_ptr<IMMDevice>>& devices);
        void CollectSessions(EDataFlow flow, std::vector<AudioSession>& sessions);
        AppVolume ReadSession(const AudioSession& session, const std::wstring& imageName);

        // The stream of a program that is shown, one for every program and direction.
        std::vector<const AudioSession*> GroupSessions();

        // The device a program is pinned to is filled in, as far as the policy API tells.
        void ApplyOutputDevices(std::vector<AppVolume>& apps);

        const CachedDevices& Devices();

        const std::vector<AudioSession>& Sessions();

        void AddDevices(const std::vector<wil::com_ptr<IMMDevice>>& devices, EDataFlow flow, std::vector<DeviceVolume>& target);

        bool FindEndpointVolume(const std::wstring& deviceId, wil::com_ptr<IAudioEndpointVolume>& endpointVolume);

        // Falls back to the executable name for processes that cannot be opened. A snapshot of all the processes is slow, so it is taken once for all the missing names.
        void FillProcessImageNames(const std::vector<DWORD>& processIds);

        // Reads the endpoint of an application. The result is cached, because the policy API is expensive and the endpoint rarely changes.
        bool GetApplicationEndpointCached(DWORD processId, const std::wstring& processPath, std::wstring& endpointId);

        // Finds the streams of a program and runs an action on all of them, so that a program that plays on two devices is changed on both.
        bool WithSessionControls(DWORD processId, bool isCapture, const std::function<bool(IAudioSessionControl*)>& action);

        bool IsProgramName(DWORD processId, const std::wstring& programName);

        wil::com_ptr<IMMDeviceEnumerator> m_enumerator;
        AudioPolicyConfig m_policy;
        std::unordered_map<DWORD, CachedEndpoint> m_endpointCache;
        std::unordered_map<DWORD, std::wstring> m_processNames;
        CachedSessions m_sessionCache;
        CachedDevices m_deviceCache;

        // The thread that plays the test sound. Only one runs at a time.
        std::thread m_testThread;
        std::atomic<bool> m_testPlaying{ false };

        // The device the test sound is playing through.
        std::wstring m_testingDeviceId;

        // The window, the shortcut and the pipe can all ask for the state at the same time, so everything that reads or changes it is done one by one.
        std::recursive_mutex m_lock;
    };
}
