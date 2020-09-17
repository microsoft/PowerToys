#pragma once
#include <winrt/base.h>
#include <wil/resource.h>
#include <wil/com.h>

#include <Windows.h>
#include <Unknwn.h>

#include <string_view>

#include <optional>
#include <vector>
#include <functional>

#include <Mmdeviceapi.h>
#include <Endpointvolume.h>

class MicrophoneDevice
{
public:
    using mute_changed_cb_t = std::function<void(bool muted)>;

private:
    friend struct VolumeNotifier;

    struct VolumeNotifier : winrt::implements<VolumeNotifier, IAudioEndpointVolumeCallback>
    {
        MicrophoneDevice* _subscribedDevice = nullptr;
        VolumeNotifier(MicrophoneDevice* subscribedDevice);

        virtual HRESULT __stdcall OnNotify(PAUDIO_VOLUME_NOTIFICATION_DATA data) override;
    };

    wil::unique_cotaskmem_string _id;
    wil::unique_prop_variant _friendly_name;
    mute_changed_cb_t _mute_changed_callback;
    winrt::com_ptr<IAudioEndpointVolumeCallback> _notifier;
    wil::com_ptr_nothrow<IAudioEndpointVolume> _endpoint;
    wil::com_ptr_nothrow<IMMDevice> _device;

    constexpr static inline std::wstring_view FALLBACK_NAME = L"Unknown device";
    constexpr static inline std::wstring_view FALLBACK_ID = L"UNKNOWN_ID";

public:
    MicrophoneDevice(MicrophoneDevice&&) noexcept = default;
    MicrophoneDevice(wil::com_ptr_nothrow<IMMDevice> device, wil::com_ptr_nothrow<IAudioEndpointVolume> endpoint);
    ~MicrophoneDevice();

    bool active() const noexcept;
    void set_muted(const bool muted) noexcept;
    bool muted() const noexcept;
    void toggle_muted() noexcept;

    std::wstring_view id() const noexcept;
    std::wstring_view name() const noexcept;
    void set_mute_changed_callback(mute_changed_cb_t callback) noexcept;

    static std::optional<MicrophoneDevice> getDefault();
    static std::vector<MicrophoneDevice> getAllActive();
};
