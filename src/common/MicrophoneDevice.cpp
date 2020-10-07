#include "pch.h"
#include "MicrophoneDevice.h"

#include <Functiondiscoverykeys_devpkey.h>

MicrophoneDevice::MicrophoneDevice(wil::com_ptr_nothrow<IMMDevice> device, wil::com_ptr_nothrow<IAudioEndpointVolume> endpoint) :
    _device{ std::move(device) },
    _endpoint{ std::move(endpoint) }
{
    if (!_device || !_endpoint)
    {
        throw std::logic_error("MicrophoneDevice was initialized with null objects!");
    }
    _device->GetId(&_id);
    wil::com_ptr_nothrow<IPropertyStore> props;
    _device->OpenPropertyStore(
        STGM_READ, &props);
    if (props)
    {
        props->GetValue(PKEY_Device_FriendlyName, &_friendly_name);
    }
}

MicrophoneDevice::~MicrophoneDevice()
{
    if (_notifier)
    {
        _endpoint->UnregisterControlChangeNotify(_notifier.get());
    }
}

bool MicrophoneDevice::active() const noexcept
{
    DWORD state = 0;
    _device->GetState(&state);
    return state == DEVICE_STATE_ACTIVE;
}

void MicrophoneDevice::set_muted(const bool muted) noexcept
{
    _endpoint->SetMute(muted, nullptr);
}

bool MicrophoneDevice::muted() const noexcept
{
    BOOL muted = FALSE;
    _endpoint->GetMute(&muted);
    return muted;
}

void MicrophoneDevice::toggle_muted() noexcept
{
    set_muted(!muted());
}

std::wstring_view MicrophoneDevice::id() const noexcept
{
    return _id ? _id.get() : FALLBACK_ID;
}

std::wstring_view MicrophoneDevice::name() const noexcept
{
    return _friendly_name.pwszVal ? _friendly_name.pwszVal : FALLBACK_NAME;
}

void MicrophoneDevice::set_mute_changed_callback(mute_changed_cb_t callback) noexcept
{
    _mute_changed_callback = std::move(callback);
    _notifier = winrt::make<VolumeNotifier>(this);

    _endpoint->RegisterControlChangeNotify(_notifier.get());
}

std::optional<MicrophoneDevice> MicrophoneDevice::getDefault()
{
    auto deviceEnumerator = wil::CoCreateInstanceNoThrow<MMDeviceEnumerator, IMMDeviceEnumerator>();
    if (!deviceEnumerator)
    {
        return std::nullopt;
    }
    wil::com_ptr_nothrow<IMMDevice> captureDevice;
    deviceEnumerator->GetDefaultAudioEndpoint(eCapture, eCommunications, &captureDevice);
    if (!captureDevice)
    {
        return std::nullopt;
    }
    wil::com_ptr_nothrow<IAudioEndpointVolume> microphoneEndpoint;
    captureDevice->Activate(__uuidof(IAudioEndpointVolume), CLSCTX_INPROC_SERVER, nullptr, reinterpret_cast<LPVOID*>(&microphoneEndpoint));
    if (!microphoneEndpoint)
    {
        return std::nullopt;
    }
    return std::make_optional<MicrophoneDevice>(std::move(captureDevice), std::move(microphoneEndpoint));
}

std::vector<MicrophoneDevice> MicrophoneDevice::getAllActive()
{
    std::vector<MicrophoneDevice> microphoneDevices;
    auto deviceEnumerator = wil::CoCreateInstanceNoThrow<MMDeviceEnumerator, IMMDeviceEnumerator>();
    if (!deviceEnumerator)
    {
        return microphoneDevices;
    }

    wil::com_ptr_nothrow<IMMDeviceCollection> captureDevices;
    deviceEnumerator->EnumAudioEndpoints(eCapture, DEVICE_STATE_ACTIVE, &captureDevices);
    if (!captureDevices)
    {
        return microphoneDevices;
    }
    UINT nDevices = 0;
    captureDevices->GetCount(&nDevices);
    microphoneDevices.reserve(nDevices);
    for (UINT i = 0; i < nDevices; ++i)
    {
        wil::com_ptr_nothrow<IMMDevice> device;
        captureDevices->Item(i, &device);
        if (!device)
        {
            continue;
        }
        wil::com_ptr_nothrow<IAudioEndpointVolume> microphoneEndpoint;
        device->Activate(__uuidof(IAudioEndpointVolume), CLSCTX_INPROC_SERVER, nullptr, reinterpret_cast<LPVOID*>(&microphoneEndpoint));
        if (!microphoneEndpoint)
        {
            continue;
        }
        microphoneDevices.emplace_back(std::move(device), std::move(microphoneEndpoint));
    }
    return microphoneDevices;
}

MicrophoneDevice::VolumeNotifier::VolumeNotifier(MicrophoneDevice* subscribedDevice) :
    _subscribedDevice{ subscribedDevice }
{
}

HRESULT __stdcall MicrophoneDevice::VolumeNotifier::OnNotify(PAUDIO_VOLUME_NOTIFICATION_DATA data)
{
    _subscribedDevice->_mute_changed_callback(data->bMuted);
    return S_OK;
}
